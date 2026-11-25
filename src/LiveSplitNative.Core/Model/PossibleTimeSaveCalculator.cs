using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Model;

/// <summary>
/// Port of LiveSplit.PossibleTimeSave.GetPossibleTimeSave + Total aggregation
/// (PossibleTimeSave.cs) with LiveSplitStateHelper.GetLiveSegmentDelta for live total.
/// </summary>
public static class PossibleTimeSaveCalculator
{
    public static TimeSpan? GetPossibleTimeSave(
        LiveSplitState state,
        int segmentIndex,
        string comparison,
        bool live = false)
    {
        if (segmentIndex < 0 || segmentIndex >= state.Run.Count)
        {
            return null;
        }

        TimingMethod method = state.CurrentTimingMethod;
        Segment segment = state.Run[segmentIndex];
        int splitIndex = segmentIndex;
        TimeSpan prevTime = TimeSpan.Zero;
        TimeSpan? bestSegments = segment.BestSegmentTime[method];

        while (splitIndex > 0 && bestSegments is not null)
        {
            TimeSpan? splitTime = state.Run[splitIndex - 1].Comparisons.TryGetValue(comparison, out SplitTime cmp)
                ? cmp[method]
                : null;
            if (splitTime is not null)
            {
                prevTime = splitTime.Value;
                break;
            }

            splitIndex--;
            bestSegments += state.Run[splitIndex].BestSegmentTime[method];
        }

        TimeSpan? comparisonTime = segment.Comparisons.TryGetValue(comparison, out SplitTime segCmp)
            ? segCmp[method]
            : null;
        TimeSpan? time = comparisonTime - prevTime - bestSegments;

        if (live && splitIndex == state.CurrentSplitIndex)
        {
            TimeSpan? segmentDelta = TimeSpan.Zero - GetLiveSegmentDelta(state, segmentIndex, comparison, method);
            if (segmentDelta < time)
            {
                time = segmentDelta;
            }
        }

        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return time;
    }

    public static TimeSpan? GetDisplayedValue(LiveSplitState state, string comparison, bool totalTimeSave)
    {
        // Upstream PossibleTimeSave.Update: !state.Run.Comparisons.Contains(comparison).
        if (!state.Run.Comparisons.Contains(comparison, StringComparer.Ordinal))
        {
            comparison = state.CurrentComparison;
        }

        if (totalTimeSave)
        {
            if (state.CurrentPhase == TimerPhase.Ended)
            {
                return TimeSpan.Zero;
            }

            int start = Math.Max(0, state.CurrentSplitIndex);
            TimeSpan? total = TimeSpan.Zero;
            bool any = false;
            for (int i = start; i < state.Run.Count; i++)
            {
                TimeSpan? save = GetPossibleTimeSave(state, i, comparison, live: true);
                if (save is null)
                {
                    continue;
                }

                total += save;
                any = true;
            }

            return any ? total : null;
        }

        return state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused
            ? GetPossibleTimeSave(state, state.CurrentSplitIndex, comparison, live: false)
            : null;
    }

    /// <summary>Port of LiveSplitStateHelper.GetLiveSegmentDelta (segment delta mode).</summary>
    public static TimeSpan? GetLiveSegmentDelta(
        LiveSplitState state,
        int splitNumber,
        string comparison,
        TimingMethod method)
    {
        TimeSpan? currentTime = state.CurrentTime[method];
        if (currentTime is null || splitNumber < 0 || splitNumber >= state.Run.Count)
        {
            return null;
        }

        for (int x = splitNumber - 1; x >= 0; x--)
        {
            TimeSpan? splitTime = state.Run[x].SplitTime[method];
            if (splitTime is null)
            {
                continue;
            }

            TimeSpan? prevCmp = state.Run[x].Comparisons.TryGetValue(comparison, out SplitTime pc) ? pc[method] : null;
            TimeSpan? curCmp = state.Run[splitNumber].Comparisons.TryGetValue(comparison, out SplitTime cc) ? cc[method] : null;
            if (prevCmp is not null && curCmp is not null)
            {
                return currentTime - curCmp - (splitTime - prevCmp);
            }
        }

        TimeSpan? comparisonAt = state.Run[splitNumber].Comparisons.TryGetValue(comparison, out SplitTime c)
            ? c[method]
            : null;
        return currentTime - comparisonAt;
    }
}
