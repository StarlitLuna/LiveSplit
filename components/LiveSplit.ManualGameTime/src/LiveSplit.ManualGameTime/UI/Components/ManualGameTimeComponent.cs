using System;

using LiveSplit.Model;

namespace LiveSplit.UI.Components;

/// <summary>
/// Tracks state for manual game-time entry. The Windows build popped a WinForms entry box
/// (<c>ShitSplitter</c>) at run start where the user typed the per-segment game time; that
/// popup is gone on the linux-port and the Avalonia front-end is expected to provide its own
/// entry surface that calls <see cref="LiveSplitState.SetGameTime"/> directly. Run lifecycle
/// hooks here keep the existing pause/undo behavior so a future Avalonia popup can plug in
/// without re-deriving the timer interaction.
/// </summary>
public class ManualGameTimeComponent : LogicComponent
{
    public ManualGameTimeSettings Settings { get; set; }

    public GraphicsCache Cache { get; set; }
    protected LiveSplitState CurrentState { get; set; }

    public override string ComponentName => "Manual Game Time";

    public ManualGameTimeComponent(LiveSplitState state)
    {
        Settings = new ManualGameTimeSettings();
        state.OnStart += state_OnStart;
        state.OnReset += state_OnReset;
        state.OnUndoSplit += State_OnUndoSplit;
        CurrentState = state;
    }

    private void State_OnUndoSplit(object sender, EventArgs e)
    {
        int curIndex = CurrentState.CurrentSplitIndex;
        CurrentState.SetGameTime(curIndex > 0 ? CurrentState.Run[curIndex - 1].SplitTime.GameTime : TimeSpan.Zero);
    }

    private void state_OnReset(object sender, TimerPhase e)
    {
        // Popup-close was here; no-op without a WinForms popup.
    }

    private void state_OnStart(object sender, EventArgs e)
    {
        CurrentState.IsGameTimePaused = true;
        CurrentState.SetGameTime(TimeSpan.Zero);
    }

    public override Avalonia.Controls.Control GetSettingsControl(LayoutMode mode)
    {
        return LiveSplit.UI.AvaloniaSettingsBuilder.Build(Settings, "Component");
    }

    public override System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document)
    {
        return Settings.GetSettings(document);
    }

    public override void SetSettings(System.Xml.XmlNode settings)
    {
        Settings.SetSettings(settings);
    }

    public override void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
    }

    public override void Dispose()
    {
        CurrentState.OnStart -= state_OnStart;
        CurrentState.OnReset -= state_OnReset;
        CurrentState.OnUndoSplit -= State_OnUndoSplit;
    }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}
