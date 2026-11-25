using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

/// <summary>LiveSplit.Racetime (RacetimeAPI, Race, RacetimeChannel, RacetimeSettings) against a fake racetime.gg.</summary>
public sealed class RacetimeTests
{
    private const string Me = "me1";
    private const string Other = "o1";

    /// <summary>Answers racetime.gg's userinfo and races/data.</summary>
    private sealed class FakeRacetime : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public string RacesJson { get; set; } = "{\"races\":[]}";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string url = request.RequestUri!.ToString();
            lock (Requests)
            {
                Requests.Add(request.Method + " " + url + " " + request.Headers.Authorization);
            }

            if (url == RacetimeAuthenticator.AuthServer + "o/userinfo")
            {
                return Task.FromResult(request.Headers.Authorization?.Parameter == "tok"
                    ? new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent($"{{\"id\":\"{Me}\",\"name\":\"Basten\",\"full_name\":\"Basten#1234\"}}"),
                    }
                    : new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }

            if (url == RacetimeApi.BaseUri + RacetimeApi.RacesEndpoint)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(RacesJson) });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    /// <summary>A websocket whose messages the test sends; null closes it.</summary>
    private sealed class FakeSocket : IRacetimeSocket
    {
        private readonly Channel<string?> _incoming = Channel.CreateUnbounded<string?>();
        private bool _open;
        private bool _closed;

        public List<string> Sent { get; } = [];

        public Uri? Uri { get; private set; }

        public string? Token { get; private set; }

        public bool IsOpen => _open;

        public bool ClosedCleanly => _closed;

        public void Receive(string? message) => _incoming.Writer.TryWrite(message);

        public Task ConnectAsync(Uri uri, string accessToken, CancellationToken token)
        {
            Uri = uri;
            Token = accessToken;
            _open = true;
            return Task.CompletedTask;
        }

        public Task SendAsync(string text, CancellationToken token)
        {
            lock (Sent)
            {
                Sent.Add(text);
            }

            return Task.CompletedTask;
        }

        public async Task<string?> ReceiveAsync(CancellationToken token)
        {
            string? message = await _incoming.Reader.ReadAsync(token);
            if (message is null)
            {
                _open = false;
                _closed = true;
            }

            return message;
        }

        public void Dispose()
        {
        }
    }

    private static string Entrant(string id, string name, string status) =>
        $"{{\"user\":{{\"id\":\"{id}\",\"name\":\"{name}\",\"full_name\":\"{name}#0001\",\"twitch_name\":null}},\"status\":{{\"value\":\"{status}\"}},\"finished_at\":null}}";

    private static string RaceData(int version, string status, string myStatus, string otherStatus = "ready", string? startedAt = null) =>
        "{\"type\":\"race.data\",\"race\":{\"name\":\"sms/cool-race-1234\",\"status\":{\"value\":\"" + status + "\"},"
        + "\"category\":{\"name\":\"Super Mario Sunshine\",\"slug\":\"sms\"},\"goal\":{\"name\":\"Any%\",\"custom\":false},"
        + "\"entrants_count\":2,\"entrants_count_finished\":0,\"entrants_count_inactive\":0,"
        + "\"started_at\":" + (startedAt is null ? "null" : "\"" + startedAt + "\"") + ","
        + "\"start_delay\":\"P0DT00H00M15S\",\"version\":" + version + ","
        + "\"entrants\":[" + Entrant(Me, "Basten", myStatus) + "," + Entrant(Other, "Other", otherStatus) + "]}}";

    private static (TimerSession Session, RacetimeChannel Channel, FakeSocket Socket, List<string> Messages) Create()
    {
        var run = new Run();
        run.AddSegment("A");
        run.AddSegment("B");
        run.AddSegment("C");
        var session = new TimerSession(new LiveSplitState(run), () => new ComparisonGeneratorSettings());
        var credentials = new MemoryCredentialStore();
        credentials.Write(RacetimeAuthenticator.AccessTokenName, "tok");
        var authenticator = new RacetimeAuthenticator(new FakeRacetime(), credentials, _ => throw new InvalidOperationException("no browser"));
        var socket = new FakeSocket();
        var channel = new RacetimeChannel(session, new RacetimeSettings(), authenticator, () => socket, action => action(), _ => Task.CompletedTask);
        var messages = new List<string>();
        channel.SystemMessage += (text, _) => messages.Add(text);
        return (session, channel, socket, messages);
    }

    /// <summary>A port nothing listens on (a LiveSplit signing in holds 4888).</summary>
    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    /// <summary>
    /// A second race window while the first waits for the browser sign-in (Basten 2 Nov: the
    /// second try froze LiveSplit): Pending, then the waiting sign-in lets go of the flag
    /// (StopPendingAuthRequest, as Windows), the second takes it over and opens the browser, and
    /// the first ends Stale without taking the flag from the second.
    /// </summary>
    [Fact]
    public async Task A_Second_Sign_In_Takes_Over_The_Waiting_One()
    {
        var opened = new List<string>();
        var authenticator = new RacetimeAuthenticator(new FakeRacetime(), new MemoryCredentialStore(), url =>
        {
            lock (opened)
            {
                opened.Add(url);
            }
        })
        {
            ListenPort = FreePort(),
        };
        Task<AuthResult> first = authenticator.AuthorizeAsync();
        await Until(() => opened.Count == 1);
        Assert.True(authenticator.IsAuthorizing);

        Assert.Equal(AuthResult.Pending, await authenticator.AuthorizeAsync());
        authenticator.StopPendingAuthRequest();
        Assert.False(authenticator.IsAuthorizing);
        Task<AuthResult> second = authenticator.AuthorizeAsync();
        await Until(() => opened.Count == 2);
        Assert.Equal(AuthResult.Stale, await first.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(authenticator.IsAuthorizing);

        // The browser comes back declined: the second sign-in ends and no one holds the flag.
        string state = System.Text.RegularExpressions.Regex.Match(opened[1], "[?&]state=([^&]+)").Groups[1].Value;
        using (var browser = new TcpClient())
        {
            await browser.ConnectAsync(RacetimeAuthenticator.RedirectAddress, authenticator.ListenPort);
            NetworkStream stream = browser.GetStream();
            byte[] request = Encoding.ASCII.GetBytes($"GET /?error=access_denied&state={state} HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");
            await stream.WriteAsync(request);
            Assert.Equal(AuthResult.Cancelled, await second.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        Assert.False(authenticator.IsAuthorizing);
    }

    /// <summary>
    /// The browser opens a connection that sends nothing and answers on another (Chromium
    /// browsers such as Arc): the sign-in still gets the answer and redirects the browser.
    /// </summary>
    [Fact]
    public async Task The_Sign_In_Answer_Is_Found_Past_An_Idle_Connection()
    {
        var opened = new List<string>();
        var authenticator = new RacetimeAuthenticator(new FakeRacetime(), new MemoryCredentialStore(), url =>
        {
            lock (opened)
            {
                opened.Add(url);
            }
        })
        {
            ListenPort = FreePort(),
        };
        Task<AuthResult> signIn = authenticator.AuthorizeAsync();
        await Until(() => opened.Count == 1);
        string state = System.Text.RegularExpressions.Regex.Match(opened[0], "[?&]state=([^&]+)").Groups[1].Value;

        using var idle = new TcpClient();
        await idle.ConnectAsync(RacetimeAuthenticator.RedirectAddress, authenticator.ListenPort);
        using var browser = new TcpClient();
        await browser.ConnectAsync(RacetimeAuthenticator.RedirectAddress, authenticator.ListenPort);
        NetworkStream stream = browser.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"GET /?error=access_denied&state={state} HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n"));
        byte[] buffer = new byte[4096];
        int read = await stream.ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        string answer = Encoding.ASCII.GetString(buffer, 0, read);
        Assert.Contains("301", answer, StringComparison.Ordinal);
        Assert.Contains(RacetimeAuthenticator.AuthServer + "o/done?error=access_denied", answer, StringComparison.Ordinal);
        Assert.Equal(AuthResult.Cancelled, await signIn.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    /// <summary>
    /// The app's OAuth client as LiveSplit.Racetime registers it with racetime.gg (Resources.resx,
    /// and LiveSplit 1.8.37's DLL): another secret gets an error page instead of a token (Basten
    /// 2 Nov: Authorize, then the question again and again).
    /// </summary>
    [Fact]
    public void The_Client_Is_LiveSplits()
    {
        Assert.Equal("dKgJFc2jZjgMtUT4Ik9kyJSCZkVqRK1poCcvpKox", RacetimeAuthenticator.ClientId);
        Assert.Equal(
            "Rl6gGXy9zPw5xkSa0gSdE69lnlqbTwuZDAFNSDTgY5QKbQ9AqsqDYlotNGMBboDGkZFbJm9fSIpfI3sCGc3UiiZ2sEpk8KpxYf4kh0G7TjmYmgn0AXyNJNzkgIorHAcB",
            RacetimeAuthenticator.ClientSecret);
        Assert.Equal("read chat_message race_action", RacetimeAuthenticator.Scopes);
        Assert.Equal("http://127.0.0.1:4888/", RacetimeAuthenticator.RedirectUri);
    }

    /// <summary>
    /// The token request's Content-Type has no charset, as Windows': racetime.gg answers
    /// "application/x-www-form-urlencoded; charset=us-ascii" with a Bad Request page (Basten 2
    /// Nov: Authorize, then the question again).
    /// </summary>
    [Fact]
    public async Task The_Token_Request_Is_Plain_Form_Data()
    {
        using HttpContent content = RacetimeAuthenticator.TokenContent("code=a&grant_type=authorization_code");
        Assert.Equal("application/x-www-form-urlencoded", content.Headers.ContentType!.ToString());
        Assert.Equal("code=a&grant_type=authorization_code", await content.ReadAsStringAsync());
    }

    [Fact]
    public void RaceJsonReadsAsRacetimeRace()
    {
        using JsonDocument json = JsonDocument.Parse(RaceData(7, "in_progress", "in_progress", startedAt: "2026-10-17T20:00:00.000Z"));
        RacetimeRace race = RacetimeRace.From(json.RootElement.GetProperty("race"));
        Assert.Equal("sms/cool-race-1234", race.Id);
        Assert.Equal("cool-race-1234", race.ChannelName);
        Assert.Equal("Super Mario Sunshine", race.GameName);
        Assert.Equal("sms", race.GameId);
        Assert.Equal("Any%", race.Goal);
        Assert.Equal(RaceState.Started, race.State);
        Assert.Equal(3, race.MenuState);
        Assert.Equal(TimeSpan.FromSeconds(15), race.StartDelay);
        Assert.Equal(7, race.Version);
        Assert.Equal(new DateTime(2026, 10, 17, 20, 0, 0, DateTimeKind.Utc), race.StartedAt);
        Assert.Equal(2, race.Entrants.Count);
        Assert.Equal(UserStatus.Racing, race.Entrants[0].Status);
        Assert.Equal("Other#0001", race.Entrants[1].FullName);
        Assert.False(race.Entrants[0].HasFinished);
    }

    /// <summary>Counts reads of the saved sign-in (on macOS each read can show a Keychain prompt).</summary>
    private sealed class CountingCredentialStore : ICredentialStore
    {
        private readonly MemoryCredentialStore _inner = new();

        public int Reads { get; private set; }

        public string? Read(string name)
        {
            Reads++;
            return _inner.Read(name);
        }

        public void Write(string name, string? value) => _inner.Write(name, value);
    }

    /// <summary>
    /// The races list never reads the saved sign-in (Basten 25 Nov: racetime access was asked
    /// again and again; only opening a race should ask). Once a race was opened, it is sent.
    /// </summary>
    [Fact]
    public async Task RacesListReadsRacesDataWithoutTheKeychainUntilARaceIsOpened()
    {
        var handler = new FakeRacetime
        {
            RacesJson = "{\"races\":[" + JsonDocument.Parse(RaceData(1, "open", "ready")).RootElement.GetProperty("race").GetRawText() + "]}",
        };
        var credentials = new CountingCredentialStore();
        credentials.Write(RacetimeAuthenticator.AccessTokenName, "tok");
        var authenticator = new RacetimeAuthenticator(handler, credentials, _ => { });
        var api = new RacetimeApi(handler, authenticator);
        IReadOnlyList<RacetimeRace> races = await api.RefreshRacesAsync();
        await api.RefreshRacesAsync();
        RacetimeRace race = Assert.Single(races);
        Assert.Empty(race.Entrants);
        Assert.Equal(0, credentials.Reads);
        Assert.Contains("GET https://racetime.gg/races/data ", handler.Requests);

        // Opening a race signs in: the saved sign-in is read once and then sent with the list.
        Assert.Equal(AuthResult.Success, await authenticator.AuthorizeAsync());
        Assert.Equal(AuthResult.Success, await authenticator.AuthorizeAsync());
        Assert.Equal(1, credentials.Reads);
        await api.RefreshRacesAsync();
        Assert.Contains("GET https://racetime.gg/races/data Bearer tok", handler.Requests);

        // A failed refresh keeps the list.
        handler.RacesJson = "not json";
        Assert.Single(await api.RefreshRacesAsync());
    }

    [Fact]
    public void RaceMenuListsOpenThenRunningRacesThenNewRace()
    {
        DateTime now = new(2026, 10, 17, 21, 0, 0, DateTimeKind.Utc);
        RacetimeRace open = Race("a/open", RaceState.Open, entrants: 1);
        RacetimeRace invite = Race("a/invite", RaceState.OpenInviteOnly, entrants: 3);
        RacetimeRace running = Race("a/running", RaceState.Started, entrants: 5) with
        {
            StartedAt = now.AddMinutes(-83).AddSeconds(-5),
            Finishes = 1,
            Forfeits = 2,
        };
        RacetimeRace ended = Race("a/ended", RaceState.Ended, entrants: 2);
        RacetimeRace longGoal = Race("a/long", RaceState.Open, entrants: 2) with { Goal = new string('g', 70) };

        IReadOnlyList<RaceMenuEntry> menu = RaceMenu.Build([running, open, ended, invite, longGoal], now);
        Assert.Equal(
            [
                "Game - Goal (1 Entrant)",
                "Game - Goal (3 Entrants)",
                "Game - " + new string('g', 53) + "... (2 Entrants)",
                "-",
                "[1:23:05] Game - Goal (1/3 Finished)",
                "-",
                "New Race...",
            ],
            menu.Select(e => e.Title ?? "-"));
        Assert.Equal("a/running", menu[4].RaceId);
        Assert.True(menu[^1].IsNewRace);

        // No races: only New Race....
        Assert.Equal(["New Race..."], RaceMenu.Build([], now).Select(e => e.Title));
    }

    private static RacetimeRace Race(string id, RaceState state, int entrants) =>
        new(id, "Game", "game", "Goal", state, entrants, 0, 0, DateTime.MaxValue, TimeSpan.Zero, 1, []);

    [Fact]
    public void SettingsAreKeptInSettingsCfgWithOtherPlugins()
    {
        const string cfg = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Settings version=\"1.8.18\"><RaceProviderPlugins>"
            + "<Plugin name=\"LiveSplit.Other.dll\" enabled=\"False\"><Something>1</Something></Plugin>"
            + "<Plugin name=\"LiveSplit.Racetime.dll\" enabled=\"True\"><LoadChatHistory>False</LoadChatHistory><HideResults>False</HideResults></Plugin>"
            + "</RaceProviderPlugins></Settings>";
        AppSettings settings = XmlAppSettingsSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(cfg)));
        Assert.True(settings.Racetime.Enabled);
        Assert.False(settings.Racetime.LoadChatHistory);
        Assert.False(settings.Racetime.HideResults);

        using var saved = new MemoryStream();
        XmlAppSettingsSerializer.Save(settings, saved);
        var document = new XmlDocument();
        document.LoadXml(Encoding.UTF8.GetString(saved.ToArray()).TrimStart('﻿'));
        XmlElement plugins = document.DocumentElement!["RaceProviderPlugins"]!;
        Assert.Equal(
            "<Plugin name=\"LiveSplit.Other.dll\" enabled=\"False\"><Something>1</Something></Plugin>"
            + "<Plugin name=\"LiveSplit.Racetime.dll\" enabled=\"True\"><LoadChatHistory>False</LoadChatHistory><HideResults>False</HideResults></Plugin>"
            // XMLSettingsFactory adds SRL (TimerForm's own factory) when the file does not have it.
            + "<Plugin name=\"SRL\" enabled=\"True\" />",
            plugins.InnerXml);

        // Without the element (a new settings.cfg): enabled, chat history loaded.
        AppSettings fresh = XmlAppSettingsSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes("<Settings/>")));
        Assert.True(fresh.Racetime.Enabled);
        Assert.True(fresh.Racetime.LoadChatHistory);
    }

    [Fact]
    public void CommandsAreTheChannelsJson()
    {
        Assert.Equal("{\"action\":\"forfeit\"}", RacetimeChannel.CreateCommand(".forfeit"));
        Assert.Equal("{\"action\":\"done\"}", RacetimeChannel.CreateCommand(".done"));
        Assert.Equal("{\"action\":\"setinfo\",\"data\":{\"setinfo\":\"hello there\"}}", RacetimeChannel.CreateCommand(".setinfo hello there"));
        string chat = RacetimeChannel.CreateCommand("glhf \"all\"");
        using JsonDocument json = JsonDocument.Parse(chat);
        Assert.Equal("message", json.RootElement.GetProperty("action").GetString());
        Assert.Equal("glhf \"all\"", json.RootElement.GetProperty("data").GetProperty("message").GetString());
    }

    [Fact]
    public async Task TheTimerFollowsTheRaceAndTheRaceHearsTheTimer()
    {
        (TimerSession session, RacetimeChannel channel, FakeSocket socket, List<string> messages) = Create();
        LiveSplitState state = session.State;
        int disconnected = 0;
        channel.Disconnected += (_, _) => disconnected++;
        Task running = Task.Run(() => channel.ConnectAsync("sms/cool-race-1234"));

        // Joined with the token; getrace answered by an open race (countdown offset), then the history.
        socket.Receive(RaceData(1, "open", "not_ready"));
        socket.Receive("{\"type\":\"chat.history\",\"messages\":[]}");
        await Until(() => channel.Race is not null && socket.Sent.Count >= 2);
        Assert.Equal(new Uri("wss://racetime.gg/ws/o/race/cool-race-1234"), socket.Uri);
        Assert.Equal("tok", socket.Token);
        Assert.Equal(["{ \"action\":\"getrace\" }", "{ \"action\":\"gethistory\" }"], socket.Sent.Take(2));
        Assert.Equal("Basten", channel.Username);
        Assert.Equal(UserStatus.NotReady, channel.PersonalStatus);
        Assert.Equal(TimeSpan.FromSeconds(-15), state.Run.Offset);
        Assert.Contains("[Race] Other#0001", state.Run.Comparisons);
        Assert.DoesNotContain("[Race] Basten#0001", state.Run.Comparisons);
        Assert.Equal("Other#0001", StandardComparisonGeneratorsFactory.GetShortComparisonName("[Race] Other#0001"));
        Assert.Contains("Authorization successful. Hello, Basten", messages);
        Assert.Contains("Joined Channel 'cool-race-1234'", messages);

        // The countdown: the timer starts at -15 s.
        socket.Receive(RaceData(2, "pending", "ready"));
        await Until(() => state.CurrentPhase == TimerPhase.Running);
        Assert.True(state.CurrentTime.RealTime < TimeSpan.Zero);

        // The same version again changes nothing.
        socket.Receive(RaceData(2, "open", "ready"));

        // Racing: the offset goes back to the run's own.
        socket.Receive(RaceData(3, "in_progress", "in_progress", "in_progress", DateTime.UtcNow.ToString("O")));
        // The version is set before the offset goes back: wait for both.
        await Until(() => channel.Race?.Version == 3 && state.Run.Offset == TimeSpan.Zero);
        Assert.Equal(TimeSpan.Zero, state.Run.Offset);
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);

        // Another entrant's split fills their comparison (split names are matched lower case).
        socket.Receive("{\"type\":\"race.split\",\"split\":{\"user_id\":\"o1\",\"split_name\":\"a\",\"split_time\":\"0:01:00.50\",\"is_undo\":false,\"is_finish\":false}}");
        await Until(() => state.Run[0].Comparisons.ContainsKey("[Race] Other#0001"));
        Assert.Equal(TimeSpan.FromSeconds(60.5), state.Run[0].Comparisons["[Race] Other#0001"].RealTime);

        // Pausing is undone at once in a race.
        session.Execute(TimerCommand.Pause);
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);

        // A split and an undo are sent to the race.
        state.AdjustedStartTime = TimeStamp.Now - TimeSpan.FromSeconds(83.25);
        session.Execute(TimerCommand.StartOrSplit);
        await Until(() => socket.Sent.Count >= 3);
        using (JsonDocument split = JsonDocument.Parse(socket.Sent[2]))
        {
            Assert.Equal("split", split.RootElement.GetProperty("action").GetString());
            JsonElement data = split.RootElement.GetProperty("data");
            Assert.Equal("A", data.GetProperty("split").GetString());
            Assert.False(data.GetProperty("is_finish").GetBoolean());
            Assert.StartsWith("1:23.", data.GetProperty("time").GetString());
        }

        session.Execute(TimerCommand.UndoSplit);
        await Until(() => socket.Sent.Count >= 4);
        Assert.Equal("{\"action\":\"split\",\"data\":{\"split\":\"A\",\"time\":\"-\"}}", socket.Sent[3]);

        // A reset forfeits.
        session.Execute(TimerCommand.Reset);
        await Until(() => socket.Sent.Count >= 5);
        Assert.Equal("{\"action\":\"forfeit\"}", socket.Sent[4]);

        // The race room closes: no reconnect.
        socket.Receive(null);
        await running.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, disconnected);
        Assert.Equal("Disconnect", messages[^1]);
        Assert.False(channel.IsConnected);
    }

    [Fact]
    public async Task FinishingInTheRaceEndsTheTimer()
    {
        (TimerSession session, RacetimeChannel channel, FakeSocket socket, _) = Create();
        LiveSplitState state = session.State;
        Task running = Task.Run(() => channel.ConnectAsync("sms/cool-race-1234"));
        socket.Receive(RaceData(1, "in_progress", "in_progress", startedAt: DateTime.UtcNow.AddSeconds(-30).ToString("O")));
        socket.Receive("{\"type\":\"chat.history\",\"messages\":[]}");

        // Joining a running race: the timer starts at the race's time.
        await Until(() => state.CurrentPhase == TimerPhase.Running);
        Assert.InRange(state.CurrentTime.RealTime!.Value.TotalSeconds, 29, 40);

        // Finished (.done from the website) after the first split: the remaining splits are
        // skipped and the last one split.
        session.Execute(TimerCommand.StartOrSplit);
        Assert.Equal(1, state.CurrentSplitIndex);
        socket.Receive(RaceData(2, "in_progress", "done"));
        await Until(() => state.CurrentPhase == TimerPhase.Ended);
        Assert.NotNull(state.Run[0].SplitTime.RealTime);
        Assert.Null(state.Run[1].SplitTime.RealTime);
        Assert.NotNull(state.Run[2].SplitTime.RealTime);

        // .undone: racing again. As Windows, the split index goes back to where it was at the
        // finish and one split is undone from there.
        socket.Receive(RaceData(3, "in_progress", "in_progress"));
        await Until(() => state.CurrentPhase == TimerPhase.Running);
        Assert.Equal(0, state.CurrentSplitIndex);

        socket.Receive(null);
        await running.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RemovingTheRaceComparisonsGoesBackToPersonalBest()
    {
        (TimerSession session, RacetimeChannel channel, _, _) = Create();
        LiveSplitState state = session.State;
        channel.Process(RaceData(1, "open", "ready"));
        Assert.Contains("[Race] Other#0001", state.Run.Comparisons);
        Assert.Contains("[Race] Basten#0001", state.Run.Comparisons);
        state.Run[0].Comparisons["[Race] Other#0001"] = new SplitTime(TimeSpan.FromSeconds(5));
        session.SwitchComparison("[Race] Other#0001");

        channel.RemoveRaceComparisons();
        Assert.Equal(Run.PersonalBestComparisonName, state.CurrentComparison);
        Assert.DoesNotContain(state.Run.Comparisons, c => c.StartsWith("[Race] ", StringComparison.Ordinal));
        Assert.Null(state.Run[0].Comparisons["[Race] Other#0001"].RealTime);
    }

    [Fact]
    public void RaceComparisonsStayWhenTheSplitsAreEdited()
    {
        (TimerSession session, RacetimeChannel channel, _, _) = Create();
        channel.Process(RaceData(1, "open", "ready"));
        Run edited = session.State.Run.Clone();
        session.SetRun(edited, TimerPhase.NotRunning, -1);
        Assert.Contains("[Race] Other#0001", session.State.Run.Comparisons);
    }
}
