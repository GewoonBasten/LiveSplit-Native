using System.Net;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.Web;
using SkiaSharp;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Share... (ShareRunDialog with Speedrun.com, Screenshot and Excel; SpeedrunComSubmitDialog).</summary>
[Collection(MainWindowCollection.Name)]
public class ShareAppTests
{
    private const string Key = "goodkey";

    /// <summary>speedrun.com: one game (real and game time, no video needed), the key, POST /runs.</summary>
    private sealed class FakeSpeedrunCom : HttpMessageHandler
    {
        public List<string> Posts { get; } = [];

        public string RunTimes { get; set; } = "[\"realtime\",\"ingame\"]";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            string? key = request.Headers.TryGetValues("X-API-Key", out IEnumerable<string>? keys) ? keys.Single() : null;
            if (request.Method == HttpMethod.Post)
            {
                Posts.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return Json("""{"data":{"id":"r1","weblink":"https://www.speedrun.com/sms/run/r1"}}""");
            }

            return path.EndsWith("/profile", StringComparison.Ordinal)
                ? key == Key ? Json("""{"data":{"id":"me","names":{"international":"Basten"}}}""") : new HttpResponseMessage(HttpStatusCode.Forbidden)
                : path.EndsWith("/games", StringComparison.Ordinal)
                    ? Json("{\"data\":[{\"id\":\"sms\",\"names\":{\"international\":\"Super Mario Sunshine\"},\"released\":2002,"
                        + "\"ruleset\":{\"default-time\":\"realtime\",\"run-times\":" + RunTimes + ",\"require-video\":false,\"emulators-allowed\":false},"
                        + "\"moderators\":{},\"platforms\":{\"data\":[{\"id\":\"gcn\",\"name\":\"GameCube\"}]},\"regions\":{\"data\":[]}}]}")
                : path.EndsWith("/categories", StringComparison.Ordinal)
                    ? Json("""{"data":[{"id":"any","name":"Any%","type":"per-game","players":{"type":"exactly","value":1}}]}""")
                : path.EndsWith("/variables", StringComparison.Ordinal)
                    ? Json("""{"data":[]}""")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    }

    private static Run SampleRun(bool personalBest = true, bool gameTime = true)
    {
        var run = new Run { GameName = "Super Mario Sunshine", CategoryName = "Any%" };
        run.Metadata.PlatformName = "GameCube";
        run.AddSegment(new Segment("Airstrip"));
        run.AddSegment(new Segment("Corona"));
        if (personalBest)
        {
            run[^1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromHours(1), gameTime ? TimeSpan.FromMinutes(58) : null);
        }

        return run;
    }

    private static List<string> Platforms(LiveSplitState state) =>
        [.. new ShareRunWindow(state, () => Task.FromResult<byte[]?>(null), Platform.NativeFileDialogs.Current).Platforms];

    private static (FakeSpeedrunCom Fake, List<string> Opened) UseFakeSpeedrunCom(string? savedKey = Key)
    {
        var fake = new FakeSpeedrunCom();
        var store = new MemoryCredentialStore();
        store.Write(SpeedrunComSubmitter.CredentialName, savedKey);
        var opened = new List<string>();
        SpeedrunComSharing.Handler = fake;
        SpeedrunComSharing.Credentials = store;
        SpeedrunComSharing.OpenBrowser = opened.Add;
        SpeedrunComSharing.Reset();
        return (fake, opened);
    }

    private static void Offline()
    {
        SpeedrunComSharing.Handler = new TestEnvironment.OfflineTheRun();
        SpeedrunComSharing.Credentials = new MemoryCredentialStore();
        SpeedrunComSharing.OpenBrowser = _ => { };
        SpeedrunComSharing.Reset();
        DialogChrome.PromptAnswersForTests = null;
    }

