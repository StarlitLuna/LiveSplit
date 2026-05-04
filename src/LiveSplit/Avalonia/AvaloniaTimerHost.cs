using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia.Threading;

using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Model.RunSavers;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Options.SettingsSavers;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.LayoutFactories;
using LiveSplit.UI.LayoutSavers;

namespace LiveSplit.Avalonia;

/// <summary>
/// Owns the <see cref="LiveSplitState"/> + Layout + <see cref="ComponentRenderer"/>. Loads
/// persisted settings, the most recent run + layout (or a fresh timer-only session if none
/// exists), drives a <see cref="TimerModel"/> so keybindings can call Split/Reset/Skip/Undo/Pause,
/// and pumps repaints at the configured refresh rate so the running clock updates without user
/// interaction. The <see cref="SkiaRenderControl"/> pulls state + renderer from here on each paint.
///
/// On <see cref="Dispose"/>, settings.cfg is written back. Dirty runs/layouts are only saved
/// through TimerWindow's explicit save prompts, matching master's shutdown behavior.
/// </summary>
public sealed class AvaloniaTimerHost : IDisposable
{
    public LiveSplitState State { get; }
    public ITimerModel Model { get; }
    public ComponentRenderer Renderer { get; }
    public bool InTimerOnlyMode { get; private set; }
    public Exception LastOperationException { get; private set; }

    private readonly Action _invalidateVisual;
    private readonly Task _refreshTask;
    private readonly HotkeyService _hotkeys;
    private readonly TimerModel _timerModel;
    private readonly Invalidator _invalidator;
    private readonly bool _activateAutoSplitters;
    private readonly bool _persistOnDispose;
    private readonly Func<string, AutoSplitter> _autoSplitterResolver;
    private int _refreshDelayMs;
    private bool _disposed;

