using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using LiveSplit.Avalonia.Dialogs;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.UI;
using LiveSplit.UI.Components;
using LiveSplit.UI.Drawing;

using SpeedrunTimingMethod = SpeedrunComSharp.TimingMethod;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class RunEditorDialogMust
{
    [Fact]
    public void UseMasterRunEditorWindowMetrics()
    {
        object spec = RunEditorLayoutSpec();

        Assert.Equal(684, Int(spec, "InitialClientWidth"));
        Assert.Equal(511, Int(spec, "InitialClientHeight"));
        Assert.Equal(700, Int(spec, "MinimumWindowWidth"));
        Assert.Equal(510, Int(spec, "MinimumWindowHeight"));
        Assert.Equal(new[] { 140, 39, 62, 49, 136, 65, 104, 88, -1, 115 }, IntList(spec, "ColumnWidths"));
        Assert.Equal(new[] { 5, 35, 35, 35, 35, 35, 25, 29, 29, 29, 29, 29, 29, 29, -1, 36, 20 }, IntList(spec, "RowHeights"));

        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));
        Assert.DoesNotContain("Width = 880", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Height = 640", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ArrangeVisibleTabsAsMasterRunEditorPages()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Equal(
            new[] { "Real Time", "Game Time", "Additional Info" },
            StringList(RunEditorLayoutSpec(), "VisibleTabHeaders"));
        Assert.Contains("Grid.SetColumn(tabStrip, 1);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(tabStrip, 6);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumnSpan(tabStrip, 9);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(runGrid, 1);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(runGrid, 7);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumnSpan(runGrid, 9);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRowSpan(runGrid, 8);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Header = \"Edit\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Header = \"History\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Header = \"Auto Splitter\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PlaceHeaderAndCommandRailControlsOnMasterGrid()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Equal(
            new[] { "Game Name:", "Run Category:", "Start Timer at:", "Attempts:" },
            StringList(RunEditorLayoutSpec(), "HeaderLabels"));
        Assert.Equal(
            new[]
            {
                "Insert Above",
                "Insert Below",
                "Remove Segment",
                "Move Up",
                "Move Down",
                "Add Comparison",
                "Import Comparison...",
                "Other...",
            },
            StringList(RunEditorLayoutSpec(), "CommandRailLabels"));
        Assert.Contains("Grid.SetColumn(iconBorder, 0);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(iconBorder, 1);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRowSpan(iconBorder, 4);", source, StringComparison.Ordinal);
        Assert.Contains("Width = 120", source, StringComparison.Ordinal);
        Assert.Contains("Height = 120", source, StringComparison.Ordinal);
        Assert.Contains("Margin = new Thickness(10)", source, StringComparison.Ordinal);
        Assert.Contains("AddToRoot(root, insertAboveBtn, 0, 7);", source, StringComparison.Ordinal);
        Assert.Contains("AddToRoot(root, insertBelowBtn, 0, 8);", source, StringComparison.Ordinal);
        Assert.Contains("AddToRoot(root, removeSegmentBtn, 0, 9);", source, StringComparison.Ordinal);
        Assert.Contains("AddToRoot(root, moveUpBtn, 0, 10);", source, StringComparison.Ordinal);
        Assert.Contains("AddToRoot(root, moveDownBtn, 0, 11);", source, StringComparison.Ordinal);
        Assert.Contains("AddToRoot(root, addComparisonBtn, 0, 12);", source, StringComparison.Ordinal);
        Assert.Contains("AddToRoot(root, importComparisonBtn, 0, 13);", source, StringComparison.Ordinal);
        Assert.Contains("AddToRoot(root, otherBtn, 0, 14);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ArrangeAdditionalInfoMetadataLikeMaster()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Equal(new[] { 103, -1, 20, 112, -1 }, IntList(RunEditorLayoutSpec(), "MetadataColumnWidths"));
        Assert.Contains("new ColumnDefinitions(\"103,*,20,112,*\")", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(rulesLabel, 0);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(rulesBox, 1);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumnSpan(rulesBox, 5);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(platformLabel, 2);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(regionLabel, 3);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetRow(buttonPanel, 8);", source, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumnSpan(buttonPanel, 5);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildIndependentCustomComparisonBarsForTimingTabs()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.DoesNotContain("stack.Children.Add(_customComparisonsPanel)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private readonly StackPanel _customComparisonsPanel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AvoidUserFacingMojibakeInDialogText()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.DoesNotContain("â", source, StringComparison.Ordinal);
        Assert.DoesNotContain("�", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex("[^\\u0000-\\u007F]"), source);
    }

    [Fact]
    public void ExposeComparisonRenameAndRemoveControls()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Contains("\"Add Comparison", source, StringComparison.Ordinal);
        Assert.Contains("\"Import Comparison", source, StringComparison.Ordinal);
        Assert.Contains("\"Rename Comparison\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Remove Comparison\"", source, StringComparison.Ordinal);
        Assert.Contains("TryRenameComparison", source, StringComparison.Ordinal);
        Assert.Contains("TryRemoveComparison", source, StringComparison.Ordinal);
        Assert.Contains("State.CallComparisonRenamed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExposeMasterGameIconContextActions()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Contains("Set Icon...", source, StringComparison.Ordinal);
        Assert.Contains("Download Box Art", source, StringComparison.Ordinal);
        Assert.Contains("Download Icon", source, StringComparison.Ordinal);
        Assert.Contains("Open from URL...", source, StringComparison.Ordinal);
        Assert.Contains("OpenGameIconFromUrl", source, StringComparison.Ordinal);
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
    public void AcceptDoesNotTransferDialogOwnedAutoSplitterComponent()
    {
        Run target = NewRun();
        var originalComponent = new TrackingComponent();
        var originalSplitter = new AutoSplitter
        {
            Component = originalComponent,
            Games = ["Original Game"],
            URLs = ["http://example.com/original.dll"],
        };
        target.AutoSplitter = originalSplitter;

        Run edited = target.Clone();
        edited.GameName = "Edited Game";
        edited.AutoSplitter = new AutoSplitter
        {
            Component = new TrackingComponent(),
            Games = ["Edited Game"],
            URLs = ["http://example.com/edited.dll"],
        };

        RunEditorDialogModel.ApplyAcceptedRun(target, edited);

        Assert.Same(originalSplitter, target.AutoSplitter);
        Assert.Same(originalComponent, target.AutoSplitter.Component);
    }

    [Fact]
    public void RunCloneDoesNotShareLiveAutoSplitterComponent()
    {
        Run target = NewRun();
        target.AutoSplitter = new AutoSplitter
        {
            Component = new TrackingComponent(),
            Games = ["Original Game"],
            URLs = ["http://example.com/original.dll"],
        };

        Run clone = target.Clone();

        Assert.NotSame(target.AutoSplitter, clone.AutoSplitter);
        Assert.Null(clone.AutoSplitter.Component);
        Assert.Equal(target.AutoSplitter.Games, clone.AutoSplitter.Games);
        Assert.Equal(target.AutoSplitter.URLs, clone.AutoSplitter.URLs);
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
    public void InsertSegmentImportsBestSegmentAndSeedsAttemptHistoryLikeMaster()
    {
        Run run = NewRun(2);
        run.AttemptHistory.Add(new Attempt(1, new Time(realTime: TimeSpan.FromSeconds(12)), null, null, null));
        run.AttemptHistory.Add(new Attempt(2, new Time(realTime: TimeSpan.FromSeconds(18)), null, null, null));
        run[0].SegmentHistory[1] = new Time(realTime: TimeSpan.FromSeconds(5));
        run[1].SegmentHistory[1] = new Time(realTime: TimeSpan.FromSeconds(7));
        run[1].SegmentHistory[2] = new Time(realTime: TimeSpan.FromSeconds(8));
        run[1].BestSegmentTime = new Time(realTime: TimeSpan.FromSeconds(6), gameTime: TimeSpan.FromSeconds(4));

        RunEditorDialogModel.InsertSegment(run, 1, "Inserted");

        Assert.Equal("Inserted", run[1].Name);
        Assert.Equal(default, run[1].SegmentHistory[1]);
        Assert.Equal(default, run[1].SegmentHistory[2]);
        Assert.Equal(TimeSpan.FromSeconds(6), run[2].SegmentHistory[0].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(4), run[2].SegmentHistory[0].GameTime);
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void RemoveSegmentRepairsFollowingHistoryAndBestSegmentLikeMaster()
    {
        Run run = NewRun(2);
        run.AttemptHistory.Add(new Attempt(1, new Time(realTime: TimeSpan.FromSeconds(12)), null, null, null));
        run[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(5));
        run[1].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(12));
        run[0].BestSegmentTime = new Time(realTime: TimeSpan.FromSeconds(5));
        run[1].BestSegmentTime = new Time(realTime: TimeSpan.FromSeconds(7));
        run[0].SegmentHistory[1] = new Time(realTime: TimeSpan.FromSeconds(5));
        run[1].SegmentHistory[1] = new Time(realTime: TimeSpan.FromSeconds(7));

        RunEditorDialogModel.RemoveSegment(run, 0);

        Assert.Single(run);
        Assert.Equal(TimeSpan.FromSeconds(12), run[0].SegmentHistory[1].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(12), run[0].BestSegmentTime.RealTime);
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

    [Fact]
    public void MoveSegmentRecalculatesCumulativeComparisonsLikeMaster()
    {
        Run run = NewRun();
        RunEditorDialogModel.TryAddComparison(run, "Route A");
        run[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(30));
        run[2].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(60));
        run[0].Comparisons["Route A"] = new Time(realTime: TimeSpan.FromSeconds(12));
        run[1].Comparisons["Route A"] = new Time(realTime: TimeSpan.FromSeconds(40));
        run[2].Comparisons["Route A"] = new Time(realTime: TimeSpan.FromSeconds(90));

        RunEditorDialogModel.MoveSegment(run, 0, 1);

        Assert.Equal("Segment 2", run[0].Name);
        Assert.Equal("Segment 1", run[1].Name);
        Assert.Equal(TimeSpan.FromSeconds(20), run[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(30), run[1].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(28), run[0].Comparisons["Route A"].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(40), run[1].Comparisons["Route A"].RealTime);
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void MoveSegmentDropsMismatchedAdjacentHistoryLikeMaster()
    {
        Run run = NewRun(2);
        run.AttemptHistory.Add(new Attempt(1, new Time(realTime: TimeSpan.FromSeconds(10)), null, null, null));
        run[0].SegmentHistory[1] = new Time(realTime: TimeSpan.FromSeconds(3));
        run[1].SegmentHistory[1] = default;

        RunEditorDialogModel.MoveSegment(run, 0, 1);

        Assert.False(run[0].SegmentHistory.ContainsKey(1));
        Assert.False(run[1].SegmentHistory.ContainsKey(1));
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
    public void ResolveComparisonNameByRetryingDuplicateInvalidAndRaceNames()
    {
        Run run = NewRun();
        RunEditorDialogModel.TryAddComparison(run, "Route A");
        var retryNames = new Queue<string>(["[Race] runner", "", "Route B"]);
        var errors = new List<RunEditorComparisonNameError>();

        string resolved = RunEditorDialogModel.ResolveComparisonNameWithRetry(
            run,
            "Route A",
            existingName: null,
            retryNameProvider: () => retryNames.Dequeue(),
            invalidNamePrompt: (error, _) =>
            {
                errors.Add(error);
                return MessageResult.Ok;
            });

        Assert.Equal("Route B", resolved);
        Assert.Equal(
            new[]
            {
                RunEditorComparisonNameError.Duplicate,
                RunEditorComparisonNameError.Race,
                RunEditorComparisonNameError.Invalid,
            },
            errors);
    }

    [Fact]
    public void ResolveComparisonNameStopsWhenInvalidNamePromptIsCancelled()
    {
        Run run = NewRun();
        bool retried = false;

        string resolved = RunEditorDialogModel.ResolveComparisonNameWithRetry(
            run,
            "[Race] runner",
            existingName: null,
            retryNameProvider: () =>
            {
                retried = true;
                return "Route B";
            },
            invalidNamePrompt: (_, _) => MessageResult.Cancel);

        Assert.Null(resolved);
        Assert.False(retried);
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

    [Fact]
    public void ImportComparisonMatchesSegmentsLikeMaster()
    {
        Run target = NewRun();
        target[0].Name = "Intro";
        target[1].Name = "Middle";
        target[2].Name = "Finish";
        var imported = new Run(new StandardComparisonGeneratorsFactory());
        imported.AddSegment("Intro");
        imported.AddSegment("Skipped Segment");
        imported.AddSegment("Imported Finish");
        imported[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(5));
        imported[1].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(8));
        imported[2].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(12));

        bool importedComparison = RunEditorDialogModel.TryImportComparisonFromRun(target, imported, "Imported PB");

        Assert.True(importedComparison);
        Assert.Contains("Imported PB", target.CustomComparisons);
        Assert.Equal(TimeSpan.FromSeconds(5), target[0].Comparisons["Imported PB"].RealTime);
        Assert.False(target[1].Comparisons.ContainsKey("Imported PB"));
        Assert.Equal(TimeSpan.FromSeconds(12), target[2].Comparisons["Imported PB"].RealTime);
        Assert.True(target.HasChanged);
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
    public void MetadataEditsClearSpeedrunComRunIdOnlyWhenChanged()
    {
        Run run = NewRun();
        run.Metadata.PlatformName = "PC";
        run.Metadata.RegionName = "USA";
        run.Metadata.UsesEmulator = false;
        run.Metadata.RunID = "abc123";

        RunEditorDialogModel.SetAdditionalInfo(run, "PC", "USA", usesEmulator: false);

        Assert.Equal("abc123", run.Metadata.RunID);

        RunEditorDialogModel.SetAdditionalInfo(run, "Console", "USA", usesEmulator: false);

        Assert.Null(run.Metadata.RunID);
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void GameAndCategoryEditsClearSpeedrunComRunIdLikeMaster()
    {
        Run run = NewRun();
        run.GameName = "Old Game";
        run.CategoryName = "Any%";
        run.Metadata.RunID = "abc123";

        RunEditorDialogModel.SetGameName(run, "New Game");

        Assert.Equal("New Game", run.GameName);
        Assert.Null(run.Metadata.RunID);

        run.Metadata.RunID = "def456";

        RunEditorDialogModel.SetCategoryName(run, "100%");

        Assert.Equal("100%", run.CategoryName);
        Assert.Null(run.Metadata.RunID);
    }

    [Fact]
    public void SpeedrunComVariableEditsMatchMasterBindingAndClearRunId()
    {
        Run run = NewRun();
        run.Metadata.RunID = "abc123";

        Assert.True(RunEditorDialogModel.SetSpeedrunComVariableValue(
            run.Metadata,
            "Difficulty",
            ["Easy", "Hard"],
            isUserDefined: false,
            value: "Hard"));

        Assert.Equal("Hard", run.Metadata.VariableValueNames["Difficulty"]);
        Assert.Null(run.Metadata.RunID);

        run.Metadata.RunID = "def456";

        Assert.True(RunEditorDialogModel.SetSpeedrunComVariableValue(
            run.Metadata,
            "Difficulty",
            ["Easy", "Hard"],
            isUserDefined: false,
            value: ""));

        Assert.False(run.Metadata.VariableValueNames.ContainsKey("Difficulty"));
        Assert.Null(run.Metadata.RunID);

        Assert.False(RunEditorDialogModel.SetSpeedrunComVariableValue(
            run.Metadata,
            "Difficulty",
            ["Easy", "Hard"],
            isUserDefined: false,
            value: "Impossible"));
    }

    [Fact]
    public void ExposeSpeedrunComMetadataControlsAndAssociationState()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Contains("\"Additional Info\"", source, StringComparison.Ordinal);
        Assert.Contains("Associate with Speedrun.com...", source, StringComparison.Ordinal);
        Assert.Contains("Show on Speedrun.com...", source, StringComparison.Ordinal);
        Assert.Contains("Submit Run...", source, StringComparison.Ordinal);

        Assert.True(RunEditorDialogModel.GetSpeedrunComAssociationState(NewRun().Metadata).CanSubmit);

        Run run = NewRun();
        run.Metadata.RunID = "abc123";
        var state = RunEditorDialogModel.GetSpeedrunComAssociationState(run.Metadata);

        Assert.False(state.CanSubmit);
        Assert.Equal("Show on Speedrun.com...", state.AssociateButtonText);
    }

    [Fact]
    public void WireSpeedrunComMetadataControlsToMasterActions()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Contains("AssociateSpeedrunComRun", source, StringComparison.Ordinal);
        Assert.Contains("ShowSpeedrunComRun", source, StringComparison.Ordinal);
        Assert.Contains("SubmitSpeedrunComRun", source, StringComparison.Ordinal);
        Assert.Contains("SpeedrunCom.Client.Runs.GetRunFromSiteUri", source, StringComparison.Ordinal);
        Assert.Contains("PatchRun", source, StringComparison.Ordinal);
        Assert.Contains("SpeedrunCom.ValidateRun", source, StringComparison.Ordinal);
        Assert.Contains("SpeedrunComSubmitDialog", source, StringComparison.Ordinal);
        Assert.Contains("PlatformLauncher.Open", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSpeedrunComRulesTextLikeMaster()
    {
        string rules = RunEditorDialogModel.BuildSpeedrunComRulesText(
            SpeedrunTimingMethod.RealTimeWithoutLoads,
            requiresVideo: true,
            categoryRules: "Category-specific rules.");

        Assert.Contains("Runs of this game are timed without the loading times and require video proof.", rules);
        Assert.Contains("Category-specific rules.", rules);
    }

    [Fact]
    public void HideEmulatorOptionUnlessGameRulesAllowIt()
    {
        Assert.False(RunEditorDialogModel.ShouldShowEmulatorCheckbox(gameAvailable: false, emulatorsAllowed: false));
        Assert.False(RunEditorDialogModel.ShouldShowEmulatorCheckbox(gameAvailable: true, emulatorsAllowed: false));
        Assert.True(RunEditorDialogModel.ShouldShowEmulatorCheckbox(gameAvailable: true, emulatorsAllowed: true));
    }

    [Fact]
    public void UseDropdownMetadataInputsWithEmptyChoiceLikeMaster()
    {
        Assert.Equal(
            new[] { "", "PC", "Console" },
            RunEditorDialogModel.BuildMetadataChoiceList(["PC", "Console"], currentValue: "PC"));
        Assert.Equal(
            new[] { "", "PC", "Console" },
            RunEditorDialogModel.BuildMetadataChoiceList(["PC"], currentValue: "Console"));

        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Contains("BuildMetadataChoiceBox", source, StringComparison.Ordinal);
        Assert.Contains("RefreshMetadataChoiceBoxes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_platformBox = new TextBox", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_regionBox = new TextBox", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExposeEditableSpeedrunComVariableControls()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Contains("BuildSpeedrunComVariableControls", source, StringComparison.Ordinal);
        Assert.Contains("SetSpeedrunComVariableValue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Speedrun.com Variables (read-only)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RenderRulesLinksAsClickableLaunchers()
    {
        Assert.Equal(
            new[] { "https://example.com/rules", "http://example.net/info" },
            RunEditorDialogModel.ExtractRulesLinks("See https://example.com/rules and http://example.net/info."));

        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/RunEditorDialog.cs"));

        Assert.Contains("BuildSpeedrunComRulesControl", source, StringComparison.Ordinal);
        Assert.Contains("ExtractRulesLinks", source, StringComparison.Ordinal);
        Assert.Contains("PlatformLauncher.Open", source, StringComparison.Ordinal);
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

    [Fact]
    public void CleanSumOfBestInteractionUsesPerCandidateYesNoCancelSemantics()
    {
        var responses = new Queue<MessageResult>([MessageResult.Yes, MessageResult.No, MessageResult.Cancel]);
        int promptCount = 0;
        var interaction = new RunEditorCleanSumOfBestInteraction(_ =>
        {
            promptCount++;
            return responses.Dequeue();
        });
        SumOfBest.CleanUpCallbackParameters repeated = CleanParameters("Intro", "Finish", 5);
        SumOfBest.CleanUpCallbackParameters noCandidate = CleanParameters("Intro", "Middle", 6);
        SumOfBest.CleanUpCallbackParameters cancelCandidate = CleanParameters("Middle", "Finish", 7);
        SumOfBest.CleanUpCallbackParameters afterCancelCandidate = CleanParameters("Start", "Finish", 8);

        Assert.True(interaction.Callback(repeated));
        Assert.True(interaction.Callback(repeated));
        Assert.False(interaction.Callback(noCandidate));
        Assert.False(interaction.Callback(cancelCandidate));
        Assert.False(interaction.Callback(afterCancelCandidate));
        Assert.Equal(3, promptCount);
        Assert.True(interaction.UserWasPrompted);
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

    private static SumOfBest.CleanUpCallbackParameters CleanParameters(string startName, string endName, int seconds)
        => new()
        {
            startingSegment = new Segment(startName),
            endingSegment = new Segment(endName),
            timeBetween = TimeSpan.FromSeconds(seconds),
            combinedSumOfBest = TimeSpan.FromSeconds(seconds + 1),
            attempt = new Attempt(1, new Time(realTime: TimeSpan.FromSeconds(seconds)), null, null, null),
            method = TimingMethod.RealTime,
        };

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

    private static object RunEditorLayoutSpec()
    {
        Type type = Type.GetType("LiveSplit.Avalonia.Dialogs.RunEditorDialogLayoutSpec, LiveSplit");
        Assert.NotNull(type);
        object value = type.GetProperty("Master", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(null);
        Assert.NotNull(value);
        return value;
    }

    private static IReadOnlyList<int> IntList(object instance, string propertyName)
        => Assert.IsAssignableFrom<IEnumerable<int>>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance)).ToList();

    private static IReadOnlyList<string> StringList(object instance, string propertyName)
        => Assert.IsAssignableFrom<IEnumerable<string>>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance)).ToList();

    private static int Int(object instance, string propertyName)
        => Assert.IsType<int>(
            instance.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(instance));

    private sealed class TrackingComponent : IComponent
    {
        public string ComponentName => "Tracking";
        public float HorizontalWidth => 1;
        public float MinimumHeight => 1;
        public float VerticalHeight => 1;
        public float MinimumWidth => 1;
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public System.Collections.Generic.IDictionary<string, Action> ContextMenuControls => null;
        public void DrawHorizontal(IDrawingContext ctx, LiveSplitState state, float height) { }
        public void DrawVertical(IDrawingContext ctx, LiveSplitState state, float width) { }
        public System.Xml.XmlNode GetSettings(System.Xml.XmlDocument document) => document.CreateElement("Settings");
        public void SetSettings(System.Xml.XmlNode settings) { }
        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) { }
        public void Dispose() { }
    }

}
