using System;
using System.Drawing;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.TimeFormatters;

namespace LiveSplit.UI.Components;

public class DeltaSettings
{
    public Color TextColor { get; set; }
    public bool OverrideTextColor { get; set; }
    public TimeAccuracy Accuracy { get; set; }
    public bool DropDecimals { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }
    public GradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public string Comparison { get; set; }
    public LiveSplitState CurrentState { get; set; }
    public bool Display2Rows { get; set; }

    public bool OverrideText { get; set; }
    public bool DifferentialText { get; set; }
    public string CustomText { get; set; }
    public string CustomTextAhead { get; set; }

    public LayoutMode Mode { get; set; }

    public DeltaSettings()
    {

        TextColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        Accuracy = TimeAccuracy.Tenths;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        Comparison = "Current Comparison";
        Display2Rows = false;
        DropDecimals = true;
        OverrideText = false;
        DifferentialText = false;
        CustomText = "";
        CustomTextAhead = "";

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        Accuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["Accuracy"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"]);
        Comparison = SettingsHelper.ParseString(element["Comparison"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"]);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"]);
        OverrideText = SettingsHelper.ParseBool(element["OverrideText"]);
        DifferentialText = SettingsHelper.ParseBool(element["DifferentialText"]);
        CustomText = SettingsHelper.ParseString(element["CustomText"]);
        CustomTextAhead = SettingsHelper.ParseString(element["CustomTextAhead"]);
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.4") ^
        SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "Accuracy", Accuracy) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideText", OverrideText) ^
        SettingsHelper.CreateSetting(document, parent, "DifferentialText", DifferentialText) ^
        SettingsHelper.CreateSetting(document, parent, "CustomText", CustomText) ^
        SettingsHelper.CreateSetting(document, parent, "CustomTextAhead", CustomTextAhead);
    }

}
