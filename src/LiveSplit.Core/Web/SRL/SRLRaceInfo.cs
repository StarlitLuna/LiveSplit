using System;
using System.Collections.Generic;
using System.Linq;

using LiveSplit.Model;

namespace LiveSplit.Web.SRL;

public class SRLRaceInfo : IRaceInfo
{
    private readonly dynamic _data;

    public SRLRaceInfo(dynamic data)
    {
        _data = data;
        foreach (dynamic entrant in Entrants)
        {
            if (ToInt(entrant.time) >= 0)
            {
                Finishes++;
            }

            if ((string)entrant.statetext == "Forfeit")
            {
                Forfeits++;
            }
        }
    }

    public string Id => _data.id;
    public string GameName => _data.game.name;
    public int Finishes { get; set; }
    public int Forfeits { get; set; }
    public int NumEntrants => ToInt(_data.numentrants);
    public string Goal => _data.goal;
    public int State => ToInt(_data.state);
    public int Starttime => ToInt(_data.time);
    public string GameId => _data.game.abbrev;

    private IEnumerable<dynamic> Entrants => _data.entrants.Properties.Values;

    public bool IsParticipant(string username)
    {
        IEnumerable<string> racers = ((IEnumerable<string>)_data.entrants.Properties.Keys)
            .Select(x => x.ToLowerInvariant());
        return racers.Contains((username ?? string.Empty).ToLowerInvariant());
    }

    public IEnumerable<string> LiveStreams
    {
        get
        {
            foreach (dynamic entrant in Entrants)
            {
                if ((string)entrant.statetext == "Forfeit" || ToInt(entrant.time) >= 0)
                {
                    continue;
                }

                string twitch = entrant.twitch;
                if (!string.IsNullOrEmpty(twitch))
                {
                    yield return twitch;
                }
            }
        }
    }

    private static int ToInt(dynamic value)
    {
        return value is null ? 0 : Convert.ToInt32(value);
    }
}
