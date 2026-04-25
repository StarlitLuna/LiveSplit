using System;
using System.Drawing;
using System.Xml;

using LiveSplit.Model;

namespace LiveSplit.UI.Components;

public class TextComponentSettings
{
    public Color TextColor { get; set; }
    public bool OverrideTextColor { get; set; }
    public Color TimeColor { get; set; }
    public bool OverrideTimeColor { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }
    public GradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public string Text1 { get; set; }
    public string Text2 { get; set; }

    // Legacy font overrides — read from old configs, not written or exposed in UI
    public bool OverrideFont1 { get; set; }
    public FontDescriptor Font1 { get; set; }
    public bool OverrideFont2 { get; set; }
    public FontDescriptor Font2 { get; set; }

    public LayoutMode Mode { get; set; }
    public bool Display2Rows { get; set; }
    public bool CustomVariable { get; set; }

    public LiveSplitState CurrentState { get; set; }

    public TextComponentSettings()
    {

        TextColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        TimeColor = Color.FromArgb(255, 255, 255);
        OverrideTimeColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        Text1 = "Text";
        Text2 = "";

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        TimeColor = SettingsHelper.ParseColor(element["TimeColor"]);
        OverrideTimeColor = SettingsHelper.ParseBool(element["OverrideTimeColor"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"]);
        Text1 = SettingsHelper.ParseString(element["Text1"]);
        Text2 = SettingsHelper.ParseString(element["Text2"]);
        Font1 = SettingsHelper.GetFontFromElement(element["Font1"]);
        Font2 = SettingsHelper.GetFontFromElement(element["Font2"]);
        OverrideFont1 = SettingsHelper.ParseBool(element["OverrideFont1"]);
        OverrideFont2 = SettingsHelper.ParseBool(element["OverrideFont2"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        CustomVariable = SettingsHelper.ParseBool(element["CustomVariable"], false);
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
        SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "Text1", Text1) ^
        SettingsHelper.CreateSetting(document, parent, "Text2", Text2) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "CustomVariable", CustomVariable);
    }

}
