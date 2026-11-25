using System.Globalization;
using System.Text.Json;

namespace LiveSplitNative.Core.Web;

/// <summary>speedrun.com's timing methods (SpeedrunComSharp.TimingMethod).</summary>
public enum SrcTimingMethod
{
    GameTime,
    RealTime,
    RealTimeWithoutLoads,
}

/// <summary>An element with an ID and a name (a platform or region).</summary>
public sealed record SrcNamed(string Id, string Name);

/// <summary>
/// A game with its default timing method, platforms and regions; for submitting, also its
/// ruleset (the timing methods it uses, whether runs need a video, whether emulators are
/// allowed), the year it came out and its moderators' user IDs.
/// </summary>
public sealed record SrcGame(
    string Id,
    string Name,
    SrcTimingMethod DefaultTimingMethod,
    IReadOnlyList<SrcNamed> Platforms,
    IReadOnlyList<SrcNamed> Regions)
{
    public IReadOnlyList<SrcTimingMethod> TimingMethods { get; init; } = [];

    public bool RequiresVideo { get; init; }

    public bool EmulatorsAllowed { get; init; }

    public int? YearOfRelease { get; init; }

    public IReadOnlyList<string> ModeratorIds { get; init; } = [];

    /// <summary>The game's icon and box art (Assets.Icon / Assets.CoverMedium), when it has them.</summary>
    public Uri? IconUri { get; init; }

    public Uri? CoverUri { get; init; }
}

/// <summary>
/// A category: per game or per level, and how many players a run has (Players.Value; with
/// <see cref="PlayersExactly"/> when the count is exact rather than "up to").
/// </summary>
public sealed record SrcCategory(string Id, string Name, bool IsPerGame, int Players)
{
    public bool PlayersExactly { get; init; }

    /// <summary>The category's rules as speedrun.com gives them (Category.Rules), if any.</summary>
    public string? Rules { get; init; }
}

/// <summary>
/// A variable: the category it belongs to (null for all), whether it applies to full-game runs
/// (scope global or full-game), whether it is a subcategory, and its values (ID → label).
/// </summary>
public sealed record SrcVariable(
    string Id,
    string Name,
    string? CategoryId,
    bool IsFullGame,
    bool IsSubcategory,
    bool IsUserDefined,
    IReadOnlyDictionary<string, string> Values)
{
    /// <summary>Whether a run needs a value for it (Variable.IsMandatory).</summary>
    public bool IsMandatory { get; init; }
}

/// <summary>A run's times (RunTimes): each one only when the run has it.</summary>
public sealed record SrcRunTimes(TimeSpan? Primary, TimeSpan? RealTime, TimeSpan? RealTimeWithoutLoads, TimeSpan? GameTime);

/// <summary>A leaderboard record: its place, times and the players' names.</summary>
public sealed record SrcRecord(int Place, SrcRunTimes Times, IReadOnlyList<string> Players);

