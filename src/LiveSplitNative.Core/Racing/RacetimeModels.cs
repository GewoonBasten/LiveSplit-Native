using System.Globalization;
using System.Text.Json;
using System.Xml;

namespace LiveSplitNative.Core.Racing;

/// <summary>LiveSplit.Racetime RaceState.</summary>
public enum RaceState
{
    Unknown,
    Open,
    OpenInviteOnly,
    Ready,
    Starting,
    Started,
    Ended,
    Cancelled,
}

/// <summary>LiveSplit.Racetime UserStatus.</summary>
public enum UserStatus
{
    Unknown,
    NotInRace,
    NotReady,
    Ready,
    Finished,
    Disqualified,
    Forfeit,
    Racing,
}

/// <summary>An entrant of a race (RacetimeUser over Race.EntrantToUserConverter).</summary>
public sealed record RacetimeEntrant(string Id, string Name, string FullName, string? TwitchName, UserStatus Status, DateTime FinishedAt)
{
    public bool HasFinished => FinishedAt != DateTime.MaxValue;

    internal static RacetimeEntrant From(JsonElement entrant)
    {
        JsonElement user = Json.Get(entrant, "user");
        return new RacetimeEntrant(
            Json.String(user, "id") ?? "",
            Json.String(user, "name") ?? "",
            Json.String(user, "full_name") ?? Json.String(user, "name") ?? "",
            Json.String(user, "twitch_name"),
            Json.String(Json.Get(entrant, "status"), "value") switch
            {
                "not_ready" => UserStatus.NotReady,
                "ready" => UserStatus.Ready,
                "done" => UserStatus.Finished,
                "in_progress" => UserStatus.Racing,
                "dnf" => UserStatus.Forfeit,
                "dq" => UserStatus.Disqualified,
                _ => UserStatus.Unknown,
            },
            Json.Date(Json.String(entrant, "finished_at")) ?? DateTime.MaxValue);
    }
}

/// <summary>
/// LiveSplit.Racetime's Race over racetime.gg's race JSON (races/data and race.data messages):
/// Id is the race's name "game/slug-words".
/// </summary>
public sealed record RacetimeRace(
    string Id,
    string GameName,
    string GameId,
    string? Goal,
    RaceState State,
    int NumEntrants,
    int Finishes,
    int Forfeits,
    DateTime StartedAt,
    TimeSpan StartDelay,
    int Version,
    IReadOnlyList<RacetimeEntrant> Entrants)
{
    public string ChannelName => Id.Contains('/') ? Id[(Id.IndexOf('/') + 1)..] : Id;

    /// <summary>IRaceInfo.State: 1 for open races, 3 for running ones, 42 for the rest.</summary>
    public int MenuState => State is RaceState.Open or RaceState.OpenInviteOnly ? 1 : State == RaceState.Started ? 3 : 42;

    /// <summary>IRaceInfo.Starttime: seconds since 1970 at the start (0 before it).</summary>
    public int StartTime => StartedAt == DateTime.MaxValue
        ? 0
        : (int)(StartedAt - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

    public static RacetimeRace From(JsonElement race)
    {
        var entrants = new List<RacetimeEntrant>();
        if (Json.Get(race, "entrants") is { ValueKind: JsonValueKind.Array } list)
        {
            foreach (JsonElement entrant in list.EnumerateArray())
            {
                entrants.Add(RacetimeEntrant.From(entrant));
            }
        }

        TimeSpan startDelay = TimeSpan.Zero;
        try
        {
            if (Json.String(race, "start_delay") is { } delay)
            {
                startDelay = XmlConvert.ToTimeSpan(delay);
            }
        }
        catch (FormatException)
        {
        }

        return new RacetimeRace(
            Json.String(race, "name") ?? "",
            Json.String(Json.Get(race, "category"), "name") ?? "",
            Json.String(Json.Get(race, "category"), "slug") ?? "",
            Json.String(Json.Get(race, "goal"), "name"),
            Json.String(Json.Get(race, "status"), "value") switch
            {
                "open" => RaceState.Open,
                "invitational" => RaceState.OpenInviteOnly,
                "pending" => RaceState.Starting,
                "in_progress" => RaceState.Started,
                "finished" => RaceState.Ended,
                "cancelled" => RaceState.Cancelled,
                _ => RaceState.Unknown,
            },
            Json.Int(race, "entrants_count"),
            Json.Int(race, "entrants_count_finished"),
            Json.Int(race, "entrants_count_inactive"),
            Json.Date(Json.String(race, "started_at")) ?? DateTime.MaxValue,
            startDelay,
            Json.Int(race, "version"),
            entrants);
    }
}

/// <summary>A race.split message (SplitUpdate): another entrant's split.</summary>
public sealed record RacetimeSplitUpdate(string UserId, string SplitName, TimeSpan? SplitTime, bool IsUndo, bool IsFinish)
{
    public static RacetimeSplitUpdate From(JsonElement split) => new(
        Json.String(split, "user_id") ?? "",
        Json.String(split, "split_name") ?? "",
        Json.String(split, "split_time") is { } time && time != "-" ? Model.TimeSpanParser.ParseNullable(time) : null,
        Json.Bool(split, "is_undo"),
        Json.Bool(split, "is_finish"));
}

/// <summary>Null-tolerant JSON reading (the Windows code reads dynamic JSON).</summary>
internal static class Json
{
    public static JsonElement Get(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) ? value : default;

    public static string? String(JsonElement element, string name) =>
        Get(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    public static int Int(JsonElement element, string name) =>
        Get(element, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out int number) ? number : 0;

    public static bool Bool(JsonElement element, string name) =>
        Get(element, name).ValueKind == JsonValueKind.True;

    public static DateTime? Date(string? text) =>
        text is not null && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime date)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : null;
}
