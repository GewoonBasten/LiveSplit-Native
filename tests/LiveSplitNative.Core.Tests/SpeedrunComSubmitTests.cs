using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// Share's Speedrun.com (SpeedrunCom.ValidateRun / SubmitRun, 93a11b85) against a fake
/// speedrun.com, and Share's Excel (ExcelRunSaver).
/// </summary>
public class SpeedrunComSubmitTests
{
    private const string Key = "goodkey";

    private const string Games =
        """
        {"data":[
          {"id":"sms","names":{"international":"Super Mario Sunshine"},"released":2002,
           "ruleset":{"default-time":"realtime","run-times":["realtime","ingame"],"require-video":true,"emulators-allowed":true},
           "moderators":{"mod1":"super-moderator"},
           "platforms":{"data":[{"id":"gcn","name":"GameCube"}]},
           "regions":{"data":[{"id":"pal","name":"EUR / PAL"}]}},
          {"id":"loads","names":{"international":"Loads Game"},"released":2020,
           "ruleset":{"default-time":"realtime_noloads","run-times":["realtime","realtime_noloads"],"require-video":false,"emulators-allowed":false},
           "moderators":{},
           "platforms":{"data":[{"id":"pc","name":"PC"}]},"regions":{"data":[]}}
        ]}
        """;

    private const string Categories =
        """
        {"data":[
          {"id":"any","name":"Any%","type":"per-game","players":{"type":"exactly","value":1}},
          {"id":"coop","name":"Co-op","type":"per-game","players":{"type":"exactly","value":2}},
          {"id":"upto","name":"Up To","type":"per-game","players":{"type":"up-to","value":4}}
        ]}
        """;

    private const string Variables =
        """
        {"data":[
          {"id":"ver","name":"Version","category":null,"scope":{"type":"full-game"},"mandatory":true,"user-defined":false,"is-subcategory":true,
           "values":{"values":{"jp":{"label":"JPN"},"us":{"label":"USA"}}}},
          {"id":"note","name":"Note","category":"any","scope":{"type":"global"},"mandatory":false,"user-defined":true,"is-subcategory":false,
           "values":{"values":{}}},
          {"id":"other","name":"Other","category":"coop","scope":{"type":"full-game"},"mandatory":true,"user-defined":false,
           "values":{"values":{"o":{"label":"O"}}}}
        ]}
        """;

