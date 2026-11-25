using System.Net;
using System.Text.Json;
using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.Core.Tests;

/// <summary>LiveSplit.TheRun 0.4.5 (CollectorComponent, CollectorSettings) against a fake therun.gg.</summary>
[Collection(TheRunKeyFolder.Name)]
public sealed class TheRunTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef0123"; // 36 characters
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "therun-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string _savedFolder = TheRunUploadKey.Folder;

    public TheRunTests() => TheRunUploadKey.Folder = _folder;

    public void Dispose()
    {
        TheRunUploadKey.Folder = _savedFolder;
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>Answers like therun.gg and records every request.</summary>
    private sealed class FakeTheRun : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Url, string Body, HttpRequestMessage Request)> Requests { get; } = [];

        public string UploadUrl { get; set; } =
            "https://splits-bucket-main.s3.eu-west-1.amazonaws.com/Basten/Super Mario Sunshine-Any%25.lss?X-Amz-Algorithm=AWS4&X-Amz-Credential=ABC/20261017/eu-west-1/s3/aws4_request&X-Amz-Signature=f00";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add((request.Method, request.RequestUri!.OriginalString, body, request));
            }

            string url = request.RequestUri!.OriginalString;
            if (request.Method == HttpMethod.Get && url.StartsWith(TheRunClient.FileUploadBaseUrl, StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { url = UploadUrl })) };
            }

            if (url.StartsWith(TheRunClient.ValidateBaseUrl, StringComparison.Ordinal))
            {
                return url.EndsWith(Key, StringComparison.Ordinal)
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"result\":{\"data\":{\"username\":\"Basten\",\"picture\":\"https://example.org/p.png\"}}}"),
                    }
                    : new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        }
    }

    [Fact]
    public void Settings_Default_On_Except_Layout_Path_And_Never_Hold_The_Key()
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(new TheRunComponentSettings().ToInnerXml());
        Assert.Equal(
            ["Version", "IsStatsUploadingEnabled", "IsUploadOnResetEnabled", "IsLiveTrackingEnabled", "IsLayoutPathUploadEnabled"],
            root.ChildNodes.OfType<XmlElement>().Select(e => e.Name));
        Assert.Equal("1.0.0", root["Version"]!.InnerText);

        TheRunComponentSettings missing = TheRunComponentSettings.FromInnerXml("<Version>1.0.0</Version>");
        Assert.True(missing.IsStatsUploadingEnabled && missing.IsUploadOnResetEnabled && missing.IsLiveTrackingEnabled);
        Assert.False(missing.IsLayoutPathUploadEnabled);

        // An old layout's key moves to the key file and is not written back.
        TheRunComponentSettings old = TheRunComponentSettings.FromInnerXml($"<Version>0.1</Version><Path>{Key}</Path>");
        Assert.Equal("", TheRunUploadKey.Read());
        Assert.Equal(Key, TheRunUploadKey.Resolve(old));
        Assert.Equal(Key, File.ReadAllText(TheRunUploadKey.FilePath));
        Assert.Equal(Path.Combine(_folder, "Livesplit.TheRun", "uploadkey.txt"), TheRunUploadKey.FilePath);
        Assert.DoesNotContain(Key, old.ToInnerXml());
        Assert.Equal(Key, TheRunUploadKey.Resolve(TheRunComponentSettings.FromInnerXml(null)));
    }

    /// <summary>A finished run: 1:01 and 1:59 against a PB of 1:00 and 2:00, started 17 Oct 12:00 UTC.</summary>
    private static LiveSplitState FinishedRun()
    {
        var run = new Run { GameName = "Super Mario Sunshine", CategoryName = "Any%" };
        run.Metadata.PlatformName = "Wii";
        run.Metadata.RegionName = "EUR / PAL";
        run.Metadata.VariableValueNames["Game Version"] = "GCN";
        run.AddSegment("Bianco");
        run.AddSegment("Corona");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(60));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(120));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(55));
        run[0].SplitTime = new SplitTime(TimeSpan.FromSeconds(61));
        run[1].SplitTime = new SplitTime(TimeSpan.FromSeconds(119.5));
        run[0].IconXml = "<![CDATA[aWNvbg==]]>";
        run.LayoutPath = "/Users/basten/layout.lsl";
        var started = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        return new LiveSplitState(run)
        {
            CurrentPhase = TimerPhase.Ended,
            CurrentSplitIndex = 2,
            AttemptStarted = new AtomicDateTime(started, false),
            AttemptEnded = new AtomicDateTime(started.AddSeconds(125), false),
        };
    }

    [Fact]
    public void Live_Run_Is_Written_As_JavaScriptSerializer_Writes_It()
    {
        string json = TheRunLogic.LiveRunJson(FinishedRun(), "Super Mario Sunshine", "Any%", Key, false, TimeSpan.FromSeconds(1.5), true);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal(
            [
                "metadata", "currentTime", "currentSplitName", "currentSplitIndex", "timingMethod", "currentDuration",
                "startTime", "endTime", "uploadKey", "isPaused", "isGameTimePaused", "gameTimePauseTime", "totalPauseTime",
                "currentPauseTime", "timePausedAt", "wasJustResumed", "currentComparison", "runData",
            ],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Wii", root.GetProperty("metadata").GetProperty("platform").GetString());
        Assert.Equal("GCN", root.GetProperty("metadata").GetProperty("variables").GetProperty("Game Version").GetString());
        Assert.False(root.GetProperty("metadata").GetProperty("emulator").GetBoolean());
        Assert.Equal(119500, root.GetProperty("currentTime").GetDouble());
        Assert.Equal("", root.GetProperty("currentSplitName").GetString());
        Assert.Equal(2, root.GetProperty("currentSplitIndex").GetInt32());
        Assert.Equal(0, root.GetProperty("timingMethod").GetInt32());
        Assert.Equal(125000, root.GetProperty("currentDuration").GetDouble());
        Assert.Contains("\"startTime\":\"\\/Date(1792238400000)\\/\"", json);
        Assert.Contains("\"endTime\":\"\\/Date(1792238525000)\\/\"", json);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("totalPauseTime").ValueKind);
        JsonElement pause = root.GetProperty("currentPauseTime");
        Assert.Equal(
            ["Ticks", "Days", "Hours", "Milliseconds", "Minutes", "Seconds", "TotalDays", "TotalHours", "TotalMilliseconds", "TotalMinutes", "TotalSeconds"],
            pause.EnumerateObject().Select(p => p.Name));
        Assert.Equal(15000000, pause.GetProperty("Ticks").GetInt64());
        Assert.Equal(500, pause.GetProperty("Milliseconds").GetInt32());
        Assert.Equal(1500, pause.GetProperty("TotalMilliseconds").GetDouble());
        Assert.True(root.GetProperty("wasJustResumed").GetBoolean());
        Assert.Equal("Personal Best", root.GetProperty("currentComparison").GetString());

        JsonElement first = root.GetProperty("runData")[0];
        Assert.Equal(["name", "splitTime", "pbSplitTime", "bestPossible", "comparisons"], first.EnumerateObject().Select(p => p.Name));
        Assert.Equal(61000, first.GetProperty("splitTime").GetDouble());
        Assert.Equal(55000, first.GetProperty("bestPossible").GetDouble());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("runData")[1].GetProperty("bestPossible").ValueKind);
        Assert.Contains(first.GetProperty("comparisons").EnumerateArray(), c => c.GetProperty("name").GetString() == "Personal Best" && c.GetProperty("time").GetDouble() == 60000);
    }

    [Fact]
    public void Splits_For_Upload_Lose_Icons_And_The_Layout_Path_Unless_Asked()
    {
        LiveSplitState state = FinishedRun();
        var document = new XmlDocument();
        document.LoadXml(TheRunLogic.SplitsForUpload(state.Run, keepLayoutPath: false));
        Assert.Equal("", document.DocumentElement!["LayoutPath"]!.InnerText);
        Assert.Equal("", ((XmlElement)document.GetElementsByTagName("Segment")[0]!)["Icon"]!.InnerText);
        Assert.Equal("Super Mario Sunshine", document.DocumentElement["GameName"]!.InnerText);

        document.LoadXml(TheRunLogic.SplitsForUpload(state.Run, keepLayoutPath: true));
        Assert.Equal("/Users/basten/layout.lsl", document.DocumentElement!["LayoutPath"]!.InnerText);
    }

    [Fact]
    public void Upload_Url_Is_Encoded_As_The_Bucket_Expects()
    {
        string url = TheRunClient.EncodeUrl(
            "https://splits-bucket-main.s3.eu-west-1.amazonaws.com/Bas ten/Super Mario Sunshine-Any%.lss?X-Amz-Algorithm=AWS4&X-Amz-Credential=ABC/2026/s3/aws4_request&X-Amz-SignedHeaders=host",
            "Super Mario Sunshine",
            "Any%");

        Assert.Equal(
            "https://splits-bucket-main.s3.eu-west-1.amazonaws.com/Bas+ten/Super+Mario+Sunshine-Any%25.lss?X-Amz-Algorithm=AWS4&X-Amz-Credential=ABC%2f2026%2fs3%2faws4_request&X-Amz-SignedHeaders=host",
            url);
    }

    [Fact]
    public async Task A_Finished_Run_Sends_The_Live_Run_Then_The_Splits()
    {
        var fake = new FakeTheRun();
        var collector = new TheRunCollector(new TheRunClient(fake));
        LiveSplitState state = FinishedRun();

        await collector.HandleSplitAsync(state, new TheRunComponentSettings(), Key);

        Assert.Equal(3, fake.Requests.Count);
        Assert.Equal(HttpMethod.Post, fake.Requests[0].Method);
        Assert.Equal(TheRunClient.SplitWebhookUrl, fake.Requests[0].Url);
        Assert.Contains("\"uploadKey\":\"" + Key + "\"", fake.Requests[0].Body);
        Assert.Equal("cross-site", fake.Requests[0].Request.Headers.GetValues("Sec-Fetch-Site").Single());
        Assert.Equal(HttpMethod.Get, fake.Requests[1].Method);
        Assert.Equal(
            TheRunClient.FileUploadBaseUrl + "?filename=Super+Mario+Sunshine-Any%25.lss&uploadKey=" + Key,
            fake.Requests[1].Url);
        Assert.Equal(HttpMethod.Put, fake.Requests[2].Method);
        Assert.StartsWith("https://splits-bucket-main.s3.eu-west-1.amazonaws.com/Basten/Super+Mario+Sunshine-Any%25.lss?", fake.Requests[2].Url);
        Assert.Contains("<GameName>Super Mario Sunshine</GameName>", fake.Requests[2].Body);
        Assert.Equal("attachment", fake.Requests[2].Request.Content!.Headers.ContentDisposition!.DispositionType);
        Assert.NotNull(collector.LastSyncTime);
        Assert.StartsWith("therun.gg (synced just now)", TheRunLogic.Name(collector.LastSyncTime, DateTime.Now));
    }

    [Fact]
    public async Task Nothing_Is_Sent_Without_A_Valid_Key_Or_With_Live_Tracking_Off()
    {
        var fake = new FakeTheRun();
        var collector = new TheRunCollector(new TheRunClient(fake));
        LiveSplitState state = FinishedRun();

        await collector.HandleSplitAsync(state, new TheRunComponentSettings(), "short");
        await collector.HandleSplitAsync(state, new TheRunComponentSettings { IsLiveTrackingEnabled = false }, Key);
        state.Run.GameName = "";
        await collector.HandleResetAsync(state, new TheRunComponentSettings(), Key);
        Assert.Empty(fake.Requests);

        // Reset with live tracking off still syncs the splits (Sync on Reset).
        state.Run.GameName = "Super Mario Sunshine";
        await collector.HandleResetAsync(state, new TheRunComponentSettings { IsLiveTrackingEnabled = false }, Key);
        Assert.Equal([HttpMethod.Get, HttpMethod.Put], fake.Requests.Select(r => r.Method));
    }

    [Fact]
    public void Closing_Uploads_Only_When_Sync_On_Reset_Is_Off()
    {
        var fake = new FakeTheRun();
        var collector = new TheRunCollector(new TheRunClient(fake));
        collector.Dispose(FinishedRun(), new TheRunComponentSettings(), Key);
        Assert.Empty(fake.Requests);

        collector.Dispose(FinishedRun(), new TheRunComponentSettings { IsUploadOnResetEnabled = false }, Key);
        Assert.Equal([HttpMethod.Get, HttpMethod.Put], fake.Requests.Select(r => r.Method));
        Assert.NotNull(collector.LastSyncTime);
    }

    [Fact]
    public async Task Key_Check_Finds_The_Account_Or_Says_Why_Not()
    {
        var client = new TheRunClient(new FakeTheRun());
        TheRunKeyCheck valid = await client.ValidateAsync(Key);
        Assert.Equal(new TheRunUser("Basten", "https://example.org/p.png"), valid.User);

        TheRunKeyCheck invalid = await client.ValidateAsync("not-a-key");
        Assert.Null(invalid.User);
        Assert.True(invalid.Reached);
    }

    [Theory]
    [InlineData(30, "just now")]
    [InlineData(5 * 60 + 10, "5m ago")]
    [InlineData(3 * 3600 + 5, "3h ago")]
    public void Sync_Time_Reads_As_Windows_Shows_It(int secondsAgo, string expected)
    {
        var now = new DateTime(2026, 10, 17, 21, 0, 0);
        Assert.Equal(expected, TheRunLogic.FormatTimeAgo(now.AddSeconds(-secondsAgo), now));
        Assert.Equal($"therun.gg (synced {expected})", TheRunLogic.Name(now.AddSeconds(-secondsAgo), now));
        Assert.Equal("therun.gg", TheRunLogic.Name(null, now));
    }
}

/// <summary>The tests that change <see cref="TheRunUploadKey.Folder"/> run one at a time.</summary>
[CollectionDefinition(Name)]
public sealed class TheRunKeyFolder
{
    public const string Name = "therun.gg upload key folder";
}
