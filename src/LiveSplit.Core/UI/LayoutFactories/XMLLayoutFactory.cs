using System;
using System.Drawing;
using System.IO;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Options;
using LiveSplit.UI.Components;

namespace LiveSplit.UI.LayoutFactories;

public class XMLLayoutFactory : ILayoutFactory
{
    public Stream Stream { get; set; }

    public XMLLayoutFactory(Stream stream)
    {
        Stream = stream;
    }

    private static LayoutSettings ParseSettings(XmlElement element, Version version)
    {
        var settings = new LayoutSettings
        {
            TextColor = SettingsHelper.ParseColor(element["TextColor"]),
            BackgroundColor = SettingsHelper.ParseColor(element["BackgroundColor"]),
            ThinSeparatorsColor = SettingsHelper.ParseColor(element["ThinSeparatorsColor"]),
            SeparatorsColor = SettingsHelper.ParseColor(element["SeparatorsColor"]),
            PersonalBestColor = SettingsHelper.ParseColor(element["PersonalBestColor"]),
            AheadGainingTimeColor = SettingsHelper.ParseColor(element["AheadGainingTimeColor"]),
            AheadLosingTimeColor = SettingsHelper.ParseColor(element["AheadLosingTimeColor"]),
            BehindGainingTimeColor = SettingsHelper.ParseColor(element["BehindGainingTimeColor"]),
            BehindLosingTimeColor = SettingsHelper.ParseColor(element["BehindLosingTimeColor"]),
            BestSegmentColor = SettingsHelper.ParseColor(element["BestSegmentColor"]),
            UseRainbowColor = SettingsHelper.ParseBool(element["UseRainbowColor"], false),
            NotRunningColor = SettingsHelper.ParseColor(element["NotRunningColor"]),
            PausedColor = SettingsHelper.ParseColor(element["PausedColor"], Color.FromArgb(122, 122, 122)),
            AntiAliasing = SettingsHelper.ParseBool(element["AntiAliasing"], true),
            DropShadows = SettingsHelper.ParseBool(element["DropShadows"], true),
            Opacity = SettingsHelper.ParseFloat(element["Opacity"], 1),
            MousePassThroughWhileRunning = SettingsHelper.ParseBool(element["MousePassThroughWhileRunning"]),
            AllowResizing = SettingsHelper.ParseBool(element["AllowResizing"], true),
            AllowMoving = SettingsHelper.ParseBool(element["AllowMoving"], true),
            TextOutlineColor = SettingsHelper.ParseColor(element["TextOutlineColor"], Color.FromArgb(0, 0, 0, 0)),
            ShadowsColor = SettingsHelper.ParseColor(element["ShadowsColor"], Color.FromArgb(128, 0, 0, 0)),
            ShowBestSegments = SettingsHelper.ParseBool(element["ShowBestSegments"]),
            AlwaysOnTop = SettingsHelper.ParseBool(element["AlwaysOnTop"]),
            TimerFont = SettingsHelper.GetFontFromElements(element["TimerFont"], element["TimerFontDescriptor"]),
            ImageOpacity = SettingsHelper.ParseFloat(element["ImageOpacity"], 1f),
            ImageBlur = SettingsHelper.ParseFloat(element["ImageBlur"], 0f)
        };

        if (version >= new Version(1, 3))
        {
            settings.BackgroundColor2 = SettingsHelper.ParseColor(element["BackgroundColor2"]);
            settings.TimesFont = SettingsHelper.GetFontFromElements(element["TimesFont"], element["TimesFontDescriptor"]);
            settings.TextFont = SettingsHelper.GetFontFromElements(element["TextFont"], element["TextFontDescriptor"]);
        }
        else
        {
            if (settings.BackgroundColor == Color.Black)
            {
                settings.BackgroundColor = settings.BackgroundColor2 = Color.Transparent;
            }
            else
            {
                settings.BackgroundColor2 = settings.BackgroundColor;
            }

            settings.TimesFont = SettingsHelper.GetFontFromElement(element["MainFont"]);
            settings.TextFont = SettingsHelper.GetFontFromElement(element["SplitNamesFont"]);
        }

        if (version >= new Version(1, 6, 1))
        {
            settings.BackgroundType = SettingsHelper.ParseEnum(element["BackgroundType"], BackgroundType.SolidColor);
        }
        else
        {
            XmlElement gradientType = element["BackgroundGradient"];
            if (gradientType == null || gradientType.InnerText == "Plain")
            {
                settings.BackgroundType = BackgroundType.SolidColor;
            }
            else if (gradientType.InnerText == "Vertical")
            {
                settings.BackgroundType = BackgroundType.VerticalGradient;
            }
            else
            {
                settings.BackgroundType = BackgroundType.HorizontalGradient;
            }
        }

        settings.LegacyBackgroundImage = SettingsHelper.GetImageFromElement(element["BackgroundImage"]);
        settings.BackgroundImage = SettingsHelper.GetImageFromElement(element["BackgroundImageData"]);
        if (settings.BackgroundImage is null && IsCommonEncodedImage(settings.LegacyBackgroundImage))
        {
            settings.BackgroundImage = settings.LegacyBackgroundImage;
        }

        if (settings.TimerFont == null || settings.TimesFont == null || settings.TextFont == null)
        {
            LayoutSettings defaults = new Options.SettingsFactories.StandardLayoutSettingsFactory().Create();
            settings.TimerFont ??= defaults.TimerFont;
            settings.TimesFont ??= defaults.TimesFont;
            settings.TextFont ??= defaults.TextFont;
        }

        return settings;
    }