    public AvaloniaTimerHost(
        Action invalidateVisual,
        string splitsPath = null,
        string layoutPath = null,
        bool startBackgroundServices = true,
        bool persistOnDispose = true,
        Func<string, AutoSplitter> autoSplitterResolver = null
    )
    {
        _invalidateVisual = invalidateVisual;
        _activateAutoSplitters = startBackgroundServices;
        _persistOnDispose = persistOnDispose;
        _autoSplitterResolver = autoSplitterResolver ?? (static game => AutoSplitterFactory.Instance.Create(game));
        // Plugin and on-disk auto-splitter resolution walks ComponentManager.BasePath.
        ComponentManager.BasePath = UserDataPaths.ExecutableDir;

        ISettings settings = LoadOrCreateSettings();
        LayoutSettings layoutSettings = new StandardLayoutSettingsFactory().Create();
        _invalidator = new Invalidator((_, _, _, _) => Invalidate());

        State = new LiveSplitState(null, null, null, layoutSettings, settings);

        var comparisons = new StandardComparisonGeneratorsFactory();
        IRun run = LoadRunOrFallback(splitsPath, settings, comparisons, out string resolvedSplitsPath);
        run.FixSplits();
        State.Run = run;

        bool loadedFallbackLayout;
        ILayout layout = LoadLayoutOrFallback(layoutPath, settings, State, run == null || string.IsNullOrEmpty(resolvedSplitsPath), out loadedFallbackLayout);
        State.Layout = layout;
        State.LayoutSettings = layout.Settings;

        _timerModel = new TimerModel();
        Model = new DoubleTapPrevention(_timerModel) { CurrentState = State };
        State.CurrentHotkeyProfile = SelectInitialHotkeyProfile(settings);
        InTimerOnlyMode = loadedFallbackLayout && string.IsNullOrEmpty(resolvedSplitsPath);

        // Restore the per-splits-file timing method and hotkey profile from the MRU entry.
        if (!string.IsNullOrEmpty(resolvedSplitsPath))
        {
            ApplyRecentSplitsFileState(resolvedSplitsPath, settings, State);
        }

        // Apply the user's enabled-comparison-generators preference + restore the previously
        // selected comparison so the timer column matches what the user saw last session.
        SwitchComparisonGenerators(State);
        SwitchComparison(State, settings.LastComparison);
        RegenerateComparisons(State);

        CreateAutoSplitter(State, _activateAutoSplitters, _autoSplitterResolver);

        Renderer = new ComponentRenderer
        {
            VisibleComponents = State.Layout.LayoutComponents.Select(lc => lc.Component),
        };

        WireStateEvents(invalidateVisual);

        if (startBackgroundServices)
        {
            // System-wide split/reset/skip/undo/pause hotkey listener. Falls back silently if
            // libuiohook can't grab globals (Wayland without portal, headless CI); the per-window
            // KeyBindings in TimerWindow.axaml still fire when the LiveSplit window is focused.
            _hotkeys = new HotkeyService(State, Model, RequestResetFromHotkey, StartOrSplitFromHotkey);
            _hotkeys.Start();

            _refreshDelayMs = GetRefreshDelay(settings.RefreshRate);
            _refreshTask = Task.Run(async () =>
            {
                while (!_disposed)
                {
                    Dispatcher.UIThread.Post(invalidateVisual, DispatcherPriority.Background);
                    await Task.Delay(_refreshDelayMs).ConfigureAwait(false);
                }
            });
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            State.Run.AutoSplitter?.Deactivate();
            if (_persistOnDispose)
            {
                State.Settings.LastComparison = State.CurrentComparison;
                UpdateRecentSplitsTimingForCurrentRun();
                SaveSettingsToDisk(State.Settings);
            }
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }

        try
        {
            _refreshTask?.Wait(100);
        }
        catch
        {
            // Shutdown race; ignore.
        }

        try
        {
            _hotkeys?.Dispose();
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }

        try
        {
            DisposeLayoutComponents(State.Layout);
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }

    // --- Public mutation helpers (called by TimerWindow's File/Close menu and drag-drop) ---

    public bool LoadRun(string path)
    {
        LastOperationException = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            var factory = new StandardFormatsRunFactory { FilePath = path };
            using FileStream stream = File.OpenRead(path);
            factory.Stream = stream;
            IRun run = factory.Create(new StandardComparisonGeneratorsFactory());
            run.FilePath = path;
            run.FixSplits();

            ApplyLoadedRun(run, path, addToRecents: true);
            return true;
        }
        catch (Exception e)
        {
            LastOperationException = e;
            Options.Log.Error(e);
            return false;
        }
    }

    public bool LoadRunFromStream(Stream stream)
    {
        LastOperationException = null;
        if (stream is null)
        {
            return false;
        }

        try
        {
            var factory = new StandardFormatsRunFactory { Stream = stream };
            IRun run = factory.Create(new StandardComparisonGeneratorsFactory());
            run.FilePath = null;
            run.HasChanged = true;
            run.FixSplits();

            ApplyLoadedRun(run, path: null, addToRecents: false);
            return true;
        }
        catch (Exception e)
        {
            LastOperationException = e;
            Options.Log.Error(e);
            return false;
        }
    }

