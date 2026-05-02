using System.Linq;

using LiveSplit.Racetime;

using Xunit;

namespace LiveSplit.Tests.Web;

public class RacetimeRaceInfoMust
{
    [Fact]
    public void ReadRaceDataFromRacetimeRacesDataJsonShape()
    {
        var races = RacetimeAPI.ParseRacesData("""
            {
              "races": [
                {
                  "name": "oot/silent-jellyfish-1234",
                  "status": { "value": "in_progress" },
                  "category": { "slug": "oot", "name": "The Legend of Zelda: Ocarina of Time" },
                  "goal": { "name": "Any%" },
                  "info": "No SRM",
                  "started_at": "2026-05-02T16:30:00Z",
                  "entrants_count": 4,
                  "entrants_count_finished": 1,
                  "entrants_count_inactive": 1,
                  "entrants": [
                    {
                      "user": { "name": "Alice", "twitch_name": "alice_stream", "twitch_channel": "alice_stream" },
                      "status": { "value": "in_progress" },
                      "finished_at": null
                    },
                    {
                      "user": { "name": "Bob", "twitch_name": "bob_stream", "twitch_channel": "bob_stream" },
                      "status": { "value": "done" },
                      "finished_at": "2026-05-02T17:01:00Z"
                    },
                    {
                      "user": { "name": "Cora", "twitch_name": "cora_stream", "twitch_channel": "cora_stream" },
                      "status": { "value": "dnf" },
                      "finished_at": null
                    },
                    {
                      "user": { "name": "Drew", "twitch_name": "", "twitch_channel": "" },
                      "status": { "value": "ready" },
                      "finished_at": null
                    }
                  ]
                }
              ]
            }
            """).ToArray();

        var race = Assert.Single(races);

        Assert.Equal("oot/silent-jellyfish-1234", race.Id);
        Assert.Equal("The Legend of Zelda: Ocarina of Time", race.GameName);
        Assert.Equal("oot", race.GameId);
        Assert.Equal("Any%", race.Goal);
        Assert.Equal(3, race.State);
        Assert.Equal(1_777_739_400, race.Starttime);
        Assert.Equal(4, race.NumEntrants);
        Assert.Equal(1, race.Finishes);
        Assert.Equal(1, race.Forfeits);
        Assert.True(race.IsParticipant("alice"));
        Assert.True(race.IsParticipant("ALICE"));
        Assert.False(race.IsParticipant("not-racing"));
        Assert.Equal(new[] { "alice_stream" }, race.LiveStreams.ToArray());
    }
}
