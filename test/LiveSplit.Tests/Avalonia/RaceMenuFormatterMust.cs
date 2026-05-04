using System;
using System.Collections.Generic;

using LiveSplit.Avalonia;
using LiveSplit.Model;
using LiveSplit.Racetime;
using LiveSplit.UI.Components;
using LiveSplit.Web.SRL;

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

    [Theory]
    [InlineData(RaceJoinCapability.JoinRace, "Join Race")]
    [InlineData(RaceJoinCapability.OpenViewer, "Open Viewer")]
    public void FormatOpenRaceActionFromProviderCapability(RaceJoinCapability capability, string expected)
    {
        Assert.Equal(expected, RaceMenuFormatter.FormatOpenRaceAction(capability));
    }

    [Fact]
    public void MarkSpeedRunsLiveBrowserFallbackAsOpenViewer()
    {
        Assert.Equal(RaceJoinCapability.OpenViewer, SpeedRunsLiveAPI.Instance.JoinCapability);
    }

    [Fact]
    public void MarkRacetimeBrowserFallbackAsOpenViewer()
    {
        Assert.Equal(RaceJoinCapability.OpenViewer, new RacetimeAPI().JoinCapability);
    }

    [Theory]
    [InlineData(RaceJoinCapability.JoinRace, "New Race...")]
    [InlineData(RaceJoinCapability.OpenViewer, "New Race in Browser...")]
    public void FormatCreateRaceActionFromProviderCapability(RaceJoinCapability capability, string expected)
    {
        Assert.Equal(expected, RaceMenuFormatter.FormatCreateRaceAction(capability));
    }

    [Fact]
    public void ResolveInProgressRaceParticipantAsJoinForJoinCapableProvider()
    {
        var provider = new FakeRaceProvider(RaceJoinCapability.JoinRace, "runner");
        var race = new FakeRaceInfo { ParticipantUsername = "runner" };

        Assert.Equal(RaceMenuAction.JoinRace, RaceMenuFormatter.ResolveRaceAction(provider, race, isInProgress: true));
    }

    [Fact]
    public void ResolveInProgressRaceNonParticipantAsViewerForJoinCapableProvider()
    {
        var provider = new FakeRaceProvider(RaceJoinCapability.JoinRace, "runner");
        var race = new FakeRaceInfo { ParticipantUsername = "other-runner" };

        Assert.Equal(RaceMenuAction.OpenViewer, RaceMenuFormatter.ResolveRaceAction(provider, race, isInProgress: true));
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
        public string ParticipantUsername { get; set; }

        public bool IsParticipant(string username) => username == ParticipantUsername;
    }

    private sealed class FakeRaceProvider : RaceProviderAPI
    {
        private readonly RaceJoinCapability _joinCapability;
        private readonly string _username;

        public FakeRaceProvider(RaceJoinCapability joinCapability, string username)
        {
            _joinCapability = joinCapability;
            _username = username;
            JoinRace = (_, _) => { };
        }

        public override string ProviderName => "Fake";
        public override string Username => _username;
        public override RaceJoinCapability JoinCapability => _joinCapability;
        public override IEnumerable<IRaceInfo> GetRaces() => [];
        public override void RefreshRacesListAsync() { }
    }
}
