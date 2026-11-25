using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Model;

/// <summary>Port of LiveSplit.Model.LiveSplitState current-time / pause accounting.</summary>
public sealed class LiveSplitState
{
    public LiveSplitState(Run run)
    {
        Run = run;
        CurrentPhase = TimerPhase.NotRunning;
        CurrentSplitIndex = -1;
        CurrentComparison = "Personal Best";
        CurrentTimingMethod = TimingMethod.RealTime;
        AdjustedStartTime = StartTimeWithOffset = StartTime = TimeStamp.Now;
        TimePausedAt = TimeSpan.Zero;
    }

    public Run Run { get; set; }
    public AtomicDateTime AttemptStarted { get; set; }
    public AtomicDateTime AttemptEnded { get; set; }
    public TimeStamp AdjustedStartTime { get; set; }
    public TimeStamp StartTimeWithOffset { get; set; }
    public TimeStamp StartTime { get; set; }
    public TimeSpan TimePausedAt { get; set; }
    public TimeSpan? GameTimePauseTime { get; set; }
    public TimerPhase CurrentPhase { get; set; }
    public string CurrentComparison { get; set; }
    public TimingMethod CurrentTimingMethod { get; set; }
    public int CurrentSplitIndex { get; set; }

    private TimeSpan? _loadingTimes;

    public TimeSpan LoadingTimes
    {
        get => _loadingTimes ?? TimeSpan.Zero;
        set
        {
            _loadingTimes = value;
            if (IsGameTimePaused && CurrentTime.RealTime is { } rt)
            {
                GameTimePauseTime = rt - value;
            }
        }
    }

    public bool IsGameTimeInitialized
    {
        get => _loadingTimes.HasValue;
        set => _loadingTimes = value ? LoadingTimes : null;
    }

    private bool _isGameTimePaused;

    public bool IsGameTimePaused
    {
        get => _isGameTimePaused;
        set
        {
            if (!value && _isGameTimePaused)
            {
                LoadingTimes = CurrentTime.RealTime!.Value - (CurrentTime.GameTime ?? CurrentTime.RealTime.Value);
            }
            else if (value && !_isGameTimePaused)
            {
                GameTimePauseTime = CurrentTime.GameTime ?? CurrentTime.RealTime;
            }

            _isGameTimePaused = value;
        }
    }

    public Segment? CurrentSplit =>
        CurrentSplitIndex >= 0 && CurrentSplitIndex < Run.Count ? Run[CurrentSplitIndex] : null;

    public SplitTime CurrentTime
    {
        get
        {
            var curTime = new SplitTime
            {
                RealTime = CurrentPhase switch
                {
                    TimerPhase.NotRunning => TimeSpan.Zero,
                    TimerPhase.Running => TimeStamp.Now - AdjustedStartTime,
                    TimerPhase.Paused => TimePausedAt,
                    _ => Run.Count > 0 ? Run[^1].SplitTime.RealTime : TimeSpan.Zero,
                }
            };

            curTime.GameTime = CurrentPhase == TimerPhase.Ended
                ? (Run.Count > 0 ? Run[^1].SplitTime.GameTime : null)
                : IsGameTimePaused
                    ? GameTimePauseTime
                    : curTime.RealTime - (IsGameTimeInitialized ? LoadingTimes : null);

            return curTime;
        }
    }

    public TimeSpan? PauseTime
    {
        get
        {
            if (CurrentPhase == TimerPhase.Paused)
            {
                return TimeStamp.Now - StartTimeWithOffset - TimePausedAt;
            }

            // Upstream TimeStamp is a class without ==, so this is a reference check: any
            // resume, even after a zero-length pause, gives PauseTime 0 rather than null
            // until Start or UndoAllPauses puts the same instance back.
            if (CurrentPhase != TimerPhase.NotRunning && !ReferenceEquals(StartTimeWithOffset, AdjustedStartTime))
            {
                return AdjustedStartTime - StartTimeWithOffset;
            }

            return null;
        }
    }

    /// <summary>
    /// Upstream LiveSplitState.SetGameTime: the loading times become what makes the game time this
    /// value now (and, while game time is paused, it stays at this value).
    /// </summary>
    public void SetGameTime(TimeSpan? gameTime)
    {
        SplitTime current = CurrentTime;
        if (current.RealTime is { } realTime && gameTime is { } value)
        {
            LoadingTimes = realTime - value;
            if (IsGameTimePaused)
            {
                GameTimePauseTime = value;
            }
        }
    }

    /// <summary>
    /// Upstream LiveSplitState.CurrentAttemptDuration: the real time since the attempt started
    /// (pauses included) while it runs, start to end once it ended, else zero.
    /// </summary>
    public TimeSpan CurrentAttemptDuration => CurrentPhase switch
    {
        TimerPhase.Running or TimerPhase.Paused => TimeStamp.Now - StartTime,
        TimerPhase.Ended => AttemptEnded - AttemptStarted,
        _ => TimeSpan.Zero,
    };

    /// <summary>
    /// Upstream LiveSplitState.Clone: a copy with its own run (see <see cref="Run.Clone"/>) and the
    /// same timer fields, for working on the attempt without touching the live timer (Windows
    /// TimerForm.SaveSplits resets a copy and saves that). Fields are assigned in upstream's order
    /// and LoadingTimes through its setter, so a copy taken while game time is paused gets
    /// upstream's GameTimePauseTime (0 - LoadingTimes, as the copy is not running yet then).
    /// </summary>
    public LiveSplitState Clone() => new(Run.Clone())
    {
        AdjustedStartTime = AdjustedStartTime,
        StartTimeWithOffset = StartTimeWithOffset,
        StartTime = StartTime,
        TimePausedAt = TimePausedAt,
        GameTimePauseTime = GameTimePauseTime,
        _isGameTimePaused = _isGameTimePaused,
        LoadingTimes = LoadingTimes,
        CurrentPhase = CurrentPhase,
        CurrentSplitIndex = CurrentSplitIndex,
        CurrentComparison = CurrentComparison,
        CurrentTimingMethod = CurrentTimingMethod,
        AttemptStarted = AttemptStarted,
        AttemptEnded = AttemptEnded,
    };
}
