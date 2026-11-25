using System.Text.Json;
using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.Web;

/// <summary>
/// Port of LiveSplit.Web.CompositeGameList and the Splits Editor's game search
/// (RunEditorDialog.FillCbxGameName / SearchForGameName): every game on speedrun.com
/// (SpeedrunComSharp GetGameHeaders, games?_bulk=yes&amp;max=1000 page by page), read once per
/// session and kept in speedruncom_game_names.json (name → ID) for the next one. A search
/// matches the games whose names or abbreviations contain the text, closest first, ten at most.
/// </summary>
public sealed class SpeedrunComGameList(Func<Uri, CancellationToken, Task<string>> get, string? cachePath)
{
    public const string CacheFileName = "speedruncom_game_names.json";

    private readonly object _lock = new();
    private Task? _loading;
    private IReadOnlyList<string>? _names;
    private Dictionary<string, string> _ids = new(StringComparer.Ordinal);
    private List<(string Key, string[] Games)> _abbreviations = [];

    /// <summary>The list the Splits Editor uses (speedrun.com over HTTP, cached beside settings.cfg).</summary>
    public static SpeedrunComGameList Current { get; set; } = new(
        (uri, token) => SharedHttp.Value.GetStringAsync(uri, token),
        Path.Combine(Serialization.XmlAppSettingsSerializer.ResolveConfigDirectory(), CacheFileName));

    private static readonly Lazy<HttpClient> SharedHttp = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LiveSplitNative/1.0");
        return client;
    });

    /// <summary>speedrun.com for the game's categories.</summary>
    public SpeedrunComClient Client { get; } = new(get);

    /// <summary>The game names (sorted), or null before any were read.</summary>
    public IReadOnlyList<string>? GameNames
    {
        get
        {
            lock (_lock)
            {
                return _names;
            }
        }
    }

    /// <summary>Whether the names came from speedrun.com this session (not only the cache).</summary>
    public bool LoadedFromApi { get; private set; }

    /// <summary>Raised (on any thread) when the names were read from the cache or from speedrun.com.</summary>
    public event Action? Changed;

    /// <summary>
    /// The cache first, then speedrun.com, once per session (later calls share the first one).
    /// A failed download keeps the cached names.
    /// </summary>
    public Task LoadAsync()
    {
        lock (_lock)
        {
            return _loading ??= Task.Run(LoadCoreAsync);
        }
    }

    private async Task LoadCoreAsync()
    {
        if (cachePath is not null && File.Exists(cachePath))
        {
            try
            {
                Dictionary<string, string>? cached = JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(cachePath));
                if (cached is not null)
                {
                    Set(cached, [.. cached.Keys]);
                }
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
            }
        }

        try
        {
            var names = new List<string>();
            var ids = new Dictionary<string, string>(StringComparer.Ordinal);
            Uri? next = new(SpeedrunComClient.BaseUri, "games?_bulk=yes&max=1000");
            while (next is not null)
            {
                using JsonDocument page = JsonDocument.Parse(await get(next, CancellationToken.None).ConfigureAwait(false));
                if (page.RootElement.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement game in data.EnumerateArray())
                    {
                        string? id = game.TryGetProperty("id", out JsonElement i) ? i.GetString() : null;
                        string? name = game.TryGetProperty("names", out JsonElement n) && n.TryGetProperty("international", out JsonElement international)
                            ? international.GetString()
                            : null;
                        if (!string.IsNullOrEmpty(name) && id is not null && ids.TryAdd(name, id))
                        {
                            names.Add(name);
                        }
                    }
                }

                next = NextPage(page.RootElement);
            }

            names.Sort(StringComparer.CurrentCulture);
            var sorted = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in names)
            {
                sorted[name] = ids[name];
            }

            Set(sorted, names);
            if (cachePath is not null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                    await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(sorted));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception)
        {
            // Offline or an answer that could not be read: the cached names stay (Log.Error upstream).
        }
        finally
        {
            LoadedFromApi = true;
        }
    }

    private static Uri? NextPage(JsonElement root)
    {
        if (root.TryGetProperty("pagination", out JsonElement pagination)
            && pagination.TryGetProperty("links", out JsonElement links)
            && links.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement link in links.EnumerateArray())
            {
                if (link.TryGetProperty("rel", out JsonElement rel) && rel.GetString() == "next"
                    && link.TryGetProperty("uri", out JsonElement uri) && Uri.TryCreate(uri.GetString(), UriKind.Absolute, out Uri? next))
                {
                    return next;
                }
            }
        }

        return null;
    }

    private void Set(Dictionary<string, string> ids, IReadOnlyList<string> names)
    {
        // RunEditorDialog: each game under every abbreviation of its name, grouped by abbreviation.
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (string name in names)
        {
            foreach (string abbreviation in name.GetAbbreviations())
            {
                if (!groups.TryGetValue(abbreviation, out List<string>? games))
                {
                    groups[abbreviation] = games = [];
                    order.Add(abbreviation);
                }

                games.Add(name);
            }
        }

        List<(string, string[])> abbreviations = [.. order.Select(key => (key.ToLowerInvariant(), groups[key].ToArray()))];
        lock (_lock)
        {
            _ids = ids;
            _names = names;
            _abbreviations = abbreviations;
        }

        Changed?.Invoke();
    }

    /// <summary>CompositeGameList.GetGameID: the ID of the game with exactly this name, or null.</summary>
    public string? GetGameId(string gameName)
    {
        lock (_lock)
        {
            return _ids.TryGetValue(gameName, out string? id) ? id : null;
        }
    }

    /// <summary>SearchForGameName: up to ten games whose name or abbreviation contains the text, the closest first.</summary>
    public IReadOnlyList<string> Search(string text)
    {
        List<(string Key, string[] Games)> abbreviations;
        lock (_lock)
        {
            abbreviations = _abbreviations;
        }

        string name = text.ToLowerInvariant();
        return [.. abbreviations
            .Where(x => x.Key.Contains(name, StringComparison.Ordinal))
            .OrderBy(x => x.Key.Similarity(name))
            .SelectMany(x => x.Games)
            .Distinct()
            .Take(10)];
    }

    /// <summary>
    /// RefreshCategoryAutoCompleteList: the game's full-game categories on speedrun.com, or Any%,
    /// Low% and 100% when the game is not found.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetCategoryNamesAsync(string gameName, CancellationToken token = default)
    {
        string[] fallback = ["Any%", "Low%", "100%"];
        try
        {
            string? id = GetGameId(gameName);
            if (id is null && !string.IsNullOrWhiteSpace(gameName))
            {
                id = (await Client.FindGameAsync(gameName, token).ConfigureAwait(false))?.Id;
            }

            if (id is null)
            {
                return fallback;
            }

            return [.. (await Client.GetCategoriesAsync(id, token).ConfigureAwait(false)).Where(c => c.IsPerGame).Select(c => c.Name)];
        }
        catch (Exception)
        {
            // Not found, offline or an answer that could not be read (Log.Error upstream).
            return fallback;
        }
    }
}
