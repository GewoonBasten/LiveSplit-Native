using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Server;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

/// <summary>LiveSplit.Server.CommandServer: its commands and the WebSocket server.</summary>
public sealed class CommandServerTests
{
    private sealed class FakeHost : ICommandHost
    {
        public HotkeyProfileStub HotkeyProfile { get; } = HotkeyProfileStub.CreateDefault();

        public string HotkeyProfileName => "Default";

        public string? LayoutPath { get; set; }

        public string? SplitsPath { get; set; } = "/runs/sms.lss";

        public string Version => "1.2.3";

        public ServerStateType ServerState => ServerStateType.Websocket;

        public int HotkeyRefreshes { get; private set; }

        public List<string> Saved { get; } = [];

        public Task<bool> SaveLayoutAsync(string? path)
        {
            Saved.Add("layout " + path);
            return Task.FromResult(path is not null);
        }

        public Task<bool> SaveSplitsAsync(string? path)
        {
            Saved.Add("splits " + path);
            return Task.FromResult(true);
        }

        public Task<bool> OpenLayoutAsync(string path) => Task.FromResult(false);

        public Task<bool> OpenSplitsAsync(string path) => Task.FromResult(false);

        public Task<byte[]?> ScreenshotAsync() => Task.FromResult<byte[]?>([1, 2, 3]);

        public void HotkeysChanged() => HotkeyRefreshes++;
    }

    private static (TimerSession Session, CommandProcessor Commands, FakeHost Host) Create()
    {
        var run = new Run { GameName = "Super Mario Sunshine", CategoryName = "Any%" };
        foreach (string name in new[] { "Airstrip", "Bianco", "Ricco" })
        {
            run.AddSegment(name);
        }

        run[0].Comparisons[Run.PersonalBestComparisonName] = new SplitTime(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(55));
        run[1].Comparisons[Run.PersonalBestComparisonName] = new SplitTime(TimeSpan.FromSeconds(120));
        run[2].Comparisons[Run.PersonalBestComparisonName] = new SplitTime(TimeSpan.FromSeconds(180));
        var session = new TimerSession(new LiveSplitState(run), () => new ComparisonGeneratorSettings());
        var host = new FakeHost();
        return (session, new CommandProcessor(session, host), host);
    }

    [Fact]
    public async Task Timer_Commands_Drive_The_Timer_And_Times_Are_Answered_Precisely()
    {
        (TimerSession session, CommandProcessor commands, FakeHost host) = Create();
        LiveSplitState state = session.State;
        Assert.Equal("NotRunning", await commands.ProcessAsync("getcurrenttimerphase"));
        Assert.Equal("00:00:00", await commands.ProcessAsync("getcurrenttime"));
        Assert.Equal("-", await commands.ProcessAsync("getdelta"));
        Assert.Null(await commands.ProcessAsync("startorsplit"));
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);

        // A split 62.5 s in: the delta to Personal Best is +2.5 s.
        state.AdjustedStartTime = TimeStamp.Now - TimeSpan.FromSeconds(62.5);
        await commands.ProcessAsync("split");
        Assert.Equal("1", await commands.ProcessAsync("getsplitindex"));
        Assert.StartsWith("00:00:02.5", await commands.ProcessAsync("getdelta"));
        Assert.Equal("Airstrip", await commands.ProcessAsync("getprevioussplitname"));
        Assert.Equal("Bianco", await commands.ProcessAsync("getcurrentsplitname"));
        Assert.Equal("Ricco", await commands.ProcessAsync("getnextsplitname"));
        Assert.Equal("Ricco", await commands.ProcessAsync("getsplitname -1"));
        Assert.Equal("-", await commands.ProcessAsync("getsplitname 7"));
        Assert.Equal("00:02:00", await commands.ProcessAsync("getcomparisonsplittime"));
        Assert.Equal("3", await commands.ProcessAsync("getsplitcount"));

