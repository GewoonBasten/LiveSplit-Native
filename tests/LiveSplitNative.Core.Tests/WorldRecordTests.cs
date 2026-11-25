using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// LiveSplit.WorldRecord against saved speedrun.com answers (the API's JSON shapes): the
/// lookup (game, category, variables, leaderboard filters) and what the component shows.
/// </summary>
public class WorldRecordTests
{
    private const string Games =
        """
        {"data":[
          {"id":"other","names":{"international":"Super Mario Sunshine Category Extensions"},"ruleset":{"default-time":"ingame"},
           "platforms":{"data":[]},"regions":{"data":[]}},
          {"id":"sms","names":{"international":"Super Mario Sunshine"},"ruleset":{"default-time":"realtime"},
           "platforms":{"data":[{"id":"gcn","name":"GameCube"},{"id":"wii","name":"Wii"}]},
           "regions":{"data":[{"id":"jpn","name":"JPN / NTSC"},{"id":"usa","name":"USA / NTSC"}]}}
        ]}
        """;

    private const string Categories =
        """
        {"data":[
          {"id":"il","name":"Any%","type":"per-level","players":{"type":"exactly","value":1}},
          {"id":"any","name":"Any%","type":"per-game","players":{"type":"exactly","value":1}},
          {"id":"coop","name":"Co-op","type":"per-game","players":{"type":"exactly","value":2}}
        ]}
        """;

    private const string Variables =
        """
        {"data":[
          {"id":"plat","name":"Platform","category":null,"scope":{"type":"full-game"},"is-subcategory":true,"user-defined":false,
           "values":{"values":{"g":{"label":"GCN"},"s":{"label":"Switch"}}}},
          {"id":"ace","name":"ACE","category":"any","scope":{"type":"global"},"is-subcategory":false,"user-defined":false,
           "values":{"values":{"no":{"label":"No ACE"},"yes":{"label":"ACE"}}}},
          {"id":"other","name":"Version","category":"coop","scope":{"type":"full-game"},"is-subcategory":true,"user-defined":false,
           "values":{"values":{"v1":{"label":"GCN"}}}},
          {"id":"lvl","name":"Platform","category":null,"scope":{"type":"single-level"},"is-subcategory":true,"user-defined":false,
           "values":{"values":{"x":{"label":"GCN"}}}}
        ]}
        """;

    private static string Leaderboard(params (double Seconds, string[] Players)[] runs) =>
        "{\"data\":{\"runs\":["
        + string.Join(",", runs.Select(r =>
            "{\"place\":1,\"run\":{\"players\":["
            + string.Join(",", r.Players.Select(p => p.StartsWith('@')
                ? $"{{\"rel\":\"guest\",\"name\":\"{p[1..]}\"}}"
                : $"{{\"rel\":\"user\",\"id\":\"id-{p}\"}}"))
            + $"],\"times\":{{\"primary\":\"PT{r.Seconds}S\",\"primary_t\":{r.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},"
            + $"\"realtime\":\"PT{r.Seconds}S\",\"realtime_t\":{r.Seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)},"
            + "\"realtime_noloads\":null,\"realtime_noloads_t\":0,\"ingame\":null,\"ingame_t\":0}}}"))
        + "],\"players\":{\"data\":["
        + string.Join(",", runs.SelectMany(r => r.Players).Where(p => !p.StartsWith('@')).Distinct()
            .Select(p => $"{{\"rel\":\"user\",\"id\":\"id-{p}\",\"names\":{{\"international\":\"{p}\"}}}}"))
        + "]}}}";

    /// <summary>speedrun.com as saved answers by request path; the requests are recorded.</summary>
    private sealed class FakeSpeedrunCom
    {
        public List<Uri> Requests { get; } = [];
        public string LeaderboardJson { get; set; } = Leaderboard((4327, new[] { "inkstar" }));

        public SpeedrunComClient Client => new((uri, _) =>
        {
            Requests.Add(uri);
            string path = uri.AbsolutePath;
            return Task.FromResult(
                path.EndsWith("/games", StringComparison.Ordinal) ? Games
                : path.EndsWith("/categories", StringComparison.Ordinal) ? Categories
                : path.EndsWith("/variables", StringComparison.Ordinal) ? Variables
                : path.Contains("/leaderboards/", StringComparison.Ordinal) ? LeaderboardJson
                : throw new HttpRequestException("404 " + uri));
        });

