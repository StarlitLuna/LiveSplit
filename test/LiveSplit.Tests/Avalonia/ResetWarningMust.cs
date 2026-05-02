using System;

using LiveSplit.Avalonia;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class ResetWarningMust
{
    [Fact]
    public void RequestBestTimesUpdateWhenCurrentRunHasNewPersonalBest()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromMinutes(2));
        run[0].SplitTime = new Time(realTime: TimeSpan.FromMinutes(1));

        var state = new LiveSplitState(
            run,
            null,
            new Layout(),
            new StandardLayoutSettingsFactory().Create(),
            new StandardSettingsFactory().Create())
        {
            CurrentTimingMethod = TimingMethod.RealTime
        };

        Assert.True(ResetWarning.ShouldAskToUpdateBestTimes(state));
    }

    [Fact]
    public void SkipBestTimesUpdateQuestionWhenNoBestTimeWasBeaten()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromMinutes(1));
        run[0].BestSegmentTime = new Time(realTime: TimeSpan.FromMinutes(1));
        run[0].SplitTime = new Time(realTime: TimeSpan.FromMinutes(2));

        var state = new LiveSplitState(
            run,
            null,
            new Layout(),
            new StandardLayoutSettingsFactory().Create(),
            new StandardSettingsFactory().Create())
        {
            CurrentTimingMethod = TimingMethod.RealTime
        };

        Assert.False(ResetWarning.ShouldAskToUpdateBestTimes(state));
    }
}
