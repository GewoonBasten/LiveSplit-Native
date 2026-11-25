using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Racing;

/// <summary>
/// Port of LiveSplit.Racetime's RacetimeChannel: the race's websocket. racetime.gg's race data
/// starts and syncs the timer (countdown offset, start time, finish and forfeit), the other
/// entrants' splits fill their "[Race] name" comparisons, and the timer's splits, undos and resets
/// go back to the race. Every timer change is made on the session's thread.
/// </summary>
public sealed class RacetimeChannel
{
    public static readonly int[] ReconnectDelays = [0, 5, 5, 10, 10, 10, 15];

    private static readonly Regex CommandRegex = new(@"^\.([a-z]+)\s*?(.+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly TimerSession _session;
    private readonly RacetimeAuthenticator _authenticator;
    private readonly Func<IRacetimeSocket> _socketFactory;
    private readonly Action<Action> _post;
    private readonly Func<TimeSpan, Task> _delay;
    private readonly List<int> _versions = [];
    private IRacetimeSocket? _socket;
    private CancellationTokenSource? _websocketCts;

    // reconnect_cts: leaving the race stops reconnecting (upstream cancels it but never checks it,
    // so a closed race window kept reconnecting).
    private readonly CancellationTokenSource _reconnectCts = new();
    private int _currentSplit;

    public RacetimeChannel(
        TimerSession session,
        RacetimeSettings settings,
        RacetimeAuthenticator authenticator,
        Func<IRacetimeSocket> socketFactory,
        Action<Action> post,
        Func<TimeSpan, Task>? delay = null)
    {
        _session = session;
        Settings = settings;
        _authenticator = authenticator;
        _socketFactory = socketFactory;
        _post = post;
        _delay = delay ?? (time => Task.Delay(time));

        Model.OnSplit += State_OnSplit;
        Model.OnUndoSplit += State_OnUndoSplit;
        Model.OnReset += State_OnReset;
        Model.OnPause += Model_OnPause;
    }

    public RacetimeSettings Settings { get; }

    public RacetimeRace? Race { get; private set; }

    public UserStatus PersonalStatus => GetPersonalStatus(Race);

    public int ConnectionError { get; set; }

    public bool IsConnected { get; private set; }

    public string? Username { get; private set; }

    public string? UserId { get; private set; }

    public TimeSpan Offset { get; set; }

    /// <summary>The authenticator, once a connection was authorized (the chat page's token).</summary>
    public RacetimeAuthenticator? Token { get; private set; }

    public string FullWebRoot => RacetimeApi.BaseUri;

    private TimerModel Model => _session.Model;

    private LiveSplitState State => _session.State;

    public event EventHandler? ChannelJoined;

    public event EventHandler? Disconnected;

    public event EventHandler? RaceChanged;

    public event EventHandler? AuthenticationFailed;

    public event EventHandler? Authorized;

    /// <summary>SendSystemMessage (LiveSplitMessage): text and whether it is important.</summary>
    public event Action<string, bool>? SystemMessage;

    /// <summary>Every message received and every command sent (RawMessageReceived).</summary>
    public event Action<string>? RawMessageReceived;

    // Pausing is not allowed in a race: a pause is undone at once.
    private void Model_OnPause(object? sender, EventArgs e) => Model.Pause();

    private UserStatus GetPersonalStatus(RacetimeRace? race)
    {
        string? name = _authenticator.Identity?.Name;
        RacetimeEntrant? user = race?.Entrants.FirstOrDefault(x => name is not null && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        return user?.Status ?? UserStatus.Unknown;
    }

    /// <summary>Connect: "game/slug" joins ws/o/race/slug; up to 5 tries, 5 s apart.</summary>
    public async Task ConnectAsync(string id)
    {
        string slug = id.Contains('/') ? id.Split('/')[1] : id;
        for (int attempts = 0; attempts < 5; attempts++)
        {
            try
            {
                await RunAsync(slug);
                break;
            }
            catch (Exception)
            {
                IsConnected = false;
            }

            if (_reconnectCts.IsCancellationRequested)
            {
                break;
            }

            await _delay(TimeSpan.FromSeconds(5));
        }
    }

    public async Task RunAsync(string id)
    {
        while (true)
        {
            if (IsConnected)
            {
                return;
            }

            _websocketCts = new CancellationTokenSource();
            CancellationToken cancel = _websocketCts.Token;
            bool silent = false;
            using (IRacetimeSocket socket = _socketFactory())
            {
                _socket = socket;
                IsConnected = true;
                bool connected = await ConnectSocketAsync(socket, id, cancel);
                if (connected)
                {
                    while (socket.IsOpen && !cancel.IsCancellationRequested)
                    {
                        if (!await ReceiveAndProcessAsync(socket, cancel) && !socket.IsOpen)
                        {
                            break;
                        }
                    }

                    if (socket.ClosedCleanly)
                    {
                        ConnectionError = -1;
                    }
                    else if (!cancel.IsCancellationRequested)
                    {
                        ConnectionError++;
                    }
                }
                else if (_pendingRestart)
                {
                    _pendingRestart = false;
                    IsConnected = false;
                    _socket = null;
                    continue;
                }
                else
                {
                    silent = _stale;
                    _stale = false;
                }
            }

            _socket = null;
            if (!silent)
            {
                IsConnected = false;
                if (ConnectionError >= 0 && !_reconnectCts.IsCancellationRequested)
                {
                    int delay = ReconnectDelays[Math.Min(ReconnectDelays.Length - 1, ConnectionError)];
                    SendSystemMessage($"Auto-reconnect in {delay}s...");
                    await _delay(TimeSpan.FromSeconds(delay));
                    if (!_reconnectCts.IsCancellationRequested)
                    {
                        continue;
                    }
                }

                SendSystemMessage("Disconnect");
            }

            _websocketCts?.Dispose();
            _websocketCts = null;
            Post(() => Disconnected?.Invoke(this, EventArgs.Empty));
            return;
        }
    }

    private bool _pendingRestart;
    private bool _stale;

    /// <summary>Authorize, open the socket, getrace and (first time) gethistory; false = cleanup.</summary>
    private async Task<bool> ConnectSocketAsync(IRacetimeSocket socket, string id, CancellationToken cancel)
    {
        AuthResult result = await _authenticator.AuthorizeAsync();
        Username = _authenticator.Identity?.Name;
        UserId = _authenticator.Identity?.Id;
        switch (result)
        {
            case AuthResult.Success:
                SendSystemMessage($"Authorization successful. Hello, {_authenticator.Identity?.Name}");
                Post(() => Authorized?.Invoke(this, EventArgs.Empty));
                break;
            case AuthResult.Failure:
                SendSystemMessage(_authenticator.Error ?? "", true);
                Post(() => AuthenticationFailed?.Invoke(this, EventArgs.Empty));
                ConnectionError++;
                return false;
            case AuthResult.Cancelled:
                SendSystemMessage("Authorization declined by user.");
                ConnectionError = -1;
                return false;
            case AuthResult.Pending:
                _authenticator.StopPendingAuthRequest();
                _pendingRestart = true;
                return false;
            case AuthResult.Stale:
                _stale = true;
                return false;
        }

        Token = _authenticator;
        try
        {
            await socket.ConnectAsync(new Uri(RacetimeApi.SocketRoot + "ws/o/race/" + id), _authenticator.AccessToken ?? "", cancel);
        }
        catch (Exception)
        {
            ConnectionError++;
            return false;
        }

        if (socket.IsOpen)
        {
            Post(() => ChannelJoined?.Invoke(this, EventArgs.Empty));
            SendSystemMessage($"Joined Channel '{id}'");
            try
            {
                _ = socket.SendAsync("{ \"action\":\"getrace\" }", CancellationToken.None);
                await ReceiveAndProcessAsync(socket, cancel);
            }
            catch (Exception)
            {
                SendSystemMessage("Unable to obtain Race information. Try reloading");
                return false;
            }

            try
            {
                // Not after every reconnect.
                if (ConnectionError >= 0 && Settings.LoadChatHistory)
                {
                    SendSystemMessage("Loading chat history...");
                    _ = socket.SendAsync("{ \"action\":\"gethistory\" }", CancellationToken.None);
                    await ReceiveAndProcessAsync(socket, cancel);
                }
            }
            catch (Exception)
            {
                SendSystemMessage("Unable to load chat history");
            }
        }

        ConnectionError = -1;
        return true;
    }

    private async Task<bool> ReceiveAndProcessAsync(IRacetimeSocket socket, CancellationToken cancel)
    {
        string? message;
        try
        {
            message = await socket.ReceiveAsync(cancel);
            if (cancel.IsCancellationRequested || message is null)
            {
                return false;
            }
        }
        catch (Exception)
        {
            return false;
        }

        Post(() => RawMessageReceived?.Invoke(message));
        _session.Update((_, _) => Process(message), SessionChange.Timer | SessionChange.Comparison);
        return true;
    }

    /// <summary>One message from racetime.gg (Parse + the race / split handling), on the session's thread.</summary>
    internal void Process(string message)
    {
        try
        {
            using JsonDocument json = JsonDocument.Parse(message);
            JsonElement root = json.RootElement;
            switch (Json.String(root, "type"))
            {
                case "race.data":
                    RacetimeRace race = RacetimeRace.From(Json.Get(root, "race"));
                    if (!_versions.Contains(race.Version))
                    {
                        _versions.Add(race.Version);
                        UpdateRaceData(race);
                    }

                    break;
                case "race.split":
                    UpdateSplitComparison(RacetimeSplitUpdate.From(Json.Get(root, "split")));
                    break;
            }
        }
        catch (Exception)
        {
        }
    }

    private void UpdateRaceData(RacetimeRace newRace)
    {
        TimerModel m = Model;
        LiveSplitState state = State;
        RaceState r = Race?.State ?? RaceState.Unknown;
        RaceState nr = newRace.State;
        UserStatus u = GetPersonalStatus(Race);
        UserStatus nu = GetPersonalStatus(newRace);

        Race = newRace;
        UpdateRaceComparisons(newRace);

        // Only when the race's or the runner's state changed.
        if (r != nr || u != nu)
        {
            // We are (now) part of the race.
            if (nu is not UserStatus.NotInRace and not UserStatus.Unknown)
            {
                if (r is RaceState.Open or RaceState.OpenInviteOnly && nr == RaceState.Starting)
                {
                    // The race is starting.
                    m.Start();
                }
                else if (nr == RaceState.Started && nu == UserStatus.Racing)
                {
                    // The race is running and we're not finished: sync the timer.
                    if (state.CurrentPhase == TimerPhase.Ended)
                    {
                        try
                        {
                            state.CurrentSplitIndex = _currentSplit;
                            m.UndoSplit();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    else if (state.CurrentPhase == TimerPhase.Paused)
                    {
                        m.Pause();
                    }
                    else if (state.CurrentPhase == TimerPhase.NotRunning)
                    {
                        state.Run.Offset = DateTime.UtcNow - newRace.StartedAt;
                        state.AdjustedStartTime = state.StartTimeWithOffset = TimeStamp.Now - state.Run.Offset;
                        m.Start();
                    }
                    else
                    {
                        state.Run.Offset = Offset;
                    }
                }
                else if (nr is RaceState.Open or RaceState.OpenInviteOnly)
                {
                    // Nothing has started yet: the countdown.
                    if (Offset.TotalMilliseconds == 0)
                    {
                        Offset = state.Run.Offset;
                    }

                    state.Run.Offset = newRace.StartDelay.Negate();
                    state.AdjustedStartTime = TimeStamp.Now - state.Run.Offset;
                }
                else if (nr == RaceState.Started && nu is UserStatus.Finished or UserStatus.Forfeit && u is UserStatus.Racing or UserStatus.Finished)
                {
                    try
                    {
                        _currentSplit = state.CurrentSplitIndex;
                        for (int i = 0; i < 300; i++)
                        {
                            m.SkipSplit();
                        }
                    }
                    catch (Exception)
                    {
                    }

                    m.Split();
                }
            }
        }

        RaceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSplitComparison(RacetimeSplitUpdate split)
    {
        if (split.UserId == UserId)
        {
            return;
        }

        Run run = State.Run;
        RacetimeEntrant? user = Race?.Entrants.FirstOrDefault(x => x.Id == split.UserId);
        if (user is null || run.Count == 0)
        {
            return;
        }

        string comparisonName = RacetimeComparisonGenerator.GetRaceComparisonName(user.FullName);
        bool Matches(Segment segment) => segment.Name.Trim().ToLowerInvariant() == split.SplitName;
        TimeSpan? Time(Segment segment) => segment.Comparisons.TryGetValue(comparisonName, out SplitTime time) ? time.RealTime : null;

        Segment? segment = run.Segments.FirstOrDefault(x => Matches(x) && Time(x) is null);
        if (split.IsUndo)
        {
            segment = run.Segments.LastOrDefault(x => Matches(x) && Time(x) is not null);
        }

        if (split.IsFinish)
        {
            segment = run[^1];
        }

        if (segment is not null)
        {
            SplitTime newTime = segment.Comparisons.TryGetValue(comparisonName, out SplitTime old) ? old : default;
            newTime.RealTime = split.SplitTime;
            segment.Comparisons[comparisonName] = newTime;
        }
    }

    private void UpdateRaceComparisons(RacetimeRace race)
    {
        foreach (RacetimeEntrant entrant in race.Entrants)
        {
            if (entrant.Name != Username)
            {
                AddComparison(entrant.FullName);
            }
        }
    }

    private void AddComparison(string userName)
    {
        Run run = State.Run;
        string comparisonName = RacetimeComparisonGenerator.GetRaceComparisonName(userName);
        if (run.ComparisonGenerators.All(x => x.Name != comparisonName))
        {
            StandardComparisonGeneratorsFactory.AddShortComparisonName(comparisonName, userName);
            run.ComparisonGenerators.Add(new RacetimeComparisonGenerator(run, comparisonName));
        }
    }

    /// <summary>The race window closing: the "[Race]" comparisons go (Personal Best if one was shown).</summary>
    public void RemoveRaceComparisons() => _session.Update((_, state) =>
    {
        if (RacetimeComparisonGenerator.IsRaceComparison(state.CurrentComparison))
        {
            state.CurrentComparison = Run.PersonalBestComparisonName;
        }

        state.Run.ComparisonGenerators.RemoveAll(x => RacetimeComparisonGenerator.IsRaceComparison(x.Name));
        foreach (Segment segment in state.Run.Segments)
        {
            foreach (string key in segment.Comparisons.Keys.Where(RacetimeComparisonGenerator.IsRaceComparison).ToList())
            {
                segment.Comparisons[key] = default;
            }
        }
    }, SessionChange.Comparison);

    private void State_OnReset(object? sender, TimerPhase value)
    {
        if (PersonalStatus == UserStatus.Racing)
        {
            SendChannelMessage(".forfeit");
        }
    }

    private void State_OnUndoSplit(object? sender, EventArgs e)
    {
        if (State.CurrentSplitIndex == State.Run.Count - 1 && PersonalStatus != UserStatus.Racing)
        {
            Undone();
        }

        if (PersonalStatus == UserStatus.Racing && State.CurrentSplit is { } split)
        {
            SendChannelCommand(new JsonObject
            {
                ["action"] = "split",
                ["data"] = new JsonObject { ["split"] = split.Name, ["time"] = "-" },
            }.ToJsonString());
        }
    }

    private void State_OnSplit(object? sender, EventArgs e)
    {
        if (PersonalStatus == UserStatus.Racing && State.CurrentSplitIndex > 0)
        {
            Segment split = State.Run[State.CurrentSplitIndex - 1];
            var formatter = new GeneralTimeFormatter
            {
                Accuracy = TimeAccuracy.Hundredths,
                NullFormat = NullFormat.ZeroWithAccuracy,
                DigitsFormat = DigitsFormat.SingleDigitMinutes,
            };
            SendChannelCommand(new JsonObject
            {
                ["action"] = "split",
                ["data"] = new JsonObject
                {
                    ["split"] = split.Name,
                    ["is_finish"] = State.CurrentSplitIndex >= State.Run.Count,
                    ["time"] = formatter.Format(split.SplitTime.RealTime),
                },
            }.ToJsonString());
        }

        if (State.CurrentSplitIndex >= State.Run.Count && PersonalStatus == UserStatus.Racing)
        {
            SendChannelMessage(".done");
        }
    }

    /// <summary>Leaves the race: the socket closes and the timer is no longer followed.</summary>
    public void Disconnect()
    {
        if (IsConnected)
        {
            _websocketCts?.Cancel();
        }

        _reconnectCts.Cancel();

        Model.OnPause -= Model_OnPause;
        Model.OnSplit -= State_OnSplit;
        Model.OnReset -= State_OnReset;
        Model.OnUndoSplit -= State_OnUndoSplit;
    }

    /// <summary>Forfeit: a reset, which sends .forfeit while racing.</summary>
    public void Forfeit()
    {
        if (PersonalStatus == UserStatus.Racing)
        {
            _session.Execute(TimerCommand.Reset);
        }
    }

    /// <summary>CreateCommand: ".action text" is {"action":…,"data":{action:text}}; other text is a chat message.</summary>
    public static string CreateCommand(string message)
    {
        var command = new JsonObject();
        Match match = CommandRegex.Match(message);
        if (match.Success)
        {
            string action = match.Groups[1].Value.ToLowerInvariant().Trim();
            command["action"] = action;
            if (match.Groups[2].Value.Trim().Length > 0)
            {
                command["data"] = new JsonObject { [action] = match.Groups[2].Value.Trim() };
            }
        }
        else
        {
            command["action"] = "message";
            command["data"] = new JsonObject { ["message"] = message, ["guid"] = Guid.NewGuid().ToString() };
        }

        return command.ToJsonString();
    }

    public void SendChannelCommand(string data)
    {
        RawMessageReceived?.Invoke(data);
        if (IsConnected && _socket is { } socket && _websocketCts is { } cts)
        {
            _ = SendQuietlyAsync(socket, data, cts.Token);
        }
    }

    private static async Task SendQuietlyAsync(IRacetimeSocket socket, string data, CancellationToken cancel)
    {
        try
        {
            await socket.SendAsync(data, cancel);
        }
        catch (Exception)
        {
        }
    }

    public void SendChannelMessage(string message) => SendChannelCommand(CreateCommand(message.Trim()));

    private void SendSystemMessage(string message, bool important = false) =>
        Post(() => SystemMessage?.Invoke(message, important));

    public void Ready() => SendChannelMessage(".ready");

    public void Quit() => SendChannelMessage(".quit");

    public void Enter() => SendChannelMessage(".enter");

    public void Accept() => SendChannelMessage(".acceptinvite");

    public void Unready() => SendChannelMessage(".unready");

    public void Done() => _session.Update((model, _) => model.Split(), SessionChange.Timer | SessionChange.Comparison);

    public void Undone() => SendChannelMessage(".undone");

    private void Post(Action action) => _post(action);
}
