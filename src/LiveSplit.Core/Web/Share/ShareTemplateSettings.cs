using LiveSplit.Model;

namespace LiveSplit.Web.Share;

public sealed class ShareTemplateSettings
{
    public const string DefaultTwitterCompletedFormat = "I got a $pb in $title.";
    public const string DefaultTwitterRunningFormat = "I'm $delta in $title.";
    public const string DefaultTwitchFormatText = "$title Speedrun";

    public string TwitterCompletedFormat { get; set; } = DefaultTwitterCompletedFormat;
    public string TwitterRunningFormat { get; set; } = DefaultTwitterRunningFormat;
    public string TwitchFormat { get; set; } = DefaultTwitchFormatText;

    public static ShareTemplateSettings Default => new();

    public string GetTwitterFormat(TimerPhase phase)
    {
        return phase is TimerPhase.NotRunning or TimerPhase.Ended
            ? NonEmptyOrDefault(TwitterCompletedFormat, DefaultTwitterCompletedFormat)
            : NonEmptyOrDefault(TwitterRunningFormat, DefaultTwitterRunningFormat);
    }

    public string GetTwitchFormat()
    {
        return NonEmptyOrDefault(TwitchFormat, DefaultTwitchFormatText);
    }

    public void SetTwitterFormat(TimerPhase phase, string format)
    {
        if (phase is TimerPhase.NotRunning or TimerPhase.Ended)
        {
            TwitterCompletedFormat = format ?? string.Empty;
            return;
        }

        TwitterRunningFormat = format ?? string.Empty;
    }

    private static string NonEmptyOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }
}
