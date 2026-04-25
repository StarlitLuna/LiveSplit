using System;
using System.Drawing;
using System.Xml;

using LiveSplit.Localization;

namespace LiveSplit.UI.Components;

public class BlankSpaceSettings
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    public float SpaceHeight { get; set; }
    public float SpaceWidth { get; set; }

    public LayoutMode Mode { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }
    public GradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public BlankSpaceSettings()
    {

        SpaceHeight = 100;
        SpaceWidth = 100;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        SpaceHeight = SettingsHelper.ParseFloat(element["SpaceHeight"]);
        SpaceWidth = SettingsHelper.ParseFloat(element["SpaceWidth"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"], GradientType.Plain.ToString());
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
    {
        return CreateSettingsNode(null, null);
    }

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.7") ^
        SettingsHelper.CreateSetting(document, parent, "SpaceHeight", SpaceHeight) ^
        SettingsHelper.CreateSetting(document, parent, "SpaceWidth", SpaceWidth) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient);
    }

}
