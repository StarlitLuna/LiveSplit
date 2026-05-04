using System;
using System.IO;
using System.Threading.Tasks;

using LiveSplit.Avalonia.Dialogs;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.UI;
using LiveSplit.Web.Share;

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

    [Fact]
    public void KeepMasterShareDialogTitleAndNotesBehavior()
    {
        string source = File.ReadAllText(FindRepoFile("src/LiveSplit/Avalonia/Dialogs/ShareRunDialog.cs"));

        Assert.Contains("Title = \"Run Sharer\"", source, StringComparison.Ordinal);
        Assert.Contains("MaxLength = 280", source, StringComparison.Ordinal);
        Assert.Contains("SpeedrunCom = new(", source, StringComparison.Ordinal);
        Assert.Contains("supportsNotes: false", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DisableUnavailableShareNoteTokensLikeMaster()
    {
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Finish");
        var state = CreateState(run);

        Assert.False(ShareRunDialog.IsInsertTokenEnabled("$game", supportsNotes: true, run, state));
        Assert.False(ShareRunDialog.IsInsertTokenEnabled("$category", supportsNotes: true, run, state));
        Assert.False(ShareRunDialog.IsInsertTokenEnabled("$title", supportsNotes: true, run, state));
        Assert.False(ShareRunDialog.IsInsertTokenEnabled("$pb", supportsNotes: true, run, state));
        Assert.False(ShareRunDialog.IsInsertTokenEnabled("$delta", supportsNotes: true, run, state));
        Assert.False(ShareRunDialog.IsInsertTokenEnabled("$splitname", supportsNotes: true, run, state));
        Assert.False(ShareRunDialog.IsInsertTokenEnabled("$splittime", supportsNotes: true, run, state));
        Assert.False(ShareRunDialog.IsInsertTokenEnabled("$stream", supportsNotes: false, run, state));

        run.GameName = "Game";
        run.CategoryName = "Any%";
        run[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(42));
        state.CurrentPhase = TimerPhase.Running;
        state.CurrentSplitIndex = 1;

        Assert.True(ShareRunDialog.IsInsertTokenEnabled("$game", supportsNotes: true, run, state));
        Assert.True(ShareRunDialog.IsInsertTokenEnabled("$category", supportsNotes: true, run, state));
        Assert.True(ShareRunDialog.IsInsertTokenEnabled("$title", supportsNotes: true, run, state));
        Assert.True(ShareRunDialog.IsInsertTokenEnabled("$pb", supportsNotes: true, run, state));
        Assert.True(ShareRunDialog.IsInsertTokenEnabled("$delta", supportsNotes: true, run, state));
        Assert.True(ShareRunDialog.IsInsertTokenEnabled("$splitname", supportsNotes: true, run, state));
        Assert.True(ShareRunDialog.IsInsertTokenEnabled("$splittime", supportsNotes: true, run, state));
        Assert.True(ShareRunDialog.IsInsertTokenEnabled("$stream", supportsNotes: true, run, state));
    }

    [Fact]
    public void StartImgurNotesEmptyLikeMaster()
    {
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Finish");
        run.GameName = "Example Game";
        run.CategoryName = "Any%";
        run[0].PersonalBestSplitTime = new Time(realTime: TimeSpan.FromSeconds(83));
        var state = CreateState(run);
        var templates = new ShareTemplateSettings
        {
            TwitterCompletedFormat = "twitter completed",
            TwitterRunningFormat = "twitter running",
            TwitchFormat = "twitch format",
        };

        string notes = ShareRunDialog.GetInitialNotesForPlatform("Imgur", templates, state, run);

        Assert.Equal(string.Empty, notes);
    }

    [Fact]
    public async Task RejectExistingSpeedrunComRunBeforeAuthenticationPrompt()
    {
        var run = new Run(new StandardComparisonGeneratorsFactory());
        run.AddSegment("Finish");
        run.Metadata.RunID = "existing-run";
        bool authenticationPrompted = false;

        ShareRunDialog.SpeedrunComReadiness readiness =
            await ShareRunDialog.CheckSpeedrunComReadiness(run, () =>
            {
                authenticationPrompted = true;
                return Task.FromResult(true);
            });

        Assert.False(authenticationPrompted);
        Assert.False(readiness.CanSubmit);
        Assert.Equal("This run already exists on speedrun.com.", readiness.Message);
    }

    [Fact]
    public async Task PromptTwitchAuthenticationWhenStreamTokenNeedsChannel()
    {
        bool prompted = false;

        string streamLink = await ShareRunDialog.ResolveStreamLinkForNotes(
            "$title $stream",
            isLoggedIn: () => false,
            verifyStoredLogin: () => false,
            promptLogin: () =>
            {
                prompted = true;
                return Task.FromResult(true);
            },
            channelName: () => "runner");

        Assert.True(prompted);
        Assert.Equal("http://twitch.tv/runner", streamLink);
    }

    [Fact]
    public async Task TwitchFallbackNoGameStillUpdatesTitle()
    {
        var run = new Run(new StandardComparisonGeneratorsFactory())
        {
            GameName = "Unmatched Game"
        };
        run.AddSegment("Finish");
        string submittedTitle = null;
        Twitch.TwitchGame submittedGame = new("placeholder", "1");

        bool submitted = await ShareRunDialog.SubmitTwitchShareAsync(
            run,
            "Example title",
            _ => [new Twitch.TwitchGame("Other Game", "2")],
            _ => Task.FromResult(ShareRunDialog.TwitchGameResolveResult.NoGame()),
            (title, game) =>
            {
                submittedTitle = title;
                submittedGame = game;
            });

        Assert.True(submitted);
        Assert.Equal("Example title", submittedTitle);
        Assert.Null(submittedGame);
    }

    [Fact]
    public async Task TwitchFallbackCancelDoesNotUpdateTitle()
    {
        var run = new Run(new StandardComparisonGeneratorsFactory())
        {
            GameName = "Unmatched Game"
        };
        run.AddSegment("Finish");
        bool titleUpdated = false;

        bool submitted = await ShareRunDialog.SubmitTwitchShareAsync(
            run,
            "Example title",
            _ => throw new InvalidOperationException("Twitch search failed."),
            _ => Task.FromResult(ShareRunDialog.TwitchGameResolveResult.Canceled()),
            (_, _) => titleUpdated = true);

        Assert.False(submitted);
        Assert.False(titleUpdated);
    }

    [Fact]
    public async Task CopyTwitterScreenshotBeforeOpeningCompose()
    {
        byte[] expectedPng = { 137, 80, 78, 71 };
        bool screenshotRequested = false;
        byte[] copiedPng = null;

        bool copied = await ShareRunDialog.CopyTwitterScreenshotAsync(
            () =>
            {
                screenshotRequested = true;
                return expectedPng;
            },
            png =>
            {
                copiedPng = png;
                return Task.CompletedTask;
            });

        Assert.True(copied);
        Assert.True(screenshotRequested);
        Assert.Same(expectedPng, copiedPng);
    }

    [Fact]
    public void UseMasterShareResultMessages()
    {
        ShareRunDialog.ShareResultMessage success =
            ShareRunDialog.GetShareResultMessage(ShareRunDialog.ShareResult.Success, "Imgur");
        ShareRunDialog.ShareResultMessage loginFailure =
            ShareRunDialog.GetShareResultMessage(ShareRunDialog.ShareResult.LoginFailure, "Twitch");
        ShareRunDialog.ShareResultMessage failure =
            ShareRunDialog.GetShareResultMessage(ShareRunDialog.ShareResult.Failure, "Speedrun.com");

        Assert.Equal("Run Shared", success.Title);
        Assert.Equal("Your run was successfully shared to Imgur.", success.Message);
        Assert.Equal("Error", loginFailure.Title);
        Assert.Equal("Your login information seems to be incorrect.", loginFailure.Message);
        Assert.Equal("Error", failure.Title);
        Assert.Equal("The run could not be shared.", failure.Message);
    }

    private static LiveSplitState CreateState(IRun run)
    {
        var layoutSettings = new StandardLayoutSettingsFactory().Create();
        return new LiveSplitState(
            run,
            form: null,
            layout: new Layout { Settings = layoutSettings },
            layoutSettings,
            new StandardSettingsFactory().Create())
        {
            CurrentTimingMethod = TimingMethod.RealTime,
            CurrentComparison = Run.PersonalBestComparisonName,
            CurrentHotkeyProfile = LiveSplit.Options.HotkeyProfile.DefaultHotkeyProfileName,
        };
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