/// <summary>
/// The parts of the speedrun.com REST API (v1) the World Record component uses, as LiveSplit's
/// SpeedrunComSharp reads them. The transport is given so tests can answer with saved JSON.
/// </summary>
public sealed class SpeedrunComClient(Func<Uri, CancellationToken, Task<string>> get)
{
    public static readonly Uri BaseUri = new("https://www.speedrun.com/api/v1/");

    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LiveSplitNative/1.0");
        return client;
    });

    /// <summary>The client every World Record component shares (HTTP to speedrun.com).</summary>
    public static SpeedrunComClient Default { get; } = new((uri, token) => Http.Value.GetStringAsync(uri, token));

    /// <summary>
    /// The game with exactly this name (CompositeGameList.GetGameID, then GetGame with platforms
    /// and regions); null when there is none.
    /// </summary>
    public async Task<SrcGame?> FindGameAsync(string name, CancellationToken token = default)
    {
        string json = await get(new Uri(BaseUri, $"games?name={Uri.EscapeDataString(name)}&embed=platforms,regions&max=200"), token)
            .ConfigureAwait(false);
        return ParseGames(json).FirstOrDefault(g => g.Name == name);
    }

    public async Task<IReadOnlyList<SrcCategory>> GetCategoriesAsync(string gameId, CancellationToken token = default) =>
        ParseCategories(await get(new Uri(BaseUri, $"games/{Uri.EscapeDataString(gameId)}/categories"), token).ConfigureAwait(false));

    public async Task<IReadOnlyList<SrcVariable>> GetVariablesAsync(string gameId, CancellationToken token = default) =>
        ParseVariables(await get(new Uri(BaseUri, $"games/{Uri.EscapeDataString(gameId)}/variables"), token).ConfigureAwait(false));

    /// <summary>
    /// LeaderboardsClient.GetLeaderboardForFullGameCategory with the players embedded: the top
    /// runs (all of them when tied), filtered as given.
    /// </summary>
    public async Task<IReadOnlyList<SrcRecord>> GetLeaderboardAsync(
        string gameId,
        string categoryId,
        int top,
        string? platformId,
        string? regionId,
        bool? emulators,
        SrcTimingMethod? timing,
        IEnumerable<(string VariableId, string ValueId)> variables,
        CancellationToken token = default)
    {
        string uri = LeaderboardUri(gameId, categoryId, top, platformId, regionId, emulators, timing, variables);
        return ParseLeaderboard(await get(new Uri(BaseUri, uri), token).ConfigureAwait(false));
    }

    /// <summary>The leaderboard request as LeaderboardsClient.getLeaderboard builds its parameters.</summary>
    public static string LeaderboardUri(
        string gameId,
        string categoryId,
        int top,
        string? platformId,
        string? regionId,
        bool? emulators,
        SrcTimingMethod? timing,
        IEnumerable<(string VariableId, string ValueId)> variables)
    {
        var parameters = new List<string> { "embed=players", $"top={top.ToString(CultureInfo.InvariantCulture)}" };
        if (!string.IsNullOrEmpty(platformId))
        {
            parameters.Add("platform=" + Uri.EscapeDataString(platformId));
        }

        if (!string.IsNullOrEmpty(regionId))
        {
            parameters.Add("region=" + Uri.EscapeDataString(regionId));
        }

        if (emulators is { } onlyEmulators)
        {
            parameters.Add("emulators=" + (onlyEmulators ? "true" : "false"));
        }

        if (timing is { } method)
        {
            parameters.Add("timing=" + ToApiString(method));
        }

        foreach ((string variableId, string valueId) in variables)
        {
            parameters.Add($"var-{Uri.EscapeDataString(variableId)}={Uri.EscapeDataString(valueId)}");
        }

        return $"leaderboards/{Uri.EscapeDataString(gameId)}/category/{Uri.EscapeDataString(categoryId)}?{string.Join("&", parameters)}";
    }

    public static string ToApiString(SrcTimingMethod method) => method switch
    {
        SrcTimingMethod.RealTime => "realtime",
        SrcTimingMethod.RealTimeWithoutLoads => "realtime_noloads",
        _ => "ingame",
    };

    public static SrcTimingMethod ParseTimingMethod(string? text) => text switch
    {
        "realtime" => SrcTimingMethod.RealTime,
        "realtime_noloads" => SrcTimingMethod.RealTimeWithoutLoads,
        "ingame" => SrcTimingMethod.GameTime,
        _ => throw new FormatException("Unknown timing method " + text),
    };

    public static IReadOnlyList<SrcGame> ParseGames(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        var games = new List<SrcGame>();
        JsonElement data = Data(document.RootElement);
        foreach (JsonElement game in data.ValueKind == JsonValueKind.Array ? data.EnumerateArray().ToList() : [data])
        {
            games.Add(ParseGame(game));
        }

        return games;
    }

    /// <summary>Game.Parse: one game object.</summary>
    public static SrcGame ParseGame(JsonElement game)
    {
        JsonElement ruleset = game.GetProperty("ruleset");
        var timingMethods = new List<SrcTimingMethod>();
        if (ruleset.TryGetProperty("run-times", out JsonElement runTimes) && runTimes.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement method in runTimes.EnumerateArray())
            {
                timingMethods.Add(ParseTimingMethod(method.GetString()));
            }
        }

        var moderators = new List<string>();
        if (game.TryGetProperty("moderators", out JsonElement mods) && mods.ValueKind == JsonValueKind.Object)
        {
            if (mods.TryGetProperty("data", out JsonElement embedded) && embedded.ValueKind == JsonValueKind.Array)
            {
                moderators.AddRange(embedded.EnumerateArray().Select(m => m.GetProperty("id").GetString() ?? ""));
            }
            else
            {
                moderators.AddRange(mods.EnumerateObject().Select(m => m.Name));
            }
        }

        return new SrcGame(
            game.GetProperty("id").GetString() ?? "",
            game.GetProperty("names").GetProperty("international").GetString() ?? "",
            ParseTimingMethod(ruleset.GetProperty("default-time").GetString()),
            Named(game, "platforms"),
            Named(game, "regions"))
        {
            TimingMethods = timingMethods,
            RequiresVideo = Flag(ruleset, "require-video"),
            EmulatorsAllowed = Flag(ruleset, "emulators-allowed"),
            YearOfRelease = game.TryGetProperty("released", out JsonElement released) && released.ValueKind == JsonValueKind.Number
                ? released.GetInt32()
                : null,
            ModeratorIds = moderators,
            IconUri = Asset(game, "icon"),
            CoverUri = Asset(game, "cover-medium"),
        };
    }

    private static Uri? Asset(JsonElement game, string name) =>
        game.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Object
            && assets.TryGetProperty(name, out JsonElement asset) && asset.ValueKind == JsonValueKind.Object
            && asset.TryGetProperty("uri", out JsonElement uri) && uri.ValueKind == JsonValueKind.String
            && Uri.TryCreate(uri.GetString(), UriKind.Absolute, out Uri? result)
            ? result
            : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

    public static IReadOnlyList<SrcCategory> ParseCategories(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        var categories = new List<SrcCategory>();
        foreach (JsonElement category in Data(document.RootElement).EnumerateArray())
        {
            int players = category.TryGetProperty("players", out JsonElement p) && p.TryGetProperty("value", out JsonElement value)
                && value.ValueKind == JsonValueKind.Number
                ? value.GetInt32()
                : 1;
            categories.Add(new SrcCategory(
                category.GetProperty("id").GetString() ?? "",
                category.GetProperty("name").GetString() ?? "",
                category.GetProperty("type").GetString() == "per-game",
                players)
            {
                PlayersExactly = category.TryGetProperty("players", out JsonElement type)
                    && type.TryGetProperty("type", out JsonElement kind) && kind.GetString() == "exactly",
                Rules = category.TryGetProperty("rules", out JsonElement rules) && rules.ValueKind == JsonValueKind.String
                    ? rules.GetString()
                    : null,
            });
        }

        return categories;
    }

    public static IReadOnlyList<SrcVariable> ParseVariables(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        var variables = new List<SrcVariable>();
        foreach (JsonElement variable in Data(document.RootElement).EnumerateArray())
        {
            string scope = variable.GetProperty("scope").GetProperty("type").GetString() ?? "";
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (variable.TryGetProperty("values", out JsonElement valuesElement))
            {
                if (valuesElement.TryGetProperty("values", out JsonElement detailed) && detailed.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty value in detailed.EnumerateObject())
                    {
                        values[value.Name] = value.Value.GetProperty("label").GetString() ?? "";
                    }
                }
                else if (valuesElement.TryGetProperty("choices", out JsonElement choices) && choices.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty value in choices.EnumerateObject())
                    {
                        values[value.Name] = value.Value.GetString() ?? "";
                    }
                }
            }

            variables.Add(new SrcVariable(
                variable.GetProperty("id").GetString() ?? "",
                variable.GetProperty("name").GetString() ?? "",
                variable.TryGetProperty("category", out JsonElement category) && category.ValueKind == JsonValueKind.String
                    ? category.GetString()
                    : null,
                scope is "global" or "full-game",
                variable.TryGetProperty("is-subcategory", out JsonElement sub) && sub.ValueKind == JsonValueKind.True,
                variable.TryGetProperty("user-defined", out JsonElement user) && user.ValueKind == JsonValueKind.True,
                values)
            {
                IsMandatory = Flag(variable, "mandatory"),
            });
        }

        return variables;
    }

    /// <summary>Leaderboard.Parse: the records, with the players' names from the embedded players.</summary>
    public static IReadOnlyList<SrcRecord> ParseLeaderboard(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement data = Data(document.RootElement);
        var userNames = new Dictionary<string, string>(StringComparer.Ordinal);
        if (data.TryGetProperty("players", out JsonElement players) && players.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonElement player in players.GetProperty("data").EnumerateArray())
            {
                if (player.TryGetProperty("rel", out JsonElement rel) && rel.GetString() == "user"
                    && player.TryGetProperty("id", out JsonElement id)
                    && player.TryGetProperty("names", out JsonElement names))
                {
                    userNames[id.GetString() ?? ""] = names.GetProperty("international").GetString() ?? "";
                }
            }
        }

        var records = new List<SrcRecord>();
        foreach (JsonElement entry in data.GetProperty("runs").EnumerateArray())
        {
            JsonElement run = entry.GetProperty("run");
            var runPlayers = new List<string>();
            foreach (JsonElement player in run.GetProperty("players").EnumerateArray())
            {
                string? rel = player.TryGetProperty("rel", out JsonElement r) ? r.GetString() : null;
                if (rel == "user")
                {
                    string id = player.GetProperty("id").GetString() ?? "";
                    runPlayers.Add(userNames.TryGetValue(id, out string? name) ? name : id);
                }
                else
                {
                    runPlayers.Add(player.TryGetProperty("name", out JsonElement guest) ? guest.GetString() ?? "" : "");
                }
            }

            JsonElement times = run.GetProperty("times");
            records.Add(new SrcRecord(
                entry.TryGetProperty("place", out JsonElement place) && place.ValueKind == JsonValueKind.Number ? place.GetInt32() : 0,
                new SrcRunTimes(Time(times, "primary"), Time(times, "realtime"), Time(times, "realtime_noloads"), Time(times, "ingame")),
                runPlayers));
        }

        return records;
    }

    private static JsonElement Data(JsonElement root) => root.GetProperty("data");

    /// <summary>
    /// RunTimes.Parse: "x_t" seconds when "x" is set. TimeSpan.FromSeconds on .NET Framework
    /// rounds to whole milliseconds, which the leaderboard times are anyway.
    /// </summary>
    private static TimeSpan? Time(JsonElement times, string name) =>
        times.TryGetProperty(name, out JsonElement text) && text.ValueKind == JsonValueKind.String
            && times.TryGetProperty(name + "_t", out JsonElement seconds) && seconds.ValueKind == JsonValueKind.Number
            ? TimeSpan.FromMilliseconds(Math.Round(seconds.GetDouble() * 1000))
            : null;

    private static IReadOnlyList<SrcNamed> Named(JsonElement game, string property)
    {
        var list = new List<SrcNamed>();
        if (game.TryGetProperty(property, out JsonElement embedded)
            && embedded.ValueKind == JsonValueKind.Object
            && embedded.TryGetProperty("data", out JsonElement items))
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                list.Add(new SrcNamed(item.GetProperty("id").GetString() ?? "", item.GetProperty("name").GetString() ?? ""));
            }
        }

        return list;
    }
}
