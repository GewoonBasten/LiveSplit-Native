using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Model;

/// <summary>
/// Port of LiveSplit.Model.TimerModel control flow (Start/Split/Skip/Undo/Pause/Reset).
/// Verified against upstream TimerModel.cs @ LiveSplit 93a11b85.
/// </summary>
public sealed class TimerModel
{
    public LiveSplitState? CurrentState { get; set; }

    public event EventHandler? OnSplit;
    public event EventHandler? OnUndoSplit;
    public event EventHandler? OnSkipSplit;
    public event EventHandler? OnStart;
    public event EventHandler<TimerPhase>? OnReset;
    public event EventHandler? OnPause;
    public event EventHandler? OnResume;
    public event EventHandler? OnUndoAllPauses;
    public event EventHandler? OnSwitchComparisonPrevious;
    public event EventHandler? OnSwitchComparisonNext;
    public event EventHandler? OnScrollUp;
    public event EventHandler? OnScrollDown;

    public void Start()
    {
        LiveSplitState state = RequireState();
        if (state.CurrentPhase != TimerPhase.NotRunning)
        {
            return;
        }

        state.CurrentPhase = TimerPhase.Running;
        state.CurrentSplitIndex = 0;
        state.AttemptStarted = TimeStamp.CurrentDateTime;
        state.AdjustedStartTime = state.StartTimeWithOffset = TimeStamp.Now - state.Run.Offset;
        state.StartTime = TimeStamp.Now;
        state.TimePausedAt = state.Run.Offset;
        state.IsGameTimeInitialized = false;
        state.Run.AttemptCount++;
        state.Run.HasChanged = true;
        OnStart?.Invoke(this, EventArgs.Empty);
    }

    public void Split()
    {
        LiveSplitState state = RequireState();
        if (state.CurrentPhase != TimerPhase.Running || !(state.CurrentTime.RealTime > TimeSpan.Zero))
        {
            return;
        }

        if (state.CurrentSplit is null)
        {
            return;
        }

        state.CurrentSplit.SplitTime = state.CurrentTime;

        // Upstream copies every run custom variable into the segment so Splits columns of
        // type CustomVariable can show the value at split time. LiveSplitNative only has
        // permanent variables so far; temporary ones (server, autosplitters) join them later.
        foreach ((string name, string value) in state.Run.Metadata.PermanentCustomVariables)
        {
            state.CurrentSplit.CustomVariableValues[name] = value;
        }

        state.CurrentSplitIndex++;
        if (state.Run.Count == state.CurrentSplitIndex)
        {
            state.CurrentPhase = TimerPhase.Ended;
            state.AttemptEnded = TimeStamp.CurrentDateTime;
        }

        state.Run.HasChanged = true;
        OnSplit?.Invoke(this, EventArgs.Empty);
    }

    public void SkipSplit()
    {
        LiveSplitState state = RequireState();
        if ((state.CurrentPhase is not (TimerPhase.Running or TimerPhase.Paused))
            || state.CurrentSplitIndex >= state.Run.Count - 1
            || state.CurrentSplit is null)
        {
            return;
        }

        state.CurrentSplit.SplitTime = default;
        state.CurrentSplit.CustomVariableValues.Clear();
        state.CurrentSplitIndex++;
        state.Run.HasChanged = true;
        OnSkipSplit?.Invoke(this, EventArgs.Empty);
    }

    public void UndoSplit()
    {
        LiveSplitState state = RequireState();
        if (state.CurrentPhase == TimerPhase.NotRunning || state.CurrentSplitIndex <= 0)
        {
            return;
        }

        if (state.CurrentPhase == TimerPhase.Ended)
        {
            state.CurrentPhase = TimerPhase.Running;
        }

        state.CurrentSplitIndex--;
        if (state.CurrentSplit is not null)
        {
            state.CurrentSplit.SplitTime = default;
            state.CurrentSplit.CustomVariableValues.Clear();
        }

        state.Run.HasChanged = true;
        OnUndoSplit?.Invoke(this, EventArgs.Empty);
    }

    public void Reset() => Reset(updateSplits: true);