        // pause only pauses, resume only resumes.
        await commands.ProcessAsync("pause");
        await commands.ProcessAsync("pause");
        Assert.Equal(TimerPhase.Paused, state.CurrentPhase);
        await commands.ProcessAsync("resume");
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);

        // Game time.
        await commands.ProcessAsync("setgametime 1:02.250");
        Assert.True(state.IsGameTimeInitialized);
        await commands.ProcessAsync("pausegametime");
        Assert.True(state.IsGameTimePaused);
        Assert.StartsWith("00:01:02.2", await commands.ProcessAsync("getcurrentgametime"));
        await commands.ProcessAsync("unpausegametime");
        Assert.False(state.IsGameTimePaused);

        // Undo, skip, reset.
        await commands.ProcessAsync("unsplit");
        Assert.Equal(0, state.CurrentSplitIndex);
        await commands.ProcessAsync("skipsplit");
        Assert.Equal(1, state.CurrentSplitIndex);
        await commands.ProcessAsync("reset");
        Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);
        _ = host;
    }

    [Fact]
    public async Task Information_Comparisons_Variables_And_App_Commands()
    {
        (TimerSession session, CommandProcessor commands, FakeHost host) = Create();
        Assert.Equal("pong", await commands.ProcessAsync("ping"));
        Assert.Null(await commands.ProcessAsync("nosuchcommand"));
        Assert.Null(await commands.ProcessAsync("getsplitname"));
        Assert.Equal("Super Mario Sunshine", await commands.ProcessAsync("getgamename"));
        Assert.Equal("Any%", await commands.ProcessAsync("getcategoryname"));
        Assert.Equal("Personal Best", await commands.ProcessAsync("getcomparisonname"));
        await commands.ProcessAsync("setcomparison Best Segments");
        Assert.Equal("Best Segments", session.State.CurrentComparison);
        Assert.Equal("00:03:00", await commands.ProcessAsync("getfinaltime Personal Best"));
        await commands.ProcessAsync("switchto gametime");
        Assert.Equal("GameTime", await commands.ProcessAsync("gettimingmethod"));
        Assert.Equal("-", await commands.ProcessAsync("getfinaltime Personal Best"));

        // Custom variables: a new one is temporary; text with line breaks comes back on one line.
        await commands.ProcessAsync("setcustomvariable [\"deaths\",\"3\\nnow\"]");
        Assert.Equal("3 now", await commands.ProcessAsync("getcustomvariablevalue deaths"));
        Assert.Equal("-", await commands.ProcessAsync("getcustomvariablevalue missing"));
        Assert.False(session.State.Run.HasChanged);

        await commands.ProcessAsync("setsplitname 1 Bianco Hills");
        Assert.Equal("Bianco Hills", session.State.Run[1].Name);
        Assert.True(session.State.Run.HasChanged);

        Assert.Equal("False", await commands.ProcessAsync("globalhotkeysenabled"));
        await commands.ProcessAsync("enableglobalhotkeys");
        Assert.True(host.HotkeyProfile.GlobalHotkeysEnabled);
        Assert.Equal(1, host.HotkeyRefreshes);

        Assert.Equal("/runs/sms.lss", await commands.ProcessAsync("getsplitspath"));
        Assert.Equal("-", await commands.ProcessAsync("getlayoutpath"));
        Assert.Equal("False", await commands.ProcessAsync("savelayout"));
        Assert.Null(await commands.ProcessAsync("savelayoutas /tmp/layout.txt"));
        Assert.Equal("True", await commands.ProcessAsync("savelayoutas /tmp/layout.lsl"));
        Assert.Equal(["layout ", "layout /tmp/layout.lsl"], host.Saved);
        Assert.Equal("data:image/png;base64,AQID", await commands.ProcessAsync("getsplitsscreenshot"));
        Assert.Equal("1.2.3", await commands.ProcessAsync("getlivesplitversion"));
        Assert.Equal("Websocket", await commands.ProcessAsync("getservertype"));
        Assert.Equal("Default", await commands.ProcessAsync("gethotkeyprofile"));
        Assert.Equal("-", await commands.ProcessAsync("getautosplitterpath"));
    }

    [Fact]
    public async Task WebSocket_Server_Answers_On_Livesplit_Only()
    {
        (_, CommandProcessor commands, _) = Create();
        using var server = new CommandServer(commands.ProcessAsync);
        server.StartWs(0);
        Assert.Equal(ServerStateType.Websocket, server.ServerState);

        using (var client = new ClientWebSocket())
        {
            await client.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/livesplit"), CancellationToken.None);
            async Task<string> Ask(string command)
            {
                await client.SendAsync(Encoding.UTF8.GetBytes(command), WebSocketMessageType.Text, true, CancellationToken.None);
                byte[] buffer = new byte[4096];
                WebSocketReceiveResult result = await client.ReceiveAsync(buffer, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
                return Encoding.UTF8.GetString(buffer, 0, result.Count);
            }

            Assert.Equal("pong", await Ask("ping"));
            await client.SendAsync(Encoding.UTF8.GetBytes("starttimer"), WebSocketMessageType.Text, true, CancellationToken.None);
            Assert.Equal("Running", await Ask("getcurrenttimerphase"));
            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
        }

        // Another path: no WebSocket.
        using (var other = new ClientWebSocket())
        {
            await Assert.ThrowsAnyAsync<WebSocketException>(() => other.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/other"), CancellationToken.None));
        }

        int port = server.Port;
        server.StopWs();
        Assert.Equal(ServerStateType.Off, server.ServerState);
        using var tcp = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(() => tcp.ConnectAsync("127.0.0.1", port));
    }

    [Fact]
    public async Task Tcp_Server_Answers_Line_By_Line()
    {
        (_, CommandProcessor commands, _) = Create();
        using var server = new CommandServer(commands.ProcessAsync);
        server.StartTcp(0);
        Assert.Equal(ServerStateType.TCP, server.ServerState);
        using (var client = new TcpClient())
        {
            await client.ConnectAsync("127.0.0.1", server.Port);
            NetworkStream stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("ping");
            Assert.Equal("pong", await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));

            // Commands without an answer, then two answers in a row.
            await writer.WriteLineAsync("starttimer");
            await writer.WriteLineAsync("getcurrenttimerphase");
            await writer.WriteLineAsync("getgamename");
            Assert.Equal("Running", await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal("Super Mario Sunshine", await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)));
        }

        server.StopTcp();
        Assert.Equal(ServerStateType.Off, server.ServerState);
    }

    [Fact]
    public void Accept_Key_Is_Rfc6455s()
    {
        Assert.Equal("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=", CommandServer.AcceptKey("dGhlIHNhbXBsZSBub25jZQ=="));
    }

    [Fact]
    public void Server_Settings_Are_Kept_In_Settings_Cfg()
    {
        var settings = new AppSettings { ServerStartup = ServerStartupType.PreviousState, ServerState = ServerStateType.Websocket };
        using var stream = new MemoryStream();
        Serialization.XmlAppSettingsSerializer.Save(settings, stream);
        string xml = Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("<ServerStartup>3</ServerStartup>", xml);
        Assert.Contains("<ServerState>2</ServerState>", xml);
        AppSettings loaded = Serialization.XmlAppSettingsSerializer.Load(new MemoryStream(stream.ToArray()));
        Assert.Equal(ServerStartupType.PreviousState, loaded.ServerStartup);
        Assert.Equal(ServerStateType.Websocket, loaded.ServerState);
    }
}
