using System;
using System.Collections.Generic;

using LiveSplit.Avalonia;
using LiveSplit.Model;

using Xunit;

namespace LiveSplit.Tests.Avalonia;

public class RaceMenuFormatterMust
{
    [Fact]
    public void FormatOpenRaceTitleWithEntrantCount()
    {
        var race = new FakeRaceInfo
        {
            GameName = "Example Game",
            Goal = "Any%",
            NumEntrants = 2
        };

        Assert.Equal("Example Game - Any% (2 Entrants)", RaceMenuFormatter.FormatOpenRaceTitle(race));
    }

    [Fact]
    public void FormatInProgressRaceTitleWithElapsedTimeAndFinishCount()
    {
        var race = new FakeRaceInfo
        {
            GameName = "Example Game",
            Goal = "Any%",
            Finishes = 1,
            Forfeits = 1,
            NumEntrants = 4,
            Starttime = 1_700_000_000
        };
        var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_125).UtcDateTime;

        Assert.Equal("[2:05] Example Game - Any% (1/3 Finished)", RaceMenuFormatter.FormatInProgressRaceTitle(race, now));
    }

    private sealed class FakeRaceInfo : IRaceInfo
    {
        public int Finishes { get; set; }
        public int Forfeits { get; set; }
        public string GameId { get; set; }
        public string GameName { get; set; }
        public string Goal { get; set; }
        public string Id { get; set; }
        public IEnumerable<string> LiveStreams { get; set; } = [];
        public int NumEntrants { get; set; }
        public int Starttime { get; set; }
        public int State { get; set; }

        public bool IsParticipant(string username) => false;
    }
}
