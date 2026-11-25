using System.Text.Json;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.Racing;

/// <summary>
/// LiveSplit.Racetime RacetimeAPI: the races racetime.gg lists (races/data, with the signed-in
/// account's token) for the "racetime.gg Races" menu.
/// </summary>
public sealed class RacetimeApi(HttpMessageHandler handler, RacetimeAuthenticator authenticator)
{
    public const string ProviderName = "racetime.gg";
    public const string BaseUri = "https://racetime.gg/";
    public const string RacesEndpoint = "races/data";
    public const string SocketRoot = "wss://racetime.gg/";

    /// <summary>New Race... opens racetime.gg (CREATE_RACE_ADDRESS "/").</summary>
    public const string CreateRaceAddress = "https://racetime.gg/";

    private readonly HttpClient _http = new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };

    public RacetimeAuthenticator Authenticator { get; } = authenticator;

    /// <summary>The last list read (none until the first refresh works).</summary>
    public IReadOnlyList<RacetimeRace> Races { get; private set; } = [];

    /// <summary>RefreshRacesList: a failed refresh keeps the previous list.</summary>
    public async Task<IReadOnlyList<RacetimeRace>> RefreshRacesAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUri + RacesEndpoint);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Authenticator.AccessToken);
            using HttpResponseMessage response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var races = new List<RacetimeRace>();
            if (Json.Get(json.RootElement, "races") is { ValueKind: JsonValueKind.Array } list)
            {
                foreach (JsonElement race in list.EnumerateArray())
                {
                    // The list's entrants are dropped (r.entrants = new List).
                    races.Add(RacetimeRace.From(race) with { Entrants = [] });
                }
            }

            Races = races;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
        }

        return Races;
    }
}

/// <summary>An item of the "racetime.gg Races" menu: a race to join, a separator (no title), or New Race....</summary>
public sealed record RaceMenuEntry(string? Title, string? RaceId, bool IsNewRace = false)
{
    public bool IsSeparator => Title is null;
}

/// <summary>TimerForm.RacesRefreshed / UpdateTitle for a race provider's menu.</summary>
public static class RaceMenu
{
    /// <summary>
    /// Open races ("Game - Goal (N Entrants)"), then running ones ("[time] Game - Goal (x/y
    /// Finished)"), each group followed by a separator, then New Race....
    /// </summary>
    public static IReadOnlyList<RaceMenuEntry> Build(IEnumerable<RacetimeRace> races, DateTime nowUtc)
    {
        var items = new List<RaceMenuEntry>();
        foreach (RacetimeRace race in races.Where(r => r.MenuState == 1))
        {
            int entrants = race.NumEntrants;
            string plural = entrants == 1 ? "" : "s";
            items.Add(new RaceMenuEntry($"{GameAndGoal(race)} ({entrants} Entrant{plural})", race.Id));
        }

        if (items.Count > 0)
        {
            items.Add(new RaceMenuEntry(null, null));
        }

        foreach (RacetimeRace race in races.Where(r => r.MenuState == 3))
        {
            items.Add(new RaceMenuEntry(RunningTitle(race, nowUtc), race.Id));
        }

        if (items.Count > 0 && !items[^1].IsSeparator)
        {
            items.Add(new RaceMenuEntry(null, null));
        }

        items.Add(new RaceMenuEntry("New Race...", null, IsNewRace: true));
        return items;
    }

    /// <summary>UpdateTitle: the time since the race started, at whole seconds.</summary>
    public static string RunningTitle(RacetimeRace race, DateTime nowUtc)
    {
        DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(race.StartTime);
        TimeSpan timeSpan = nowUtc - startTime;
        if (timeSpan < TimeSpan.Zero)
        {
            timeSpan = TimeSpan.Zero;
        }

        string time = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Seconds,
            NullFormat = NullFormat.ZeroWithAccuracy,
            DigitsFormat = DigitsFormat.SingleDigitMinutes,
        }.Format(timeSpan);
        return string.Format(
            Localization.UiText.T("[{0}] {1} ({2}/{3} Finished)"),
            time,
            GameAndGoal(race),
            race.Finishes,
            race.NumEntrants - race.Forfeits);
    }

    /// <summary>GetShortenedGameAndGoal: over 65 characters is cut to 60 and "...".</summary>
    public static string GameAndGoal(RacetimeRace race)
    {
        string goal = $"{race.GameName} - {race.Goal}";
        return goal.Length > 65 ? goal[..60] + "..." : goal;
    }
}