    [AvaloniaFact]
    public void Speedrun_com_is_offered_for_a_personal_best_when_the_timer_is_not_going()
    {
        Assert.Equal(new[] { "Speedrun.com", "Screenshot", "Excel" }, Platforms(new LiveSplitState(SampleRun())));
        Assert.Equal(new[] { "Screenshot", "Excel" }, Platforms(new LiveSplitState(SampleRun(personalBest: false))));

        var running = new LiveSplitState(SampleRun());
        new TimerModel { CurrentState = running }.Start();
        Assert.Equal(new[] { "Screenshot", "Excel" }, Platforms(running));

        // A finished attempt becomes the personal best in the sharer's copy only.
        var ended = new LiveSplitState(SampleRun(personalBest: false));
        var model = new TimerModel { CurrentState = ended };
        model.Start();
        model.Split();
        model.Split();
        Assert.Equal(TimerPhase.Ended, ended.CurrentPhase);
        LiveSplitState copy = ended.Clone();
        var window = new ShareRunWindow(copy, () => Task.FromResult<byte[]?>(null), Platform.NativeFileDialogs.Current);
        Assert.Equal(new[] { "Speedrun.com", "Screenshot", "Excel" }, window.Platforms);
        Assert.Equal(TimerPhase.NotRunning, copy.CurrentPhase);
        Assert.NotNull(copy.Run[^1].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimerPhase.Ended, ended.CurrentPhase);
        Assert.Null(ended.Run[^1].PersonalBestSplitTime.RealTime);

        Assert.Equal("Speedrun.com is a site intended to provide centralized leaderboards for speedrunning.", window.Description);
        window.SelectedPlatform = "Screenshot";
        Assert.Equal("Sharing will save a screenshot of LiveSplit.", window.Description);
    }

    [AvaloniaFact]
    public async Task Excel_and_Screenshot_save_files()
    {
        string dir = HeadlessUi.NewTempDir();
        using var bitmap = new SKBitmap(4, 3);
        bitmap.Erase(SKColors.Transparent);
        byte[] png = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100).ToArray();
        var window = new ShareRunWindow(new LiveSplitState(SampleRun()), () => Task.FromResult<byte[]?>(png), Platform.NativeFileDialogs.Current)
        {
            MessagesForTests = [],
        };
        string? answer = Path.Combine(dir, "splits.xlsx");
        window.SavePathForTests = _ => answer;

        window.SelectedPlatform = "Excel";
        Assert.True(await window.ShareAsync());
        Assert.True(File.Exists(answer));
        Assert.Equal(("Run Shared", "Your run was successfully shared to Excel."), window.MessagesForTests![^1]);

        window.SelectedPlatform = "Screenshot";
        answer = Path.Combine(dir, "shot.jpeg");
        Assert.True(await window.ShareAsync());
        byte[] jpeg = await File.ReadAllBytesAsync(answer);
        Assert.Equal(new byte[] { 0xFF, 0xD8 }, jpeg[..2]);
        answer = Path.Combine(dir, "shot.png");
        Assert.True(await window.ShareAsync());
        using SKBitmap saved = SKBitmap.Decode(answer);
        Assert.Equal((4, 3), (saved.Width, saved.Height));
        // Upstream draws the timer on black.
        Assert.Equal(SKColors.Black, saved.GetPixel(0, 0));

