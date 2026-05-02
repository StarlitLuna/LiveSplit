using System.Linq;

using LiveSplit.Model;
using LiveSplit.TimeFormatters;

namespace LiveSplit.Web.Share;

public static class ShareNotesFormatter
{
    public static string DefaultTwitterFormat(TimerPhase phase)
    {
        return phase is TimerPhase.NotRunning or TimerPhase.Ended
            ? "I got a $pb in $title."
            : "I'm $delta in $title.";
    }

    public static string DefaultTwitchFormat()
    {
        return "$title Speedrun";
    }

    public static string Format(
        IRun run,
        TimerPhase phase,
        int currentSplitIndex,
        TimingMethod timingMethod,
        string template,
        string streamLink = "")
    {
        template ??= string.Empty;

        var timeFormatter = new RegularTimeFormatter(TimeAccuracy.Seconds);
        var deltaTimeFormatter = new DeltaTimeFormatter();

        string game = run.GameName ?? string.Empty;
        string category = run.GetExtendedCategoryName();
        string pb = timeFormatter.Format(run.Last().PersonalBestSplitTime[timingMethod]) ?? string.Empty;
        string title = run.GetExtendedName();

        string splitName = string.Empty;
        string splitTime = "-";
        string deltaTime = "-";

        if ((phase == TimerPhase.Running || phase == TimerPhase.Paused)
            && currentSplitIndex > 0
            && currentSplitIndex <= run.Count)
        {
            ISegment lastSplit = run[currentSplitIndex - 1];

            splitName = lastSplit.Name ?? string.Empty;
            splitTime = timeFormatter.Format(lastSplit.SplitTime[timingMethod]) ?? "-";
            deltaTime = deltaTimeFormatter.Format(
                lastSplit.SplitTime[timingMethod] - lastSplit.PersonalBestSplitTime[timingMethod]);
        }

        return template
            .Replace("$game", game)
            .Replace("$category", category)
            .Replace("$title", title)
            .Replace("$pb", pb)
            .Replace("$splitname", splitName)
            .Replace("$splittime", splitTime)
            .Replace("$delta", deltaTime)
            .Replace("$stream", streamLink ?? string.Empty);
    }
}
