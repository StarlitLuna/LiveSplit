using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using LiveSplit.Model;
using LiveSplit.UI.Components;

namespace LiveSplit.Racetime;

public class RacetimeAPI : RaceProviderAPI
{
    private static readonly Uri BaseUri = new("https://racetime.gg/");
    private readonly HttpClient _client;
    private IReadOnlyList<RacetimeRaceInfo> _races = [];

    public RacetimeAPI()
        : this(new HttpClient())
    {
    }

    internal RacetimeAPI(HttpClient client)
    {
        _client = client;
        JoinRace = Join;
        CreateRace = Create;
    }

    public override string ProviderName => "racetime.gg";
    public override string Username => string.Empty;
    public override RaceJoinCapability JoinCapability => RaceJoinCapability.OpenViewer;

    public static IEnumerable<IRaceInfo> ParseRacesData(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        RacetimeRacesData data = JsonSerializer.Deserialize<RacetimeRacesData>(json, options);
        return data?.Races?.Select(x => new RacetimeRaceInfo(x)).ToArray() ?? [];
    }

    public override IEnumerable<IRaceInfo> GetRaces()
    {
        return _races;
    }

    public override void RefreshRacesListAsync()
    {
        _ = RefreshRacesList();
    }

    internal async Task RefreshRacesList()
    {
        try
        {
            using HttpResponseMessage response = await _client.GetAsync(new Uri(BaseUri, "races/data")).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _races = ParseRacesData(json).Cast<RacetimeRaceInfo>().ToArray();
            RacesRefreshedCallback?.Invoke(this);
        }
        catch
        {
            _races = [];
            RacesRefreshedCallback?.Invoke(this);
        }
    }

    private static void Join(ITimerModel model, string raceId)
    {
        if (string.IsNullOrWhiteSpace(raceId))
        {
            throw new NotSupportedException("Racetime join requires a race id.");
        }

        OpenBrowser(new Uri(BaseUri, raceId));
    }

    private static void Create(ITimerModel model)
    {
        OpenBrowser(BaseUri);
    }

    private static void OpenBrowser(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(
                "Racetime authenticated channel operations are not available in the cross-platform port. Open the racetime.gg race page in a browser instead.",
                ex);
        }
    }
}
