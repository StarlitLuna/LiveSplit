using System;

using LiveSplit.Avalonia;
using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class HotkeyServiceMust
{
    [Fact]
    public void FocusedFallbackUsesActiveHotkeyProfileAndToggleGlobalHotkeys()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        settings.HotkeyProfiles.Clear();
        settings.HotkeyProfiles["Alt"] = new HotkeyProfile
        {
            SplitKey = new KeyOrButton(Key.NumPad4),
            ToggleGlobalHotkeys = new KeyOrButton(Key.T),
            GlobalHotkeysEnabled = false,
            DoubleTapPrevention = false,
        };

        var state = new LiveSplitState(
            new Run(new LiveSplit.Model.Comparisons.StandardComparisonGeneratorsFactory()),
            form: null,
            layout: new Layout(),
            layoutSettings: new LiveSplit.Options.SettingsFactories.StandardLayoutSettingsFactory().Create(),
            settings)
        {
            CurrentHotkeyProfile = "Alt",
            CurrentComparison = Run.PersonalBestComparisonName,
        };

        int splitCount = 0;
        using var service = new HotkeyService(state, new StubTimerModel(state, () => splitCount++));

        Assert.True(service.DispatchFocusedKey(Key.T));
        Assert.True(settings.HotkeyProfiles["Alt"].GlobalHotkeysEnabled);

        Assert.True(service.DispatchFocusedKey(Key.NumPad4));
        Assert.Equal(1, splitCount);
    }

#pragma warning disable CS0067
    private sealed class StubTimerModel : ITimerModel
    {
        private readonly Action _split;

        public StubTimerModel(LiveSplitState state, Action split)
        {
            CurrentState = state;
            _split = split;
        }

        public LiveSplitState CurrentState { get; set; }
        public event EventHandler OnSplit;
        public event EventHandler OnUndoSplit;
        public event EventHandler OnSkipSplit;
        public event EventHandler OnStart;
        public event EventHandlerT<TimerPhase> OnReset;
        public event EventHandler OnPause;
        public event EventHandler OnUndoAllPauses;
        public event EventHandler OnResume;
        public event EventHandler OnScrollUp;
        public event EventHandler OnScrollDown;
        public event EventHandler OnSwitchComparisonPrevious;
        public event EventHandler OnSwitchComparisonNext;

        public void Start() { }
        public void Split() { _split(); OnSplit?.Invoke(this, EventArgs.Empty); }
        public void SkipSplit() => OnSkipSplit?.Invoke(this, EventArgs.Empty);
        public void UndoSplit() => OnUndoSplit?.Invoke(this, EventArgs.Empty);
        public void Reset() => OnReset?.Invoke(this, TimerPhase.NotRunning);
        public void Reset(bool updateSplits = true) => Reset();
        public void Pause() => OnPause?.Invoke(this, EventArgs.Empty);
        public void UndoAllPauses() => OnUndoAllPauses?.Invoke(this, EventArgs.Empty);
        public void ScrollUp() => OnScrollUp?.Invoke(this, EventArgs.Empty);
        public void ScrollDown() => OnScrollDown?.Invoke(this, EventArgs.Empty);
        public void SwitchComparisonPrevious() => OnSwitchComparisonPrevious?.Invoke(this, EventArgs.Empty);
        public void SwitchComparisonNext() => OnSwitchComparisonNext?.Invoke(this, EventArgs.Empty);
        public void InitializeGameTime() { }
        public void ResetAndSetAttemptAsPB() { }
    }
#pragma warning restore CS0067
}
