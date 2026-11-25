using System.Net;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Web;
using SkiaSharp;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// The Splits Editor's speedrun.com tools (MetadataControl: Additional Info, Associate, Submit
/// Run...), its game icon menu and Import Comparison From URL..., against a fake speedrun.com.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class SplitsEditorSpeedrunComAppTests
{
    private const string Key = "goodkey";

    private static readonly byte[] Png = MakePng();

    private static byte[] MakePng()
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Orange);
        return SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private sealed class FakeSpeedrunCom : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public List<string> Posts { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            Requests.Add(request.Method + " " + request.RequestUri.PathAndQuery);
            if (request.Method == HttpMethod.Post)
            {
                Posts.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
                return Json("""{"data":{"id":"new1","weblink":"https://www.speedrun.com/sms/runs/new1"}}""");
            }

            string? key = request.Headers.TryGetValues("X-API-Key", out IEnumerable<string>? keys) ? keys.Single() : null;
            return path switch
            {
                "/api/v1/profile" => key == Key
                    ? Json("""{"data":{"id":"me","names":{"international":"Basten"}}}""")
                    : new HttpResponseMessage(HttpStatusCode.Forbidden),
                "/api/v1/games" => Json("""
                    {"data":[{"id":"sms","names":{"international":"Super Mario Sunshine"},"released":2002,
                     "ruleset":{"default-time":"ingame","run-times":["realtime","ingame"],"require-video":false,"emulators-allowed":true},
                     "moderators":{},
                     "assets":{"icon":{"uri":"https://www.speedrun.com/static/game/sms/icon.png"},"cover-medium":{"uri":"https://www.speedrun.com/static/game/sms/cover.png"}},
                     "platforms":{"data":[{"id":"gcn","name":"GameCube"},{"id":"wii","name":"Wii"}]},
                     "regions":{"data":[{"id":"jpn","name":"JPN / NTSC"},{"id":"usa","name":"USA / NTSC"}]}}]}
                    """),
                "/api/v1/games/sms/categories" => Json("""
                    {"data":[{"id":"any","name":"Any%","type":"per-game","players":{"type":"exactly","value":1},
                     "rules":"[b]Timing[/b] ends on Bowser. See https://www.speedrun.com/sms/guides"},
                     {"id":"120","name":"120 Shines","type":"per-game","players":{"type":"exactly","value":1}}]}
                    """),
                "/api/v1/games/sms/variables" => Json("""
                    {"data":[
                     {"id":"ver","name":"Version","category":null,"scope":{"type":"full-game"},"mandatory":true,"user-defined":false,
                      "values":{"values":{"jp":{"label":"JPN"},"us":{"label":"USA"}}}},
                     {"id":"seed","name":"Seed","category":"any","scope":{"type":"global"},"mandatory":false,"user-defined":true,
                      "values":{"values":{}}}]}
                    """),
                "/api/v1/runs/abc123" => Json("""
                    {"data":{"id":"abc123","weblink":"https://www.speedrun.com/sms/runs/abc123",
                     "game":{"data":{"id":"sms","names":{"international":"Super Mario Sunshine"}}},
                     "category":{"data":{"id":"120","name":"120 Shines"}},
                     "platform":{"data":{"id":"wii","name":"Wii"}},
                     "region":{"data":[]},
                     "system":{"platform":"wii","emulated":true,"region":null},
                     "values":{"ver":"us"}}}
                    """),
                "/static/game/sms/icon.png" or "/static/game/sms/cover.png" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Png) },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") },
            };
        }

        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json) };
    }

    private static (FakeSpeedrunCom Fake, List<string> Opened) UseFake(string? key = Key)
    {
        var fake = new FakeSpeedrunCom();
        var store = new MemoryCredentialStore();
        store.Write(SpeedrunComSubmitter.CredentialName, key);
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
        DialogChrome.PromptFieldsAnswersForTests = null;
    }

    private static Run SampleRun()
    {
        var run = new Run { GameName = "Super Mario Sunshine", CategoryName = "Any%" };
        run.Metadata.RunId = "old";
        run.Metadata.PlatformName = "GameCube";
        run.Metadata.RegionName = "EUR / PAL";
        run.Metadata.VariableValueNames["Version"] = "JPN";
        run.Metadata.VariableValueNames["Gone"] = "x";
        run.AddSegment("Airstrip");
        run.AddSegment("Corona");
        run[^1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromHours(1), null);
        return run;
    }

    private static async Task<SplitsEditorWindow> OpenAdditionalInfo(Run run)
    {
        var editor = new SplitsEditorWindow(run, TimingMethod.RealTime) { MessagesForTests = [] };
        editor.Show();
        editor.FindControl<TabControl>("EditorTabs")!.SelectedIndex = 2;
        await editor.MetadataRefreshed;
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    [AvaloniaFact]
    public async Task Additional_Info_Shows_The_Games_Options_And_Drops_What_It_Does_Not_Have()
    {
        UseFake();
        try
        {
            SplitsEditorWindow editor = await OpenAdditionalInfo(SampleRun());
            var platform = editor.FindControl<ComboBox>("PlatformBox")!;
            var region = editor.FindControl<ComboBox>("RegionBox")!;
            Assert.Equal(new[] { "", "GameCube", "Wii" }, platform.ItemsSource!.Cast<string>());
            Assert.Equal("GameCube", platform.SelectedItem);
            // EUR / PAL isn't one of the game's regions: cleared, so the run no longer matches its ID.
            Assert.Equal("", editor.RegionNameForTests);
            Assert.Null(editor.RunIdForTests);
            Assert.True(region.IsEnabled);
            Assert.True(editor.FindControl<CheckBox>("EmulatorBox")!.IsVisible);
            Assert.Equal(new[] { "Version:", "Seed:" }, editor.VariableFieldsForTests.Select(f => f.Label));
            KeyValuePair<string, string> kept = Assert.Single(editor.VariablesForTests);
            Assert.Equal(("Version", "JPN"), (kept.Key, kept.Value));
            string rules = string.Concat(editor.FindControl<SelectableTextBlock>("RulesText")!.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text));
            Assert.StartsWith("Runs of this game are timed with the Game Time.", rules);
            Assert.Contains("Timing ends on Bowser. See https://www.speedrun.com/sms/guides", rules);

            // A variable and the platform go into the run.
            var version = (ComboBox)editor.VariableFieldsForTests[0].Field!;
            version.SelectedItem = "USA";
            platform.SelectedItem = "Wii";
            Run built = editor.BuildRunForTests();
            Assert.Equal("Wii", built.Metadata.PlatformName);
            Assert.Equal("USA", built.Metadata.VariableValueNames["Version"]);
            Assert.False(built.Metadata.VariableValueNames.ContainsKey("Gone"));
            editor.Close();
        }
        finally
        {
            Offline();
        }
    }

    [AvaloniaFact]
    public async Task Offline_Keeps_Everything_And_Shows_It_Unchangeable()
    {
        SplitsEditorWindow editor = await OpenAdditionalInfo(SampleRun());
        Assert.True(editor.LoadedMetadata!.LookupFailed);
        var platform = editor.FindControl<ComboBox>("PlatformBox")!;
        Assert.Equal("GameCube", platform.SelectedItem);
        Assert.False(platform.IsEnabled);
        Assert.Equal("EUR / PAL", editor.RegionNameForTests);
        Assert.Equal("old", editor.RunIdForTests);
        Assert.Equal("Show on Speedrun.com...", editor.FindControl<Button>("AssociateButton")!.Content);
        Assert.False(editor.FindControl<Button>("SubmitRunButton")!.IsEnabled);
        Run built = editor.BuildRunForTests();
        Assert.Equal("old", built.Metadata.RunId);
        Assert.Equal("x", built.Metadata.VariableValueNames["Gone"]);
        // Another game typed: the ID goes.
        editor.FindControl<AutoCompleteBox>("GameNameBox")!.Text = "Super Mario Galaxy";
        Assert.Null(editor.BuildRunForTests().Metadata.RunId);
        editor.Close();
    }

    [AvaloniaFact]
    public async Task Associate_Takes_The_Runs_Details_From_Its_Page()
    {
        (_, List<string> opened) = UseFake();
        try
        {
            var run = SampleRun();
            run.Metadata.RunId = null;
            SplitsEditorWindow editor = await OpenAdditionalInfo(run);
            DialogChrome.PromptAnswersForTests = new Queue<string?>(["not a url!", "https://www.speedrun.com/sms/runs/nope", "https://www.speedrun.com/sms/runs/abc123"]);
            await editor.AssociateRunAsync();
            Assert.Equal(("Invalid URL", "The URL provided is not a valid speedrun.com Run URL."), editor.MessagesForTests![^1]);
            await editor.AssociateRunAsync();
            Assert.Equal(("Error", "The run could not be associated."), editor.MessagesForTests[^1]);
            await editor.AssociateRunAsync();
            await editor.MetadataRefreshed;
            Dispatcher.UIThread.RunJobs();

            Run built = editor.BuildRunForTests();
            Assert.Equal("120 Shines", built.CategoryName);
            Assert.Equal("Wii", built.Metadata.PlatformName);
            Assert.Equal("", built.Metadata.RegionName);
            Assert.True(built.Metadata.UsesEmulator);
            KeyValuePair<string, string> variable = Assert.Single(built.Metadata.VariableValueNames);
            Assert.Equal(("Version", "USA"), (variable.Key, variable.Value));
            Assert.Equal("abc123", built.Metadata.RunId);
            var associate = editor.FindControl<Button>("AssociateButton")!;
            Assert.Equal("Show on Speedrun.com...", associate.Content);
            // 120 Shines has no Seed variable.
            Assert.Equal(new[] { "Version:" }, editor.VariableFieldsForTests.Select(f => f.Label));

            associate.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            for (int i = 0; i < 100 && opened.Count == 0; i++)
            {
                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Equal(["https://www.speedrun.com/sms/runs/abc123"], opened);
            editor.Close();
        }
        finally
        {
            Offline();
        }
    }

    [AvaloniaFact]
    public async Task Submit_Run_Submits_The_Edited_Run_And_Keeps_Its_New_Id()
    {
        (FakeSpeedrunCom fake, _) = UseFake();
        try
        {
            var run = SampleRun();
            run.Metadata.RunId = null;
            run.Metadata.RegionName = "";
            SplitsEditorWindow editor = await OpenAdditionalInfo(run);
            editor.SubmitWindowForTests = async submit =>
            {
                submit.MessagesForTests = [];
                submit.Show();
                Assert.Contains("Game Time:", submit.Fields);
                submit.GameTimeBox!.Text = "58:00";
                submit.DateBox!.SelectedDate = new DateTime(2026, 10, 3);
                return await submit.SubmitAsync();
            };
            await editor.SubmitRunAsync();

            Assert.Contains("\"times\":{\"realtime\":3600,\"ingame\":3480}", Assert.Single(fake.Posts));
            Run built = editor.BuildRunForTests();
            Assert.Equal("new1", built.Metadata.RunId);
            Assert.Equal(TimeSpan.FromMinutes(58), built[^1].PersonalBestSplitTime.GameTime);
            Assert.False(editor.FindControl<Button>("SubmitRunButton")!.IsEnabled);
            editor.Close();
        }
        finally
        {
            Offline();
        }
    }

    [AvaloniaFact]
    public async Task Game_Icon_Menu_Downloads_Opens_And_Removes_The_Icon()
    {
        UseFake();
        Func<Uri, CancellationToken, Task<byte[]>> download = MainWindow.DownloadFromUrl;
        try
        {
            var editor = new SplitsEditorWindow(SampleRun(), TimingMethod.RealTime) { MessagesForTests = [] };
            Assert.Equal(
                ["Set Icon...", "Download Box Art", "Download Icon", "Open from URL...", "Remove Icon"],
                editor.GameIconMenuModel().Items.OfType<NativeMenuAction>().Select(a => a.Header));
            Assert.False(editor.GameIconMenuModel().Items.OfType<NativeMenuAction>().Last().IsEnabled);

            await editor.DownloadIconAsync();
            Assert.NotNull(editor.GameIconXmlForTests);
            Assert.True(editor.GameIconMenuModel().Items.OfType<NativeMenuAction>().Last().IsEnabled);
            editor.GameIconMenuModel().Items.OfType<NativeMenuAction>().Last().Invoke();
            Assert.Null(editor.GameIconXmlForTests);
            await editor.DownloadBoxArtAsync();
            Assert.NotNull(editor.GameIconXmlForTests);
            Assert.Empty(editor.MessagesForTests!);

            MainWindow.DownloadFromUrl = (uri, _) => uri.Host == "img.example"
                ? Task.FromResult(Png)
                : uri.Host == "text.example" ? Task.FromResult("<html/>"u8.ToArray()) : throw new HttpRequestException("404");
            DialogChrome.PromptAnswersForTests = new Queue<string?>(["https://text.example/a", "https://gone.example/a", "https://img.example/a.png"]);
            await editor.OpenGameIconFromUrlAsync();
            Assert.Equal(("Error", "The URL was not recognized as an image."), editor.MessagesForTests![^1]);
            await editor.OpenGameIconFromUrlAsync();
            Assert.Equal(("Error", "The Game Icon couldn't be downloaded."), editor.MessagesForTests[^1]);
            editor.GameIconMenuModel().Items.OfType<NativeMenuAction>().Last().Invoke();
            await editor.OpenGameIconFromUrlAsync();
            Assert.NotNull(editor.GameIconXmlForTests);

            // Not on speedrun.com: Windows' error.
            editor.FindControl<AutoCompleteBox>("GameNameBox")!.Text = "Unknown Game";
            await editor.DownloadIconAsync();
            Assert.Equal(("Error", "Could not download the icon of the game!"), editor.MessagesForTests[^1]);
        }
        finally
        {
            MainWindow.DownloadFromUrl = download;
            Offline();
        }
    }

    [AvaloniaFact]
    public async Task Import_Comparison_From_Url_Adds_The_Downloaded_Runs_Comparison()
    {
        var web = new Run { GameName = "Super Mario Sunshine", CategoryName = "Any%" };
        web.AddSegment("Airstrip");
        web.AddSegment("Corona");
        web[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromMinutes(4));
        web[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromMinutes(59));
        using var lss = new MemoryStream();
        XmlRunSerializer.Save(web, lss);
        Func<Uri, CancellationToken, Task<byte[]>> download = MainWindow.DownloadFromUrl;
        MainWindow.DownloadFromUrl = (uri, _) => uri.AbsolutePath.EndsWith(".lss", StringComparison.Ordinal)
            ? Task.FromResult(lss.ToArray())
            : uri.Host == "bad.example" ? Task.FromResult("<html/>"u8.ToArray()) : throw new HttpRequestException("404");
        try
        {
            var editor = new SplitsEditorWindow(SampleRun(), TimingMethod.RealTime) { MessagesForTests = [] };
            Assert.Equal(
                ["From File...", "From URL..."],
                editor.ImportComparisonMenuModel().Items.OfType<NativeMenuAction>().Select(a => a.Header));
            DialogChrome.PromptFieldsAnswersForTests = new Queue<string[]?>(
            [
                ["Web", "https://gone.example/x"],
                ["Web", "https://bad.example/x"],
                ["Web", "https://example.com/web.lss"],
            ]);
            await editor.ImportComparisonFromUrlAsync();
            Assert.Equal(("Error", "The splits file couldn't be downloaded."), editor.MessagesForTests![^1]);
            await editor.ImportComparisonFromUrlAsync();
            Assert.Equal(("Error", "The selected file was not recognized as a splits file."), editor.MessagesForTests[^1]);
            await editor.ImportComparisonFromUrlAsync();
            Run built = editor.BuildRunForTests();
            Assert.Contains("Web", built.CustomComparisons);
            Assert.Equal(TimeSpan.FromMinutes(59), built[^1].Comparisons["Web"].RealTime);
        }
        finally
        {
            MainWindow.DownloadFromUrl = download;
            DialogChrome.PromptFieldsAnswersForTests = null;
        }
    }
}