        public string LeaderboardRequest => Requests.Single(u => u.AbsolutePath.Contains("/leaderboards/", StringComparison.Ordinal)).Query;
    }

    private static Run SmsRun(TimeSpan pb)
    {
        var run = new Run { GameName = "Super Mario Sunshine", CategoryName = "Any%" };
        run.AddSegment("Corona Mountain");
        run[0].PersonalBestSplitTime = new SplitTime(pb);
        run.Metadata.VariableValueNames["Platform"] = "GCN";
        run.Metadata.VariableValueNames["ACE"] = "No ACE";
        run.Metadata.PlatformName = "GameCube";
        run.Metadata.RegionName = "USA / NTSC";
        return run;
    }

    private static readonly LayoutSettings Colors = LayoutSettings.CreateStandard();

    [Fact]
    public async Task The_Lookup_Finds_The_Game_Category_And_Record_With_Subcategories()
    {
        var api = new FakeSpeedrunCom();
        var settings = new WorldRecordComponentSettings();
        WorldRecordResult? result = await WorldRecordLogic.FetchAsync(
            api.Client, WorldRecordLogic.Query(SmsRun(TimeSpan.FromHours(2)), settings));

        Assert.NotNull(result);
        Assert.Equal(("sms", "any"), (result.Game.Id, result.Category.Id));
        Assert.Equal(SrcTimingMethod.RealTime, result.Game.DefaultTimingMethod);
        SrcRecord record = Assert.Single(result.Ties);
        Assert.Equal(["inkstar"], record.Players);
        Assert.Equal(TimeSpan.FromSeconds(4327), record.Times.Primary);
        Assert.Null(record.Times.GameTime);

        // Default filters: subcategories only (Platform), not other variables, platform or region.
        Assert.Equal("?embed=players&top=1&var-plat=g", api.LeaderboardRequest);
        Assert.Contains(api.Requests, u => u.Query.Contains("name=Super%20Mario%20Sunshine", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_Filter_Goes_Into_The_Leaderboard_Request()
    {
        var api = new FakeSpeedrunCom();
        var settings = new WorldRecordComponentSettings
        {
            FilterVariables = true,
            FilterPlatform = true,
            FilterRegion = true,
            TimingMethod = "Real Time Without Loads",
        };
        Run run = SmsRun(TimeSpan.FromHours(2));
        run.Metadata.UsesEmulator = true;
        await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(run, settings));
        Assert.Equal(
            "?embed=players&top=1&platform=gcn&region=usa&emulators=true&timing=realtime_noloads&var-plat=g&var-ace=no",
            api.LeaderboardRequest);
    }

    [Fact]
    public async Task No_Record_When_The_Game_Or_Category_Is_Not_On_Speedrun_Com()
    {
        var api = new FakeSpeedrunCom();
        var settings = new WorldRecordComponentSettings();
        Run unknownGame = SmsRun(TimeSpan.FromHours(2));
        unknownGame.GameName = "Super Mario";
        Assert.Null(await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(unknownGame, settings)));
        Run unknownCategory = SmsRun(TimeSpan.FromHours(2));
        unknownCategory.CategoryName = "120 Shines";
        Assert.Null(await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(unknownCategory, settings)));
        Run noCategory = SmsRun(TimeSpan.FromHours(2));
        noCategory.CategoryName = "";
        Assert.Null(await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(noCategory, settings)));
    }

    [Fact]
    public async Task Shows_The_Record_And_Who_Holds_It()
    {
        var api = new FakeSpeedrunCom();
        var state = new LiveSplitState(SmsRun(TimeSpan.FromHours(2)));
        var settings = new WorldRecordComponentSettings();
        WorldRecordResult? result = await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(state.Run, settings));

        // Centered Text (the default): one line, shorter forms when it does not fit.
        WorldRecordView centered = WorldRecordLogic.View(state, settings, Colors, result, false, horizontal: false);
        Assert.True(centered.Centered);
        Assert.Equal("World Record is 1:12:07 by inkstar", centered.Info.Name);
        Assert.Contains("WR: 1:12:07 by inkstar", centered.Info.AlternateNames);
        Assert.Equal("", centered.Info.Value);

        settings.CenteredText = false;
        WorldRecordView row = WorldRecordLogic.View(state, settings, Colors, result, false, horizontal: false);
        Assert.False(row.Centered);
        Assert.Equal(("World Record", "1:12:07 by inkstar"), (row.Info.Name, row.Info.Value));
        Assert.Equal(["WR"], row.Info.AlternateNames);
        Assert.True(row.Info.ValueInTextFont);

        // Two rows or a horizontal layout are never centered.
        settings.CenteredText = true;
        Assert.False(WorldRecordLogic.View(state, settings, Colors, result, false, horizontal: true).Centered);
    }

    [Fact]
    public async Task Separate_Time_Puts_The_Holder_Beside_The_Name()
    {
        var api = new FakeSpeedrunCom();
        var state = new LiveSplitState(SmsRun(TimeSpan.FromHours(2)));
        var settings = new WorldRecordComponentSettings { CenteredText = false, SeparateTime = true };
        WorldRecordResult? result = await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(state.Run, settings));

        WorldRecordView row = WorldRecordLogic.View(state, settings, Colors, result, false, horizontal: false);
        Assert.Equal(("World Record by inkstar", "1:12:07"), (row.Info.Name, row.Info.Value));
        Assert.Equal(["WR by inkstar", "World Record", "WR"], row.Info.AlternateNames);
        Assert.False(row.Info.ValueInTextFont);

        // Saved only when on; Centered Text still wins.
        Assert.Contains("<SeparateTime>True</SeparateTime>", settings.ToInnerXml());
        Assert.True(WorldRecordComponentSettings.FromInnerXml(settings.ToInnerXml()).SeparateTime);
        Assert.True(WorldRecordComponentSettings.FromInnerXml("<RunnersWithName>True</RunnersWithName>").SeparateTime);
        Assert.DoesNotContain("SeparateTime", new WorldRecordComponentSettings().ToInnerXml());
        settings.CenteredText = true;
        Assert.Equal("World Record is 1:12:07 by inkstar", WorldRecordLogic.View(state, settings, Colors, result, false, false).Info.Name);
    }

    [Fact]
    public async Task Ties_Guests_And_Co_Op_Runs()
    {
        var api = new FakeSpeedrunCom { LeaderboardJson = Leaderboard((4327, new[] { "inkstar" }), (4327, new[] { "Alice", "@Bob" })) };
        var state = new LiveSplitState(SmsRun(TimeSpan.FromHours(2)));
        var settings = new WorldRecordComponentSettings { CenteredText = false };
        WorldRecordResult? result = await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(state.Run, settings));
        Assert.Equal(["Alice", "Bob"], result!.Ties[1].Players);
        Assert.Equal("1:12:07 (2-way tie)", WorldRecordLogic.View(state, settings, Colors, result, false, false).Info.Value);

        settings.CenteredText = true;
        WorldRecordView centered = WorldRecordLogic.View(state, settings, Colors, result, false, false);
        Assert.Equal("World Record is 1:12:07 by inkstar, Alice & Bob", centered.Info.Name);
        Assert.Contains("WR: 1:12:07 (2-way tie)", centered.Info.AlternateNames);
    }

    [Fact]
    public async Task A_Pb_At_Or_Under_The_Record_Is_Shown_As_Mine()
    {
        var api = new FakeSpeedrunCom();
        var state = new LiveSplitState(SmsRun(TimeSpan.FromSeconds(4327.4)));
        var settings = new WorldRecordComponentSettings { CenteredText = false };
        WorldRecordResult? result = await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(state.Run, settings));
        // The record has whole seconds, so the PB counts in whole seconds too.
        Assert.Equal("1:12:07 by me", WorldRecordLogic.View(state, settings, Colors, result, false, false).Info.Value);
    }

    [Theory]
    [InlineData(4327.5, WorldRecordPrecisionType.FromLeaderboard, "1:12:07.5")]
    [InlineData(4327, WorldRecordPrecisionType.FromLeaderboard, "1:12:07")]
    [InlineData(4327.5, WorldRecordPrecisionType.Seconds, "1:12:07")]
    [InlineData(4327, WorldRecordPrecisionType.Milliseconds, "1:12:07.000")]
    public async Task Precision(double seconds, WorldRecordPrecisionType precision, string shown)
    {
        var api = new FakeSpeedrunCom { LeaderboardJson = Leaderboard((seconds, new[] { "inkstar" })) };
        var state = new LiveSplitState(SmsRun(TimeSpan.FromHours(2)));
        var settings = new WorldRecordComponentSettings { CenteredText = false, PrecisionType = precision };
        WorldRecordResult? result = await WorldRecordLogic.FetchAsync(api.Client, WorldRecordLogic.Query(state.Run, settings));
        Assert.Equal(shown + " by inkstar", WorldRecordLogic.View(state, settings, Colors, result, false, false).Info.Value);
    }

    [Fact]
    public void Loading_And_Unknown()
    {
        var state = new LiveSplitState(SmsRun(TimeSpan.FromHours(2)));
        var settings = new WorldRecordComponentSettings();
        Assert.Equal("Loading World Record...", WorldRecordLogic.View(state, settings, Colors, null, true, false).Info.Name);
        Assert.Equal("Unknown World Record", WorldRecordLogic.View(state, settings, Colors, null, false, false).Info.Name);
        settings.CenteredText = false;
        Assert.Equal("Loading...", WorldRecordLogic.View(state, settings, Colors, null, true, false).Info.Value);
        Assert.Equal("-", WorldRecordLogic.View(state, settings, Colors, null, false, false).Info.Value);
    }

    [Fact]
    public async Task The_Fetcher_Looks_Up_Again_Only_When_Something_Changes()
    {
        var api = new FakeSpeedrunCom();
        var fetcher = new WorldRecordFetcher(api.Client);
        Run run = SmsRun(TimeSpan.FromHours(2));
        var settings = new WorldRecordComponentSettings();

        Task? first = fetcher.Update(run, settings);
        Assert.NotNull(first);
        // Loading until the lookup is back (the fake one may already be).
        Assert.True(fetcher.IsLoading || first.IsCompleted);
        await first;
        Assert.False(fetcher.IsLoading);
        Assert.Equal("inkstar", fetcher.Result!.Ties[0].Players[0]);
        Assert.Null(fetcher.Update(run, settings));

        // Another filter: a new lookup, nothing shown until it is back.
        settings.FilterPlatform = true;
        Task? second = fetcher.Update(run, settings);
        Assert.NotNull(second);
        await second;
        Assert.Contains("platform=gcn", api.Requests[^1].Query, StringComparison.Ordinal);

        var component = new LayoutComponent(StandardLayoutFactory.WorldRecordPath);
        Assert.Same(WorldRecordFetcher.For(component, api.Client), WorldRecordFetcher.For(component.Clone(), api.Client));
    }

    [Fact]
    public void Settings_Are_Windows_Settings()
    {
        Assert.Equal(
            "<Version>1.6</Version><TextColor>FFFFFFFF</TextColor><OverrideTextColor>False</OverrideTextColor>"
            + "<TimeColor>FFFFFFFF</TimeColor><OverrideTimeColor>False</OverrideTimeColor>"
            + "<BackgroundColor>00000000</BackgroundColor><BackgroundColor2>00000000</BackgroundColor2>"
            + "<BackgroundGradient>Plain</BackgroundGradient><Display2Rows>False</Display2Rows><CenteredText>True</CenteredText>"
            + "<FilterRegion>False</FilterRegion><FilterPlatform>False</FilterPlatform><FilterVariables>False</FilterVariables>"
            + "<FilterSubcategories>True</FilterSubcategories><TimingMethod>Default for Leaderboard</TimingMethod>"
            + "<PrecisionType>FromLeaderboard</PrecisionType>",
            new WorldRecordComponentSettings().ToInnerXml());
        WorldRecordComponentSettings read = WorldRecordComponentSettings.FromInnerXml(
            "<Version>1.6</Version><CenteredText>False</CenteredText><FilterPlatform>True</FilterPlatform>"
            + "<TimingMethod>Game Time</TimingMethod><PrecisionType>Milliseconds</PrecisionType>");
        Assert.Equal((false, true, "Game Time", WorldRecordPrecisionType.Milliseconds), (read.CenteredText, read.FilterPlatform, read.TimingMethod, read.PrecisionType));
        Assert.True(read.FilterSubcategories);

        StandardLayoutFactory.BuiltinComponentInfo info = Assert.Single(StandardLayoutFactory.BuiltinCatalog, c => c.Path == StandardLayoutFactory.WorldRecordPath);
        Assert.Equal((StandardLayoutFactory.ComponentCategory.Information, "World Record"), (info.Category, info.DisplayName));
        Assert.Equal("World Record", StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.WorldRecordPath).DisplayName);
    }
}
