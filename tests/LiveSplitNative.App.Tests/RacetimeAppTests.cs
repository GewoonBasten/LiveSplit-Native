using System.Net;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>racetime.gg: the "racetime.gg Races" menu and the race window.</summary>
[Collection(MainWindowCollection.Name)]
public class RacetimeAppTests
{
    private const string OpenRace =
        "{\"name\":\"sms/cool-race-1234\",\"status\":{\"value\":\"open\"},\"category\":{\"name\":\"Super Mario Sunshine\",\"slug\":\"sms\"},"
        + "\"goal\":{\"name\":\"Any%\",\"custom\":false},\"entrants_count\":2,\"entrants_count_finished\":0,\"entrants_count_inactive\":0,"
        + "\"started_at\":null,\"start_delay\":\"P0DT00H00M15S\",\"version\":1,\"entrants\":[]}";

    /// <summary>racetime.gg with one open race and a signed-in account (token "tok").</summary>
    private sealed class OnlineRacetime : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath switch
            {
                "/races/data" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"races\":[" + OpenRace + "]}") },
                "/o/userinfo" when request.Headers.Authorization?.Parameter == "tok" =>
                    new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"me1\",\"name\":\"Basten\",\"full_name\":\"Basten#1234\"}") },
                _ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") },
            });
    }

    private static MainWindow Open(string dir, bool racetimeEnabled = true)
    {
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var appSettings = new AppSettings();
        appSettings.Racetime.Enabled = racetimeEnabled;
        XmlAppSettingsSerializer.SaveFile(appSettings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Close(MainWindow window)
    {
        window.Session.State.Run.HasChanged = false;
        window.CurrentLayout.HasChanged = false;
        window.Close();
    }

    [AvaloniaFact]
    public async Task Races_Menu_Follows_Share_With_The_Races_And_New_Race()
    {
        string dir = HeadlessUi.NewTempDir();
        HttpMessageHandler saved = RacetimeController.Handler;
        try
        {
            RacetimeController.Handler = new OnlineRacetime();
            RacetimeController.ResetApi();
            await RacetimeController.Api.RefreshRacesAsync();
            MainWindow window = Open(dir);
            List<NativeMenuEntry> items = [.. window.ContextMenuModelForTests().Items];
            int share = items.FindIndex(i => i is NativeMenuAction { Header: "Share..." });
            NativeMenuAction races = Assert.IsType<NativeMenuAction>(items[share + 1]);
            Assert.Equal("racetime.gg Races", races.Header);
            Assert.IsType<NativeMenuSeparator>(items[share + 2]);
            Assert.Equal(
                ["Super Mario Sunshine - Any% (2 Entrants)", "-", "New Race..."],
                races.Children!.Select(c => c is NativeMenuAction a ? a.Header : "-"));
            Close(window);
        }
        finally
        {
            RacetimeController.Handler = saved;
            RacetimeController.ResetApi();
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void Disabled_Racetime_Has_No_Menu()
    {
        string dir = HeadlessUi.NewTempDir();
        try
        {
            MainWindow window = Open(dir, racetimeEnabled: false);
            Assert.DoesNotContain(window.ContextMenuModelForTests().Items, i => i is NativeMenuAction { Header: "racetime.gg Races" });
            Close(window);
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void New_Race_Opens_Racetime()
    {
        Action<string> saved = RacetimeController.OpenBrowser;
        var opened = new List<string>();
        try
        {
            RacetimeController.OpenBrowser = opened.Add;
            RacetimeController.Create();
            Assert.Equal(["https://racetime.gg/"], opened);
        }
        finally
        {
            RacetimeController.OpenBrowser = saved;
        }
    }

    [AvaloniaFact]
    public async Task Race_Window_Joins_And_Follows_The_Race()
    {
        string dir = HeadlessUi.NewTempDir();
        HttpMessageHandler saved = RacetimeController.Handler;
        try
        {
            RacetimeController.Handler = new OnlineRacetime();
            RacetimeController.Credentials.Write(RacetimeAuthenticator.AccessTokenName, "tok");
            RacetimeController.ResetApi();
            MainWindow window = Open(dir);
            RacetimeChannelWindow race = window.Racetime.Join("sms/cool-race-1234");
            Assert.Equal("Connecting to cool-race-1234", race.Title);
            Assert.Equal(1145, race.Width);
            Assert.Equal(619, race.Height);
            Assert.True(race.Topmost);

            // The race's data names the window and adds the other entrant's comparison.
            race.Channel.Process("{\"type\":\"race.data\",\"race\":" + OpenRace.Replace(
                "\"entrants\":[]",
                "\"entrants\":[{\"user\":{\"id\":\"o1\",\"name\":\"Other\",\"full_name\":\"Other#0001\"},\"status\":{\"value\":\"ready\"}}]") + "}");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Any% [Super Mario Sunshine] - cool-race-1234", race.Title);
            Assert.Equal("https://racetime.gg/sms/cool-race-1234/livesplit", race.PageAddress);
            Assert.Contains("[Race] Other#0001", window.Session.State.Run.Comparisons);

            // Closing it removes the race's comparisons.
            window.Session.SwitchComparison("[Race] Other#0001");
            race.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(window.Racetime.Windows);
            Assert.Equal(Run.PersonalBestComparisonName, window.Session.State.CurrentComparison);
            Assert.DoesNotContain(window.Session.State.Run.Comparisons, c => c.StartsWith("[Race] ", StringComparison.Ordinal));
            await Task.Delay(10);
            Close(window);
        }
        finally
        {
            RacetimeController.Handler = saved;
            RacetimeController.Credentials.Write(RacetimeAuthenticator.AccessTokenName, null);
            RacetimeController.ResetApi();
            HeadlessUi.Delete(dir);
        }
    }

    [Fact]
    public void Authorization_Script_Adds_The_Token_To_Racetime_Requests()
    {
        string script = MacWebView.AuthorizationScript("https://racetime.gg/", "abc\"def");
        Assert.Contains("var o=\"https://racetime.gg\"", script);
        Assert.Contains("a=\"Bearer abc\\u0022def\"", script);
        Assert.Contains("setRequestHeader('Authorization',a)", script);
    }
}
