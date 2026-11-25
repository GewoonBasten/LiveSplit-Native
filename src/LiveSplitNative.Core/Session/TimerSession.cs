using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Session;

/// <summary>
/// The one owner of timer state (F1). Every input reaches the timer as a call here; the session
/// applies it on the dispatcher's thread and raises <see cref="Changed"/>, and views redraw from
/// that event instead of being refreshed by whoever changed something. It never touches a window.
/// </summary>
public sealed class TimerSession
{
    private readonly Func<ComparisonGeneratorSettings> _generatorSettings;
    private readonly ISessionDispatcher _dispatcher;

    public TimerSession(
        LiveSplitState state,
        Func<ComparisonGeneratorSettings> generatorSettings,
        ISessionDispatcher? dispatcher = null)
    {
        State = state;
        _generatorSettings = generatorSettings;
        _dispatcher = dispatcher ?? ImmediateSessionDispatcher.Instance;
        Model = new TimerModel { CurrentState = state };

        // Windows TimerForm.CurrentState_OnReset regenerates before anything redraws.
        Model.OnReset += (_, _) => State.Run.RegenerateComparisons(_generatorSettings());
    }

    public LiveSplitState State { get; }

    /// <summary>The timer model, for its OnSplit / OnReset … events. Change state through the session.</summary>
    public TimerModel Model { get; }

    public event EventHandler<SessionChangedEventArgs>? Changed;

    public void Execute(TimerCommand command) => OnDispatcher(() =>
    {
        SessionChange change = Apply(command);
        Raise(change);
    });

    /// <summary>
    /// Windows hotkey delay: the Split and Pause hotkeys wait HotkeyDelay seconds before acting;
    /// every other command acts at once.
    /// </summary>
    public void ExecuteFromHotkey(TimerCommand command, TimeSpan hotkeyDelay)
    {
        if (hotkeyDelay > TimeSpan.Zero && UsesHotkeyDelay(command))
        {
            _ = Task.Delay(hotkeyDelay).ContinueWith(_ => Execute(command), TaskScheduler.Default);
            return;
        }

        Execute(command);
    }

    /// <summary>Windows TimerForm.hook_KeyOrButtonPressed delays only the Split and Pause keys.</summary>
    public static bool UsesHotkeyDelay(TimerCommand command) =>
        command is TimerCommand.StartOrSplit or TimerCommand.Pause;

    /// <summary>
    /// TimerForm_MouseWheel: TimerModel.ScrollUp / ScrollDown. Only the split lists move (their
    /// components keep the scroll position), so nothing else changes.
    /// </summary>
    public void Scroll(bool up) => OnDispatcher(() =>
    {
        if (up)
        {
            Model.ScrollUp();
        }
        else
        {
            Model.ScrollDown();
        }

        Raise(SessionChange.None);
    });

    /// <summary>TimerForm.SwitchComparison: a comparison the run does not have becomes Personal Best.</summary>
    public void SwitchComparison(string name) => OnDispatcher(() =>
    {
        ComparisonSwitching.SwitchComparison(State, name);
        Raise(SessionChange.Comparison);
    });

    public void SetTimingMethod(TimingMethod method) => OnDispatcher(() =>
    {
        State.CurrentTimingMethod = method;
        Raise(SessionChange.Comparison);
    });

    /// <summary>TimerForm.RegenerateComparisons (after a settings change such as Simple Sum of Best).</summary>
    public void RegenerateComparisons() => OnDispatcher(() =>
    {
        State.Run.RegenerateComparisons(_generatorSettings());
        Raise(SessionChange.Comparison);
    });

    /// <summary>TimerForm.SwitchComparisonGenerators with the Choose Active Comparisons states.</summary>
    public void SwitchComparisonGenerators(IReadOnlyDictionary<string, bool> generatorStates) => OnDispatcher(() =>
    {
        ComparisonSwitching.SwitchComparisonGenerators(State, generatorStates, _generatorSettings());
        Raise(SessionChange.Comparison);
    });

    /// <summary>
    /// Sets a new or edited run (TimerForm.SetRun: generators carried over, comparisons
    /// regenerated, comparison kept when present) and puts the timer at the given phase and split.
    /// </summary>
    public void SetRun(Run run, TimerPhase phase, int splitIndex) => OnDispatcher(() =>
    {
        ComparisonSwitching.SetRun(State, run, _generatorSettings());
        State.CurrentPhase = phase;
        State.CurrentSplitIndex = splitIndex;
        Raise(SessionChange.Run | SessionChange.Comparison | SessionChange.Timer);
    });

    /// <summary>
    /// "Save as Personal Best?" answered Yes (TimerForm.SaveSplits): TimerModel.ResetAndSetAttemptAsPB,
    /// which ends the attempt and makes it the PB even though it was slower.
    /// </summary>
    public void ResetAndSetAttemptAsPersonalBest() => OnDispatcher(() =>
    {
        Model.ResetAndSetAttemptAsPb();
        Raise(SessionChange.Timer | SessionChange.Comparison);
    });

    /// <summary>
    /// A change a plugin makes to the timer directly (racetime.gg syncs the start time and
    /// splits with the race), applied on the dispatcher's thread like every other input.
    /// </summary>
    public void Update(Action<TimerModel, LiveSplitState> change, SessionChange changed) => OnDispatcher(() =>
    {
        change(Model, State);
        Raise(changed);
    });

    private SessionChange Apply(TimerCommand command)
    {
        switch (command)
        {
            case TimerCommand.StartOrSplit:
                StartOrSplit();
                return SessionChange.Timer | SessionChange.Comparison;
            case TimerCommand.Reset:
                Model.Reset();
                return SessionChange.Timer | SessionChange.Comparison;
            case TimerCommand.ResetWithoutSaving:
                Model.Reset(false);
                return SessionChange.Timer | SessionChange.Comparison;
            case TimerCommand.UndoSplit:
                Model.UndoSplit();
                return SessionChange.Timer;
            case TimerCommand.SkipSplit:
                Model.SkipSplit();
                return SessionChange.Timer;
            case TimerCommand.Pause:
                Model.Pause();
                return SessionChange.Timer;
            case TimerCommand.UndoAllPauses:
                Model.UndoAllPauses();
                return SessionChange.Timer;
            case TimerCommand.SwitchComparisonPrevious:
                Model.SwitchComparisonPrevious();
                return SessionChange.Comparison;
            case TimerCommand.SwitchComparisonNext:
                Model.SwitchComparisonNext();
                return SessionChange.Comparison;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }

    // Windows TimerForm.StartOrSplit.
    private void StartOrSplit()
    {
        switch (State.CurrentPhase)
        {
            case TimerPhase.Running:
                Model.Split();
                break;
            case TimerPhase.Paused:
                Model.Pause();
                break;
            case TimerPhase.NotRunning:
                Model.Start();
                break;
            case TimerPhase.Ended:
                Model.Reset();
                break;
        }
    }

    private void OnDispatcher(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Post(action);
        }
    }

    private void Raise(SessionChange change) => Changed?.Invoke(this, new SessionChangedEventArgs(change));
}