    private static bool IsCommonEncodedImage(byte[] data)
    {
        if (data is null || data.Length < 4)
        {
            return false;
        }

        return (data.Length >= 8
                && data[0] == 0x89
                && data[1] == 0x50
                && data[2] == 0x4E
                && data[3] == 0x47)
            || (data[0] == 0xFF && data[1] == 0xD8)
            || (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            || (data[0] == 0x42 && data[1] == 0x4D);
    }

    public ILayout Create(LiveSplitState state)
    {
        var document = new XmlDocument();
        document.Load(Stream);
        var layout = new Layout();
        XmlElement parent = document["Layout"];
        Version version = SettingsHelper.ParseAttributeVersion(parent);

        layout.X = SettingsHelper.ParseInt(parent["X"]);
        layout.Y = SettingsHelper.ParseInt(parent["Y"]);
        layout.VerticalWidth = SettingsHelper.ParseInt(parent["VerticalWidth"]);
        layout.VerticalHeight = SettingsHelper.ParseInt(parent["VerticalHeight"]);
        layout.HorizontalWidth = SettingsHelper.ParseInt(parent["HorizontalWidth"]);
        layout.HorizontalHeight = SettingsHelper.ParseInt(parent["HorizontalHeight"]);
        layout.Mode = SettingsHelper.ParseEnum<LayoutMode>(parent["Mode"]);
        layout.Settings = ParseSettings(parent["Settings"], version);

        XmlElement components = parent["Components"];
        foreach (object componentNode in components.GetElementsByTagName("Component"))
        {
            var componentElement = componentNode as XmlElement;
            XmlElement path = componentElement["Path"];
            XmlElement settings = componentElement["Settings"];
            ILayoutComponent layoutComponent = ComponentManager.LoadLayoutComponent(path.InnerText, state);
            if (layoutComponent != null)
            {
                try
                {
                    layoutComponent.Component.SetSettings(settings);

                    XmlElement fontOverridesElement = componentElement["FontOverrides"];
                    if (fontOverridesElement != null && layoutComponent is LayoutComponent lc)
                    {
                        lc.FontOverrides.OverrideTimerFont = SettingsHelper.ParseBool(fontOverridesElement["OverrideTimerFont"]);
                        if (lc.FontOverrides.OverrideTimerFont)
                        {
                            lc.FontOverrides.TimerFont = SettingsHelper.GetFontFromElements(fontOverridesElement["TimerFont"], fontOverridesElement["TimerFontDescriptor"]);
                        }

                        lc.FontOverrides.OverrideTimesFont = SettingsHelper.ParseBool(fontOverridesElement["OverrideTimesFont"]);
                        if (lc.FontOverrides.OverrideTimesFont)
                        {
                            lc.FontOverrides.TimesFont = SettingsHelper.GetFontFromElements(fontOverridesElement["TimesFont"], fontOverridesElement["TimesFontDescriptor"]);
                        }

                        lc.FontOverrides.OverrideTextFont = SettingsHelper.ParseBool(fontOverridesElement["OverrideTextFont"]);
                        if (lc.FontOverrides.OverrideTextFont)
                        {
                            lc.FontOverrides.TextFont = SettingsHelper.GetFontFromElements(fontOverridesElement["TextFont"], fontOverridesElement["TextFontDescriptor"]);
                        }
                    }
                    else if (layoutComponent is LayoutComponent lcLegacy)
                    {
                        // Migrate legacy per-component font overrides via reflection.
                        // Components that had their own font override settings can provide
                        // a MigrateFontOverrides(FontOverrides) method to populate the new
                        // unified FontOverrides on the LayoutComponent wrapper.
                        layoutComponent.Component.GetType()
                            .GetMethod("MigrateFontOverrides", [typeof(FontOverrides)])
                            ?.Invoke(layoutComponent.Component, [lcLegacy.FontOverrides]);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }

                layout.LayoutComponents.Add(layoutComponent);
            }
            else
            {
                throw new Exception(path.InnerText + " could not be found");
            }
        }

        return layout;
    }
}
