using System.Linq;

using LiveSplit.Web;
using LiveSplit.Web.SRL;

using Xunit;

namespace LiveSplit.Tests.Web;

public class SRLRaceInfoMust
{
    [Fact]
    public void ReadRaceDataFromSpeedRunsLiveJsonShape()
    {
        dynamic data = JSON.FromString("""
            {
              "id": "abc123",
              "game": { "name": "Example Game", "abbrev": "eg" },
              "numentrants": 4,
              "goal": "Any%",
              "state": 3,
              "time": 123456,
              "entrants": {
                "Alice": { "time": -1, "statetext": "Ready", "twitch": "alice" },
                "Bob": { "time": 645, "statetext": "Done", "twitch": "bob" },
                "Cora": { "time": -1, "statetext": "Forfeit", "twitch": "cora" },
                "Drew": { "time": -1, "statetext": "Entered", "twitch": "drew" }
              }
            }
            """);

        var race = new SRLRaceInfo(data);

        Assert.Equal("abc123", race.Id);
        Assert.Equal("Example Game", race.GameName);
        Assert.Equal("eg", race.GameId);
        Assert.Equal("Any%", race.Goal);
        Assert.Equal(3, race.State);
        Assert.Equal(123456, race.Starttime);
        Assert.Equal(4, race.NumEntrants);
        Assert.Equal(1, race.Finishes);
        Assert.Equal(1, race.Forfeits);
        Assert.True(race.IsParticipant("alice"));
        Assert.True(race.IsParticipant("ALICE"));
        Assert.False(race.IsParticipant("not-racing"));
        Assert.Equal(new[] { "alice", "drew" }, race.LiveStreams.ToArray());
    }
}
