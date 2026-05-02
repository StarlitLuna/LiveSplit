using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.Model.RunFactories;
using LiveSplit.TimeFormatters;
using LiveSplit.Web.Share;

using Xunit;

namespace LiveSplit.Tests.Web;

public class SharePlatformMust
{
    [Fact]
    public void BuildImgurUploadTitleFromPersonalBestAndRunNames()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run.GameName = "Example Game";
        run.CategoryName = "Any%";
        run[0].PersonalBestSplitTime = new Time(realTime: new System.TimeSpan(0, 1, 23));

        string title = Imgur.BuildTitle(run, TimingMethod.RealTime);

        Assert.Equal("1:23 in Example Game - Any%", title);
    }

    [Theory]
    [InlineData("", "https://twitter.com/intent/tweet")]
    [InlineData("Example Game - Any%", "https://twitter.com/intent/tweet?text=Example%20Game%20-%20Any%25")]
    public void BuildTwitterIntentUrl(string text, string expected)
    {
        Assert.Equal(expected, Twitter.MakeUri(text));
    }

    [Fact]
    public void ReplaceShareNotePlaceholdersForCompletedRuns()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run.GameName = "Example Game";
        run.CategoryName = "Any%";
        run[0].PersonalBestSplitTime = new Time(realTime: new System.TimeSpan(0, 1, 23));

        string notes = ShareNotesFormatter.Format(
            run,
            TimerPhase.NotRunning,
            0,
            TimingMethod.RealTime,
            "I got a $pb in $title. $game / $category",
            streamLink: "");

        Assert.Equal("I got a 1:23 in Example Game - Any%. Example Game / Any%", notes);
    }

    [Fact]
    public void ReplaceShareNotePlaceholdersForActiveSplits()
    {
        IRun run = new StandardRunFactory().Create(new StandardComparisonGeneratorsFactory());
        run.GameName = "Example Game";
        run.CategoryName = "Any%";
        run[0].Name = "First Split";
        run[0].SplitTime = new Time(realTime: new System.TimeSpan(0, 1, 20));
        run[0].PersonalBestSplitTime = new Time(realTime: new System.TimeSpan(0, 1, 25));

        string notes = ShareNotesFormatter.Format(
            run,
            TimerPhase.Running,
            1,
            TimingMethod.RealTime,
            "$splitname $splittime $delta $stream",
            streamLink: "http://twitch.tv/example");

        Assert.Equal($"First Split 1:20 {TimeFormatConstants.MINUS}5.0 http://twitch.tv/example", notes);
    }

    [Theory]
    [InlineData("https://livesplit.org/twitch/#access_token=abc123&scope=channel%3Amanage%3Abroadcast", "abc123")]
    [InlineData("http://livesplit.org/twitch/?ignored=true#access_token=token_value_42&token_type=bearer", "token_value_42")]
    [InlineData("token_value_42", "token_value_42")]
    public void ExtractTwitchAccessTokenFromRedirectOrRawToken(string input, string expected)
    {
        Assert.Equal(expected, TwitchAccessTokenPrompt.ExtractAccessToken(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://livesplit.org/twitch/#error=access_denied")]
    public void RejectMissingTwitchAccessToken(string input)
    {
        Assert.Null(TwitchAccessTokenPrompt.ExtractAccessToken(input));
    }
}
