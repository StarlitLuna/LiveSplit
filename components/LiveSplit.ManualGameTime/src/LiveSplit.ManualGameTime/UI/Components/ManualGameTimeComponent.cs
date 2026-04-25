using System;

using global::Avalonia.Threading;

using LiveSplit.Model;

namespace LiveSplit.UI.Components;

/// <summary>
/// Tracks state for manual game-time entry. The Windows build owned a small WinForms popup
/// (<c>ShitSplitter</c>) that opened at run start; the linux-port replacement is
/// <see cref="ManualGameTimeWindow"/>, an Avalonia top-level window opened on the UI thread.
/// </summary>
public class ManualGameTimeComponent : LogicComponent
{
    public ManualGameTimeSettings Settings { get; set; }

    public GraphicsCache Cache { get; set; }
    protected LiveSplitState CurrentState { get; set; }

    private ManualGameTimeWindow _entryWindow;

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
        CloseWindow();
    }

    private void state_OnStart(object sender, EventArgs e)
    {
        CurrentState.IsGameTimePaused = true;
        CurrentState.SetGameTime(TimeSpan.Zero);

        // Open the entry window on the UI thread; OnStart fires from whatever thread the timer
        // model invokes Start() on (usually the UI thread, but the control server and ASR
        // runtime can drive it from worker threads).
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                _entryWindow ??= new ManualGameTimeWindow(CurrentState);
                if (!_entryWindow.IsVisible)
                {
                    _entryWindow.Show();
                }
            }
            catch (Exception ex)
            {
                Options.Log.Error(ex);
            }
        });
    }

    private void CloseWindow()
    {
        if (_entryWindow is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                _entryWindow?.Close();
            }
            catch (Exception ex)
            {
                Options.Log.Error(ex);
            }
            finally
            {
                _entryWindow = null;
            }
        });
    }

    public override global::Avalonia.Controls.Control GetSettingsControl(LayoutMode mode)
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
        CloseWindow();
        CurrentState.OnStart -= state_OnStart;
        CurrentState.OnReset -= state_OnReset;
        CurrentState.OnUndoSplit -= State_OnUndoSplit;
    }

    public int GetSettingsHashCode()
    {
        return Settings.GetSettingsHashCode();
    }
}
