using System;
using System.IO;

using LiveSplit.Avalonia.Dialogs;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class RunEditorDialogMust
{
    [Fact]
    public void BuildIndependentCustomComparisonBarsForTimingTabs()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.DoesNotContain("stack.Children.Add(_customComparisonsPanel)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly StackPanel _customComparisonsPanel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptCopiesEditsWithoutSavingOrClearingDirtyFlag()
    {
        Run target = NewRun();
        target.FilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.lss");
        target.HasChanged = false;
        File.WriteAllText(target.FilePath, "existing");

        Run edited = target.Clone();
        edited.GameName = "Edited Game";

        RunEditorDialogModel.ApplyAcceptedRun(target, edited);

        Assert.Equal("Edited Game", target.GameName);
        Assert.True(target.HasChanged);
        Assert.Equal("existing", File.ReadAllText(target.FilePath));
    }

    [Fact]
    public void CopyIntoPreservesMetadataThatEditorDoesNotSurface()
    {
        Run target = NewRun();
        target.Metadata.PlatformName = "PC";
        target.Metadata.RegionName = "USA";
        target.Metadata.UsesEmulator = true;

        Run edited = target.Clone();
        edited.GameName = "Edited Game";

        RunEditorDialogModel.ApplyAcceptedRun(target, edited);

        Assert.Equal("PC", target.Metadata.PlatformName);
        Assert.Equal("USA", target.Metadata.RegionName);
        Assert.True(target.Metadata.UsesEmulator);
    }

    [Fact]
    public void SplitTimeEditUpdatesPersonalBestSplitTimeForSelectedTimingMethod()
    {
        Run run = NewRun();

        RunEditorDialogModel.SetPersonalBestSplitTime(run, 1, TimingMethod.GameTime, TimeSpan.FromSeconds(75));

        Assert.Equal(TimeSpan.FromSeconds(75), run[1].PersonalBestSplitTime.GameTime);
        Assert.Null(run[1].PersonalBestSplitTime.RealTime);
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void SegmentTimeEditRecalculatesCumulativePersonalBestSplitTime()
    {
        Run run = NewRun();
        run[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(25));

        RunEditorDialogModel.SetPersonalBestSegmentTime(run, 1, TimingMethod.RealTime, TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(30), run[1].PersonalBestSplitTime.RealTime);
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void AddSegmentSeedsCustomComparisons()
    {
        Run run = NewRun();
        run.CustomComparisons.Add("Route A");
        foreach (ISegment segment in run)
        {
            segment.Comparisons["Route A"] = new Time(realTime: TimeSpan.FromSeconds(1));
        }

        RunEditorDialogModel.InsertSegment(run, 1, "Inserted");

        Assert.Equal("Inserted", run[1].Name);
        Assert.True(run[1].Comparisons.ContainsKey(Run.PersonalBestComparisonName));
        Assert.True(run[1].Comparisons.ContainsKey("Route A"));
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void MoveSegmentPreservesSegmentHistoryAndComparisons()
    {
        Run run = NewRun();
        run[0].SegmentHistory.Add(4, new Time(realTime: TimeSpan.FromSeconds(3)));
        run[0].Comparisons[Run.PersonalBestComparisonName] = new Time(realTime: TimeSpan.FromSeconds(7));

        RunEditorDialogModel.MoveSegment(run, 0, 2);

        Assert.Equal("Segment 1", run[2].Name);
        Assert.Equal(TimeSpan.FromSeconds(3), run[2].SegmentHistory[4].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(7), run[2].PersonalBestSplitTime.RealTime);
    }

    [Theory]
    [InlineData("Personal Best")]
    [InlineData("[Race] runner")]
    [InlineData("Route A")]
    public void AddComparisonRejectsDuplicatesBuiltInAndRaceNames(string name)
    {
        Run run = NewRun();
        run.CustomComparisons.Add("Route A");

        bool added = RunEditorDialogModel.TryAddComparison(run, name);

        Assert.False(added);
    }

    [Fact]
    public void RenameComparisonMovesTimesAndRejectsInvalidNames()
    {
        Run run = NewRun();
        RunEditorDialogModel.TryAddComparison(run, "Old Route");
        run[0].Comparisons["Old Route"] = new Time(realTime: TimeSpan.FromSeconds(12));

        bool renamed = RunEditorDialogModel.TryRenameComparison(run, "Old Route", "New Route");

        Assert.True(renamed);
        Assert.DoesNotContain("Old Route", run.CustomComparisons);
        Assert.Contains("New Route", run.CustomComparisons);
        Assert.Equal(TimeSpan.FromSeconds(12), run[0].Comparisons["New Route"].RealTime);
        Assert.False(run[0].Comparisons.ContainsKey("Old Route"));
        Assert.False(RunEditorDialogModel.TryRenameComparison(run, "New Route", "[Race] x"));
    }

    [Fact]
    public void RemoveComparisonDropsTimesButKeepsPersonalBest()
    {
        Run run = NewRun();
        RunEditorDialogModel.TryAddComparison(run, "Route A");

        bool removed = RunEditorDialogModel.TryRemoveComparison(run, "Route A");
        bool removedPb = RunEditorDialogModel.TryRemoveComparison(run, Run.PersonalBestComparisonName);

        Assert.True(removed);
        Assert.False(removedPb);
        Assert.DoesNotContain("Route A", run.CustomComparisons);
        Assert.All(run, segment => Assert.False(segment.Comparisons.ContainsKey("Route A")));
    }

    [Theory]
    [InlineData(true, "C:\\layouts\\main.lsl", "C:\\layouts\\main.lsl")]
    [InlineData(true, "", "?default")]
    [InlineData(false, "C:\\layouts\\main.lsl", null)]
    public void LinkedLayoutSelectionUpdatesRunLayoutPath(bool linked, string selectedPath, string expected)
    {
        Run run = NewRun();

        RunEditorDialogModel.SetLinkedLayout(run, linked, selectedPath);

        Assert.Equal(expected, run.LayoutPath);
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void AdditionalInfoEditsUpdateMetadata()
    {
        Run run = NewRun();

        RunEditorDialogModel.SetAdditionalInfo(run, "PC", "USA", usesEmulator: true);

        Assert.Equal("PC", run.Metadata.PlatformName);
        Assert.Equal("USA", run.Metadata.RegionName);
        Assert.True(run.Metadata.UsesEmulator);
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void ClearTimesUsesMasterRunClearTimesSemanticsAndMarksDirty()
    {
        Run run = NewRun();
        run.AttemptCount = 7;
        run.AttemptHistory.Add(new Attempt(1, new Time(realTime: TimeSpan.FromSeconds(10)), null, null, null));
        run.CustomComparisons.Add("Route A");
        run[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(3));
        run[0].BestSegmentTime = new Time(realTime: TimeSpan.FromSeconds(2));
        run[0].Comparisons["Route A"] = new Time(realTime: TimeSpan.FromSeconds(4));

        RunEditorDialogModel.ClearTimes(run);

        Assert.Equal(0, run.AttemptCount);
        Assert.Empty(run.AttemptHistory);
        Assert.Empty(run[0].SegmentHistory);
        Assert.Equal(default, run[0].BestSegmentTime);
        Assert.False(run[0].Comparisons.ContainsKey("Route A"));
        Assert.True(run.HasChanged);
    }

    private static Run NewRun(int segmentCount = 3)
    {
        var run = new Run(new StandardComparisonGeneratorsFactory());
        for (int i = 0; i < segmentCount; i++)
        {
            run.AddSegment($"Segment {i + 1}");
        }

        return run;
    }

    private static string FindRepoFile(string relativePath)
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }
}
