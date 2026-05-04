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
    [InlineData(TimerPhase.NotRunning, "I got a $pb in $title.")]
    [InlineData(TimerPhase.Ended, "I got a $pb in $title.")]
    [InlineData(TimerPhase.Running, "I'm $delta in $title.")]
    [InlineData(TimerPhase.Paused, "I'm $delta in $title.")]
    public void SelectDefaultTwitterTemplateForTimerPhase(TimerPhase phase, string expected)
    {
        Assert.Equal(expected, ShareTemplateSettings.Default.GetTwitterFormat(phase));
    }

    [Fact]
    public void SaveAndLoadShareTemplatesCrossPlatform()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.xml");
        try
        {
            var saved = new ShareTemplateSettings
            {
                TwitterCompletedFormat = "completed $title $pb",
                TwitterRunningFormat = "running $title $delta",
                TwitchFormat = "twitch $title",
            };

            var store = new ShareTemplateSettingsStore(path);
            store.Save(saved);

            ShareTemplateSettings loaded = store.Load();

            Assert.Equal("completed $title $pb", loaded.TwitterCompletedFormat);
            Assert.Equal("running $title $delta", loaded.TwitterRunningFormat);
            Assert.Equal("twitch $title", loaded.TwitchFormat);
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }
    }

    [Fact]
    public void MigrateLegacyMasterTwitterTemplatesWhenNewStoreDoesNotExist()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.xml");
        string legacyPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.config");
        try
        {
            System.IO.File.WriteAllText(
                legacyPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <userSettings>
                    <LiveSplit.Web.Share.ShareSettings>
                      <setting name="TwitterFormat" serializeAs="String">
                        <value>legacy completed $title</value>
                      </setting>
                      <setting name="TwitterFormatRunning" serializeAs="String">
                        <value>legacy running $delta</value>
                      </setting>
                      <setting name="TwitchFormat" serializeAs="String">
                        <value>legacy twitch $stream</value>
                      </setting>
                    </LiveSplit.Web.Share.ShareSettings>
                  </userSettings>
                </configuration>
                """);

            var store = new ShareTemplateSettingsStore(path, legacyPath);

            ShareTemplateSettings loaded = store.Load();

            Assert.Equal("legacy completed $title", loaded.TwitterCompletedFormat);
            Assert.Equal("legacy running $delta", loaded.TwitterRunningFormat);
            Assert.Equal("legacy twitch $stream", loaded.TwitchFormat);
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }

            if (System.IO.File.Exists(legacyPath))
            {
                System.IO.File.Delete(legacyPath);
            }
        }
    }

    [Fact]
    public void MigrateLegacyMasterShareTemplatesWithSettingsNamespace()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.xml");
        string legacyPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{System.Guid.NewGuid():N}.config");
        try
        {
            System.IO.File.WriteAllText(
                legacyPath,
                """
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <userSettings>
                    <LiveSplit.Web.Share.Properties.Settings>
                      <setting name="TwitterFormat" serializeAs="String">
                        <value>master completed $title</value>
                      </setting>
                      <setting name="TwitterFormatRunning" serializeAs="String">
                        <value>master running $delta</value>
                      </setting>
                    </LiveSplit.Web.Share.Properties.Settings>
                  </userSettings>
                </configuration>
                """);

            var store = new ShareTemplateSettingsStore(path, legacyPath);

            ShareTemplateSettings loaded = store.Load();

            Assert.Equal("master completed $title", loaded.TwitterCompletedFormat);
            Assert.Equal("master running $delta", loaded.TwitterRunningFormat);
            Assert.Equal(ShareTemplateSettings.DefaultTwitchFormatText, loaded.TwitchFormat);
        }
        finally
        {
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }

            if (System.IO.File.Exists(legacyPath))
            {
                System.IO.File.Delete(legacyPath);
            }
        }
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
