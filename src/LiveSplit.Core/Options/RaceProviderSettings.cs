using System;
using System.Xml;

using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace LiveSplit.Options;

public abstract class RaceProviderSettings : ICloneable
{
    public bool Enabled = true;

    public abstract string Name { get; set; }
    public abstract string DisplayName { get; }
    public abstract string WebsiteLink { get; }
    public abstract string RulesLink { get; }

    public abstract object Clone();

    public virtual Control GetSettingsControl()
    {
        return new TextBlock
        {
            Text = "There are no options available for this racing service.",
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    public virtual void FromXml(XmlElement element, Version version)
    {
        XmlAttribute enabled = element.Attributes["enabled"];
        if (enabled is null || !bool.TryParse(enabled.Value, out Enabled))
        {
            Enabled = true;
        }
    }

    public virtual XmlElement ToXml(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Plugin");

        XmlAttribute providerName = document.CreateAttribute("name");
        providerName.InnerText = Name;
        parent.Attributes.Append(providerName);

        XmlAttribute enabled = document.CreateAttribute("enabled");
        enabled.InnerText = Enabled.ToString();
        parent.Attributes.Append(enabled);

        return parent;
    }
}

public class UnloadedRaceProviderSettings : RaceProviderSettings
{
    public override string Name { get; set; }
    public override string DisplayName => Name;
    public override string WebsiteLink => string.Empty;
    public override string RulesLink => string.Empty;

    private string Content { get; set; } = string.Empty;

    public override object Clone()
    {
        return new UnloadedRaceProviderSettings
        {
            Enabled = Enabled,
            Name = Name,
            Content = Content
        };
    }

    public override Control GetSettingsControl()
    {
        return new TextBlock
        {
            Text = "Plugin could not be loaded",
            Foreground = Brushes.Red,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    public override void FromXml(XmlElement element, Version version)
    {
        base.FromXml(element, version);

        Content = element.InnerXml;

        if (element.Attributes["name"] is { } name)
        {
            Name = name.InnerText;
        }
    }

    public override XmlElement ToXml(XmlDocument document)
    {
        XmlElement element = base.ToXml(document);

        if (element.Attributes["name"] is null)
        {
            element.Attributes.Append(document.CreateAttribute("name"));
        }

        element.Attributes["name"].Value = Name;
        element.InnerXml = Content;
        return element;
    }
}
