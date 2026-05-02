using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.Options.SettingsFactories;
using LiveSplit.Options.SettingsSavers;
using LiveSplit.UI.Components;
using LiveSplit.Web.SRL;

using Xunit;

namespace LiveSplit.Tests.Options;

public class RaceProviderSettingsMust
{
    [Fact]
    public void IncludeBuiltInSpeedRunsLiveProviderByDefault()
    {
        IDictionary<string, IRaceProviderFactory> previousFactories = ComponentManager.RaceProviderFactories;
        ComponentManager.RaceProviderFactories = null;

        try
        {
            ISettings settings = new StandardSettingsFactory().Create();

            var provider = Assert.IsType<SRLSettings>(
                Assert.Single(settings.RaceProvider.Where(x => x.Name == "SRL")));
            Assert.True(provider.Enabled);
            RaceProviderAPI api = ComponentManager.RaceProviderFactories["SRL"].Create(null, provider);
            Assert.NotNull(api.JoinRace);
            Assert.NotNull(api.CreateRace);
        }
        finally
        {
            ComponentManager.RaceProviderFactories = previousFactories;
        }
    }

    [Fact]
    public void SaveAndLoadRaceProviderSettingsCompatibleWithMasterSettingsXml()
    {
        IDictionary<string, IRaceProviderFactory> previousFactories = ComponentManager.RaceProviderFactories;
        ComponentManager.RaceProviderFactories = new Dictionary<string, IRaceProviderFactory>
        {
            [FakeRaceProviderSettings.ProviderName] = new FakeRaceProviderFactory()
        };

        try
        {
            ISettings settings = new StandardSettingsFactory().Create();
            settings.AgreedToSRLRules = true;
            FakeRaceProviderSettings provider = Assert.IsType<FakeRaceProviderSettings>(
                settings.RaceProvider.Single(x => x.Name == FakeRaceProviderSettings.ProviderName));
            provider.Enabled = false;
            provider.CustomValue = "preserved";

            using var stream = new MemoryStream();
            new XMLSettingsSaver().Save(settings, stream);
            stream.Position = 0;

            ISettings loaded = new XMLSettingsFactory(stream).Create();

            Assert.Equal("SpeedRunsLive", loaded.RaceViewer.Name);
            Assert.True(loaded.AgreedToSRLRules);

            FakeRaceProviderSettings loadedProvider = Assert.IsType<FakeRaceProviderSettings>(
                loaded.RaceProvider.Single(x => x.Name == FakeRaceProviderSettings.ProviderName));
            Assert.False(loadedProvider.Enabled);
            Assert.Equal("preserved", loadedProvider.CustomValue);
        }
        finally
        {
            ComponentManager.RaceProviderFactories = previousFactories;
        }
    }

