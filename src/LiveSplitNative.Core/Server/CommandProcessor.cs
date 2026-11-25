using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Server;

/// <summary>What the server's commands need from the app (files, screenshots, hotkeys).</summary>
public interface ICommandHost
{
    HotkeyProfileStub HotkeyProfile { get; }

    string HotkeyProfileName { get; }

    string? LayoutPath { get; }

    string? SplitsPath { get; }

    string Version { get; }

    ServerStateType ServerState { get; }

    /// <summary>Saves the layout (to <paramref name="path"/> when given); true when it was saved.</summary>
    Task<bool> SaveLayoutAsync(string? path);

    /// <summary>Saves the splits (to <paramref name="path"/> when given); true when they were saved.</summary>
    Task<bool> SaveSplitsAsync(string? path);

    Task<bool> OpenLayoutAsync(string path);

    Task<bool> OpenSplitsAsync(string path);

    /// <summary>The timer window as a PNG, or null.</summary>
    Task<byte[]?> ScreenshotAsync();

    /// <summary>The global hotkeys setting changed (RefreshHotkeyHooks).</summary>
    void HotkeysChanged();
}

/// <summary>
/// Port of LiveSplit.Server.CommandServer.ProcessMessage: one line from a client ("split",
/// "getcurrenttime", "setgametime 1:23.4", ...) applied to the timer, and the answer, if any.
/// Times are answered in .NET's "c" format with "-" for none (PreciseTimeFormatter). Runs on the
/// session's thread.
/// </summary>
public sealed class CommandProcessor
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly TimerSession _session;
    private readonly ICommandHost _host;

    public CommandProcessor(TimerSession session, ICommandHost host)
    {
        _session = session;
        _host = host;
        _session.Model.OnStart += (_, _) =>
        {
            if (AlwaysPauseGameTime)
            {
                State.IsGameTimePaused = true;
            }
        };
    }

    /// <summary>alwayspausegametime: game time is paused at every start until unpausegametime.</summary>
    public bool AlwaysPauseGameTime { get; private set; }

    private LiveSplitState State => _session.State;

    /// <summary>PreciseTimeFormatter: TimeSpan's "c" format, "-" for none.</summary>
    public static string Format(TimeSpan? time) => time?.ToString("c", CultureInfo.InvariantCulture) ?? "-";

    private static TimeSpan? ParseTime(string text) => text == "-" ? null : TimeSpanParser.Parse(text);

    private void Change(Action<TimerModel, LiveSplitState> change, SessionChange changed = SessionChange.Timer | SessionChange.Comparison) =>
        _session.Update(change, changed);

    public async Task<string?> ProcessAsync(string message)
    {
        string? response = null;
        string[] args = message.Split([' '], 2);
        string command = args[0];
        string Arg() => args[1];
        try
        {
            switch (command)
            {
                case "startorsplit":
                    Change((m, s) =>
                    {
                        if (s.CurrentPhase == TimerPhase.Running)
                        {
                            m.Split();
                        }
                        else
                        {
                            m.Start();
                        }
                    });
                    break;
                case "split":
                    Change((m, _) => m.Split());
                    break;
                case "undosplit":
                case "unsplit":
                    Change((m, _) => m.UndoSplit());
                    break;
                case "skipsplit":
                    Change((m, _) => m.SkipSplit());
                    break;
                case "pause":
                    Change((m, s) =>
                    {
                        if (s.CurrentPhase != TimerPhase.Paused)
                        {
                            m.Pause();
                        }
                    });
                    break;
                case "undoallpauses":
                    Change((m, _) => m.UndoAllPauses());
                    break;
                case "resume":
                    Change((m, s) =>
                    {
                        if (s.CurrentPhase == TimerPhase.Paused)
                        {
                            m.Pause();
                        }
                    });
                    break;
                case "reset":
                    Change((m, _) => m.Reset());
                    break;
                case "start":
                case "starttimer":
                    Change((m, _) => m.Start());
                    break;
                case "setgametime":
                {
                    TimeSpan? time = ParseTime(Arg());
                    Change((_, s) => s.SetGameTime(time));
                    break;
                }
                case "setloadingtimes":
                {
                    TimeSpan? time = ParseTime(Arg());
                    Change((_, s) => s.LoadingTimes = time ?? TimeSpan.Zero);
                    break;
                }
                case "addloadingtimes":
                {
                    TimeSpan? time = ParseTime(Arg());
                    Change((_, s) => s.LoadingTimes += time ?? TimeSpan.Zero);
                    break;
                }
                case "pausegametime":
                    Change((_, s) => s.IsGameTimePaused = true);
                    break;
                case "unpausegametime":
                    AlwaysPauseGameTime = false;
                    Change((_, s) => s.IsGameTimePaused = false);
                    break;
                case "alwayspausegametime":
                    AlwaysPauseGameTime = true;
                    Change((_, s) => s.IsGameTimePaused = true);
                    break;
                case "getgamename":
                    response = State.Run.GameName;
                    break;
                case "getcategoryname":
                    response = State.Run.CategoryName;
                    break;
                case "getcategoryvariables":
                {
                    // Without speedrun.com's game: the region and platform names as saved, every variable.
                    RunMetadata md = State.Run.Metadata;
                    string? region = md.RegionName.Length > 0 ? md.RegionName : null;
                    string? platform = md.PlatformName.Length > 0 ? md.PlatformName : null;
                    var variables = new Dictionary<string, string>(md.VariableValueNames, StringComparer.Ordinal);
                    response = JsonSerializer.Serialize(new CategoryMetadata(region, platform, md.UsesEmulator, variables), SerializerOptions);
                    break;
                }
                case "getdelta":
                {
                    string comparison = args.Length > 1 ? args[1] : State.CurrentComparison;
                    TimeSpan? delta = null;
                    if (State.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
                    {
                        delta = LiveSplitStateHelper.GetLastDelta(State, State.CurrentSplitIndex, comparison, State.CurrentTimingMethod);
                    }
                    else if (State.CurrentPhase == TimerPhase.Ended)
                    {
                        delta = State.Run[^1].SplitTime[State.CurrentTimingMethod] - Comparison(State.Run[^1], comparison);
                    }

                    response = Format(delta);
                    break;
                }
                case "getsplitindex":
                    response = State.CurrentSplitIndex.ToString(CultureInfo.InvariantCulture);
                    break;
                case "getsplitcount":
                    response = State.Run.Count.ToString(CultureInfo.InvariantCulture);
                    break;
                case "getsplitname":
                {
                    if (!int.TryParse(Arg(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                    {
                        break;
                    }

                    if (index < 0)
                    {
                        index = State.Run.Count - Math.Abs(index);
                    }

                    response = index >= 0 && index < State.Run.Count ? State.Run[index].Name : "-";
                    break;
                }
                case "getcurrentsplitname":
                    response = State.CurrentSplit?.Name ?? "-";
                    break;
                case "getlastsplitname":
                case "getprevioussplitname":
                    response = State.CurrentSplitIndex > 0 ? State.Run[State.CurrentSplitIndex - 1].Name : "-";
                    break;
                case "getnextsplitname":
                case "getupcomingsplitname":
                    response = State.CurrentSplitIndex < State.Run.Count - 1 ? State.Run[State.CurrentSplitIndex + 1].Name : "-";
                    break;
                case "getlastsplittime":
                case "getprevioussplittime":
                    response = State.CurrentSplitIndex > 0
                        ? Format(State.Run[State.CurrentSplitIndex - 1].SplitTime[State.CurrentTimingMethod])
                        : "-";
                    break;
                case "getcurrentsplittime":
                case "getcomparisonsplittime":
                    if (State.CurrentSplit is { } current)
                    {
                        string comparison = args.Length > 1 ? args[1] : State.CurrentComparison;
                        response = Format(Comparison(current, comparison));
                    }
                    else
                    {
                        response = "-";
                    }

                    break;
                case "getcurrentrealtime":
                    response = Format(CurrentTime(TimingMethod.RealTime));
                    break;
                case "getcurrentgametime":
                    response = Format(CurrentTime(State.IsGameTimeInitialized ? TimingMethod.GameTime : TimingMethod.RealTime));
                    break;
                case "getcurrenttime":
                {
                    TimingMethod method = State.CurrentTimingMethod;
                    if (method == TimingMethod.GameTime && !State.IsGameTimeInitialized)
                    {
                        method = TimingMethod.RealTime;
                    }

                    response = Format(CurrentTime(method));
                    break;
                }
                case "getfinaltime":
                case "getfinalsplittime":
                {
                    string comparison = args.Length > 1 ? args[1] : State.CurrentComparison;
                    response = Format(State.CurrentPhase == TimerPhase.Ended
                        ? State.CurrentTime[State.CurrentTimingMethod]
                        : Comparison(State.Run[^1], comparison));
                    break;
                }
                case "getbestpossibletime":
                case "getpredictedtime":
                {
                    string comparison = command == "getbestpossibletime"
                        ? BestSegmentsComparisonGenerator.ComparisonName
                        : args.Length > 1 ? args[1] : State.CurrentComparison;
                    response = Format(PredictTime(comparison));
                    break;
                }
                case "getpausedrealtime":
                    response = Format(State.PauseTime);
                    break;
                case "getpausedgametime":
                    response = Format(State.GameTimePauseTime);
                    break;
                case "getoffset":
                    response = Format(State.Run.Offset);
                    break;
                case "gettimerphase":
                case "getcurrenttimerphase":
                    response = State.CurrentPhase.ToString();
                    break;
                case "getcomparisonname":
                    response = State.CurrentComparison;
                    break;
                case "setcomparison":
                {
                    string comparison = Arg();
                    Change((_, s) => s.CurrentComparison = comparison, SessionChange.Comparison);
                    break;
                }
                case "switchto":
                    switch (Arg())
                    {
                        case "gametime":
                            _session.SetTimingMethod(TimingMethod.GameTime);
                            break;
                        case "realtime":
                            _session.SetTimingMethod(TimingMethod.RealTime);
                            break;
                    }

                    break;
                case "gettimingmethod":
                    response = State.CurrentTimingMethod.ToString();
                    break;
                case "setsplitname":
                case "setcurrentsplitname":
                {
                    if (args.Length < 2)
                    {
                        break;
                    }

                    int index = State.CurrentSplitIndex;
                    string title = args[1];
                    if (command == "setsplitname")
                    {
                        string[] options = args[1].Split([' '], 2);
                        if (options.Length < 2 || !int.TryParse(options[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                        {
                            break;
                        }

                        title = options[1];
                    }

                    if (index < 0)
                    {
                        index = State.Run.Count - Math.Abs(index);
                    }

                    if (index >= 0 && index < State.Run.Count)
                    {
                        int at = index;
                        Change((_, s) =>
                        {
                            s.Run[at].Name = title;
                            s.Run.HasChanged = true;
                        }, SessionChange.Run);
                    }

                    break;
                }
                case "getcustomvariablevalue":
                {
                    string? value = State.Run.Metadata.CustomVariableValue(Arg());
                    response = string.IsNullOrEmpty(value) ? "-" : Regex.Replace(value, @"\r\n?|\n", " ");
                    break;
                }
                case "setcustomvariable":
                {
                    if (args.Length < 2)
                    {
                        break;
                    }

                    string[]? options;
                    try
                    {
                        options = JsonSerializer.Deserialize<string[]>(args[1]);
                    }
                    catch (JsonException)
                    {
                        break;
                    }

                    if (options is null || options.Length < 2)
                    {
                        break;
                    }

                    Change((_, s) =>
                    {
                        if (s.Run.Metadata.SetCustomVariable(options[0], options[1]))
                        {
                            s.Run.HasChanged = true;
                        }
                    }, SessionChange.None);
                    break;
                }
                case "globalhotkeysenabled":
                    response = _host.HotkeyProfile.GlobalHotkeysEnabled.ToString();
                    break;
                case "enableglobalhotkeys":
                    _host.HotkeyProfile.GlobalHotkeysEnabled = true;
                    _host.HotkeysChanged();
                    break;
                case "disableglobalhotkeys":
                    _host.HotkeyProfile.GlobalHotkeysEnabled = false;
                    _host.HotkeysChanged();
                    break;
                case "switchhotkeyprofile":
                    // One profile here (Default): switching to it changes nothing.
                    break;
                case "ping":
                    response = "pong";
                    break;
                case "getlayoutpath":
                    response = string.IsNullOrEmpty(_host.LayoutPath) ? "-" : _host.LayoutPath;
                    break;
                case "savelayout":
                case "savelayoutas":
                {
                    string? path = null;
                    if (command == "savelayoutas")
                    {
                        if (!Arg().EndsWith(".lsl", StringComparison.Ordinal))
                        {
                            break;
                        }

                        path = Arg();
                    }

                    response = (await _host.SaveLayoutAsync(path)).ToString();
                    break;
                }
                case "getsplitspath":
                    response = string.IsNullOrEmpty(_host.SplitsPath) ? "-" : _host.SplitsPath;
                    break;
                case "savesplits":
                case "savesplitsas":
                {
                    string? path = null;
                    if (command == "savesplitsas")
                    {
                        if (!Arg().EndsWith(".lss", StringComparison.Ordinal))
                        {
                            break;
                        }

                        path = Arg();
                    }

                    response = (await _host.SaveSplitsAsync(path)).ToString();
                    break;
                }
                case "switchlayout":
                    response = (await _host.OpenLayoutAsync(Arg())).ToString();
                    break;
                case "switchsplits":
                    response = (await _host.OpenSplitsAsync(Arg())).ToString();
                    break;
                case "getsplitsscreenshot":
                case "savesplitsscreenshot":
                {
                    byte[]? png = await _host.ScreenshotAsync();
                    if (png is null)
                    {
                        break;
                    }

                    if (command == "getsplitsscreenshot")
                    {
                        response = "data:image/png;base64," + Convert.ToBase64String(png);
                    }
                    else
                    {
                        bool success;
                        try
                        {
                            await File.WriteAllBytesAsync(Arg(), png);
                            success = true;
                        }
                        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                        {
                            success = false;
                        }

                        response = success.ToString();
                    }

                    break;
                }
                case "getattemptcount":
                    response = State.Run.AttemptCount.ToString(CultureInfo.InvariantCulture);
                    break;
                case "getcompletedcount":
                    response = State.Run.AttemptHistory.Count(x => x.Time.RealTime is not null).ToString(CultureInfo.InvariantCulture);
                    break;
                case "getautosplitterpath":
                    // No scriptable autosplitters (ASL) here.
                    response = "-";
                    break;
                case "autosplitteractivated":
                    response = false.ToString();
                    break;
                case "gethotkeyprofile":
                    response = _host.HotkeyProfileName;
                    break;
                case "getlivesplitversion":
                    response = _host.Version;
                    break;
                case "getlivesplitpath":
                    response = Environment.ProcessPath ?? "-";
                    break;
                case "getservertype":
                    response = _host.ServerState.ToString();
                    break;
            }
        }
        catch (Exception e) when (e is IndexOutOfRangeException or FormatException or OverflowException or ArgumentException)
        {
            // A missing or malformed argument (Log.Error upstream): no answer.
            response = null;
        }

        return string.IsNullOrEmpty(response) ? null : response;
    }

    /// <summary>A segment's comparison time in the current timing method (a missing comparison is none).</summary>
    private TimeSpan? Comparison(Segment segment, string comparison) =>
        ComparisonValue(segment, comparison, State.CurrentTimingMethod);

    private TimeSpan? CurrentTime(TimingMethod method) =>
        State.CurrentPhase == TimerPhase.NotRunning ? State.Run.Offset : State.CurrentTime[method];

    private TimeSpan? PredictTime(string comparison)
    {
        TimingMethod method = State.CurrentTimingMethod;
        if (State.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            TimeSpan? delta = LiveSplitStateHelper.GetLastDelta(State, State.CurrentSplitIndex, comparison, method) ?? TimeSpan.Zero;
            TimeSpan? liveDelta = State.CurrentTime[method] - ComparisonValue(State.CurrentSplit, comparison, method);
            if (liveDelta > delta)
            {
                delta = liveDelta;
            }

            return delta + ComparisonValue(State.Run[^1], comparison, method);
        }

        return State.CurrentPhase == TimerPhase.Ended
            ? State.Run[^1].SplitTime[method]
            : ComparisonValue(State.Run[^1], comparison, method);
    }

    private static TimeSpan? ComparisonValue(Segment? segment, string comparison, TimingMethod method) =>
        segment is not null && segment.Comparisons.TryGetValue(comparison, out SplitTime time) ? time[method] : null;

    private sealed record CategoryMetadata(string? Region, string? Platform, bool UsesEmulator, IReadOnlyDictionary<string, string> Variables);
}
