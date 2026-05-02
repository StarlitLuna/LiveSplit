using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using global::Avalonia.Controls;

using LiveSplit.Avalonia;
using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.Web.SRL;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

[Collection("Localization")]
public class TimerContextMenuBuilderMust
{
    [Fact]
    public void MatchMasterTopLevelOrderWithoutLanguageWhenOnlyDefaultLanguageIsAvailable()
    {
        string catalogRoot = CreateCatalogRoot(includeChinese: false);
        UiTextCatalog.Initialize(catalogRoot);

        try
        {
            var context = CreateContext();

            Assert.Equal(
                [
                    "Edit Splits...",
                    "Open Splits",
                    "Save Splits",
                    "Save Splits As...",
                    "Close Splits",
                    "<separator>",
                    "Control",
                    "Compare Against",
                    "<separator>",
                    "Share...",
                    "<separator>",
                    "Edit Layout...",
                    "Open Layout",
                    "Save Layout",
                    "Save Layout As...",
                    "<separator>",
                    "Settings",
                    "<separator>",
                    "About",
                    "Exit",
                ],
                LabelsWithSeparators(TimerContextMenuBuilder.BuildRootItems(context)));
        }
        finally
        {
            LanguageResolver.SetCurrentLanguageSetting(string.Empty);
            UiTextCatalog.Initialize(AppDomain.CurrentDomain.BaseDirectory);
            Directory.Delete(catalogRoot, recursive: true);
        }
    }

    [Fact]
    public void InsertLanguageBeforeAboutSeparatorWhenNonDefaultLanguagesAreAvailable()
    {
        string catalogRoot = CreateCatalogRoot(includeChinese: true);
        UiTextCatalog.Initialize(catalogRoot);

        try
        {
            var context = CreateContext();

            Assert.Contains("Language", LabelsWithSeparators(TimerContextMenuBuilder.BuildRootItems(context)));
            Assert.Equal(
                ["Settings", "Language", "<separator>", "About", "Exit"],
                LabelsWithSeparators(TimerContextMenuBuilder.BuildRootItems(context)).TakeLast(5).ToArray());
        }
        finally
        {
            LanguageResolver.SetCurrentLanguageSetting(string.Empty);
            UiTextCatalog.Initialize(AppDomain.CurrentDomain.BaseDirectory);
            Directory.Delete(catalogRoot, recursive: true);
        }
    }

    [Fact]
    public void InsertRaceProvidersAfterShareBeforeRaceSectionSeparator()
    {
        var context = CreateContext();
        context.RaceProviderItems =
        [
            new MenuItem { Header = "racetime.gg Races" },
            new MenuItem { Header = "SRL Races" },
        ];

        Assert.Equal(
            ["Share...", "racetime.gg Races", "SRL Races", "<separator>", "Edit Layout..."],
            LabelsWithSeparators(TimerContextMenuBuilder.BuildRootItems(context)).Skip(9).Take(5).ToArray());
    }

    [Fact]
    public void BuildOpenSplitsMenuWithMasterRecentGroupingAndCommands()
    {
        var older = new RecentSplitsFile(@"C:\runs\older.lss", TimingMethod.RealTime, HotkeyProfile.DefaultHotkeyProfileName, "Game", "Any%");
        var newer = new RecentSplitsFile(@"C:\runs\newer.lss", TimingMethod.RealTime, HotkeyProfile.DefaultHotkeyProfileName, "Game", "Any%");
        var context = CreateContext();
        context.RecentSplits = [older, newer];

        MenuItem openSplits = MenuItems(TimerContextMenuBuilder.BuildRootItems(context))
            .Single(x => (string)x.Header == "Open Splits");

        Assert.Equal(
            ["Game - Any%", "<separator>", "From File...", "From URL...", "<separator>", "Edit History"],
            LabelsWithSeparators(openSplits.Items.Cast<object>()));

        MenuItem game = MenuItems(openSplits.Items.Cast<object>()).First();
        Assert.Equal(["newer.lss", "older.lss"], MenuItems(game.Items.Cast<object>()).Select(x => x.Header?.ToString()).ToArray());
    }

    [Fact]
    public void BuildOpenLayoutMenuWithMasterRecentCommands()
    {
        var context = CreateContext();
        context.RecentLayouts = [@"C:\layouts\old-layout.lsl", @"C:\layouts\new-layout.lsl"];

        MenuItem openLayout = MenuItems(TimerContextMenuBuilder.BuildRootItems(context))
            .Single(x => (string)x.Header == "Open Layout");

        Assert.Equal(
            ["new-layout", "old-layout", "<separator>", "From File...", "From URL...", "Default", "<separator>", "Edit History"],
            LabelsWithSeparators(openLayout.Items.Cast<object>()));
    }

    [Fact]
    public void BuildPausedControlMenuWithMasterLabelsAndEnabledStates()
    {
        var context = CreateContext();
        context.State.CurrentPhase = TimerPhase.Paused;
        context.State.TimePausedAt = TimeSpan.FromSeconds(5);

        MenuItem control = MenuItems(TimerContextMenuBuilder.BuildRootItems(context))
            .Single(x => (string)x.Header == "Control");

        MenuItem split = MenuItems(control.Items.Cast<object>()).Single(x => (string)x.Header == "Resume");
        MenuItem pause = MenuItems(control.Items.Cast<object>()).Single(x => (string)x.Header == "Pause");
        MenuItem undoPauses = MenuItems(control.Items.Cast<object>()).Single(x => (string)x.Header == "Undo All Pauses");

        Assert.True(split.IsEnabled);
        Assert.False(pause.IsEnabled);
        Assert.True(undoPauses.IsEnabled);
    }

