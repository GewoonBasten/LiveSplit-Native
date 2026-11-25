using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

/// <summary>A whole race against a fake racetime.gg server (Basten 25 Nov: "can you do a fake test").</summary>
public sealed class RacetimeFakeRaceTests
{
    private sealed class FakeHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.ToString() == RacetimeAuthenticator.AuthServer + "o/userinfo" && request.Headers.Authorization?.Parameter == "tok")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"me1\",\"name\":\"Basten\",\"full_name\":\"Basten#1234\"}"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }

    /// <summary>The race room: answers getrace / gethistory itself, pushes what the test says.</summary>
    private sealed class FakeServer
    {
        public string Status = "open";
        public string Me = "not_ready";
        public string Rival = "ready";
        public string Third = "ready";
        public string? StartedAt;
        public int Version = 1;
        public readonly List<string> Sent = [];
        public readonly List<Connection> Connections = [];

        public Connection Current => Connections[^1];

        public string RaceJson()
        {
            static string Entrant(string id, string name, string status) =>
                $"{{\"user\":{{\"id\":\"{id}\",\"name\":\"{name}\",\"full_name\":\"{name}#0001\",\"twitch_name\":null}},\"status\":{{\"value\":\"{status}\"}},\"finished_at\":null}}";
            return "{\"type\":\"race.data\",\"race\":{\"name\":\"sms/fake-race-0001\",\"status\":{\"value\":\"" + Status + "\"},"
                + "\"category\":{\"name\":\"Super Mario Sunshine\",\"slug\":\"sms\"},\"goal\":{\"name\":\"Any%\",\"custom\":false},"
                + "\"entrants_count\":3,\"entrants_count_finished\":0,\"entrants_count_inactive\":0,"
                + "\"started_at\":" + (StartedAt is null ? "null" : "\"" + StartedAt + "\"") + ","
                + "\"start_delay\":\"P0DT00H00M15S\",\"version\":" + Version + ","
                + "\"entrants\":[" + Entrant("me1", "Basten", Me) + "," + Entrant("o1", "Rival", Rival) + "," + Entrant("o2", "Third", Third) + "]}}";
        }

        /// <summary>The race changed: a new version goes to the open connection.</summary>
        public void Push()
        {
            Version++;
            Current.Receive(RaceJson());
        }

        public IRacetimeSocket Open()
        {
            var connection = new Connection(this);
            lock (Connections)
            {
                Connections.Add(connection);
            }

            return connection;
        }

        public sealed class Connection(FakeServer server) : IRacetimeSocket
        {
            private readonly Channel<string?> _incoming = Channel.CreateUnbounded<string?>();
            private bool _dropped;

            public bool IsOpen { get; private set; }

            public bool ClosedCleanly { get; private set; }

            public void Receive(string? message) => _incoming.Writer.TryWrite(message);

            /// <summary>The network goes away (not a clean close).</summary>
            public void Drop()
            {
                _dropped = true;
                _incoming.Writer.TryWrite(null);
            }

            public Task ConnectAsync(Uri uri, string accessToken, CancellationToken token)
            {
                Assert.Equal(new Uri("wss://racetime.gg/ws/o/race/fake-race-0001"), uri);
                Assert.Equal("tok", accessToken);
                IsOpen = true;
                return Task.CompletedTask;
            }

            public Task SendAsync(string text, CancellationToken token)
            {
                lock (server.Sent)
                {
                    server.Sent.Add(text);
                }

                if (text.Contains("\"getrace\"", StringComparison.Ordinal))
                {
                    Receive(server.RaceJson());
                }
                else if (text.Contains("\"gethistory\"", StringComparison.Ordinal))
                {
                    Receive("{\"type\":\"chat.history\",\"messages\":[]}");
                }

                return Task.CompletedTask;
            }

            public async Task<string?> ReceiveAsync(CancellationToken token)
            {
                string? message = await _incoming.Reader.ReadAsync(token);
                if (message is null)
                {
                    IsOpen = false;
                    if (_dropped)
                    {
                        throw new IOException("connection lost");
                    }

                    ClosedCleanly = true;
                }

                return message;
            }

            public void Dispose()
            {
            }
        }
    }

    private static async Task Until(Func<bool> condition, string what)
    {
        for (int i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "timed out waiting for: " + what);
    }

    private static List<JsonElement> Splits(FakeServer server)
    {
        lock (server.Sent)
        {
            return server.Sent
                .Select(s => JsonDocument.Parse(s).RootElement)
                .Where(j => j.TryGetProperty("action", out JsonElement a) && a.GetString() == "split")
                .ToList();
        }
    }

    private static bool SentMessage(FakeServer server, string text)
    {
        lock (server.Sent)
        {
            // ".ready" is sent as {"action":"ready"} (RacetimeChannel.CreateCommand).
            return server.Sent.Any(s => s.StartsWith("{\"action\":\"" + text.TrimStart('.') + "\"", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task A_Whole_Race_With_A_Dropped_Connection()
    {
        var run = new Run();
        run.AddSegment("Bianco Hills");
        run.AddSegment("Ricco Harbor");
        run.AddSegment("Corona Mountain");
        var session = new TimerSession(new LiveSplitState(run), () => new ComparisonGeneratorSettings());
        LiveSplitState state = session.State;
        var credentials = new MemoryCredentialStore();
        credentials.Write(RacetimeAuthenticator.AccessTokenName, "tok");
        var authenticator = new RacetimeAuthenticator(new FakeHttp(), credentials, _ => throw new InvalidOperationException("no browser"));
        var server = new FakeServer();
        var channel = new RacetimeChannel(session, new RacetimeSettings(), authenticator, server.Open, action => action(), _ => Task.CompletedTask);
        var messages = new List<string>();
        channel.SystemMessage += (text, _) =>
        {
            lock (messages)
            {
                messages.Add(text);
            }
        };
        int disconnected = 0;
        channel.Disconnected += (_, _) => disconnected++;

        // 1. Join an open race with two other runners.
        Task connection = Task.Run(() => channel.ConnectAsync("sms/fake-race-0001"));
        await Until(() => channel.Race is not null, "joined");
        Assert.Equal(UserStatus.NotReady, channel.PersonalStatus);
        Assert.Contains("[Race] Rival#0001", state.Run.Comparisons);
        Assert.Contains("[Race] Third#0001", state.Run.Comparisons);
        Assert.Equal(TimeSpan.FromSeconds(-15), state.Run.Offset);

        // 2. Ready up.
        channel.Ready();
        Assert.True(SentMessage(server, ".ready"));
        server.Me = "ready";
        server.Push();
        await Until(() => channel.PersonalStatus == UserStatus.Ready, "ready");

        // 3. Countdown, then the race starts.
        server.Status = "pending";
        server.Push();
        await Until(() => state.CurrentPhase == TimerPhase.Running, "countdown starts the timer");
        Assert.True(state.CurrentTime.RealTime < TimeSpan.Zero);
        server.Status = "in_progress";
        server.Me = server.Rival = server.Third = "in_progress";
        server.StartedAt = DateTime.UtcNow.AddSeconds(-1).ToString("O");
        server.Push();
        await Until(() => channel.PersonalStatus == UserStatus.Racing && channel.Race?.Version == server.Version, "racing");
        // As Windows' RacetimeChannel: a second "open" update (Ready) stores the countdown's -15 s as
        // the offset to restore, because a run offset of 0 counts as "not stored yet".
        Assert.Equal(TimeSpan.FromSeconds(-15), state.Run.Offset);
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);

        // 4. The rival splits; their comparison fills.
        server.Current.Receive("{\"type\":\"race.split\",\"split\":{\"user_id\":\"o1\",\"split_name\":\"bianco hills\",\"split_time\":\"0:05:10.25\",\"is_undo\":false,\"is_finish\":false}}");
        await Until(() => state.Run[0].Comparisons.TryGetValue("[Race] Rival#0001", out SplitTime t) && t.RealTime is not null, "rival split");
        Assert.Equal(TimeSpan.FromSeconds(310.25), state.Run[0].Comparisons["[Race] Rival#0001"].RealTime);

        // 5. My first split goes to the race. (The fake race skips the real 15 s countdown, and a
        //    split below 0 is ignored, so the clock moves on 5:20 here.)
        state.AdjustedStartTime = TimeStamp.Now - TimeSpan.FromSeconds(320);
        session.Execute(TimerCommand.StartOrSplit);
        await Until(() => Splits(server).Count == 1, "my first split sent");
        Assert.Equal("Bianco Hills", Splits(server)[0].GetProperty("data").GetProperty("split").GetString());
        Assert.False(Splits(server)[0].GetProperty("data").GetProperty("is_finish").GetBoolean());

        // 6. The connection drops mid-race: it reconnects, the timer keeps running, nothing is lost.
        server.Current.Drop();
        await Until(() => server.Connections.Count == 2 && channel.IsConnected && server.Current.IsOpen, "reconnected");
        lock (messages)
        {
            Assert.Contains("Auto-reconnect in 0s...", messages);
        }

        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.Equal(TimeSpan.FromSeconds(310.25), state.Run[0].Comparisons["[Race] Rival#0001"].RealTime);
        Assert.Equal(0, disconnected);

        // 7. Splits after the reconnect still reach the race; the last is the finish and sends .done.
        session.Execute(TimerCommand.StartOrSplit);
        session.Execute(TimerCommand.StartOrSplit);
        await Until(() => Splits(server).Count == 3, "remaining splits sent");
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.Equal("Corona Mountain", Splits(server)[2].GetProperty("data").GetProperty("split").GetString());
        Assert.True(Splits(server)[2].GetProperty("data").GetProperty("is_finish").GetBoolean());
        Assert.True(SentMessage(server, ".done"));
        TimeSpan? finalTime = state.Run[2].SplitTime.RealTime;
        Assert.NotNull(finalTime);

        // 8. The server confirms: I'm done, the rival finishes, the third forfeits, the race ends.
        //    My time and the ended timer stay as they are.
        server.Me = "done";
        server.Push();
        server.Rival = "done";
        server.Third = "dnf";
        server.Push();
        server.Status = "finished";
        server.Push();
        await Until(() => channel.Race?.Version == server.Version, "race finished");
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.Equal(finalTime, state.Run[2].SplitTime.RealTime);
        Assert.Equal(3, Splits(server).Count);

        // 9. Leaving the race: no reconnect, the race comparisons go away.
        channel.Disconnect();
        await connection.WaitAsync(TimeSpan.FromSeconds(5));
        channel.RemoveRaceComparisons();
        Assert.Equal(2, server.Connections.Count);
        Assert.False(channel.IsConnected);
        Assert.DoesNotContain(state.Run.Comparisons, c => c.StartsWith("[Race] ", StringComparison.Ordinal));
        Assert.Equal(Run.PersonalBestComparisonName, state.CurrentComparison);
    }
}
