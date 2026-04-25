using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia.Threading;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.Drawing;
using LiveSplit.UI.LayoutFactories;

namespace LiveSplit.Avalonia;

/// <summary>
/// Owns the LiveSplitState + Layout + ComponentRenderer that back the Avalonia vertical slice.
/// Creates a Timer-only layout (matching <c>--avalonia</c> first-launch behavior) and drives a
/// TimerModel so keybindings can call Split/Reset/Skip/Undo/Pause. The <see cref="SkiaRenderControl"/>
/// pulls the state + renderer from here on each paint.
///
/// This is the Linux-parity counterpart to <c>TimerForm</c>'s init in <c>src/LiveSplit.View</c>.
/// It intentionally stops short of loading recent-splits / recent-layouts / settings from disk —
/// Phase 7 wires those up once the settings + run-editor dialogs have Avalonia shells. The slice
/// today: a fresh timer-only session on every launch.
/// </summary>
public sealed class AvaloniaTimerHost : IDisposable
{
    public LiveSplitState State { get; }
    public ITimerModel Model { get; }
    public ComponentRenderer Renderer { get; }

    private readonly Task _refreshTask;
    private bool _disposed;

    public AvaloniaTimerHost(Action invalidateVisual)
    {
        ISettings settings = new StandardSettingsFactory().Create();
        LayoutSettings layoutSettings = new StandardLayoutSettingsFactory().Create();

        State = new LiveSplitState(null, null, null, layoutSettings, settings);

        var runFactory = new StandardRunFactory();
        IRun run = runFactory.Create(new StandardComparisonGeneratorsFactory());
        run.FixSplits();
        State.Run = run;

        State.Layout = new TimerOnlyLayoutFactory().Create(State);
        State.LayoutSettings = State.Layout.Settings;

        Model = new TimerModel { CurrentState = State };
        State.CurrentHotkeyProfile = "Default";

        Renderer = new ComponentRenderer
        {
            VisibleComponents = State.Layout.LayoutComponents.Select(lc => lc.Component),
        };

        _refreshTask = Task.Run(async () =>
        {
            // Pump repaints while the window is open. Avalonia is paint-on-demand, so the Timer
            // running its internal clock doesn't by itself cause repaints — we nudge the UI
            // thread ~30 times/sec.
            while (!_disposed)
            {
                Dispatcher.UIThread.Post(invalidateVisual, DispatcherPriority.Background);
                await Task.Delay(33).ConfigureAwait(false);
            }
        });
    }

    public void Dispose()
    {
        _disposed = true;
        try
        {
            _refreshTask?.Wait(100);
        }
        catch
        {
            // shutdown race — ignore
        }
    }
}
