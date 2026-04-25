using System;

using LiveSplit.UI;

namespace LiveSplit.Options;

public class FontOverrides : ICloneable
{
    public bool OverrideTimerFont { get; set; }

    public FontDescriptor TimerFont { get; set; }

    public bool OverrideTimesFont { get; set; }

    public FontDescriptor TimesFont { get; set; }

    public bool OverrideTextFont { get; set; }

    public FontDescriptor TextFont { get; set; }

    public bool HasOverrides => OverrideTimerFont || OverrideTimesFont || OverrideTextFont;

    public void ApplyTo(LayoutSettings settings, out FontDescriptor origTimer, out FontDescriptor origTimes, out FontDescriptor origText)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        origTimer = settings.TimerFont;
        origTimes = settings.TimesFont;
        origText = settings.TextFont;

        if (OverrideTimerFont && TimerFont != null)
        {
            settings.TimerFont = TimerFont;
        }

        if (OverrideTimesFont && TimesFont != null)
        {
            settings.TimesFont = TimesFont;
        }

        if (OverrideTextFont && TextFont != null)
        {
            settings.TextFont = TextFont;
        }
    }

    public static void Restore(LayoutSettings settings, FontDescriptor origTimer, FontDescriptor origTimes, FontDescriptor origText)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        settings.TimerFont = origTimer;
        settings.TimesFont = origTimes;
        settings.TextFont = origText;
    }

    public object Clone()
    {
        return new FontOverrides()
        {
            OverrideTimerFont = OverrideTimerFont,
            TimerFont = TimerFont?.Clone(),
            OverrideTimesFont = OverrideTimesFont,
            TimesFont = TimesFont?.Clone(),
            OverrideTextFont = OverrideTextFont,
            TextFont = TextFont?.Clone(),
        };
    }
}
