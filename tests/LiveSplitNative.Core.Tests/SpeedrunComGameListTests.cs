using System.Text.Json;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.Core.Tests;

/// <summary>CompositeGameList and the Splits Editor's game and category suggestions.</summary>
public sealed class SpeedrunComGameListTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "src-games-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static string Page(IEnumerable<(string Id, string Name)> games, string? next) =>
        JsonSerializer.Serialize(new
        {
            data = games.Select(g => new { id = g.Id, names = new { international = g.Name, japanese = (string?)null }, abbreviation = "x" }),
            pagination = new
            {
                offset = 0,
                max = 1000,
                size = 2,
                links = next is null ? Array.Empty<object>() : [new { rel = "next", uri = next }],
            },
        });

    /// <summary>speedrun.com with two pages of games and Super Mario Sunshine's categories.</summary>
    private sealed class FakeSpeedrunCom
    {
        public List<string> Requests { get; } = [];

        public bool Offline { get; set; }

        public Task<string> Get(Uri uri, CancellationToken token)
        {
            lock (Requests)
            {
                Requests.Add(uri.ToString());
            }

            if (Offline)
            {
                throw new HttpRequestException("offline");
            }

            string url = uri.ToString();
            if (url == "https://www.speedrun.com/api/v1/games?_bulk=yes&max=1000")
            {
                return Task.FromResult(Page(
                    [("v1pxjz68", "Super Mario Sunshine"), ("o1y9wo6q", "Super Mario 64"), ("pd0wq31e", "Super Metroid")],
                    "https://www.speedrun.com/api/v1/games?_bulk=yes&max=1000&offset=1000"));
            }

            if (url == "https://www.speedrun.com/api/v1/games?_bulk=yes&max=1000&offset=1000")
            {
                return Task.FromResult(Page([("j1npme6p", "Sunshine Island"), ("v1pxjz68", "Super Mario Sunshine"), ("k6qqkx6g", "The Legend of Zelda: Ocarina of Time")], null));
            }

            if (url == "https://www.speedrun.com/api/v1/games/v1pxjz68/categories")
            {
                return Task.FromResult(
                    "{\"data\":[{\"id\":\"a\",\"name\":\"Any%\",\"type\":\"per-game\"},{\"id\":\"b\",\"name\":\"120 Shines\",\"type\":\"per-game\"},"
                    + "{\"id\":\"c\",\"name\":\"Individual Level\",\"type\":\"per-level\"},{\"id\":\"d\",\"name\":\"Any% No ACE\",\"type\":\"per-game\"}]}");
            }

            if (url.StartsWith("https://www.speedrun.com/api/v1/games?name=", StringComparison.Ordinal))
            {
                return Task.FromResult("{\"data\":[]}");
            }

            throw new HttpRequestException("404 " + url);
        }
    }

    [Fact]
    public async Task Games_Are_Read_Page_By_Page_Sorted_And_Cached()
    {
        var speedrunCom = new FakeSpeedrunCom();
        string cache = Path.Combine(_folder, SpeedrunComGameList.CacheFileName);
        var list = new SpeedrunComGameList(speedrunCom.Get, cache);
        int changed = 0;
        list.Changed += () => changed++;
        await list.LoadAsync();
        Assert.True(list.LoadedFromApi);
        Assert.Equal(1, changed);
        Assert.Equal(
            ["Sunshine Island", "Super Mario 64", "Super Mario Sunshine", "Super Metroid", "The Legend of Zelda: Ocarina of Time"],
            list.GameNames);
        Assert.Equal("v1pxjz68", list.GetGameId("Super Mario Sunshine"));
        Assert.Null(list.GetGameId("super mario sunshine"));

        // Once per session.
        await list.LoadAsync();
        Assert.Equal(2, speedrunCom.Requests.Count);

        // The next session starts from the cache (name → ID), even offline.
        Dictionary<string, string> cached = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cache))!;
        Assert.Equal("k6qqkx6g", cached["The Legend of Zelda: Ocarina of Time"]);
        var offline = new FakeSpeedrunCom { Offline = true };
        var next = new SpeedrunComGameList(offline.Get, cache);
        await next.LoadAsync();
        Assert.Equal(5, next.GameNames!.Count);
        Assert.Equal("o1y9wo6q", next.GetGameId("Super Mario 64"));
    }

    [Fact]
    public async Task Search_Matches_Names_And_Abbreviations_Closest_First()
    {
        var list = new SpeedrunComGameList(new FakeSpeedrunCom().Get, null);
        Assert.Empty(list.Search("sms"));
        await list.LoadAsync();

        // "SMS" is Super Mario Sunshine's abbreviation; "sunshine" is in two names, the shorter closer.
        Assert.Equal(["Super Mario Sunshine"], list.Search("sms"));
        Assert.Equal(["Sunshine Island", "Super Mario Sunshine"], list.Search("Sunshine"));
        Assert.Equal(["Super Mario 64", "Super Mario Sunshine"], list.Search("super mario"));
        Assert.Contains("The Legend of Zelda: Ocarina of Time", list.Search("ocarina"));
        Assert.Empty(list.Search("xyz"));
        Assert.True(list.Search("s").Count <= 10);
    }

    [Fact]
    public async Task Categories_Are_The_Games_Full_Game_Categories()
    {
        var list = new SpeedrunComGameList(new FakeSpeedrunCom().Get, null);
        await list.LoadAsync();
        Assert.Equal(["Any%", "120 Shines", "Any% No ACE"], await list.GetCategoryNamesAsync("Super Mario Sunshine"));

        // A game speedrun.com does not have, or no connection: Any%, Low%, 100%.
        Assert.Equal(["Any%", "Low%", "100%"], await list.GetCategoryNamesAsync("My Own Game"));
        var offline = new SpeedrunComGameList(new FakeSpeedrunCom { Offline = true }.Get, null);
        Assert.Equal(["Any%", "Low%", "100%"], await offline.GetCategoryNamesAsync("Super Mario Sunshine"));
    }

    [Fact]
    public void Similarity_Is_The_Edit_Distance_Ignoring_Case()
    {
        Assert.Equal(0, "Sunshine".Similarity("sunshine"));
        Assert.Equal(3, "kitten".Similarity("sitting"));
        Assert.Equal(5, "".Similarity("abcde"));
        Assert.Equal(["sms", "smsx", "super"], new[] { "super", "smsx", "sms" }.OrderBySimilarityTo("sms"));
    }
}