    [Fact]
    public void PreserveUnknownRaceProviderSettingsXml()
    {
        IDictionary<string, IRaceProviderFactory> previousFactories = ComponentManager.RaceProviderFactories;
        ComponentManager.RaceProviderFactories = new Dictionary<string, IRaceProviderFactory>();

        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Settings version="1.8.18">
              <HotkeyProfiles>
                <HotkeyProfile name="Default">
                  <SplitKey>NumPad1</SplitKey>
                  <ResetKey>NumPad3</ResetKey>
                  <SkipKey>NumPad2</SkipKey>
                  <UndoKey>NumPad8</UndoKey>
                  <PauseKey></PauseKey>
                  <ToggleGlobalHotkeys></ToggleGlobalHotkeys>
                  <SwitchComparisonPrevious>NumPad4</SwitchComparisonPrevious>
                  <SwitchComparisonNext>NumPad6</SwitchComparisonNext>
                  <GlobalHotkeysEnabled>False</GlobalHotkeysEnabled>
                  <DeactivateHotkeysForOtherPrograms>False</DeactivateHotkeysForOtherPrograms>
                  <DoubleTapPrevention>True</DoubleTapPrevention>
                  <HotkeyDelay>0</HotkeyDelay>
                  <AllowGamepadsAsHotkeys>False</AllowGamepadsAsHotkeys>
                </HotkeyProfile>
              </HotkeyProfiles>
              <WarnOnReset>True</WarnOnReset>
              <RaceViewer>SpeedRunsLive</RaceViewer>
              <AgreedToSRLRules>True</AgreedToSRLRules>
              <EnableDPIAwareness>False</EnableDPIAwareness>
              <UILanguage></UILanguage>
              <RecentSplits />
              <RecentLayouts />
              <LastComparison>Personal Best</LastComparison>
              <SimpleSumOfBest>False</SimpleSumOfBest>
              <RefreshRate>40</RefreshRate>
              <ServerPort>16834</ServerPort>
              <ServerStartup>0</ServerStartup>
              <ServerState>0</ServerState>
              <ComparisonGeneratorStates />
              <RaceProviderPlugins>
                <Plugin name="LiveSplit.UnknownRaceProvider.dll" enabled="False"><Token>secret</Token></Plugin>
              </RaceProviderPlugins>
              <ActiveAutoSplitters />
            </Settings>
            """;

        try
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            ISettings loaded = new XMLSettingsFactory(input).Create();

            var unknown = Assert.Single(
                loaded.RaceProvider.OfType<UnloadedRaceProviderSettings>(),
                x => x.Name == "LiveSplit.UnknownRaceProvider.dll");
            Assert.Equal("LiveSplit.UnknownRaceProvider.dll", unknown.Name);
            Assert.False(unknown.Enabled);

            using var output = new MemoryStream();
            new XMLSettingsSaver().Save(loaded, output);
            output.Position = 0;
            var document = new XmlDocument();
            document.Load(output);

            XmlElement plugin = Assert.Single(
                document["Settings"]["RaceProviderPlugins"].ChildNodes.OfType<XmlElement>(),
                x => x.GetAttribute("name") == "LiveSplit.UnknownRaceProvider.dll");
            Assert.Equal("LiveSplit.UnknownRaceProvider.dll", plugin.GetAttribute("name"));
            Assert.Equal("False", plugin.GetAttribute("enabled"));
            Assert.Equal("<Token>secret</Token>", plugin.InnerXml);
        }
        finally
        {
            ComponentManager.RaceProviderFactories = previousFactories;
        }
    }

    private sealed class FakeRaceProviderFactory : IRaceProviderFactory
    {
        public string UpdateName => "Fake Race Provider";
        public string XMLURL => string.Empty;
        public string UpdateURL => string.Empty;
        public Version Version => new(1, 0);

        public RaceProviderAPI Create(ITimerModel model, RaceProviderSettings settings)
        {
            return new FakeRaceProviderApi(settings);
        }

        public RaceProviderSettings CreateSettings() => new FakeRaceProviderSettings();
    }

    private sealed class FakeRaceProviderSettings : RaceProviderSettings
    {
        public const string ProviderName = "LiveSplit.FakeRaceProvider.dll";

        public override string Name { get; set; } = ProviderName;
        public override string DisplayName => "Fake Race Provider";
        public override string WebsiteLink => "https://example.invalid/";
        public override string RulesLink => "https://example.invalid/rules";
        public string CustomValue { get; set; } = string.Empty;

        public override object Clone()
        {
            return new FakeRaceProviderSettings
            {
                Enabled = Enabled,
                CustomValue = CustomValue
            };
        }

        public override void FromXml(XmlElement element, Version version)
        {
            base.FromXml(element, version);
            CustomValue = element["CustomValue"]?.InnerText ?? string.Empty;
        }

        public override XmlElement ToXml(XmlDocument document)
        {
            XmlElement element = base.ToXml(document);
            XmlElement custom = document.CreateElement("CustomValue");
            custom.InnerText = CustomValue;
            element.AppendChild(custom);
            return element;
        }
    }

    private sealed class FakeRaceProviderApi : RaceProviderAPI
    {
        public FakeRaceProviderApi(RaceProviderSettings settings)
        {
            Settings = settings;
        }

        public override string ProviderName => "Fake";
        public override string Username => string.Empty;

        public override IEnumerable<IRaceInfo> GetRaces() => [];

        public override void RefreshRacesListAsync()
        {
        }
    }
}
