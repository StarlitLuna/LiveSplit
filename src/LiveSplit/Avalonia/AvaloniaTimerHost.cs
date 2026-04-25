using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia.Threading;

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

    private readonly Task _refreshTask;
    private bool _disposed;

    public AvaloniaTimerHost(Action invalidateVisual, string splitsPath = null, string layoutPath = null)
    {
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

        CreateAutoSplitter(State);

        Renderer = new ComponentRenderer
        {
            VisibleComponents = State.Layout.LayoutComponents.Select(lc => lc.Component),
        };

        WireStateEvents(invalidateVisual);

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
    }

    // --- Bootstrap helpers (factored out of the constructor for readability) ---

    private static ISettings LoadOrCreateSettings()
    {
        try
        {
            string path = UserDataPaths.SettingsFile;
            if (File.Exists(path))
            {
                using FileStream stream = File.OpenRead(path);
                return new XMLSettingsFactory(stream).Create();
            }
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }

        return new StandardSettingsFactory().Create();
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

        return new TimerOnlyLayoutFactory().Create(state);
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
                foreach (Model.Comparisons.IComparisonGenerator gen in State.Run.ComparisonGenerators)
                {
                    gen.Generate(State.Settings);
                }
            }
            catch (Exception e)
            {
                Options.Log.Error(e);
            }

            Dispatcher.UIThread.Post(invalidateVisual, DispatcherPriority.Background);
        };
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

    private void SaveRunIfDirty()
    {
        if (State.Run == null || !State.Run.HasChanged || string.IsNullOrEmpty(State.Run.FilePath))
        {
            return;
        }

        try
        {
            using FileStream stream = File.Open(State.Run.FilePath, FileMode.Create, FileAccess.Write);
            new XMLRunSaver().Save(State.Run, stream);
            State.Run.HasChanged = false;
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
        }
    }

    private void SaveLayoutIfDirty()
    {
        if (State.Layout == null || !State.Layout.HasChanged || string.IsNullOrEmpty(State.Layout.FilePath))
        {
            return;
        }

        try
        {
            using FileStream stream = File.Open(State.Layout.FilePath, FileMode.Create, FileAccess.Write);
            new XMLLayoutSaver().Save(State.Layout, stream);
            State.Layout.HasChanged = false;
        }
        catch (Exception e)
        {
            Options.Log.Error(e);
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
