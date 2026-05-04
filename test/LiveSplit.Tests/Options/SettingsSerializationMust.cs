using System.IO;

using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Options.SettingsSavers;
using LiveSplit.Web.SRL.RaceViewers;

using Xunit;

namespace LiveSplit.Tests.Options;

public class SettingsSerializationMust
{
    [Fact]
    public void RoundTripMasterSettingsAndPortAdditions()
    {
        ISettings settings = new StandardSettingsFactory().Create();
        settings.WarnOnReset = false;
        settings.SimpleSumOfBest = true;
        settings.RefreshRate = 144;
        settings.ServerPort = 12345;
        settings.ServerStartup = ServerStartupType.Websocket;
        settings.ServerState = ServerStateType.TCP;
        settings.LastComparison = "Best Segments";
        settings.AgreedToSRLRules = true;
        settings.UpdateCheckEnabled = true;
        settings.UILanguage = "en-US";
        settings.RaceViewer = new MultiTwitch();
        settings.RecentLayouts.Add(string.Empty);
        settings.RecentLayouts.Add(@"C:\Layouts\default.lsl");
        settings.RecentSplits.Add(new RecentSplitsFile(
            @"C:\Runs\game.lss",
            TimingMethod.GameTime,
            "Profile 2",
            "Example Game",
            "Any%"));
        settings.ActiveAutoSplitters.Add("Example.AutoSplitter.dll");
        settings.HcpHistorySize = 33;
        settings.HcpNBestRuns = 7;
        settings.ComparisonGeneratorStates["Balanced PB"] = true;

        settings.HotkeyProfiles["Profile 2"] = new HotkeyProfile
        {
            SplitKey = new KeyOrButton(Key.A),
            ResetKey = new KeyOrButton(Key.B),
            UndoKey = new KeyOrButton(Key.C),
            SkipKey = new KeyOrButton(Key.D),
            SwitchComparisonPrevious = new KeyOrButton(Key.E),
            SwitchComparisonNext = new KeyOrButton(Key.F),
            ToggleGlobalHotkeys = new KeyOrButton(Key.G),
            GlobalHotkeysEnabled = true,
            DeactivateHotkeysForOtherPrograms = true,
            DoubleTapPrevention = false,
            AllowGamepadsAsHotkeys = true,
            HotkeyDelay = 1.25f,
        };

        using var stream = new MemoryStream();
        new XMLSettingsSaver().Save(settings, stream);
        stream.Position = 0;

        ISettings loaded = new XMLSettingsFactory(stream).Create();

        Assert.False(loaded.WarnOnReset);
        Assert.True(loaded.SimpleSumOfBest);
        Assert.Equal(144, loaded.RefreshRate);
        Assert.Equal(12345, loaded.ServerPort);
        Assert.Equal(ServerStartupType.Websocket, loaded.ServerStartup);
        Assert.Equal(ServerStateType.TCP, loaded.ServerState);
        Assert.Equal("Best Segments", loaded.LastComparison);
        Assert.True(loaded.AgreedToSRLRules);
        Assert.True(loaded.UpdateCheckEnabled);
        Assert.Equal("en-US", loaded.UILanguage);
        Assert.Equal("MultiTwitch", loaded.RaceViewer.Name);
        Assert.Equal(new[] { string.Empty, @"C:\Layouts\default.lsl" }, loaded.RecentLayouts);
        RecentSplitsFile recent = Assert.Single(loaded.RecentSplits);
        Assert.Equal(@"C:\Runs\game.lss", recent.Path);
        Assert.Equal(TimingMethod.GameTime, recent.LastTimingMethod);
        Assert.Equal("Profile 2", recent.LastHotkeyProfile);
        Assert.Equal("Example Game", recent.GameName);
        Assert.Equal("Any%", recent.CategoryName);
        Assert.Equal("Example.AutoSplitter.dll", Assert.Single(loaded.ActiveAutoSplitters));
        Assert.Equal(33, loaded.HcpHistorySize);
        Assert.Equal(7, loaded.HcpNBestRuns);
        Assert.True(loaded.ComparisonGeneratorStates["Balanced PB"]);

        HotkeyProfile profile = loaded.HotkeyProfiles["Profile 2"];
        Assert.Equal("A", profile.SplitKey.ToString());
        Assert.Equal("G", profile.ToggleGlobalHotkeys.ToString());
        Assert.True(profile.GlobalHotkeysEnabled);
        Assert.True(profile.DeactivateHotkeysForOtherPrograms);
        Assert.False(profile.DoubleTapPrevention);
        Assert.True(profile.AllowGamepadsAsHotkeys);
        Assert.Equal(1.25f, profile.HotkeyDelay);
    }
}
