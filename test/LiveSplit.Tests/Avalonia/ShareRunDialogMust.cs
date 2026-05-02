using System;

using LiveSplit.Avalonia.Dialogs;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class ShareRunDialogMust
{
    [Fact]
    public void PrepareEndedRunOnCloneWithoutMutatingLiveRun()
    {
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Finish");
        run[0].SplitTime = new Time(realTime: TimeSpan.FromSeconds(42));

        var layoutSettings = new StandardLayoutSettingsFactory().Create();
        var state = new LiveSplitState(
            run,
            form: null,
            layout: new Layout { Settings = layoutSettings },
            layoutSettings,
            new StandardSettingsFactory().Create())
        {
            CurrentPhase = TimerPhase.Ended,
            CurrentSplitIndex = 1,
            CurrentTimingMethod = TimingMethod.RealTime,
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentHotkeyProfile = LiveSplit.Options.HotkeyProfile.DefaultHotkeyProfileName,
        };

        IRun selected = ShareRunDialog.SelectRunForSharing(ShareRunDialog.CloneStateForSharing(state));

        Assert.NotSame(run, selected);
        Assert.Equal(TimeSpan.FromSeconds(42), selected[0].PersonalBestSplitTime.RealTime);
        Assert.Null(run[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.Equal(TimeSpan.FromSeconds(42), run[0].SplitTime.RealTime);
    }
}
