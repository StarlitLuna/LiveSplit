using System;
using System.Xml;

using Avalonia.Controls;

using LiveSplit.Racetime;
using LiveSplit.UI.Components;

using Xunit;

namespace LiveSplit.Tests.Options;

public class RacetimeSettingsMust
{
    [Fact]
    public void PreserveLegacyProviderNameAndSettingsXml()
    {
        var document = new XmlDocument();
        XmlElement plugin = document.CreateElement("Plugin");
        plugin.SetAttribute("name", "LiveSplit.Racetime.dll");
        plugin.SetAttribute("enabled", "False");
        plugin.InnerXml = """
            <LoadChatHistory>False</LoadChatHistory>
            <HideResults>True</HideResults>
            """;

        var settings = new RacetimeSettings();
        settings.FromXml(plugin, new Version(1, 8, 37));

        Assert.Equal("LiveSplit.Racetime.dll", settings.Name);
        Assert.Equal("racetime.gg", settings.DisplayName);
        Assert.False(settings.Enabled);
        Assert.False(settings.LoadChatHistory);
        Assert.True(settings.HideResults);
        Assert.IsAssignableFrom<Control>(settings.GetSettingsControl());

        XmlElement saved = settings.ToXml(document);

        Assert.Equal("LiveSplit.Racetime.dll", saved.GetAttribute("name"));
        Assert.Equal("False", saved.GetAttribute("enabled"));
        Assert.Equal("False", saved["LoadChatHistory"]?.InnerText);
        Assert.Equal("True", saved["HideResults"]?.InnerText);
    }

    [Fact]
    public void CreateProviderWithSettings()
    {
        var factory = new RacetimeFactory();

        RaceProviderAPI api = factory.Create(null, factory.CreateSettings());

        Assert.Equal("LiveSplit.Racetime.dll", api.Settings.Name);
        Assert.Equal("racetime.gg", api.ProviderName);
        Assert.Equal(string.Empty, api.Username);
    }
}
