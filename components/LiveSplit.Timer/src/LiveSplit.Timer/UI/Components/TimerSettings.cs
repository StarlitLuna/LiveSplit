using System;
using System.Drawing;
using System.Xml;

using LiveSplit.Localization;
using LiveSplit.TimeFormatters;

namespace LiveSplit.UI.Components;

public class TimerSettings
{
    private static string T(string source) => UiLocalizer.Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    public float TimerHeight { get; set; }
    public float TimerWidth { get; set; }

    public float DecimalsSize { get; set; }

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

    public LayoutMode Mode { get; set; }

    public Color TimerColor { get; set; }
    public bool OverrideSplitColors { get; set; }

    public bool CenterTimer { get; set; }

    public bool ShowGradient { get; set; }

    public string TimingMethod { get; set; }

    public Color BackgroundColor { get; set; }
    public Color BackgroundColor2 { get; set; }
    public DeltasGradientType BackgroundGradient { get; set; }
    public string GradientString
    {
        get => GetBackgroundTypeString(BackgroundGradient);
        set => BackgroundGradient = (DeltasGradientType)Enum.Parse(typeof(DeltasGradientType), value.Replace(" ", ""));
    }

    public TimerSettings()
    {

        TimerWidth = 225;
        TimerHeight = 50;
        DigitsFormat = "1";
        Accuracy = ".23";
        TimerColor = Color.FromArgb(170, 170, 170);
        OverrideSplitColors = false;
        ShowGradient = true;
        BackgroundColor = Color.Transparent;
        BackgroundColor2 = Color.Transparent;
        BackgroundGradient = DeltasGradientType.Plain;
        CenterTimer = false;
        TimingMethod = "Current Timing Method";
        DecimalsSize = 35f;

    }

    public static string GetBackgroundTypeString(DeltasGradientType type)
    {
        return type switch
        {
            DeltasGradientType.Horizontal => "Horizontal Gradient",
            DeltasGradientType.HorizontalWithDeltaColor => "Horizontal With Delta Color",
            DeltasGradientType.PlainWithDeltaColor => "Plain With Delta Color",
            DeltasGradientType.Vertical => "Vertical",
            DeltasGradientType.VerticalWithDeltaColor => "Vertical With Delta Color",
            _ => "Plain",
        };
    }

    public void SetSettings(XmlNode node)
    {
        var element = (XmlElement)node;
        Version version = SettingsHelper.ParseVersion(element["Version"]);

        TimerHeight = SettingsHelper.ParseFloat(element["TimerHeight"]);
        TimerWidth = SettingsHelper.ParseFloat(element["TimerWidth"]);
        ShowGradient = SettingsHelper.ParseBool(element["ShowGradient"], true);
        TimerColor = SettingsHelper.ParseColor(element["TimerColor"], Color.FromArgb(170, 170, 170));
        DecimalsSize = SettingsHelper.ParseFloat(element["DecimalsSize"], 35f);
        BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"], Color.Transparent);
        BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"], Color.Transparent);
        GradientString = SettingsHelper.ParseString(element["BackgroundGradient"], DeltasGradientType.Plain.ToString());
        CenterTimer = SettingsHelper.ParseBool(element["CenterTimer"], false);
        TimingMethod = SettingsHelper.ParseString(element["TimingMethod"], "Current Timing Method");

        if (version >= new Version(1, 3))
        {
            OverrideSplitColors = SettingsHelper.ParseBool(element["OverrideSplitColors"]);
        }
        else
        {
            OverrideSplitColors = !SettingsHelper.ParseBool(element["UseSplitColors"], true);
        }

        if (version >= new Version(1, 2))
        {
            if (version >= new Version(1, 5))
            {
                timerFormat = SettingsHelper.ParseString(element["TimerFormat"]);
            }
            else
            {
                TimeAccuracy accuracy = SettingsHelper.ParseEnum<TimeAccuracy>(element["TimerAccuracy"]);
                DigitsFormat = "1";
                if (accuracy == TimeAccuracy.Hundredths)
                {
                    Accuracy = ".23";
                }
                else if (accuracy == TimeAccuracy.Tenths)
                {
                    Accuracy = ".2";
                }
                else
                {
                    Accuracy = "";
                }
            }
        }
        else
        {
            DigitsFormat = "1";
            Accuracy = ".23";
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
        SettingsHelper.CreateSetting(document, parent, "TimerHeight", TimerHeight) ^
        SettingsHelper.CreateSetting(document, parent, "TimerWidth", TimerWidth) ^
        SettingsHelper.CreateSetting(document, parent, "TimerFormat", timerFormat) ^
        SettingsHelper.CreateSetting(document, parent, "OverrideSplitColors", OverrideSplitColors) ^
        SettingsHelper.CreateSetting(document, parent, "ShowGradient", ShowGradient) ^
        SettingsHelper.CreateSetting(document, parent, "TimerColor", TimerColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor", BackgroundColor) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundColor2", BackgroundColor2) ^
        SettingsHelper.CreateSetting(document, parent, "BackgroundGradient", BackgroundGradient) ^
        SettingsHelper.CreateSetting(document, parent, "CenterTimer", CenterTimer) ^
        SettingsHelper.CreateSetting(document, parent, "TimingMethod", TimingMethod) ^
        SettingsHelper.CreateSetting(document, parent, "DecimalsSize", DecimalsSize);
    }

}
