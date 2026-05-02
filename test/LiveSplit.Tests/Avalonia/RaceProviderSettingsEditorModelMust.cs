using System.Collections.Generic;
using System.Xml;

using Avalonia.Controls;

using LiveSplit.Avalonia;
using LiveSplit.Options;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class RaceProviderSettingsEditorModelMust
{
    [Fact]
    public void KeepOriginalSettingsUnchangedUntilApplied()
    {
        var original = new List<RaceProviderSettings>
        {
            new FakeRaceProviderSettings { Enabled = true, Marker = "original" }
        };

        var model = new RaceProviderSettingsEditorModel(original);
        ((FakeRaceProviderSettings)model.WorkingSettings[0]).Enabled = false;
        ((FakeRaceProviderSettings)model.WorkingSettings[0]).Marker = "edited";

        Assert.True(original[0].Enabled);
        Assert.Equal("original", ((FakeRaceProviderSettings)original[0]).Marker);
    }

    [Fact]
    public void ApplyWorkingSettingsBackToOriginalList()
    {
        var original = new List<RaceProviderSettings>
        {
            new FakeRaceProviderSettings { Enabled = true, Marker = "original" }
        };

        var model = new RaceProviderSettingsEditorModel(original);
        ((FakeRaceProviderSettings)model.WorkingSettings[0]).Enabled = false;
        ((FakeRaceProviderSettings)model.WorkingSettings[0]).Marker = "edited";

        model.Apply();

        Assert.False(original[0].Enabled);
        Assert.Equal("edited", ((FakeRaceProviderSettings)original[0]).Marker);
    }

    [Fact]
    public void ChangeEnabledStateOnWorkingSettings()
    {
        var original = new List<RaceProviderSettings>
        {
            new FakeRaceProviderSettings { Enabled = true, Marker = "original" }
        };

        var model = new RaceProviderSettingsEditorModel(original);

        model.SetEnabled(0, false);

        Assert.False(model.WorkingSettings[0].Enabled);
        Assert.True(original[0].Enabled);
    }

    private sealed class FakeRaceProviderSettings : RaceProviderSettings
    {
        public string Marker { get; set; }
        public override string Name { get; set; } = "LiveSplit.FakeRaceProvider.dll";
        public override string DisplayName => "Fake Race Provider";
        public override string WebsiteLink => string.Empty;
        public override string RulesLink => string.Empty;

        public override Control GetSettingsControl()
        {
            return new TextBlock();
        }

        public override XmlElement ToXml(XmlDocument document)
        {
            XmlElement element = base.ToXml(document);
            element.AppendChild(document.CreateElement(nameof(Marker))).InnerText = Marker;
            return element;
        }

        public override void FromXml(XmlElement element, System.Version version)
        {
            base.FromXml(element, version);
            Marker = element[nameof(Marker)]?.InnerText;
        }

        public override object Clone()
        {
            return new FakeRaceProviderSettings
            {
                Enabled = Enabled,
                Marker = Marker
            };
        }
    }
}