    [Fact]
    public void BuildServerControlItemsWithMasterToggleLabelsAndEnabledStates()
    {
        var context = CreateContext();
        context.GetServerState = () => ServerStateType.TCP;

        MenuItem control = MenuItems(TimerContextMenuBuilder.BuildRootItems(context))
            .Single(x => (string)x.Header == "Control");

        MenuItem stopTcp = MenuItems(control.Items.Cast<object>()).Single(x => (string)x.Header == "Stop TCP Server");
        MenuItem startWebSocket = MenuItems(control.Items.Cast<object>()).Single(x => (string)x.Header == "Start WebSocket Server");

        Assert.True(stopTcp.IsEnabled);
        Assert.False(startWebSocket.IsEnabled);
    }

    [Fact]
    public void BuildComparisonMenuWithCustomGeneratedSrlAndTimingSeparators()
    {
        var context = CreateContext();
        context.State.Run.CustomComparisons.Clear();
        context.State.Run.CustomComparisons.Add("Custom");
        context.State.Run.ComparisonGenerators.Clear();
        context.State.Run.ComparisonGenerators.Add(new AverageSegmentsComparisonGenerator(context.State.Run));
        context.State.Run.ComparisonGenerators.Add(new SRLComparisonGenerator("[Race] runner"));
        context.State.CurrentComparison = "Custom";
        context.State.CurrentTimingMethod = TimingMethod.GameTime;

        MenuItem comparisons = MenuItems(TimerContextMenuBuilder.BuildRootItems(context))
            .Single(x => (string)x.Header == "Compare Against");

        Assert.Equal(
            ["Custom", "<separator>", "Average Segments", "<separator>", "[Race] runner", "<separator>", "Real Time", "Game Time"],
            LabelsWithSeparators(comparisons.Items.Cast<object>()));
        Assert.NotNull(MenuItems(comparisons.Items.Cast<object>()).Single(x => (string)x.Header == "Custom").Icon);
        Assert.NotNull(MenuItems(comparisons.Items.Cast<object>()).Single(x => (string)x.Header == "Game Time").Icon);
    }

    private static TimerContextMenuContext CreateContext()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Start");
        run.AddSegment("Finish");

        var layout = new Layout
        {
            Mode = LayoutMode.Vertical,
            Settings = new StandardLayoutSettingsFactory().Create(),
        };

        var state = new LiveSplitState(run, null, layout, layout.Settings, settings)
        {
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentHotkeyProfile = HotkeyProfile.DefaultHotkeyProfileName,
            CurrentTimingMethod = TimingMethod.RealTime,
        };

        return new TimerContextMenuContext
        {
            State = state,
            RecentSplits = settings.RecentSplits,
            RecentLayouts = settings.RecentLayouts,
            LayoutComponents = [],
            RaceProviderItems = [],
            EditSplits = NoopTask,
            OpenSplits = NoopTask,
            OpenSplitsFromUrl = NoopTask,
            EditSplitsHistory = NoopTask,
            SaveSplits = NoopTask,
            SaveSplitsAs = NoopTask,
            CloseSplits = NoopTask,
            EditLayout = NoopTask,
            OpenLayout = NoopTask,
            OpenLayoutFromUrl = NoopTask,
            LoadDefaultLayout = NoopTask,
            EditLayoutHistory = NoopTask,
            SaveLayout = NoopTask,
            SaveLayoutAs = NoopTask,
            Settings = NoopTask,
            Share = NoopTask,
            About = NoopTask,
            Exit = () => { },
            OpenRecentSplits = _ => Task.CompletedTask,
            OpenRecentLayout = _ => Task.CompletedTask,
            StartOrSplit = () => { },
            Reset = NoopTask,
            UndoSplit = () => { },
            SkipSplit = () => { },
            Pause = () => { },
            UndoAllPauses = () => { },
            ToggleGlobalHotkeys = () => { },
            GetServerState = () => ServerStateType.Off,
            ToggleTcpServer = () => { },
            ToggleWebSocketServer = () => { },
            SwitchComparison = _ => { },
            SwitchTimingMethod = _ => { },
            ApplyLanguage = _ => Task.CompletedTask,
        };
    }

    private static Task NoopTask() => Task.CompletedTask;

    private static IEnumerable<MenuItem> MenuItems(IEnumerable<object> items)
        => items.OfType<MenuItem>();

    private static string[] LabelsWithSeparators(IEnumerable<object> items)
        => items.Select(x => x is Separator ? "<separator>" : ((MenuItem)x).Header?.ToString()).ToArray();

    private static string CreateCatalogRoot(bool includeChinese)
    {
        string root = Path.Combine(Path.GetTempPath(), $"livesplit-locale-{Guid.NewGuid():N}");
        string localization = Path.Combine(root, "Localization");
        Directory.CreateDirectory(localization);

        File.WriteAllText(
            Path.Combine(localization, "en-US.json"),
            """
            {
              "code": "en-US",
              "displayName": "English",
              "cultureName": "en-US",
              "keys": {
                "language.menu": "Language",
                "language.followSystem": "Follow System",
                "language.restartRequired": "Language change will take effect after restarting LiveSplit."
              },
              "sources": {}
            }
            """);

        if (includeChinese)
        {
            File.WriteAllText(
                Path.Combine(localization, "zh-CN.json"),
                """
                {
                  "code": "zh-CN",
                  "displayName": "Chinese",
                  "cultureName": "zh-CN",
                  "keys": {},
                  "sources": {}
                }
                """);
        }

        return root;
    }
}