        // Cancelling the save panel is "could not be shared", as upstream says.
        answer = null;
        Assert.False(await window.ShareAsync());
        Assert.Equal(("Error", "The run could not be shared."), window.MessagesForTests![^1]);
    }

    [AvaloniaFact]
    public async Task Speedrun_com_validates_then_submits_and_opens_the_run()
    {
        (FakeSpeedrunCom fake, List<string> opened) = UseFakeSpeedrunCom();
        try
        {
            Run run = SampleRun();
            run.Metadata.PlatformName = "Wii";
            var window = new ShareRunWindow(new LiveSplitState(run), () => Task.FromResult<byte[]?>(null), Platform.NativeFileDialogs.Current)
            {
                MessagesForTests = [],
            };
            Assert.False(await window.ShareAsync());
            Assert.Equal(
                new[] { ("Submitting Failed", "You need to specify the platform of the game."), ("Error", "The run could not be shared.") },
                window.MessagesForTests);

            window = new ShareRunWindow(new LiveSplitState(SampleRun()), () => Task.FromResult<byte[]?>(null), Platform.NativeFileDialogs.Current)
            {
                MessagesForTests = [],
            };
            SpeedrunComSubmitWindow? shown = null;
            window.SubmitWindowForTests = async submit =>
            {
                shown = submit;
                submit.MessagesForTests = [];
                submit.Show();
                // No PB attempt with a date: the date is asked; the run has a game time, so no time fields.
                Assert.Equal(new[] { "Video:", "Comment:", "Date:" }, submit.Fields);
                submit.VideoBox.Text = "not a url";
                Assert.False(await submit.SubmitAsync());
                Assert.Equal(("Submitting Failed", "You didn't provide a valid Video URL."), submit.MessagesForTests![^1]);
                submit.VideoBox.Text = "youtu.be/abc";
                submit.CommentBox.Text = "gg";
                submit.DateBox!.SelectedDate = new DateTime(2026, 10, 3);
                return await submit.SubmitAsync();
            };
            Assert.True(await window.ShareAsync());
            Assert.Equal(("Run Shared", "Your run was successfully shared to Speedrun.com."), window.MessagesForTests![^1]);
            Assert.Equal(new[] { "https://www.speedrun.com/sms/run/r1" }, opened);
            Assert.Equal(
                """{"run":{"category":"any","platform":"gcn","date":"2026-10-03","verified":false,"times":{"realtime":3600,"ingame":3480},"emulated":false,"video":"http://youtu.be/abc","comment":"gg"}}""",
                Assert.Single(fake.Posts));
            Assert.Equal("r1", window.Run.Metadata.RunId);
            Assert.False(shown!.IsVisible);
        }
        finally
        {
            Offline();
        }
    }

    [AvaloniaFact]
    public async Task Speedrun_com_asks_for_missing_times_and_for_the_api_key()
    {
        (FakeSpeedrunCom fake, List<string> opened) = UseFakeSpeedrunCom(savedKey: null);
        fake.RunTimes = "[\"realtime\",\"realtime_noloads\",\"ingame\"]";
        try
        {
            DialogChrome.PromptAnswersForTests = new Queue<string?>([Key]);
            var window = new ShareRunWindow(new LiveSplitState(SampleRun(gameTime: false)), () => Task.FromResult<byte[]?>(null), Platform.NativeFileDialogs.Current)
            {
                MessagesForTests = [],
            };
            window.SubmitWindowForTests = async submit =>
            {
                submit.MessagesForTests = [];
                submit.Show();
                Assert.Equal(new[] { "Video:", "Comment:", "Date:", "Without Loads:", "Game Time:" }, submit.Fields);
                submit.GameTimeBox!.Text = "abc";
                Assert.False(await submit.SubmitAsync());
                Assert.Equal(("Submitting Failed", "You didn't enter a valid Game Time."), submit.MessagesForTests![^1]);
                submit.GameTimeBox.Text = "57:00";
                submit.WithoutLoadsBox!.Text = "58:30";
                submit.DateBox!.SelectedDate = new DateTime(2026, 10, 3);
                return await submit.SubmitAsync();
            };
            Assert.True(await window.ShareAsync());
            // The key page was opened and the key kept.
            Assert.Equal("https://www.speedrun.com/settings/api", opened[0]);
            Assert.Equal(Key, SpeedrunComSharing.Credentials.Read(SpeedrunComSubmitter.CredentialName));
            // The game time was patched first, so the PB's game time is the one entered.
            Assert.Contains("\"times\":{\"realtime\":3600,\"realtime_noloads\":3510,\"ingame\":3420}", Assert.Single(fake.Posts));
        }
        finally
        {
            Offline();
        }
    }

    [AvaloniaFact]
    public async Task Share_menu_opens_the_run_sharer_on_a_copy()
    {
        AppCli.Parse(["--settings", Path.Combine(HeadlessUi.NewTempDir(), "settings.cfg")]);
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            ShareRunWindow? sharer = null;
            Task share = window.ShareAsync(dialog => sharer = dialog);
            HeadlessUi.Pump();
            Assert.NotNull(sharer);
            Assert.True(sharer!.IsVisible);
            Assert.Equal("Run Sharer", sharer.Title);
            Assert.NotSame(window.Session.State.Run, sharer.Run);
            sharer.Close();
            await share;
        }
        finally
        {
            window.Close();
        }
    }
}