    public bool LoadLayout(string path)
    {
        LastOperationException = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            ILayout layout = new XMLLayoutFactory(stream).Create(State);
            layout.FilePath = path;
            StandardLayoutFactory.CenturyGothicFix(layout);

            ApplyLoadedLayout(layout, path, addToRecents: true);
            return true;
        }
        catch (Exception e)
        {
            LastOperationException = e;
            Options.Log.Error(e);
            return false;
        }
    }

    public bool LoadLayoutFromStream(Stream stream)
    {
        LastOperationException = null;
        if (stream is null)
        {
            return false;
        }

        try
        {
            ILayout layout = new XMLLayoutFactory(stream).Create(State);
            layout.FilePath = null;
            layout.HasChanged = true;
            StandardLayoutFactory.CenturyGothicFix(layout);

            ApplyLoadedLayout(layout, path: null, addToRecents: false);
            return true;
        }
        catch (Exception e)
        {
            LastOperationException = e;
            Options.Log.Error(e);
            return false;
        }
    }

    public void CloseSplits()
    {
        try
        {
            State.Run.AutoSplitter?.Deactivate();
            UpdateRecentSplitsTimingForCurrentRun();
            ResetBeforeDestructiveSwap();
            IRun fresh = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
            string currentComparison = State.CurrentComparison;
            State.Run = fresh;

            bool needToChangeLayout = !IsTimerOnlyLayout(State.Layout);
            InTimerOnlyMode = true;
            State.Settings.AddToRecentSplits(string.Empty, null, TimingMethod.RealTime, State.CurrentHotkeyProfile);
            if (needToChangeLayout)
            {
                ILayout oldLayout = State.Layout;
                ILayout layout = new TimerOnlyLayoutFactory().Create(State);
                layout.Settings = oldLayout.Settings;
                layout.X = oldLayout.X;
                layout.Y = oldLayout.Y;
                layout.Mode = oldLayout.Mode;
                ApplyLayout(layout);
                State.Settings.AddToRecentLayouts(string.Empty);
            }

            SwitchComparisonGenerators(State);
            RegenerateComparisons(State);
            SwitchComparison(State, currentComparison);

            Invalidate();
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }

    public void LoadDefaultLayout(int? x = null, int? y = null)
    {
        try
        {
            ILayout layout = new StandardLayoutFactory().Create(State);
            if (x.HasValue)
            {
                layout.X = x.Value;
            }

            if (y.HasValue)
            {
                layout.Y = y.Value;
            }

            ApplyLayout(layout);
            State.Settings.AddToRecentLayouts(string.Empty);
            Invalidate();
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }

    public void SetRecentSplitsHistory(IEnumerable<string> history)
    {
        var retained = new HashSet<string>(history ?? [], StringComparer.OrdinalIgnoreCase);
        State.Settings.RecentSplits = State.Settings.RecentSplits
            .Where(x => retained.Contains(x.Path))
            .ToList();
    }

    public void SetRecentLayoutsHistory(IEnumerable<string> history)
    {
        State.Settings.RecentLayouts = (history ?? []).ToList();
    }

    private void ApplyLayout(ILayout layout)
    {
        ILayout previousLayout = State.Layout;
        if (!ReferenceEquals(previousLayout, layout))
        {
            DisposeRemovedComponents(previousLayout, layout);
            ActivateAddedComponents(previousLayout, layout);
        }

        State.Layout = layout;
        State.LayoutSettings = layout.Settings;
        Renderer.VisibleComponents = layout.LayoutComponents.Select(lc => lc.Component);
        if (!IsTimerOnlyLayout(layout))
        {
            InTimerOnlyMode = false;
        }

        LayoutApplied?.Invoke();
    }

    private void ApplyLoadedRun(IRun run, string path, bool addToRecents)
    {
        State.Run.AutoSplitter?.Deactivate();
        ResetBeforeDestructiveSwap();
        UpdateRecentSplitsTimingForCurrentRun();

        TimingMethod lastTimingMethod = State.CurrentTimingMethod;
        string lastHotkeyProfile = State.CurrentHotkeyProfile;
        if (addToRecents && !string.IsNullOrEmpty(path))
        {
            RecentSplitsFile existingRecent = State.Settings.RecentSplits.LastOrDefault(x => x.Path == path);
            if (!string.IsNullOrEmpty(existingRecent.Path))
            {
                lastTimingMethod = existingRecent.LastTimingMethod;
                if (State.Settings.HotkeyProfiles.ContainsKey(existingRecent.LastHotkeyProfile))
                {
                    lastHotkeyProfile = existingRecent.LastHotkeyProfile;
                }
            }
        }

        string comparisonToPreserve = State.CurrentComparison;
        State.Run = run;
        if (addToRecents && !string.IsNullOrEmpty(path))
        {
            State.Settings.AddToRecentSplits(path, run, lastTimingMethod, lastHotkeyProfile);
            ApplyRecentSplitsFileState(path, State.Settings, State);
        }

        SwitchComparisonGenerators(State);
        SwitchComparison(State, comparisonToPreserve);
        RegenerateComparisons(State);

        State.CallRunManuallyModified();
        CreateAutoSplitter(State, _activateAutoSplitters, _autoSplitterResolver);
        RestoreLayoutForRun(run);
        InTimerOnlyMode = false;

        Invalidate();
    }

    private void ApplyLoadedLayout(ILayout layout, string path, bool addToRecents)
    {
        ApplyLayout(layout);
        if (addToRecents && !string.IsNullOrEmpty(path))
        {
            State.Settings.AddToRecentLayouts(path);
        }

        Invalidate();
    }

    private static string SelectInitialHotkeyProfile(ISettings settings)
    {
        if (settings?.HotkeyProfiles is null || settings.HotkeyProfiles.Count == 0)
        {
            return HotkeyProfile.DefaultHotkeyProfileName;
        }

        return settings.HotkeyProfiles.First().Key;
    }

    private static void DisposeRemovedComponents(ILayout previousLayout, ILayout nextLayout)
    {
        if (previousLayout is null)
        {
            return;
        }

        HashSet<IComponent> nextComponents = nextLayout?.LayoutComponents
            .Select(x => x.Component)
            .Where(x => x is not null)
            .ToHashSet() ?? [];

        foreach (IComponent component in previousLayout.LayoutComponents
            .Select(x => x.Component)
            .Where(x => x is not null && !nextComponents.Contains(x))
            .Distinct())
        {
            DeactivateAndDispose(component);
        }
    }

    private static void ActivateAddedComponents(ILayout previousLayout, ILayout nextLayout)
    {
        if (nextLayout is null)
        {
            return;
        }

        HashSet<IComponent> previousComponents = previousLayout?.LayoutComponents
            .Select(x => x.Component)
            .Where(x => x is not null)
            .ToHashSet() ?? [];

        foreach (IDeactivatableComponent component in nextLayout.LayoutComponents
            .Select(x => x.Component)
            .OfType<IDeactivatableComponent>()
            .Where(x => !previousComponents.Contains(x))
            .Distinct())
        {
            component.Activated = true;
        }
    }

    private static void DisposeLayoutComponents(ILayout layout)
    {
        if (layout is null)
        {
            return;
        }

        foreach (IComponent component in layout.LayoutComponents
            .Select(x => x.Component)
            .Where(x => x is not null)
            .Distinct())
        {
            DeactivateAndDispose(component);
        }
    }

    private static void DeactivateAndDispose(IComponent component)
    {
        if (component is IDeactivatableComponent deactivatable)
        {
            deactivatable.Activated = false;
        }

        component.Dispose();
    }

    /// <summary>
    /// Fires after a layout swap (LoadLayout, CloseSplits, or any caller of ApplyLayout). The
    /// TimerWindow uses this to re-apply window-level layout-derived state â€” Topmost, the
    /// window's natural Width/Height, the saved Position â€” without each call site having to
    /// remember.
    /// </summary>
    public event Action LayoutApplied;
    public event EventHandler ResetRequested;

    private void Invalidate()
    {
        if (_invalidateVisual is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(_invalidateVisual, DispatcherPriority.Background);
    }

    private void RequestResetFromHotkey()
    {
        if (ResetRequested is not null)
        {
            ResetRequested(this, EventArgs.Empty);
            return;
        }

        Model.Reset();
    }

    private void StartOrSplitFromHotkey()
    {
        switch (State.CurrentPhase)
        {
            case TimerPhase.Running:
                Model.Split();
                break;
            case TimerPhase.Paused:
                Model.Pause();
                break;
            case TimerPhase.NotRunning:
                Model.Start();
                break;
            case TimerPhase.Ended:
                Model.Reset();
                break;
        }
    }

    // --- Bootstrap helpers (factored out of the constructor for readability) ---

    private static ISettings LoadOrCreateSettings()
    {
        ISettings settings = null;
        try
        {
            string path = UserDataPaths.SettingsFile;
            if (File.Exists(path))
            {
                using FileStream stream = File.OpenRead(path);
                settings = new XMLSettingsFactory(stream).Create();
            }
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }

        settings ??= new StandardSettingsFactory().Create();

        // Apply the persisted UI language preference now that we have it; locale-aware UI
        // strings rendered before this point would otherwise fall back to the system culture.
        LanguageResolver.SetCurrentLanguageSetting(settings.UILanguage);

        return settings;
    }

    private static IRun LoadRunOrFallback(string explicitPath, ISettings settings,
        IComparisonGeneratorsFactory comparisons, out string resolvedPath)
    {
        string path = explicitPath;
        if (string.IsNullOrEmpty(path) && settings.RecentSplits.Count > 0)
        {
            path = settings.RecentSplits.Last().Path;
        }

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                var factory = new StandardFormatsRunFactory { FilePath = path };
                using FileStream stream = File.OpenRead(path);
                factory.Stream = stream;
                IRun run = factory.Create(comparisons);
                run.FilePath = path;
                resolvedPath = path;
                return run;
            }
            catch (Exception e)
            {
                Options.Log.Error(e);
            }
        }

        resolvedPath = null;
        return new StandardRunFactory().Create(comparisons);
    }

    private static ILayout LoadLayoutOrFallback(string explicitPath, ISettings settings, LiveSplitState state, bool allowTimerOnlyFallback, out bool loadedFallbackLayout)
    {
        loadedFallbackLayout = false;
        string path = explicitPath;
        if (string.IsNullOrEmpty(path) && settings.RecentLayouts.Count > 0)
        {
            path = settings.RecentLayouts.LastOrDefault();
        }

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                ILayout layout = new XMLLayoutFactory(stream).Create(state);
                layout.FilePath = path;
                StandardLayoutFactory.CenturyGothicFix(layout);
                return layout;
            }
            catch (Exception e)
            {
                Options.Log.Error(e);
            }
        }

        loadedFallbackLayout = true;
        return allowTimerOnlyFallback
            ? new TimerOnlyLayoutFactory().Create(state)
            : new StandardLayoutFactory().Create(state);
    }

    private void RestoreLayoutForRun(IRun run)
    {
        if (!string.IsNullOrEmpty(run.LayoutPath))
        {
            if (string.Equals(run.LayoutPath, "?default", StringComparison.Ordinal))
            {
                ApplyLayout(new StandardLayoutFactory().Create(State));
                return;
            }

            string layoutPath = ResolveLayoutPath(run.LayoutPath, run.FilePath);
            if (!string.IsNullOrEmpty(layoutPath)
                && File.Exists(layoutPath)
                && !string.Equals(State.Layout?.FilePath, layoutPath, StringComparison.OrdinalIgnoreCase))
            {
                LoadLayout(layoutPath);
                return;
            }
        }

        if (!InTimerOnlyMode)
        {
            return;
        }

        string recentLayout = State.Settings.RecentLayouts.LastOrDefault(x => !string.IsNullOrEmpty(x));
        if (!string.IsNullOrEmpty(recentLayout) && File.Exists(recentLayout))
        {
            LoadLayout(recentLayout);
            return;
        }

        ApplyLayout(new StandardLayoutFactory().Create(State));
    }

    private static string ResolveLayoutPath(string layoutPath, string splitsPath)
    {
        if (string.IsNullOrEmpty(layoutPath) || Path.IsPathRooted(layoutPath) || string.IsNullOrEmpty(splitsPath))
        {
            return layoutPath;
        }

        string directory = Path.GetDirectoryName(splitsPath);
        return string.IsNullOrEmpty(directory) ? layoutPath : Path.Combine(directory, layoutPath);
    }

    private static bool IsTimerOnlyLayout(ILayout layout)
    {
        return layout?.LayoutComponents.Count() == 1
            && string.Equals(layout.LayoutComponents.FirstOrDefault()?.Component?.ComponentName, "Timer", StringComparison.Ordinal);
    }

    private void ResetBeforeDestructiveSwap()
    {
        if (State.CurrentPhase != TimerPhase.NotRunning)
        {
            _timerModel.Reset(updateSplits: false);
        }
    }

    public bool DispatchFocusedHotkey(global::Avalonia.Input.Key key)
    {
        return _hotkeys?.DispatchFocusedKey(key) == true;
    }

    public bool DispatchFocusedHotkey(global::Avalonia.Input.Key key, global::Avalonia.Input.KeyModifiers modifiers)
    {
        return _hotkeys?.DispatchFocusedKey(key, modifiers) == true;
    }

    public void SetNormalHotkeysSuppressed(bool suppressed)
        => _hotkeys?.SetNormalHotkeysSuppressed(suppressed);

    public void UpdateComponentsForRender(float width, float height)
    {
        if (State?.Layout is null || Renderer?.VisibleComponents is null)
        {
            return;
        }

        width = Math.Max(1f, width);
        height = Math.Max(1f, height);
        UI.LayoutMode mode = State.Layout.Mode;

        try
        {
            Renderer.CalculateOverallSize(mode);
            _invalidator.Restart();
            Renderer.Update(_invalidator, State, width, height, mode);
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
            Invalidate();
        }
    }

    public void ApplySettings(ISettings settings, string selectedHotkeyProfile)
    {
        State.Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (!string.IsNullOrEmpty(selectedHotkeyProfile)
            && State.Settings.HotkeyProfiles.ContainsKey(selectedHotkeyProfile))
        {
            State.CurrentHotkeyProfile = selectedHotkeyProfile;
        }
        else if (!State.Settings.HotkeyProfiles.ContainsKey(State.CurrentHotkeyProfile)
            && State.Settings.HotkeyProfiles.Count > 0)
        {
            State.CurrentHotkeyProfile = State.Settings.HotkeyProfiles.First().Key;
        }

        SwitchComparisonGenerators(State);
        SwitchComparison(State, State.CurrentComparison);
        RegenerateComparisons(State);
        _refreshDelayMs = GetRefreshDelay(State.Settings.RefreshRate);
        SaveSettings();
        Invalidate();
    }

    public void ApplyRunEditorAcceptedChanges()
    {
        string currentComparison = State.CurrentComparison;
        SwitchComparisonGenerators(State);
        SwitchComparison(State, currentComparison);
        RegenerateComparisons(State);
        State.CallRunManuallyModified();
        State.Run.AutoSplitter?.Deactivate();
        CreateAutoSplitter(State, _activateAutoSplitters, _autoSplitterResolver);
        RestoreLayoutForRun(State.Run);
        Invalidate();
    }

    private static int GetRefreshDelay(int refreshRate)
        => Math.Max(1, 1000 / Math.Max(1, refreshRate));

    private static void ApplyRecentSplitsFileState(string path, ISettings settings, LiveSplitState state)
    {
        RecentSplitsFile entry = settings.RecentSplits.LastOrDefault(x => x.Path == path);
        if (string.IsNullOrEmpty(entry.Path))
        {
            return;
        }

        state.CurrentTimingMethod = entry.LastTimingMethod;
        if (settings.HotkeyProfiles.ContainsKey(entry.LastHotkeyProfile))
        {
            state.CurrentHotkeyProfile = entry.LastHotkeyProfile;
        }
    }

    private static void CreateAutoSplitter(LiveSplitState state, bool activate, Func<string, AutoSplitter> resolveAutoSplitter)
    {
        if (!activate)
        {
            state.Run.AutoSplitter = null;
            return;
        }

        AutoSplitter splitter = resolveAutoSplitter(state.Run.GameName);
        state.Run.AutoSplitter = splitter;
        if (splitter == null || !state.Settings.ActiveAutoSplitters.Contains(state.Run.GameName))
        {
            return;
        }

        splitter.Activate(state);
        if (splitter.IsActivated
            && state.Run.AutoSplitterSettings != null
            && state.Run.AutoSplitterSettings.GetAttribute("gameName") == state.Run.GameName)
        {
            state.Run.AutoSplitter.Component.SetSettings(state.Run.AutoSplitterSettings);
        }
    }

    private void WireStateEvents(Action invalidateVisual)
    {
        // Repainting on every state-change keeps the UI in sync without waiting for the refresh
        // tick, particularly for split/reset transitions where the user expects instant feedback.
        EventHandler invalidate = (_, _) => Dispatcher.UIThread.Post(invalidateVisual, DispatcherPriority.Background);

        State.OnStart += invalidate;
        State.OnSplit += invalidate;
        State.OnSkipSplit += invalidate;
        State.OnUndoSplit += invalidate;
        State.OnPause += invalidate;
        State.OnResume += invalidate;
        State.OnUndoAllPauses += invalidate;
        State.OnSwitchComparisonPrevious += invalidate;
        State.OnSwitchComparisonNext += invalidate;

        // Reset additionally needs the comparison generators to regenerate (they read from the
        // current run's history, which the reset just mutated). Component-level handlers run
        // before this on the same event because it's last-subscribed; safer to leave it explicit.
        State.OnReset += (_, _) =>
        {
            try
            {
                if (InTimerOnlyMode)
                {
                    ResetTimerOnlyRun();
                }

                RegenerateComparisons(State);
            }
            catch (Exception e)
            {
                Options.Log.Error(e);
            }

            Dispatcher.UIThread.Post(invalidateVisual, DispatcherPriority.Background);
        };
    }

    private void ResetTimerOnlyRun()
    {
        IRun timerOnlyRun = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        timerOnlyRun.Offset = State.Run.Offset;
        State.Run.AutoSplitter?.Deactivate();
        State.Run = timerOnlyRun;
        SwitchComparisonGenerators(State);
        SwitchComparison(State, State.CurrentComparison);
        CreateAutoSplitter(State, _activateAutoSplitters, _autoSplitterResolver);
    }

    private static void SwitchComparisonGenerators(LiveSplitState state)
    {
        // Apply Settings.ComparisonGeneratorStates: keep only the generators the user has
        // enabled (and add back any newly-enabled ones not currently on the run). Mirrors the
        // pattern in the previous WinForms host.
        IEnumerable<IComparisonGenerator> allGenerators =
            new StandardComparisonGeneratorsFactory().GetAllGenerators(state.Run);
        foreach (IComparisonGenerator generator in allGenerators)
        {
            IComparisonGenerator existing = state.Run.ComparisonGenerators
                .FirstOrDefault(x => x.Name == generator.Name);
            if (existing != null)
            {
                state.Run.ComparisonGenerators.Remove(existing);
            }

            if (state.Settings.ComparisonGeneratorStates.TryGetValue(generator.Name, out bool enabled) && enabled)
            {
                state.Run.ComparisonGenerators.Add(generator);
            }
        }
    }

    private static void SwitchComparison(LiveSplitState state, string name)
    {
        if (string.IsNullOrEmpty(name) || !state.Run.Comparisons.Contains(name))
        {
            name = Run.PersonalBestComparisonName;
        }

        state.CurrentComparison = name;
    }

    private static void RegenerateComparisons(LiveSplitState state)
    {
        if (state?.Run == null)
        {
            return;
        }

        foreach (IComparisonGenerator gen in state.Run.ComparisonGenerators)
        {
            gen.Generate(state.Settings);
        }
    }

    // --- Persistence helpers (called from Dispose) ---

    private void UpdateRecentSplitsTimingForCurrentRun()
    {
        if (State.Run == null || string.IsNullOrEmpty(State.Run.FilePath))
        {
            return;
        }

        if (State.Settings.RecentSplits.Any(x => x.Path == State.Run.FilePath))
        {
            State.Settings.AddToRecentSplits(
                State.Run.FilePath, State.Run, State.CurrentTimingMethod, State.CurrentHotkeyProfile);
        }
    }

    public bool SaveRun()
    {
        LastOperationException = null;
        if (State.Run == null || string.IsNullOrEmpty(State.Run.FilePath))
        {
            return false;
        }

        try
        {
            State.Run.FixSplits();
            IRun runToSave = CreateRunSnapshotForSave();
            using FileStream stream = File.Open(State.Run.FilePath, FileMode.Create, FileAccess.Write);
            new XMLRunSaver().Save(runToSave, stream);
            State.Run.HasChanged = false;
            State.Settings.AddToRecentSplits(
                State.Run.FilePath,
                runToSave,
                State.CurrentTimingMethod,
                State.CurrentHotkeyProfile);
            return true;
        }
        catch (Exception e)
        {
            LastOperationException = e;
            Options.Log.Error(e);
            return false;
        }
    }

    private IRun CreateRunSnapshotForSave()
    {
        if (State.CurrentPhase == TimerPhase.NotRunning)
        {
            return State.Run;
        }

        var stateCopy = (LiveSplitState)State.Clone();
        var modelCopy = new TimerModel { CurrentState = stateCopy };
        modelCopy.Reset();
        return stateCopy.Run;
    }

    public bool SaveRunAs(string path)
    {
        if (State.Run == null || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        State.Run.FilePath = path;
        bool saved = SaveRun();
        if (saved)
        {
            State.Settings.AddToRecentSplits(path, State.Run, State.CurrentTimingMethod, State.CurrentHotkeyProfile);
        }

        return saved;
    }

    public bool SaveLayout()
    {
        LastOperationException = null;
        if (State.Layout == null || string.IsNullOrEmpty(State.Layout.FilePath))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.Open(State.Layout.FilePath, FileMode.Create, FileAccess.Write);
            new XMLLayoutSaver().Save(State.Layout, stream);
            State.Layout.HasChanged = false;
            return true;
        }
        catch (Exception e)
        {
            LastOperationException = e;
            Options.Log.Error(e);
            return false;
        }
    }

    public bool SaveLayoutAs(string path, int? x = null, int? y = null)
    {
        if (State.Layout == null || string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        UpdateLayoutPosition(x, y);
        State.Layout.FilePath = path;
        bool saved = SaveLayout();
        if (saved)
        {
            State.Settings.AddToRecentLayouts(path);
        }

        return saved;
    }

    public void UpdateLayoutPosition(int? x, int? y)
    {
        if (State.Layout == null || x == null || y == null)
        {
            return;
        }

        if (State.Layout.X == x.Value && State.Layout.Y == y.Value)
        {
            return;
        }

        State.Layout.X = x.Value;
        State.Layout.Y = y.Value;
    }

    public void SaveSettings()
        => SaveSettingsToDisk(State.Settings);

    private void SaveRunIfDirty()
    {
        if (State.Run is { HasChanged: true })
        {
            SaveRun();
        }
    }

    private void SaveLayoutIfDirty()
    {
        if (State.Layout is { HasChanged: true })
        {
            SaveLayout();
        }
    }

    private static void SaveSettingsToDisk(ISettings settings)
    {
        try
        {
            string path = UserDataPaths.SettingsFile;
            using FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write);
            new XMLSettingsSaver().Save(settings, stream);
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }
}
