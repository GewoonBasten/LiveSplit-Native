namespace LiveSplitNative.Core.Session;

/// <summary>
/// Everything an input (context menu, hotkey, later the command server and autosplitters)
/// can ask the timer to do. Each maps to what Windows TimerForm does for that input.
/// </summary>
public enum TimerCommand
{
    /// <summary>
    /// Windows TimerForm.StartOrSplit (Start/Split hotkey and menu item): start when not running,
    /// split while running, resume while paused, reset once the run has ended.
    /// </summary>
    StartOrSplit,

    /// <summary>TimerModel.Reset(): ends the attempt and keeps its times (Windows asks first when Warn On Reset applies).</summary>
    Reset,

    /// <summary>TimerModel.Reset(false): ends the attempt without updating splits.</summary>
    ResetWithoutSaving,

    UndoSplit,
    SkipSplit,

    /// <summary>TimerModel.Pause(): pauses a running timer, resumes a paused one.</summary>
    Pause,

    UndoAllPauses,
    SwitchComparisonPrevious,
    SwitchComparisonNext,
}

/// <summary>What changed, so each view redraws only what it shows.</summary>
[Flags]
public enum SessionChange
{
    None = 0,

    /// <summary>Phase, split index, times or pauses.</summary>
    Timer = 1,

    /// <summary>Current comparison, timing method, or the generated comparison values.</summary>
    Comparison = 2,

    /// <summary>A different or edited run: segment count and names may differ, so layouts recompute their size.</summary>
    Run = 4,
}

public sealed class SessionChangedEventArgs(SessionChange change) : EventArgs
{
    public SessionChange Change { get; } = change;
}

/// <summary>
/// Where session work runs. The app passes the UI thread; a command from any other thread
/// (a timer, later a global hotkey or the server) is posted there, as Windows does with Invoke.
/// </summary>
public interface ISessionDispatcher
{
    bool CheckAccess();

    void Post(Action action);
}

/// <summary>Runs everything at once on the calling thread (tests, headless tools).</summary>
public sealed class ImmediateSessionDispatcher : ISessionDispatcher
{
    public static readonly ImmediateSessionDispatcher Instance = new();

    public bool CheckAccess() => true;

    public void Post(Action action) => action();
}
