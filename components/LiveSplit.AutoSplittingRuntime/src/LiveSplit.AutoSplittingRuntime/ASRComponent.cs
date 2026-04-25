using System;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;

using Timer = System.Timers.Timer;

namespace LiveSplit.AutoSplittingRuntime;

public class ASRComponent : LogicComponent
{
    private readonly TimerModel model;
    private readonly ComponentSettings settings;
    private Timer updateTimer;

    public ASRComponent(LiveSplitState state)
    {
        model = new TimerModel() { CurrentState = state };

        settings = new ComponentSettings(model);

        InitializeUpdateTimer();
    }

    public ASRComponent(LiveSplitState state, string scriptPath)
    {
        model = new TimerModel() { CurrentState = state };

        settings = new ComponentSettings(model, scriptPath);

        InitializeUpdateTimer();
    }

    private void InitializeUpdateTimer()
    {
        updateTimer = new Timer() { Interval = 15 };
        updateTimer.Elapsed += UpdateTimerElapsed;
        updateTimer.Start();
    }

    public override string ComponentName => "Auto Splitting Runtime";

    public override void Dispose()
    {
        updateTimer.Elapsed -= UpdateTimerElapsed;
        updateTimer.Dispose();
        updateTimer = null;
        settings.runtime?.Dispose();
    }

    public override XmlNode GetSettings(XmlDocument document)
    {
        return settings.GetSettings(document);
    }

    public Avalonia.Controls.Control GetSettingsControl(LayoutMode mode)
    {
        return LiveSplit.UI.AvaloniaSettingsBuilder.Build(settings, "Component");
    }

    public override void SetSettings(XmlNode settings)
    {
        this.settings.SetSettings(settings);
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }

    public void UpdateTimerElapsed(object sender, EventArgs e)
    {
        // This refresh timer behavior is similar to the ASL refresh timer

        // Disable timer, to wait for execution of this iteration to
        // finish. This can be useful if blocking operations like
        // showing a message window are used.
        updateTimer?.Stop();

        try
        {
            InvokeIfNeeded(() =>
            {
                if (settings.runtime != null)
                {
                    settings.runtime.Step();

                    // BuildTree() rendered the runtime's settings into a WinForms TableLayoutPanel
                    // and was removed for the linux-port — the Avalonia panel reads the settings
                    // map via reflection instead, so the change-detection refresh is unnecessary
                    // here.

                    // Poll the tick rate and modify the update interval if it has been changed
                    double tickRate = settings.runtime.TickRate().TotalMilliseconds;

                    if (updateTimer != null && tickRate != updateTimer.Interval)
                    {
                        updateTimer.Interval = tickRate;
                    }
                }
            });
        }
        finally
        {
            updateTimer?.Start();
        }
    }

    private void InvokeIfNeeded(Action x)
    {
        // The Windows build hopped to the form's UI thread via Form.Invoke before stepping the
        // runtime. The Avalonia host pumps Update on its own thread already, and the runtime
        // step is non-UI work, so direct invocation is fine on the linux-port.
        x();
    }
}