    /// <summary>speedrun.com by path: the lookups, /profile for the key, POST /runs.</summary>
    private sealed class FakeSpeedrunCom : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string Body, string? Key)> Requests { get; } = [];

        public string ProfileId { get; set; } = "me";

        public HttpResponseMessage? SubmitAnswer { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            string? key = request.Headers.TryGetValues("X-API-Key", out IEnumerable<string>? keys) ? keys.Single() : null;
            string path = request.RequestUri!.AbsolutePath;
            Requests.Add((request.Method, path + request.RequestUri.Query, body, key));
            string? json =
                path.EndsWith("/profile", StringComparison.Ordinal)
                    ? key == Key ? "{\"data\":{\"id\":\"" + ProfileId + "\",\"names\":{\"international\":\"Basten\"}}}" : null
                : path.EndsWith("/runs", StringComparison.Ordinal) && request.Method == HttpMethod.Post
                    ? null
                : path.EndsWith("/games", StringComparison.Ordinal) ? Games
                : path.EndsWith("/categories", StringComparison.Ordinal) ? Categories
                : path.EndsWith("/variables", StringComparison.Ordinal) ? Variables
                : null;
            if (path.EndsWith("/runs", StringComparison.Ordinal) && request.Method == HttpMethod.Post)
            {
                return SubmitAnswer ?? new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"data":{"id":"newrun","weblink":"https://www.speedrun.com/sms/run/newrun"}}"""),
                };
            }

            return json is null
                ? new HttpResponseMessage(path.EndsWith("/profile", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound)
                {
                    Content = new StringContent("""{"status":403,"message":"Invalid API key."}"""),
                }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }

    private static Run SampleRun(string game = "Super Mario Sunshine", string category = "Any%")
    {
        var run = new Run { GameName = game, CategoryName = category };
        run.Metadata.PlatformName = "GameCube";
        run.Metadata.RegionName = "EUR / PAL";
        run.Metadata.UsesEmulator = true;
        run.Metadata.VariableValueNames["Version"] = "JPN";
        run.Metadata.VariableValueNames["Note"] = "Low%";
        var first = new Segment("Airstrip") { PersonalBestSplitTime = new SplitTime(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(4)) };
        var last = new Segment("Corona")
        {
            PersonalBestSplitTime = new SplitTime(TimeSpan.Parse("01:02:03.4560000"), TimeSpan.Parse("01:01:40")),
        };
        run.AddSegment(first);
        run.AddSegment(last);
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(TimeSpan.FromHours(2), null), null, null, null));
        run.AttemptHistory.Add(new Attempt(
            2,
            last.PersonalBestSplitTime,
            new AtomicDateTime(new DateTime(2026, 10, 1, 19, 0, 0, DateTimeKind.Utc), false),
            new AtomicDateTime(new DateTime(2026, 10, 1, 20, 2, 3, DateTimeKind.Utc), false),
            null));
        return run;
    }

    private static (SpeedrunComSubmitter Submitter, FakeSpeedrunCom Fake, MemoryCredentialStore Store) Create(string? savedKey = Key)
    {
        var fake = new FakeSpeedrunCom();
        var store = new MemoryCredentialStore();
        store.Write(SpeedrunComSubmitter.CredentialName, savedKey);
        return (new SpeedrunComSubmitter(fake, store), fake, store);
    }

    [Fact]
    public async Task Validate_gives_upstreams_reasons_in_order()
    {
        (SpeedrunComSubmitter submitter, _, _) = Create();
        async Task<string?> Reason(Action<Run> change)
        {
            Run run = SampleRun();
            change(run);
            return (await submitter.ValidateRunAsync(run)).ReasonForRejection;
        }

        Assert.Null(await Reason(_ => { }));
        Assert.Equal("This run already exists on speedrun.com.", await Reason(r => r.Metadata.RunId = "abc"));
        Assert.Equal("You need to specify a game.", await Reason(r => r.GameName = ""));
        Assert.Equal("The game could not be found on speedrun.com.", await Reason(r => r.GameName = "Super Mario"));
        Assert.Equal("You need to specify a category.", await Reason(r => r.CategoryName = ""));
        Assert.Equal("The category could not be found on speedrun.com.", await Reason(r => r.CategoryName = "120 Shines"));
        Assert.Equal("Submitting runs for more than the currently authenticated user is not implemented yet.",
            await Reason(r => r.CategoryName = "Co-op"));
        Assert.Equal("You need to specify the platform of the game.", await Reason(r => r.Metadata.PlatformName = "Wii"));
        Assert.Equal("You can't submit a run without a Real Time.",
            await Reason(r => r[^1].PersonalBestSplitTime = new SplitTime(null, TimeSpan.FromHours(1))));
        Assert.Equal("You need to specify a value for the variable \"Version\".",
            await Reason(r => r.Metadata.VariableValueNames["Version"] = "PAL"));
        // "Up to" players and a region that isn't listed are fine.
        Assert.Null(await Reason(r => r.CategoryName = "Up To"));
        Assert.Null(await Reason(r => r.Metadata.RegionName = ""));
    }

    [Fact]
    public async Task Without_a_working_key_the_authenticator_is_asked_and_a_good_key_is_kept()
    {
        (SpeedrunComSubmitter submitter, FakeSpeedrunCom fake, MemoryCredentialStore store) = Create(savedKey: null);
        Assert.Equal("You can't submit a run without being authenticated.", (await submitter.ValidateRunAsync(SampleRun())).ReasonForRejection);

        var answers = new Queue<string?>(["badkey", Key]);
        int asked = 0;
        submitter.Authenticator = () =>
        {
            asked++;
            return Task.FromResult(answers.Dequeue());
        };
        Assert.Equal("You can't submit a run without being authenticated.", (await submitter.ValidateRunAsync(SampleRun())).ReasonForRejection);
        Assert.Null(store.Read(SpeedrunComSubmitter.CredentialName));
        Assert.Null((await submitter.ValidateRunAsync(SampleRun())).ReasonForRejection);
        Assert.Equal(Key, store.Read(SpeedrunComSubmitter.CredentialName));
        Assert.Equal(2, asked);

        // The kept key works without asking again (also in a new session).
        Assert.Null((await submitter.ValidateRunAsync(SampleRun())).ReasonForRejection);
        Assert.Equal(2, asked);
        var next = new SpeedrunComSubmitter(fake, store);
        Assert.Null((await next.ValidateRunAsync(SampleRun())).ReasonForRejection);
    }

    [Fact]
    public async Task Submit_posts_the_personal_best_as_RunsClient_Submit_does()
    {
        (SpeedrunComSubmitter submitter, FakeSpeedrunCom fake, _) = Create();
        Run run = SampleRun();
        SrcSubmitResult result = await submitter.SubmitRunAsync(run, "gg", new Uri("https://youtu.be/abc"));

        Assert.True(result.Submitted, result.ReasonForRejection);
        Assert.Equal(new Uri("https://www.speedrun.com/sms/run/newrun"), result.WebLink);
        Assert.Equal("newrun", run.Metadata.RunId);
        (HttpMethod method, string path, string body, string? key) = fake.Requests.Single(r => r.Method == HttpMethod.Post);
        Assert.Equal("/api/v1/runs", path);
        Assert.Equal(Key, key);
        Assert.Equal(
            """{"run":{"category":"any","platform":"gcn","date":"2026-10-01","region":"pal","verified":false,"times":{"realtime":3723.456,"ingame":3700},"emulated":true,"video":"https://youtu.be/abc","comment":"gg","variables":{"ver":{"type":"pre-defined","value":"jp"},"note":{"type":"user-defined","value":"Low%"}}}}""",
            body);
    }

    [Fact]
    public async Task Submit_uses_the_games_timing_methods_and_the_without_loads_time()
    {
        (SpeedrunComSubmitter submitter, FakeSpeedrunCom fake, _) = Create();
        Run run = SampleRun("Loads Game");
        run.Metadata.PlatformName = "PC";
        run.Metadata.VariableValueNames.Clear();
        run.Metadata.VariableValueNames["Version"] = "USA";
        SrcSubmitResult result = await submitter.SubmitRunAsync(run, "", null, new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(59));

        Assert.True(result.Submitted, result.ReasonForRejection);
        string body = fake.Requests.Single(r => r.Method == HttpMethod.Post).Body;
        // No region, no emulator (not allowed), no comment or video; RTA and the given load-removed time.
        Assert.Equal(
            """{"run":{"category":"any","platform":"pc","date":"2026-10-02","verified":false,"times":{"realtime":3723.456,"realtime_noloads":3540},"emulated":false,"variables":{"ver":{"type":"pre-defined","value":"us"}}}}""",
            body);
    }

    [Fact]
    public async Task Submit_checks_the_video_rule_and_the_date()
    {
        (SpeedrunComSubmitter submitter, FakeSpeedrunCom fake, _) = Create();
        Assert.Equal("Runs of this game require a video.", (await submitter.SubmitRunAsync(SampleRun())).ReasonForRejection);
        Assert.Equal("The date of the run can't be in the future.",
            (await submitter.SubmitRunAsync(SampleRun(), videoUri: new Uri("https://youtu.be/a"), date: DateTime.UtcNow.AddDays(2))).ReasonForRejection);
        Assert.Equal("The date of the run can't be before the release date of the game.",
            (await submitter.SubmitRunAsync(SampleRun(), videoUri: new Uri("https://youtu.be/a"), date: new DateTime(2001, 12, 31, 0, 0, 0, DateTimeKind.Utc))).ReasonForRejection);
        Assert.DoesNotContain(fake.Requests, r => r.Method == HttpMethod.Post);

        // Moderators may leave the video out.
        (SpeedrunComSubmitter moderator, FakeSpeedrunCom modFake, _) = Create();
        modFake.ProfileId = "mod1";
        Assert.True((await moderator.SubmitRunAsync(SampleRun())).Submitted);
    }

    [Fact]
    public async Task Speedrun_coms_errors_are_the_reason()
    {
        (SpeedrunComSubmitter submitter, FakeSpeedrunCom fake, _) = Create();
        fake.SubmitAnswer = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"status":400,"message":"The submitted run is invalid.","errors":["video: required","date: bad"]}"""),
        };
        SrcSubmitResult result = await submitter.SubmitRunAsync(SampleRun(), videoUri: new Uri("https://youtu.be/a"));
        Assert.False(result.Submitted);
        Assert.Equal("video: required" + Environment.NewLine + "date: bad", result.ReasonForRejection);
        Assert.Equal("Too many runs.", SpeedrunComSubmitter.ApiErrors("""{"status":429,"message":"Too many runs."}"""));
    }

    [Fact]
    public void Personal_best_date_and_game_time_patch()
    {
        Run run = SampleRun();
        Assert.Equal(new DateTime(2026, 10, 1, 20, 2, 3, DateTimeKind.Utc), SpeedrunComSubmitter.FindPersonalBestAttemptDate(run));

        // A run with a game time keeps it.
        SpeedrunComSubmitter.PatchGameTime(run, TimeSpan.FromMinutes(1));
        Assert.Equal(TimeSpan.Parse("01:01:40"), run[^1].PersonalBestSplitTime.GameTime);

        Run realTimeOnly = SampleRun();
        realTimeOnly[^1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromHours(1), null);
        realTimeOnly.AttemptHistory[1].Time = new SplitTime(TimeSpan.FromHours(1), null);
        SpeedrunComSubmitter.PatchGameTime(realTimeOnly, TimeSpan.FromMinutes(58));
        Assert.Equal(TimeSpan.FromMinutes(58), realTimeOnly[^1].PersonalBestSplitTime.GameTime);
        Assert.Equal(TimeSpan.FromMinutes(58), realTimeOnly.AttemptHistory[1].Time.GameTime);
        Assert.Null(realTimeOnly.AttemptHistory[0].Time.GameTime);
        // The attempt still matches, so its date still counts.
        Assert.NotNull(SpeedrunComSubmitter.FindPersonalBestAttemptDate(realTimeOnly));
    }

    [Fact]
    public void Excel_has_upstreams_six_sheets_with_times_as_days()
    {
        Run run = SampleRun();
        run[0].SegmentHistory[2] = new SplitTime(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(4));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(4));
        using var stream = new MemoryStream();
        ExcelRunSaver.Save(run, stream);
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument Part(string name)
        {
            using Stream s = zip.GetEntry(name)!.Open();
            return XDocument.Load(s);
        }

        Assert.Equal(
            new[]
            {
                "Splits (Real Time)", "Attempt History (Real Time)", "Segment History (Real Time)",
                "Splits (Game Time)", "Attempt History (Game Time)", "Segment History (Game Time)",
            },
            Part("xl/workbook.xml").Descendants(main + "sheet").Select(s => (string)s.Attribute("name")!));
        Part("[Content_Types].xml");
        Part("xl/styles.xml");

        List<XElement> splits = [.. Part("xl/worksheets/sheet1.xml").Descendants(main + "c")];
        string Text(string cell) => splits.Single(c => (string)c.Attribute("r")! == cell).Value;
        Assert.Equal("Segment", Text("A1"));
        Assert.Equal("Personal Best", Text("B1"));
        Assert.Equal("Airstrip", Text("A2"));
        Assert.Equal(TimeSpan.FromMinutes(5).TotalDays, double.Parse(Text("B2"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.DoesNotContain(splits, c => c.Value == "None");

        List<XElement> attempts = [.. Part("xl/worksheets/sheet2.xml").Descendants(main + "c")];
        Assert.Equal(new[] { "Attempt ID", "Started", "Ended", "Time" }, attempts.Take(4).Select(c => c.Value));
        XElement ended = attempts.Single(c => (string)c.Attribute("r")! == "C3");
        Assert.Equal(new DateTime(2026, 10, 1, 20, 2, 3).ToOADate(), double.Parse(ended.Value, System.Globalization.CultureInfo.InvariantCulture), 6);

        List<XElement> history = [.. Part("xl/worksheets/sheet3.xml").Descendants(main + "c")];
        Assert.Equal(new[] { "Attempt ID", "Airstrip", "Corona" }, history.Take(3).Select(c => c.Value));
    }
}
