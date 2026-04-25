using System;
using System.Drawing;
using System.Xml;

using LiveSplit.Model;

namespace LiveSplit.UI.Components;

public class WorldRecordSettings
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

    public LiveSplitState CurrentState { get; set; }
    public bool Display2Rows { get; set; }
    public bool CenteredText { get; set; }

    public bool FilterVariables { get; set; }
    public bool FilterPlatform { get; set; }
    public bool FilterRegion { get; set; }
    public bool FilterSubcategories { get; set; }

    public string TimingMethod { get; set; }
    public WorldRecordPrecisionType WRPrecision { get; set; }

    public LayoutMode Mode { get; set; }

    public WorldRecordSettings()
    {

        TextColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        TimeColor = Color.FromArgb(255, 255, 255);
        OverrideTimeColor = false;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = GradientType.Plain;
        Display2Rows = false;
        CenteredText = true;
        FilterVariables = false;
        FilterPlatform = false;
        FilterRegion = false;
        FilterSubcategories = true;
        TimingMethod = "Default for Leaderboard";
        WRPrecision = WorldRecordPrecisionType.FromLeaderboard;

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
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"]);
        CenteredText = SettingsHelper.ParseBool(element["CenteredText"]);
        FilterRegion = SettingsHelper.ParseBool(element["FilterRegion"]);
        FilterPlatform = SettingsHelper.ParseBool(element["FilterPlatform"]);
        FilterVariables = SettingsHelper.ParseBool(element["FilterVariables"]);
        FilterSubcategories = SettingsHelper.ParseBool(element["FilterSubcategories"], true);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Default for Leaderboard");
        WRPrecision = SettingsHelper.ParseEnum(element["PrecisionType"], WorldRecordPrecisionType.FromLeaderboard);
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
        SettingsHelper.CreateSetting(document, parent, "TimeColor", TimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimeColor", OverrideTimeColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "CenteredText", CenteredText) ^
        SettingsHelper.CreateSetting(document, parent, "FilterRegion", FilterRegion) ^
        SettingsHelper.CreateSetting(document, parent, "FilterPlatform", FilterPlatform) ^
        SettingsHelper.CreateSetting(document, parent, "FilterVariables", FilterVariables) ^
        SettingsHelper.CreateSetting(document, parent, "FilterSubcategories", FilterSubcategories) ^
        SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
        SettingsHelper.CreateSetting(document, parent, "PrecisionType", WRPrecision);
    }

}
