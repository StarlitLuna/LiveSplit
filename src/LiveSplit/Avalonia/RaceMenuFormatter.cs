using System;

using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using LiveSplit.UI.Components;

namespace LiveSplit.Avalonia;

public static class RaceMenuFormatter
{
    public static string FormatOpenRaceTitle(IRaceInfo race)
    {
        int entrants = race.NumEntrants;
        string plural = entrants == 1 ? string.Empty : "s";
        return $"{FormatGameAndGoal(race)} ({entrants} Entrant{plural})";
    }

    public static string FormatOpenRaceAction(RaceJoinCapability capability)
    {
        return capability == RaceJoinCapability.OpenViewer ? "Open Viewer" : "Join Race";
    }

    public static string FormatInProgressRaceTitle(IRaceInfo race, DateTime utcNow)
    {
        DateTime startTime = DateTimeOffset.FromUnixTimeSeconds(race.Starttime).UtcDateTime;
        TimeSpan elapsed = utcNow - startTime;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        string time = new RegularTimeFormatter().Format(elapsed);
        int activeEntrants = race.NumEntrants - race.Forfeits;
        return $"[{time}] {FormatGameAndGoal(race)} ({race.Finishes}/{activeEntrants} Finished)";
    }

    public static string FormatGameAndGoal(IRaceInfo race)
    {
        string game = race.GameName ?? string.Empty;
        string goal = race.Goal ?? string.Empty;
        string text = string.IsNullOrWhiteSpace(goal) ? game : $"{game} - {goal}";
        return Shorten(text);
    }

    private static string Shorten(string text)
    {
        const int MaxLength = 60;
        if (string.IsNullOrEmpty(text) || text.Length <= MaxLength)
        {
            return text ?? string.Empty;
        }

        return text[..(MaxLength - 3)] + "...";
    }
}
