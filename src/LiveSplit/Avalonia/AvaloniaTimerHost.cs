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
/// On <see cref="Dispose"/>, settings.cfg, the run (if dirty), and the layout (if dirty) are
/// written back through their XML savers.
/// </summary>
public sealed class AvaloniaTimerHost : IDisposable
{
    public LiveSplitState State { get; }
    public ITimerModel Model { get; }
    public ComponentRenderer Renderer { get; }

    private readonly Action _invalidateVisual;
    private readonly Task _refreshTask;
    private readonly HotkeyService _hotkeys;
    private bool _disposed;

    public AvaloniaTimerHost(Action invalidateVisual, string splitsPath = null, string layoutPath = null)
    {
        _invalidateVisual = invalidateVisual;
        // Plugin and on-disk auto-splitter resolution walks ComponentManager.BasePath.
        ComponentManager.BasePath = UserDataPaths.ExecutableDir;

        ISettings settings = LoadOrCreateSettings();
        LayoutSettings layoutSettings = new StandardLayoutSettingsFactory().Create();

        State = new LiveSplitState(null, null, null, layoutSettings, settings);

        var comparisons = new StandardComparisonGeneratorsFactory();
        IRun run = LoadRunOrFallback(splitsPath, settings, comparisons, out string resolvedSplitsPath);
        run.FixSplits();
        State.Run = run;

        ILayout layout = LoadLayoutOrFallback(layoutPath, settings, State);
        State.Layout = layout;
        State.LayoutSettings = layout.Settings;

        Model = new TimerModel { CurrentState = State };
        State.CurrentHotkeyProfile = HotkeyProfile.DefaultHotkeyProfileName;

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

        CreateAutoSplitter(State);

        Renderer = new ComponentRenderer
        {
            VisibleComponents = State.Layout.LayoutComponents.Select(lc => lc.Component),
        };

        WireStateEvents(invalidateVisual);

        // System-wide split/reset/skip/undo/pause hotkey listener. Falls back silently if
        // libuiohook can't grab globals (Wayland without portal, headless CI); the per-window
        // KeyBindings in TimerWindow.axaml still fire when the LiveSplit window is focused.
        _hotkeys = new HotkeyService(State, Model);
        _hotkeys.Start();

        int delayMs = Math.Max(1, (int)Math.Round(1000.0 / Math.Max(1, settings.RefreshRate)));
        _refreshTask = Task.Run(async () =>
        {
            while (!_disposed)
            {
                Dispatcher.UIThread.Post(invalidateVisual, DispatcherPriority.Background);
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        });
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
            State.Settings.LastComparison = State.CurrentComparison;
            UpdateRecentSplitsTimingForCurrentRun();
            State.Run.AutoSplitter?.Deactivate();
            SaveRunIfDirty();
            SaveLayoutIfDirty();
            SaveSettingsToDisk(State.Settings);
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
    }

    // --- Public mutation helpers (called by TimerWindow's File/Close menu and drag-drop) ---

    public bool LoadRun(string path)
    {
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

            State.Run.AutoSplitter?.Deactivate();
            State.Run = run;
            State.Settings.AddToRecentSplits(path, run, State.CurrentTimingMethod, State.CurrentHotkeyProfile);
            ApplyRecentSplitsFileState(path, State.Settings, State);

            SwitchComparisonGenerators(State);
            SwitchComparison(State, State.Settings.LastComparison);
            RegenerateComparisons(State);

            CreateAutoSplitter(State);

            Invalidate();
            return true;
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
            return false;
        }
    }

    public bool LoadLayout(string path)
    {
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

            ApplyLayout(layout);
            State.Settings.AddToRecentLayouts(path);

            Invalidate();
            return true;
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
            return false;
        }
    }

    public void CloseSplits()
    {
        try
        {
            State.Run.AutoSplitter?.Deactivate();
            IRun fresh = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
            State.Run = fresh;

            ILayout layout = new TimerOnlyLayoutFactory().Create(State);
            ApplyLayout(layout);

            SwitchComparisonGenerators(State);
            SwitchComparison(State, State.Settings.LastComparison);
            RegenerateComparisons(State);

            Invalidate();
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }

    private void ApplyLayout(ILayout layout)
    {
        State.Layout = layout;
        State.LayoutSettings = layout.Settings;
        Renderer.VisibleComponents = layout.LayoutComponents.Select(lc => lc.Component);
        LayoutApplied?.Invoke();
    }

    /// <summary>
    /// Fires after a layout swap (LoadLayout, CloseSplits, or any caller of ApplyLayout). The
    /// TimerWindow uses this to re-apply window-level layout-derived state — Topmost, the
    /// window's natural Width/Height, the saved Position — without each call site having to
    /// remember.
    /// </summary>
    public event Action LayoutApplied;

    private void Invalidate()
    {
        if (_invalidateVisual is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(_invalidateVisual, DispatcherPriority.Background);
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
            path = settings.RecentSplits.LastOrDefault(x => !string.IsNullOrEmpty(x.Path)).Path;
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

    private static ILayout LoadLayoutOrFallback(string explicitPath, ISettings settings, LiveSplitState state)
    {
        string path = explicitPath;
        if (string.IsNullOrEmpty(path) && settings.RecentLayouts.Count > 0)
        {
            path = settings.RecentLayouts.LastOrDefault(x => !string.IsNullOrEmpty(x));
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

        // Master parity: a clean first launch (no explicit -l, no recent layouts, no on-disk
        // file at the recent path) loads the embedded DefaultLayout.lsl with Title + Splits +
        // Timer + PreviousSegment. TimerOnlyLayoutFactory is reserved for the explicit
        // "Close Splits" path (Host.CloseSplits) where the user just wants the timer alone.
        return new StandardLayoutFactory().Create(state);
    }

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

    private static void CreateAutoSplitter(LiveSplitState state)
    {
        AutoSplitter splitter = AutoSplitterFactory.Instance.Create(state.Run.GameName);
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
                RegenerateComparisons(State);
            }
            catch (Exception e)
            {
                Options.Log.Error(e);
            }

            Dispatcher.UIThread.Post(invalidateVisual, DispatcherPriority.Background);
        };
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
        if (State.Run == null || string.IsNullOrEmpty(State.Run.FilePath))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.Open(State.Run.FilePath, FileMode.Create, FileAccess.Write);
            new XMLRunSaver().Save(State.Run, stream);
            State.Run.HasChanged = false;
            return true;
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
            return false;
        }
    }

    public bool SaveLayout()
    {
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
            Options.Log.Error(e);
            return false;
        }
    }

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
