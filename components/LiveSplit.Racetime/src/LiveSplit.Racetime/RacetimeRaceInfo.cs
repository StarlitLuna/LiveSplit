using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

using LiveSplit.Model;

namespace LiveSplit.Racetime;

public class RacetimeRaceInfo : IRaceInfo
{
    private readonly RacetimeRace _race;

    internal RacetimeRaceInfo(RacetimeRace race)
    {
        _race = race;
    }

    public int Finishes => _race.EntrantsCountFinished;
    public int Forfeits => _race.EntrantsCountInactive;
    public string GameId => _race.Category?.Slug ?? string.Empty;
    public string GameName => _race.Category?.Name ?? string.Empty;
    public string Goal => _race.Goal?.Name;
    public string Id => _race.Name ?? string.Empty;
    public int NumEntrants => _race.EntrantsCount;

    public int Starttime
    {
        get
        {
            return _race.StartedAt is null
                ? 0
                : (int)_race.StartedAt.Value.ToUniversalTime().ToUnixTimeSeconds();
        }
    }

    public int State
    {
        get
        {
            return _race.Status?.Value switch
            {
                "open" or "invitational" => 1,
                "in_progress" => 3,
                _ => 42
            };
        }
    }

    public IEnumerable<string> LiveStreams
    {
        get
        {
            return _race.Entrants
                .Where(x => !IsInactive(x) && !HasFinished(x))
                .Select(x => FirstNonEmpty(x.User?.TwitchName, x.User?.TwitchChannel))
                .Where(x => !string.IsNullOrWhiteSpace(x));
        }
    }

    public bool IsParticipant(string username)
    {
        return !string.IsNullOrEmpty(username)
            && _race.Entrants.Any(x => string.Equals(x.User?.Name, username, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsInactive(RacetimeEntrant entrant)
    {
        return entrant.Status?.Value is "dnf" or "dq";
    }

    private static bool HasFinished(RacetimeEntrant entrant)
    {
        return entrant.Status?.Value == "done" || entrant.FinishedAt is not null;
    }

    private static string FirstNonEmpty(string first, string second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }
}

internal sealed class RacetimeRacesData
{
    [JsonPropertyName("races")]
    public List<RacetimeRace> Races { get; set; } = [];
}

internal sealed class RacetimeRace
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("status")]
    public RacetimeValue Status { get; set; }

    [JsonPropertyName("category")]
    public RacetimeCategory Category { get; set; }

    [JsonPropertyName("goal")]
    public RacetimeGoal Goal { get; set; }

    [JsonPropertyName("started_at")]
    public DateTimeOffset? StartedAt { get; set; }

    [JsonPropertyName("entrants_count")]
    public int EntrantsCount { get; set; }

    [JsonPropertyName("entrants_count_finished")]
    public int EntrantsCountFinished { get; set; }

    [JsonPropertyName("entrants_count_inactive")]
    public int EntrantsCountInactive { get; set; }

    [JsonPropertyName("entrants")]
    public List<RacetimeEntrant> Entrants { get; set; } = [];
}

internal sealed class RacetimeCategory
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

internal sealed class RacetimeGoal
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

internal sealed class RacetimeEntrant
{
    [JsonPropertyName("user")]
    public RacetimeUser User { get; set; }

    [JsonPropertyName("status")]
    public RacetimeValue Status { get; set; }

    [JsonPropertyName("finished_at")]
    public DateTimeOffset? FinishedAt { get; set; }
}

internal sealed class RacetimeUser
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("twitch_name")]
    public string TwitchName { get; set; }

    [JsonPropertyName("twitch_channel")]
    public string TwitchChannel { get; set; }
}

internal sealed class RacetimeValue
{
    [JsonPropertyName("value")]
    public string Value { get; set; }
}