    public void Reset(bool updateSplits)
    {
        LiveSplitState state = RequireState();
        if (state.CurrentPhase == TimerPhase.NotRunning)
        {
            return;
        }

        ResetState(updateSplits);
        ResetSplits();
    }

    public void Pause()
    {
        LiveSplitState state = RequireState();
        if (state.CurrentPhase == TimerPhase.Running)
        {
            state.TimePausedAt = state.CurrentTime.RealTime!.Value;
            state.CurrentPhase = TimerPhase.Paused;
            OnPause?.Invoke(this, EventArgs.Empty);
        }
        else if (state.CurrentPhase == TimerPhase.Paused)
        {
            state.AdjustedStartTime = TimeStamp.Now - state.TimePausedAt;
            state.CurrentPhase = TimerPhase.Running;
            OnResume?.Invoke(this, EventArgs.Empty);
        }
        else if (state.CurrentPhase == TimerPhase.NotRunning)
        {
            Start();
        }
    }

    /// <summary>Port of TimerModel.UndoAllPauses.</summary>
    public void UndoAllPauses()
    {
        LiveSplitState state = RequireState();
        if (state.CurrentPhase == TimerPhase.Paused)
        {
            Pause();
        }

        TimeSpan pauseTime = state.PauseTime ?? TimeSpan.Zero;
        if (state.CurrentPhase == TimerPhase.Ended && state.Run.Count > 0)
        {
            state.Run[^1].SplitTime += new SplitTime(pauseTime, pauseTime);
        }

        state.AdjustedStartTime = state.StartTimeWithOffset;
        OnUndoAllPauses?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Port of TimerModel.ResetAndSetAttemptAsPB.</summary>
    public void ResetAndSetAttemptAsPb()
    {
        LiveSplitState state = RequireState();
        if (state.CurrentPhase == TimerPhase.NotRunning)
        {
            return;
        }

        ResetState(updateTimes: true);
        SetRunAsPb();
        ResetSplits();
    }

    private void ResetState(bool updateTimes)
    {
        LiveSplitState state = CurrentState!;
        if (state.CurrentPhase != TimerPhase.Ended)
        {
            state.AttemptEnded = TimeStamp.CurrentDateTime;
        }

        state.IsGameTimePaused = false;
        state.LoadingTimes = TimeSpan.Zero;

        if (updateTimes)
        {
            UpdateTimes();
        }
    }

    private void ResetSplits()
    {
        LiveSplitState state = CurrentState!;
        TimerPhase oldPhase = state.CurrentPhase;
        state.CurrentPhase = TimerPhase.NotRunning;
        state.CurrentSplitIndex = -1;

        foreach (Segment split in state.Run.Segments)
        {
            split.SplitTime = default;
            split.CustomVariableValues.Clear();
        }

        OnReset?.Invoke(this, oldPhase);
        state.Run.FixSplits();
    }

    public void UpdateTimes()
    {
        UpdateAttemptHistory();
        UpdateBestSegments();
        UpdatePbSplits();
        UpdateSegmentHistory();
    }

    private void UpdateAttemptHistory()
    {
        LiveSplitState state = CurrentState!;
        var time = new SplitTime();
        if (state.CurrentPhase == TimerPhase.Ended)
        {
            time = state.CurrentTime;
        }

        int maxIndex = state.Run.AttemptHistory.Count == 0
            ? 0
            : state.Run.AttemptHistory.Max(x => x.Index);
        int newIndex = Math.Max(0, maxIndex + 1);
        state.Run.AttemptHistory.Add(new Attempt(newIndex, time, state.AttemptStarted, state.AttemptEnded, state.PauseTime));
    }

    private void UpdateBestSegments()
    {
        LiveSplitState state = CurrentState!;
        TimeSpan? previousSplitTimeRta = TimeSpan.Zero;
        TimeSpan? previousSplitTimeGame = TimeSpan.Zero;
        foreach (Segment split in state.Run.Segments)
        {
            var newBest = new SplitTime(split.BestSegmentTime.RealTime, split.BestSegmentTime.GameTime);
            if (split.SplitTime.RealTime is { } rta)
            {
                TimeSpan? currentSegmentRta = rta - previousSplitTimeRta;
                previousSplitTimeRta = rta;
                if (split.BestSegmentTime.RealTime is null || currentSegmentRta < split.BestSegmentTime.RealTime)
                {
                    newBest.RealTime = currentSegmentRta;
                }
            }

            if (split.SplitTime.GameTime is { } gt)
            {
                TimeSpan? currentSegmentGt = gt - previousSplitTimeGame;
                previousSplitTimeGame = gt;
                if (split.BestSegmentTime.GameTime is null || currentSegmentGt < split.BestSegmentTime.GameTime)
                {
                    newBest.GameTime = currentSegmentGt;
                }
            }

            split.BestSegmentTime = newBest;
        }
    }

    private void UpdatePbSplits()
    {
        LiveSplitState state = CurrentState!;
        if (state.Run.Count == 0)
        {
            return;
        }

        TimingMethod method = state.CurrentTimingMethod;
        Segment last = state.Run[^1];
        if ((last.SplitTime[method] != null && last.PersonalBestSplitTime[method] == null)
            || last.SplitTime[method] < last.PersonalBestSplitTime[method])
        {
            SetRunAsPb();
        }
    }

    private void UpdateSegmentHistory()
    {
        LiveSplitState state = CurrentState!;
        if (state.Run.AttemptHistory.Count == 0)
        {
            return;
        }

        int attemptIndex = state.Run.AttemptHistory[^1].Index;
        TimeSpan? splitTimeRta = TimeSpan.Zero;
        TimeSpan? splitTimeGame = TimeSpan.Zero;
        foreach (Segment split in state.Run.Segments.Take(state.CurrentSplitIndex))
        {
            var newTime = new SplitTime
            {
                RealTime = split.SplitTime.RealTime - splitTimeRta,
                GameTime = split.SplitTime.GameTime - splitTimeGame,
            };
            split.SegmentHistory[attemptIndex] = newTime;
            if (split.SplitTime.RealTime.HasValue)
            {
                splitTimeRta = split.SplitTime.RealTime;
            }

            if (split.SplitTime.GameTime.HasValue)
            {
                splitTimeGame = split.SplitTime.GameTime;
            }
        }
    }

    private void SetRunAsPb()
    {
        LiveSplitState state = CurrentState!;
        state.Run.ImportSegmentHistory();
        state.Run.FixSplits();
        foreach (Segment current in state.Run.Segments)
        {
            current.PersonalBestSplitTime = current.SplitTime;
        }

        state.Run.Metadata.RunId = null;
    }

    /// <summary>
    /// Upstream SwitchComparisonNext: steps through Run.Comparisons (custom, then generated).
    /// A current comparison that is not in the list counts as index -1, as upstream.
    /// </summary>
    public void SwitchComparisonNext()
    {
        LiveSplitState state = RequireState();
        List<string> comparisons = state.Run.Comparisons.ToList();
        state.CurrentComparison = comparisons[(comparisons.IndexOf(state.CurrentComparison) + 1) % comparisons.Count];
        OnSwitchComparisonNext?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Upstream SwitchComparisonPrevious (same list and index rule as <see cref="SwitchComparisonNext"/>).</summary>
    public void SwitchComparisonPrevious()
    {
        LiveSplitState state = RequireState();
        List<string> comparisons = state.Run.Comparisons.ToList();
        state.CurrentComparison = comparisons[(comparisons.IndexOf(state.CurrentComparison) - 1 + comparisons.Count) % comparisons.Count];
        OnSwitchComparisonPrevious?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Upstream ScrollUp: the split lists (Splits, Subsplits) move up one split.</summary>
    public void ScrollUp() => OnScrollUp?.Invoke(this, EventArgs.Empty);

    /// <summary>Upstream ScrollDown: the split lists move down one split.</summary>
    public void ScrollDown() => OnScrollDown?.Invoke(this, EventArgs.Empty);

    private LiveSplitState RequireState() =>
        CurrentState ?? throw new InvalidOperationException("TimerModel.CurrentState is not set.");
}
