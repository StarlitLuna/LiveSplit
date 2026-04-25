using System;
using System.Drawing;
using System.Linq;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;

namespace LiveSplit.UI.Components;

public class PreviousSegmentSettings
{
    public Color TextColor { get; set; }
    public bool OverrideTextColor { get; set; }
    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }
    public GradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public TimeAccuracy DeltaAccuracy { get; set; }
    public bool DropDecimals { get; set; }
    public bool Display2Rows { get; set; }
    public bool ShowPossibleTimeSave { get; set; }
    public TimeAccuracy TimeSaveAccuracy { get; set; }

    public string Comparison { get; set; }
    public LiveSplitState CurrentState { get; set; }

    public LayoutMode Mode { get; set; }

    public PreviousSegmentSettings()
    {

        TextColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        DeltaAccuracy = TimeAccuracy.Tenths;
        TimeSaveAccuracy = TimeAccuracy.Tenths;
        DropDecimals = true;
        Comparison = "Current Comparison";
        Display2Rows = false;
        ShowPossibleTimeSave = false;

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        TextColor = SettingsHelper.ParseColor(element["TextColor"]);
        OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"]);
        DeltaAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["DeltaAccuracy"]);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"]);
        Comparison = SettingsHelper.ParseString(element["Comparison"]);
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        ShowPossibleTimeSave = SettingsHelper.ParseBool(element["ShowPossibleTimeSave"], false);
        TimeSaveAccuracy = SettingsHelper.ParseEnum(element["TimeSaveAccuracy"], TimeAccuracy.Tenths);
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.6") ^
        SettingsHelper.CreateSetting(document, parent, "TextColor", TextColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "DeltaAccuracy", DeltaAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "ShowPossibleTimeSave", ShowPossibleTimeSave) ^
        SettingsHelper.CreateSetting(document, parent, "TimeSaveAccuracy", TimeSaveAccuracy);
    }

}
