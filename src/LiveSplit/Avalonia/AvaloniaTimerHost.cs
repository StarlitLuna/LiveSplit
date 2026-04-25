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
/// Owns the <see cref="LiveSplitState"/> + Layout + <see cref="ComponentRenderer"/>. Creates a
/// timer-only layout, drives a <see cref="TimerModel"/> so keybindings can call
/// Split/Reset/Skip/Undo/Pause, and pumps repaints at the configured refresh rate so the running clock updates
/// without user interaction. The <see cref="SkiaRenderControl"/> pulls state + renderer from
/// here on each paint.
///
/// Recent-splits / recent-layouts / persisted settings aren't loaded here — every launch starts
/// from a fresh timer-only session.
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

        // Avalonia is paint-on-demand; the running clock doesn't itself trigger repaints.
        int delayMs = Math.Max(1, (int)Math.Round(1000.0 / settings.RefreshRate));
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
