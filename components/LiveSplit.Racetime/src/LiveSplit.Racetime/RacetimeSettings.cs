using System;
using System.Xml;

using Avalonia.Controls;
using Avalonia.Layout;

using LiveSplit.Options;

namespace LiveSplit.Racetime;

public class RacetimeSettings : RaceProviderSettings
{
    public const string LegacyProviderName = "LiveSplit.Racetime.dll";

    public override string Name
    {
        get => LegacyProviderName;
        set { }
    }

    public override string DisplayName => "racetime.gg";
    public override string WebsiteLink => "https://racetime.gg";
    public override string RulesLink => "https://racetime.gg/about/rules";

    public bool LoadChatHistory { get; set; } = true;
    public bool HideResults { get; set; }

    public override object Clone()
    {
        return new RacetimeSettings
        {
            Enabled = Enabled,
            LoadChatHistory = LoadChatHistory,
            HideResults = HideResults
        };
    }

    public override Control GetSettingsControl()
    {
        var loadChatHistory = new CheckBox
        {
            Content = "Load chat history",
            IsChecked = LoadChatHistory,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        loadChatHistory.IsCheckedChanged += (_, _) => LoadChatHistory = loadChatHistory.IsChecked == true;

        var hideResults = new CheckBox
        {
            Content = "Hide race results",
            IsChecked = HideResults,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        hideResults.IsCheckedChanged += (_, _) => HideResults = hideResults.IsChecked == true;

        return new StackPanel
        {
            Spacing = 6,
            Children =
            {
                loadChatHistory,
                hideResults
            }
        };
    }

    public override void FromXml(XmlElement element, Version version)
    {
        base.FromXml(element, version);
        LoadChatHistory = ParseBool(element["LoadChatHistory"], true);
        HideResults = ParseBool(element["HideResults"], true);
    }

    public override XmlElement ToXml(XmlDocument document)
    {
        XmlElement element = base.ToXml(document);
        AppendSetting(document, element, nameof(LoadChatHistory), LoadChatHistory);
        AppendSetting(document, element, nameof(HideResults), HideResults);
        return element;
    }

    private static bool ParseBool(XmlElement element, bool defaultValue)
    {
        return element is null || !bool.TryParse(element.InnerText, out bool value)
            ? defaultValue
            : value;
    }

    private static void AppendSetting(XmlDocument document, XmlElement parent, string name, bool value)
    {
        XmlElement element = document.CreateElement(name);
        element.InnerText = value.ToString();
        parent.AppendChild(element);
    }
}
