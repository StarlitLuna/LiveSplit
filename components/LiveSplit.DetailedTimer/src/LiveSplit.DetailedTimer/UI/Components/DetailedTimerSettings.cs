using System;
using System.Drawing;
using System.Xml;

using LiveSplit.Localization;
using LiveSplit.Model;
using LiveSplit.TimeFormatters;

namespace LiveSplit.UI.Components;

public class DetailedTimerSettings
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    public float Height { get; set; }
    public float Width { get; set; }
    public float SegmentTimerSizeRatio { get; set; }
    public LiveSplitState CurrentState { get; set; }

    public bool TimerShowGradient { get; set; }
    public bool OverrideTimerColors { get; set; }
    public bool SegmentTimerShowGradient { get; set; }
    public bool ShowSplitName { get; set; }

    public float IconSize { get; set; }
    public bool DisplayIcon { get; set; }

    public float DecimalsSize { get; set; }
    public float SegmentTimerDecimalsSize { get; set; }

    public Color TimerColor { get; set; }
    public Color SegmentTimerColor { get; set; }
    public Color SegmentLabelsColor { get; set; }
    public Color SegmentTimesColor { get; set; }
    public Color SplitNameColor { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }

    public DeltasGradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => TimerSettings.GetBackgroundTypeString(BackgroundGradient);
        set => BackgroundGradient = (DeltasGradientType)Enum.Parse(typeof(DeltasGradientType), value.Replace(" ", ""));
    }
    private string timerFormat
    {
        get => DigitsFormat + Accuracy;
        set
        {
            int decimalIndex = value.IndexOf('.');
            if (decimalIndex < 0)
            {
                DigitsFormat = value;
                Accuracy = "";
            }
            else
            {
                DigitsFormat = value[..decimalIndex];
                Accuracy = value[decimalIndex..];
            }
        }
    }
    public string DigitsFormat { get; set; }
    public string Accuracy { get; set; }
    private string segmentTimerFormat
    {
        get => SegmentDigitsFormat + SegmentAccuracy;
        set
        {
            int decimalIndex = value.IndexOf('.');
            if (decimalIndex < 0)
            {
                SegmentDigitsFormat = value;
                SegmentAccuracy = "";
            }
            else
            {
                SegmentDigitsFormat = value[..decimalIndex];
                SegmentAccuracy = value[decimalIndex..];
            }
        }
    }
    public string SegmentDigitsFormat { get; set; }
    public string SegmentAccuracy { get; set; }
    public TimeAccuracy SegmentTimesAccuracy { get; set; }
    public GeneralTimeFormatter SegmentTimesFormatter { get; set; } = new GeneralTimeFormatter()
    {
        NullFormat = NullFormat.Dash,
        Accuracy = TimeAccuracy.Hundredths
    };
    public string SegmentLabelsFontString => SettingsHelper.FormatFont(SegmentLabelsFont);
    public FontDescriptor SegmentLabelsFont { get; set; }
    public string SegmentTimesFontString => SettingsHelper.FormatFont(SegmentTimesFont);
    public FontDescriptor SegmentTimesFont { get; set; }
    public string SplitNameFontString => SettingsHelper.FormatFont(SplitNameFont);
    public FontDescriptor SplitNameFont { get; set; }

    public string Comparison { get; set; }
    public string Comparison2 { get; set; }
    public bool HideComparison { get; set; }
    public string TimingMethod { get; set; }

    public LayoutMode Mode { get; set; }

    public DetailedTimerSettings()
    {

        Height = 75;
        Width = 200;
        SegmentTimerSizeRatio = 40;

        TimerShowGradient = true;
        OverrideTimerColors = false;
        SegmentTimerShowGradient = true;
        ShowSplitName = false;

        TimerColor = Color.FromArgb(170, 170, 170);
        SegmentTimerColor = Color.FromArgb(170, 170, 170);
        SegmentLabelsColor = Color.FromArgb(255, 255, 255);
        SegmentTimesColor = Color.FromArgb(255, 255, 255);
        SplitNameColor = Color.FromArgb(255, 255, 255);

        DigitsFormat = "1";
        Accuracy = ".23";
        SegmentDigitsFormat = "1";
        SegmentAccuracy = ".23";
        SegmentTimesAccuracy = TimeAccuracy.Hundredths;

        SegmentLabelsFont = new FontDescriptor("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
        SegmentTimesFont = new FontDescriptor("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel);
        SplitNameFont = new FontDescriptor("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);

        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = DeltasGradientType.Plain;

        IconSize = 40f;
        DisplayIcon = false;

        DecimalsSize = 35f;
        SegmentTimerDecimalsSize = 35f;

        Comparison = "Current Comparison";
        Comparison2 = "Best Segments";
        HideComparison = false;
        TimingMethod = "Current Timing Method";

    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        Height = SettingsHelper.ParseFloat(element["Height"]);
        Width = SettingsHelper.ParseFloat(element["Width"]);
        SegmentTimerSizeRatio = SettingsHelper.ParseFloat(element["SegmentTimerSizeRatio"]);
        TimerShowGradient = SettingsHelper.ParseBool(element["TimerShowGradient"]);
        SegmentTimerShowGradient = SettingsHelper.ParseBool(element["SegmentTimerShowGradient"]);
        TimerColor = SettingsHelper.ParseColor(element["TimerColor"]);
        SegmentTimerColor = SettingsHelper.ParseColor(element["SegmentTimerColor"]);
        SegmentLabelsColor = SettingsHelper.ParseColor(element["SegmentLabelsColor"]);
        SegmentTimesColor = SettingsHelper.ParseColor(element["SegmentTimesColor"]);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Current Timing Method");
        DecimalsSize = SettingsHelper.ParseFloat(element["DecimalsSize"], 35f);
        SegmentTimerDecimalsSize = SettingsHelper.ParseFloat(element["SegmentTimerDecimalsSize"], 35f);
        DisplayIcon = SettingsHelper.ParseBool(element["DisplayIcon"], false);
        IconSize = SettingsHelper.ParseFloat(element["IconSize"], 40f);
        ShowSplitName = SettingsHelper.ParseBool(element["ShowSplitName"], false);
        SplitNameColor = SettingsHelper.ParseColor(element["SplitNameColor"], Color.FromArgb(255, 255, 255));
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"], DeltasGradientType.Plain.ToString());
        Comparison = SettingsHelper.ParseString(element["Comparison"], "Current Comparison");
        Comparison2 = SettingsHelper.ParseString(element["Comparison2"], "Best Segments");
        HideComparison = SettingsHelper.ParseBool(element["HideComparison"], false);

        SegmentTimesAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["SegmentTimesAccuracy"]);
        SegmentTimesFormatter.Accuracy = SegmentTimesAccuracy;

        if (version >= new Version(1, 3))
        {
            OverrideTimerColors = SettingsHelper.ParseBool(element["OverrideTimerColors"]);
            SegmentLabelsFont = SettingsHelper.GetFontFromElement(element["SegmentLabelsFont"]);
            SegmentTimesFont = SettingsHelper.GetFontFromElement(element["SegmentTimesFont"]);
            SplitNameFont = SettingsHelper.GetFontFromElement(element["SplitNameFont"]);
        }
        else
        {
            OverrideTimerColors = !SettingsHelper.ParseBool(element["TimerUseSplitColors"]);
            SegmentLabelsFont = new FontDescriptor("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
            SegmentTimesFont = new FontDescriptor("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel);
            SplitNameFont = new FontDescriptor("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        if (version >= new Version(1, 5))
        {
            timerFormat = element["TimerFormat"].InnerText;
            segmentTimerFormat = element["SegmentTimerFormat"].InnerText;
        }
        else
        {
            DigitsFormat = "1";
            SegmentDigitsFormat = "1";
            TimeAccuracy timerAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["TimerAccuracy"]);
            Accuracy = timerAccuracy switch
            {
                TimeAccuracy.Seconds => "",
                TimeAccuracy.Tenths => ".2",
                TimeAccuracy.Hundredths => ".23",
                TimeAccuracy.Milliseconds => ".234",
                _ => ".23",
            };
            TimeAccuracy segmentTimerAccuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["SegmentTimerAccuracy"]);
            SegmentAccuracy = segmentTimerAccuracy switch
            {
                TimeAccuracy.Seconds => "",
                TimeAccuracy.Tenths => ".2",
                TimeAccuracy.Hundredths => ".23",
                TimeAccuracy.Milliseconds => ".234",
                _ => ".23",
            };
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
        return SettingsHelper.CreateSetting(document, parent, "Version", "1.5") ^
        SettingsHelper.CreateSetting(document, parent, "Height", Height) ^
        SettingsHelper.CreateSetting(document, parent, "Width", Width) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerSizeRatio", SegmentTimerSizeRatio) ^
        SettingsHelper.CreateSetting(document, parent, "TimerShowGradient", TimerShowGradient) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideTimerColors", OverrideTimerColors) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerShowGradient", SegmentTimerShowGradient) ^
        SettingsHelper.CreateSetting(document, parent, "TimerFormat", timerFormat) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerFormat", segmentTimerFormat) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimesAccuracy", SegmentTimesAccuracy) ^
        SettingsHelper.CreateSetting(document, parent, "TimerColor", TimerColor) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerColor", SegmentTimerColor) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentLabelsColor", SegmentLabelsColor) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimesColor", SegmentTimesColor) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentLabelsFont", SegmentLabelsFont) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimesFont", SegmentTimesFont) ^
        SettingsHelper.CreateSetting(document, parent, "SplitNameFont", SplitNameFont) ^
        SettingsHelper.CreateSetting(document, parent, "DisplayIcon", DisplayIcon) ^
        SettingsHelper.CreateSetting(document, parent, "IconSize", IconSize) ^
        SettingsHelper.CreateSetting(document, parent, "ShowSplitName", ShowSplitName) ^
        SettingsHelper.CreateSetting(document, parent, "SplitNameColor", SplitNameColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison", Comparison) ^
        SettingsHelper.CreateSetting(document, parent, "Comparison2", Comparison2) ^
        SettingsHelper.CreateSetting(document, parent, "HideComparison", HideComparison) ^
        SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
        SettingsHelper.CreateSetting(document, parent, "DecimalsSize", DecimalsSize) ^
        SettingsHelper.CreateSetting(document, parent, "SegmentTimerDecimalsSize", SegmentTimerDecimalsSize);
    }

    // No-ops kept to satisfy the designer-generated resx wiring. The Avalonia settings panel
    // exposes the Font* properties via reflection instead, so these handlers are unreachable
    // at runtime.
    private void btnSegmentLabelsFont_Click(object sender, EventArgs e) { }
    private void btnSegmentTimesFont_Click(object sender, EventArgs e) { }
    private void btnSplitNameFont_Click(object sender, EventArgs e) { }
}
