using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using LiveSplit.Model;
using LiveSplit.UI.Components;

namespace LiveSplit.Web.SRL;

public class SpeedRunsLiveAPI : RaceProviderAPI
{
    protected static readonly SpeedRunsLiveAPI _Instance = new();

    protected IEnumerable<SRLRaceInfo> racesList;
    protected IEnumerable<dynamic> gameList;
    protected IList<string> gameNames;

    protected SpeedRunsLiveAPI()
    {
        JoinRace = (_, raceId) =>
        {
            if (!string.IsNullOrWhiteSpace(raceId))
            {
                OpenBrowser($"http://speedrunslive.com/race/{raceId}");
            }
        };
        CreateRace = _ => OpenBrowser("http://speedrunslive.com/races/new");
    }

    public static SpeedRunsLiveAPI Instance => _Instance;
    public static readonly Uri BaseUri = new("http://api.speedrunslive.com:81/");

    public IEnumerable<dynamic> GetGameList()
    {
        gameList ??= (IEnumerable<dynamic>)JSON.FromUri(GetUri("games")).games;
        return gameList;
    }

    public IEnumerable<string> GetGameNames()
    {
        gameNames ??= GetGameList().Select(static x => (string)x.name).ToList();
        return gameNames;
    }

    public IEnumerable<string> GetCategories(string gameID)
    {
        if (string.IsNullOrEmpty(gameID))
        {
            return [];
        }

        return ((IEnumerable<dynamic>)JSON.FromUri(GetUri("goals/" + gameID + "?season=0")).goals)
            .Select(static x => (string)x.name);
    }

    public string GetGameIDFromName(string name)
    {
        dynamic gameID = GetGameList().FirstOrDefault(x => x.name == name);
        return gameID?.abbrev;
    }

    public IEnumerable<dynamic> GetEntrants(string raceID)
    {
        dynamic race = GetRace(raceID);
        return race.entrants;
    }

    public dynamic GetRace(string raceID)
    {
        IEnumerable<IRaceInfo> races = GetRaces();
        return races.First(x => x.Id == raceID);
    }

    public override IEnumerable<IRaceInfo> GetRaces()
    {
        if (racesList is null)
        {
            RefreshRacesList();
        }

        return racesList;
    }

    public override void RefreshRacesListAsync()
    {
        Task.Factory.StartNew(RefreshRacesList)
            .ContinueWith(static _ => { }, TaskContinuationOptions.OnlyOnFaulted);
    }

    public void RefreshRacesList()
    {
        var infoList = new List<SRLRaceInfo>();
        foreach (dynamic race in JSON.FromUri(GetUri("races")).races)
        {
            infoList.Add(new SRLRaceInfo(race));
        }

        racesList = infoList;
        RacesRefreshedCallback?.Invoke(this);
    }

    public override string ProviderName => "SRL";

    public override string Username
    {
        get
        {
            string credentials = WebCredentials.SpeedRunsLiveIRCCredentials;
            if (string.IsNullOrEmpty(credentials))
            {
                return string.Empty;
            }

            int separator = credentials.IndexOf(':');
            return separator >= 0 ? credentials[..separator] : credentials;
        }
    }

    protected Uri GetUri(string subUri)
    {
        return new Uri(BaseUri, subUri);
    }

    private static void OpenBrowser(string url)
    {
        Process.Start(new ProcessStartInfo(url)
        {
            UseShellExecute = true
        });
    }
}
