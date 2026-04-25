using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Xml;

using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.Model.Comparisons;
using LiveSplit.TimeFormatters;

namespace LiveSplit.UI.Components;

public class SplitsSettings
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    private int _VisualSplitCount { get; set; }
    public int VisualSplitCount
    {
        get => _VisualSplitCount;
        set => _VisualSplitCount = value;
    }
    static public ISegment HilightSplit { get; set; }
    static public ISegment SectionSplit { get; set; }

    public bool AutomaticAbbreviation { get; set; }
    public Color CurrentSplitTopColor { get; set; }
    public Color CurrentSplitBottomColor { get; set; }
    public int SplitPreviewCount { get; set; }
    public int MinimumMajorSplits { get; set; }
    public float SplitWidth { get; set; }
    public float SplitHeight { get; set; }
    public float ScaledSplitHeight { get => SplitHeight * 10f; set => SplitHeight = value / 10f; }
    public float IconSize { get; set; }

    public bool Display2Rows { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }

    public ExtendedGradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => BackgroundGradient.ToString();
        set => BackgroundGradient = (ExtendedGradientType)Enum.Parse(typeof(ExtendedGradientType), value);
    }

    public string HeaderComparison { get; set; }
    public string HeaderTimingMethod { get; set; }
    public LiveSplitState CurrentState { get; set; }

    public bool DisplayIcons { get; set; }
    public bool IconShadows { get; set; }
    public bool IndentBlankIcons { get; set; }
    public bool ShowThinSeparators { get; set; }
    public bool AlwaysShowLastSplit { get; set; }
    public bool LockLastSplit { get; set; }
    public bool SeparatorLastSplit { get; set; }

    public bool IndentSubsplits { get; set; }
    public bool HideSubsplits { get; set; }
    public bool ShowSubsplits { get; set; }
    public bool CurrentSectionOnly { get; set; }
    public bool OverrideSubsplitColor { get; set; }
    public Color SubsplitTopColor { get; set; }
    public Color SubsplitBottomColor { get; set; }
    public GradientType SubsplitGradient { get; set; }
    public string SubsplitGradientString
    {
        get => SubsplitGradient.ToString();
        set => SubsplitGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public bool ShowHeader { get; set; }
    public bool IndentSectionSplit { get; set; }
    public bool ShowIconSectionSplit { get; set; }
    public bool ShowSectionIcon { get; set; }
    public Color HeaderTopColor { get; set; }
    public Color HeaderBottomColor { get; set; }
    public GradientType HeaderGradient { get; set; }
    public string HeaderGradientString
    {
        get => HeaderGradient.ToString();
        set => HeaderGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }
    public bool OverrideHeaderColor { get; set; }
    public Color HeaderTextColor { get; set; }
    public bool HeaderText { get; set; }
    public Color HeaderTimesColor { get; set; }
    public bool HeaderTimes { get; set; }
    public TimeAccuracy HeaderAccuracy { get; set; }
    public bool SectionTimer { get; set; }
    public Color SectionTimerColor { get; set; }
    public bool SectionTimerGradient { get; set; }
    public TimeAccuracy SectionTimerAccuracy { get; set; }

    public bool DropDecimals { get; set; }
    public TimeAccuracy DeltasAccuracy { get; set; }

    public bool OverrideDeltasColor { get; set; }
    public Color DeltasColor { get; set; }

    public bool ShowColumnLabels { get; set; }
    public Color LabelsColor { get; set; }

    public Color BeforeNamesColor { get; set; }
    public Color CurrentNamesColor { get; set; }
    public Color AfterNamesColor { get; set; }
    public bool OverrideTextColor { get; set; }
    public Color BeforeTimesColor { get; set; }
    public Color CurrentTimesColor { get; set; }
    public Color AfterTimesColor { get; set; }
    public bool OverrideTimesColor { get; set; }

    public TimeAccuracy SplitTimesAccuracy { get; set; }
    public GradientType CurrentSplitGradient { get; set; }
    public string SplitGradientString
    {
        get => CurrentSplitGradient.ToString();
        set => CurrentSplitGradient = (GradientType)Enum.Parse(typeof(GradientType), value);
    }

    public event EventHandler SplitLayoutChanged;

    public LayoutMode Mode { get; set; }

    public IList<ColumnSettings> ColumnsList { get; set; }

    public SplitsSettings(LiveSplitState state)
    {
        CurrentState = state;

        AutomaticAbbreviation = false;
        VisualSplitCount = 8;
        SplitPreviewCount = 1;
        MinimumMajorSplits = 0;
        DisplayIcons = true;
        IconShadows = true;
        ShowThinSeparators = false;
        AlwaysShowLastSplit = true;
        LockLastSplit = true;
        SeparatorLastSplit = true;
        SplitTimesAccuracy = TimeAccuracy.Seconds;
        CurrentSplitTopColor = Color.FromArgb(51, 115, 244);
        CurrentSplitBottomColor = Color.FromArgb(21, 53, 116);
        SplitWidth = 20;
        SplitHeight = 3.6f;
        ScaledSplitHeight = 60;
        IconSize = 24f;
        BeforeNamesColor = Color.FromArgb(255, 255, 255);
        CurrentNamesColor = Color.FromArgb(255, 255, 255);
        AfterNamesColor = Color.FromArgb(255, 255, 255);
        OverrideTextColor = false;
        BeforeTimesColor = Color.FromArgb(255, 255, 255);
        CurrentTimesColor = Color.FromArgb(255, 255, 255);
        AfterTimesColor = Color.FromArgb(255, 255, 255);
        OverrideTimesColor = false;
        CurrentSplitGradient = GradientType.Vertical;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.FromArgb(1, 255, 255, 255);
        BackgroundGradient = ExtendedGradientType.Alternating;
        DropDecimals = true;
        DeltasAccuracy = TimeAccuracy.Tenths;
        OverrideDeltasColor = false;
        DeltasColor = Color.FromArgb(255, 255, 255);
        HeaderComparison = "Current Comparison";
        HeaderTimingMethod = "Current Timing Method";
        Display2Rows = false;
        ShowColumnLabels = false;
        LabelsColor = Color.FromArgb(255, 255, 255);

        IndentBlankIcons = true;
        IndentSubsplits = true;
        HideSubsplits = false;
        ShowSubsplits = false;
        CurrentSectionOnly = false;
        OverrideSubsplitColor = false;
        SubsplitTopColor = Color.FromArgb(0x8D, 0x00, 0x00, 0x00);
        SubsplitBottomColor = Color.Transparent;
        SubsplitGradient = GradientType.Plain;
        ShowHeader = true;
        IndentSectionSplit = true;
        ShowIconSectionSplit = true;
        ShowSectionIcon = true;
        HeaderTopColor = Color.FromArgb(0x2B, 0xFF, 0xFF, 0xFF);
        HeaderBottomColor = Color.FromArgb(0xD8, 0x00, 0x00, 0x00);
        HeaderGradient = GradientType.Vertical;
        OverrideHeaderColor = false;
        HeaderTextColor = Color.FromArgb(255, 255, 255);
        HeaderText = true;
        HeaderTimesColor = Color.FromArgb(255, 255, 255);
        HeaderTimes = true;
        HeaderAccuracy = TimeAccuracy.Tenths;
        SectionTimer = true;
        SectionTimerColor = Color.FromArgb(0x77, 0x77, 0x77);
        SectionTimerGradient = true;
        SectionTimerAccuracy = TimeAccuracy.Tenths;

        ColumnsList = [];
        ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method") });
        ColumnsList.Add(new ColumnSettings(CurrentState, "Time", ColumnsList) { Data = new ColumnData("Time", ColumnType.SplitTime, "Current Comparison", "Current Timing Method") });
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        AutomaticAbbreviation = SettingsHelper.ParseBool(element["AutomaticAbbreviation"], false);
        CurrentSplitTopColor = SettingsHelper.ParseColor(element["CurrentSplitTopColor"], Color.FromArgb(51, 115, 244));
        CurrentSplitBottomColor = SettingsHelper.ParseColor(element["CurrentSplitBottomColor"], Color.FromArgb(21, 53, 116));
        VisualSplitCount = SettingsHelper.ParseInt(element["VisualSplitCount"], 8);
        SplitPreviewCount = SettingsHelper.ParseInt(element["SplitPreviewCount"], 1);
        MinimumMajorSplits = SettingsHelper.ParseInt(element["MinimumMajorSplits"], 0);
        DisplayIcons = SettingsHelper.ParseBool(element["DisplayIcons"], true);
        ShowThinSeparators = SettingsHelper.ParseBool(element["ShowThinSeparators"], false);
        AlwaysShowLastSplit = SettingsHelper.ParseBool(element["AlwaysShowLastSplit"], true);
        SplitWidth = SettingsHelper.ParseFloat(element["SplitWidth"], 20);
        IndentBlankIcons = SettingsHelper.ParseBool(element["IndentBlankIcons"], true);
        IndentSubsplits = SettingsHelper.ParseBool(element["IndentSubsplits"], true);
        HideSubsplits = SettingsHelper.ParseBool(element["HideSubsplits"], false);
        ShowSubsplits = SettingsHelper.ParseBool(element["ShowSubsplits"], false);
        CurrentSectionOnly = SettingsHelper.ParseBool(element["CurrentSectionOnly"], false);
        OverrideSubsplitColor = SettingsHelper.ParseBool(element["OverrideSubsplitColor"], false);
        SubsplitTopColor = SettingsHelper.ParseColor(element["SubsplitTopColor"], Color.FromArgb(0x8D, 0x00, 0x00, 0x00));
        SubsplitBottomColor = SettingsHelper.ParseColor(element["SubsplitBottomColor"], Color.Transparent);
        SubsplitGradientString = SettingsHelper.ParseString(element["SubsplitGradient"], GradientType.Plain.ToString());
        ShowHeader = SettingsHelper.ParseBool(element["ShowHeader"], true);
        IndentSectionSplit = SettingsHelper.ParseBool(element["IndentSectionSplit"], true);
        ShowIconSectionSplit = SettingsHelper.ParseBool(element["ShowIconSectionSplit"], true);
        ShowSectionIcon = SettingsHelper.ParseBool(element["ShowSectionIcon"], true);
        HeaderTopColor = SettingsHelper.ParseColor(element["HeaderTopColor"], Color.FromArgb(0x2B, 0xFF, 0xFF, 0xFF));
        HeaderBottomColor = SettingsHelper.ParseColor(element["HeaderBottomColor"], Color.FromArgb(0xD8, 0x00, 0x00, 0x00));
        HeaderGradientString = SettingsHelper.ParseString(element["HeaderGradient"], GradientType.Vertical.ToString());
        OverrideHeaderColor = SettingsHelper.ParseBool(element["OverrideHeaderColor"], false);
        HeaderTextColor = SettingsHelper.ParseColor(element["HeaderTextColor"], Color.FromArgb(255, 255, 255));
        HeaderText = SettingsHelper.ParseBool(element["HeaderText"], true);
        HeaderTimesColor = SettingsHelper.ParseColor(element["HeaderTimesColor"], Color.FromArgb(255, 255, 255));
        HeaderTimes = SettingsHelper.ParseBool(element["HeaderTimes"], true);
        HeaderAccuracy = SettingsHelper.ParseEnum(element["HeaderAccuracy"], TimeAccuracy.Tenths);
        SectionTimer = SettingsHelper.ParseBool(element["SectionTimer"], true);
        SectionTimerColor = SettingsHelper.ParseColor(element["SectionTimerColor"], Color.FromArgb(0x77, 0x77, 0x77));
        SectionTimerGradient = SettingsHelper.ParseBool(element["SectionTimerGradient"], true);
        SectionTimerAccuracy = SettingsHelper.ParseEnum(element["SectionTimerAccuracy"], TimeAccuracy.Tenths);
        OverrideTimesColor = SettingsHelper.ParseBool(element["OverrideTimesColor"], false);
        BeforeTimesColor = SettingsHelper.ParseColor(element["BeforeTimesColor"], Color.FromArgb(255, 255, 255));
        CurrentTimesColor = SettingsHelper.ParseColor(element["CurrentTimesColor"], Color.FromArgb(255, 255, 255));
        AfterTimesColor = SettingsHelper.ParseColor(element["AfterTimesColor"], Color.FromArgb(255, 255, 255));
        SplitHeight = SettingsHelper.ParseFloat(element["SplitHeight"], 3.6f);
        SplitGradientString = SettingsHelper.ParseString(element["CurrentSplitGradient"], GradientType.Vertical.ToString());
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.FromArgb(1, 255, 255, 255));
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"], ExtendedGradientType.Alternating.ToString());
        SeparatorLastSplit = SettingsHelper.ParseBool(element["SeparatorLastSplit"], true);
        DropDecimals = SettingsHelper.ParseBool(element["DropDecimals"], true);
        DeltasAccuracy = SettingsHelper.ParseEnum(element["DeltasAccuracy"], TimeAccuracy.Tenths);
        OverrideDeltasColor = SettingsHelper.ParseBool(element["OverrideDeltasColor"], false);
        DeltasColor = SettingsHelper.ParseColor(element["DeltasColor"], Color.FromArgb(255, 255, 255));
        Display2Rows = SettingsHelper.ParseBool(element["Display2Rows"], false);
        SplitTimesAccuracy = SettingsHelper.ParseEnum(element["SplitTimesAccuracy"], TimeAccuracy.Seconds);
        LockLastSplit = SettingsHelper.ParseBool(element["LockLastSplit"], true);
        IconSize = SettingsHelper.ParseFloat(element["IconSize"], 24f);
        IconShadows = SettingsHelper.ParseBool(element["IconShadows"], true);
        ShowColumnLabels = SettingsHelper.ParseBool(element["ShowColumnLabels"], false);
        LabelsColor = SettingsHelper.ParseColor(element["LabelsColor"], Color.FromArgb(255, 255, 255));

        if (version >= new Version(1, 7))
        {
            HeaderComparison = SettingsHelper.ParseString(element["HeaderComparison"], "Current Comparison");
            HeaderTimingMethod = SettingsHelper.ParseString(element["HeaderTimingMethod"], "Current Timing Method");
            XmlElement columnsElement = element["Columns"];
            ColumnsList.Clear();
            foreach (object child in columnsElement.ChildNodes)
            {
                var columnData = ColumnData.FromXml((XmlNode)child);
                ColumnsList.Add(new ColumnSettings(CurrentState, columnData.Name, ColumnsList) { Data = columnData });
            }
        }
        else
        {
            HeaderComparison = SettingsHelper.ParseString(element["Comparison"], "Current Comparison");
            HeaderTimingMethod = "Current Timing Method";
            ColumnsList.Clear();
            if (SettingsHelper.ParseBool(element["ShowSplitTimes"]))
            {
                ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.Delta, HeaderComparison, "Current Timing Method") });
                ColumnsList.Add(new ColumnSettings(CurrentState, "Time", ColumnsList) { Data = new ColumnData("Time", ColumnType.SplitTime, HeaderComparison, "Current Timing Method") });
            }
            else
            {
                ColumnsList.Add(new ColumnSettings(CurrentState, "+/-", ColumnsList) { Data = new ColumnData("+/-", ColumnType.DeltaorSplitTime, HeaderComparison, "Current Timing Method") });
            }
        }

        if (version >= new Version(1, 3))
        {
            BeforeNamesColor = SettingsHelper.ParseColor(element["BeforeNamesColor"]);
            CurrentNamesColor = SettingsHelper.ParseColor(element["CurrentNamesColor"]);
            AfterNamesColor = SettingsHelper.ParseColor(element["AfterNamesColor"]);
            OverrideTextColor = SettingsHelper.ParseBool(element["OverrideTextColor"]);
        }
        else
        {
            if (version >= new Version(1, 2))
            {
                BeforeNamesColor = CurrentNamesColor = AfterNamesColor = SettingsHelper.ParseColor(element["SplitNamesColor"]);
            }
            else
            {
                BeforeNamesColor = Color.FromArgb(255, 255, 255);
                CurrentNamesColor = Color.FromArgb(255, 255, 255);
                AfterNamesColor = Color.FromArgb(255, 255, 255);
            }

            OverrideTextColor = !SettingsHelper.ParseBool(element["UseTextColor"], true);
        }
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
        int hashCode = SettingsHelper.CreateSetting(document, parent, "Version", "1.7") ^
        SettingsHelper.CreateSetting(document, parent, "AutomaticAbbreviation", AutomaticAbbreviation) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSplitTopColor", CurrentSplitTopColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSplitBottomColor", CurrentSplitBottomColor) ^
        SettingsHelper.CreateSetting(document, parent, "VisualSplitCount", VisualSplitCount) ^
        SettingsHelper.CreateSetting(document, parent, "SplitPreviewCount", SplitPreviewCount) ^
        SettingsHelper.CreateSetting(document, parent, "MinimumMajorSplits", MinimumMajorSplits) ^
        SettingsHelper.CreateSetting(document, parent, "DisplayIcons", DisplayIcons) ^
        SettingsHelper.CreateSetting(document, parent, "ShowThinSeparators", ShowThinSeparators) ^
        SettingsHelper.CreateSetting(document, parent, "AlwaysShowLastSplit", AlwaysShowLastSplit) ^
        SettingsHelper.CreateSetting(document, parent, "SplitWidth", SplitWidth) ^
        SettingsHelper.CreateSetting(document, parent, "SplitTimesAccuracy", SplitTimesAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "BeforeNamesColor", BeforeNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentNamesColor", CurrentNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "AfterNamesColor", AfterNamesColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTextColor", OverrideTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "BeforeTimesColor", BeforeTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentTimesColor", CurrentTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "AfterTimesColor", AfterTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimesColor", OverrideTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "LockLastSplit", LockLastSplit) ^
        SettingsHelper.CreateSetting(document, parent, "IconSize", IconSize) ^
        SettingsHelper.CreateSetting(document, parent, "IconShadows", IconShadows) ^
        SettingsHelper.CreateSetting(document, parent, "SplitHeight", SplitHeight) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSplitGradient", CurrentSplitGradient) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "SeparatorLastSplit", SeparatorLastSplit) ^
        SettingsHelper.CreateSetting(document, parent, "DeltasAccuracy", DeltasAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "DropDecimals", DropDecimals) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideDeltasColor", OverrideDeltasColor) ^
        SettingsHelper.CreateSetting(document, parent, "DeltasColor", DeltasColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderComparison", HeaderComparison) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTimingMethod", HeaderTimingMethod) ^
        SettingsHelper.CreateSetting(document, parent, "Display2Rows", Display2Rows) ^
        SettingsHelper.CreateSetting(document, parent, "IndentBlankIcons", IndentBlankIcons) ^
        SettingsHelper.CreateSetting(document, parent, "IndentSubsplits", IndentSubsplits) ^
        SettingsHelper.CreateSetting(document, parent, "HideSubsplits", HideSubsplits) ^
        SettingsHelper.CreateSetting(document, parent, "ShowSubsplits", ShowSubsplits) ^
        SettingsHelper.CreateSetting(document, parent, "CurrentSectionOnly", CurrentSectionOnly) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideSubsplitColor", OverrideSubsplitColor) ^
        SettingsHelper.CreateSetting(document, parent, "SubsplitGradient", SubsplitGradient) ^
        SettingsHelper.CreateSetting(document, parent, "ShowHeader", ShowHeader) ^
        SettingsHelper.CreateSetting(document, parent, "IndentSectionSplit", IndentSectionSplit) ^
        SettingsHelper.CreateSetting(document, parent, "ShowIconSectionSplit", ShowIconSectionSplit) ^
        SettingsHelper.CreateSetting(document, parent, "ShowSectionIcon", ShowSectionIcon) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderGradient", HeaderGradient) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideHeaderColor", OverrideHeaderColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderText", HeaderText) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTimes", HeaderTimes) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderAccuracy", HeaderAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "SectionTimer", SectionTimer) ^
        SettingsHelper.CreateSetting(document, parent, "SectionTimerGradient", SectionTimerGradient) ^
        SettingsHelper.CreateSetting(document, parent, "SectionTimerAccuracy", SectionTimerAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "SubsplitTopColor", SubsplitTopColor) ^
        SettingsHelper.CreateSetting(document, parent, "SubsplitBottomColor", SubsplitBottomColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTopColor", HeaderTopColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderBottomColor", HeaderBottomColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTextColor", HeaderTextColor) ^
        SettingsHelper.CreateSetting(document, parent, "HeaderTimesColor", HeaderTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "SectionTimerColor", SectionTimerColor) ^
        SettingsHelper.CreateSetting(document, parent, "ShowColumnLabels", ShowColumnLabels) ^
        SettingsHelper.CreateSetting(document, parent, "LabelsColor", LabelsColor);

        XmlElement columnsElement = null;
        if (document != null)
        {
            columnsElement = document.CreateElement("Columns");
            parent.AppendChild(columnsElement);
        }

        int count = 1;
        foreach (ColumnData columnData in ColumnsList.Select(x => x.Data))
        {
            XmlElement settings = null;
            if (document != null)
            {
                settings = document.CreateElement("Settings");
                columnsElement.AppendChild(settings);
            }

            hashCode ^= columnData.CreateElement(document, settings) * count;
            count++;
        }

        return hashCode;
    }

}
