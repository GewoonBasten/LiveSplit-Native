using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Model;

/// <summary>Port of LiveSplit.Model.LiveSplitStateHelper (delta / split color selection).</summary>
public static class LiveSplitStateHelper
{
    public const string BestSegmentsComparisonName = "Best Segments";

    public static TimeSpan? GetLastDelta(LiveSplitState state, int splitNumber, string comparison, TimingMethod method)
    {
        for (int x = splitNumber; x >= 0; x--)
        {
            if (x >= state.Run.Count)
            {
                continue;
            }

            Segment seg = state.Run[x];
            TimeSpan? split = seg.SplitTime[method];
            TimeSpan? cmp = seg.Comparisons.TryGetValue(comparison, out SplitTime c) ? c[method] : null;
            if (split.HasValue && cmp.HasValue)
            {
                return split.Value - cmp.Value;
            }
        }

        return null;
    }

    public static TimeSpan? GetPreviousSegmentTime(LiveSplitState state, int splitNumber, TimingMethod method) =>
        GetSegmentTimeOrSegmentDelta(state, splitNumber, useCurrentTime: false, segmentTime: true, Run.PersonalBestComparisonName, method);

    public static TimeSpan? GetPreviousSegmentDelta(
        LiveSplitState state, int splitNumber, string comparison, TimingMethod method) =>
        GetSegmentTimeOrSegmentDelta(state, splitNumber, useCurrentTime: false, segmentTime: false, comparison, method);

    public static TimeSpan? GetLiveSegmentTime(LiveSplitState state, int splitNumber, TimingMethod method) =>
        GetSegmentTimeOrSegmentDelta(state, splitNumber, useCurrentTime: true, segmentTime: true, Run.PersonalBestComparisonName, method);

    public static TimeSpan? GetLiveSegmentDelta(
        LiveSplitState state, int splitNumber, string comparison, TimingMethod method) =>
        GetSegmentTimeOrSegmentDelta(state, splitNumber, useCurrentTime: true, segmentTime: false, comparison, method);

    /// <summary>
    /// Port of LiveSplitStateHelper.CheckLiveDelta — show live delta only once behind
    /// (or losing a gold / best-segment pace when ShowBestSegments is on).
    /// </summary>
    public static TimeSpan? CheckLiveDelta(
        LiveSplitState state,
        LayoutSettings layoutSettings,
        bool splitDelta,
        string comparison,
        TimingMethod method)
    {
        if (state.CurrentPhase is not (TimerPhase.Running or TimerPhase.Paused))
        {
            return null;
        }

        int idx = state.CurrentSplitIndex;
        if (idx < 0 || idx >= state.Run.Count)
        {
            return null;
        }

        bool useBestSegment = layoutSettings.ShowBestSegments;
        TimeSpan? curSplit = state.Run[idx].Comparisons.TryGetValue(comparison, out SplitTime cmp)
            ? cmp[method]
            : null;
        TimeSpan? currentTime = state.CurrentTime[method];
        TimeSpan? curSegment = GetLiveSegmentTime(state, idx, method);
        TimeSpan? bestSegment = state.Run[idx].BestSegmentTime[method];
        TimeSpan? bestSegmentDelta = GetLiveSegmentDelta(state, idx, BestSegmentsComparisonName, method);
        TimeSpan? comparisonDelta = GetLiveSegmentDelta(state, idx, comparison, method);

        if ((splitDelta && currentTime > curSplit)
            || (useBestSegment && bestSegment is not null && curSegment > bestSegment && bestSegmentDelta > TimeSpan.Zero)
            || comparisonDelta > TimeSpan.Zero)
        {
            if (splitDelta)
            {
                return currentTime - curSplit;
            }

            return comparisonDelta;
        }

        return null;
    }

    /// <summary>
    /// Timer digit color — NotRunning / Paused / Ended (PB vs behind) / Running (GetSplitColor).
    /// </summary>
    public static ArgbColor GetTimerColor(
        LiveSplitState state,
        LayoutSettings layoutSettings,
        TimingMethod? timingMethod = null)
    {
        TimingMethod method = timingMethod ?? state.CurrentTimingMethod;
        TimeSpan? current = state.CurrentTime[method] ?? state.CurrentTime.RealTime;

        if (state.CurrentPhase == TimerPhase.NotRunning || current < TimeSpan.Zero)
        {
            return layoutSettings.NotRunningColor;
        }

        if (state.CurrentPhase == TimerPhase.Paused)
        {
            return layoutSettings.PausedColor;
        }

        if (state.CurrentPhase == TimerPhase.Ended)
        {
            // Ended CurrentTime is last split time (upstream). Compare to current comparison finish.
            TimeSpan? finishCmp = state.Run.Count > 0
                && state.Run[^1].Comparisons.TryGetValue(state.CurrentComparison, out SplitTime endCmp)
                ? endCmp[method]
                : null;
            return finishCmp is null || current < finishCmp
                ? layoutSettings.PersonalBestColor
                : layoutSettings.BehindLosingTimeColor;
        }

        if (state.CurrentPhase == TimerPhase.Running
            && state.CurrentSplit?.Comparisons.TryGetValue(state.CurrentComparison, out SplitTime cmp) == true
            && cmp[method] is { } splitCmp
            && current.HasValue)
        {
            return GetSplitColor(
                       state,
                       layoutSettings,
                       current.Value - splitCmp,
                       state.CurrentSplitIndex,
                       showSegmentDeltas: true,
                       showBestSegments: false,
                       state.CurrentComparison,
                       method)
                   ?? layoutSettings.AheadGainingTimeColor;
        }

        return layoutSettings.AheadGainingTimeColor;
    }

    /// <summary>
    /// Chooses a split color from Layout Settings (Ahead/Behind × Gaining/Losing + Best Segment).
    /// </summary>
    public static ArgbColor? GetSplitColor(
        LiveSplitState state,
        LayoutSettings layoutSettings,
        TimeSpan? timeDifference,
        int splitNumber,
        bool showSegmentDeltas,
        bool showBestSegments,
        string comparison,
        TimingMethod method)
    {
        ArgbColor? splitColor = null;
        if (splitNumber < 0)
        {
            return splitColor;
        }

        if (timeDifference.HasValue)
        {
            if (timeDifference.Value <= TimeSpan.Zero)
            {
                splitColor = layoutSettings.AheadGainingTimeColor;
                TimeSpan? lastDelta = GetLastDelta(state, splitNumber - 1, comparison, method);
                if (showSegmentDeltas && splitNumber > 0 && lastDelta.HasValue && timeDifference > lastDelta)
                {
                    splitColor = layoutSettings.AheadLosingTimeColor;
                }
            }
            else
            {
                splitColor = layoutSettings.BehindLosingTimeColor;
                TimeSpan? lastDelta = GetLastDelta(state, splitNumber - 1, comparison, method);
                if (showSegmentDeltas && splitNumber > 0 && lastDelta.HasValue && timeDifference < lastDelta)
                {
                    splitColor = layoutSettings.BehindGainingTimeColor;
                }
            }
        }

        if (showBestSegments && layoutSettings.ShowBestSegments && CheckBestSegment(state, splitNumber, method))
        {
            splitColor = GetBestSegmentColor(layoutSettings);
        }

        return splitColor;
    }

    public static bool CheckBestSegment(LiveSplitState state, int splitNumber, TimingMethod method)
    {
        if (splitNumber < 0 || splitNumber >= state.Run.Count || state.Run[splitNumber].SplitTime[method] is null)
        {
            return false;
        }

        TimeSpan? delta = GetPreviousSegmentDelta(state, splitNumber, BestSegmentsComparisonName, method);
        TimeSpan? curSegment = GetPreviousSegmentTime(state, splitNumber, method);
        TimeSpan? bestSegment = state.Run[splitNumber].BestSegmentTime[method];
        return bestSegment is null || curSegment < bestSegment || delta < TimeSpan.Zero;
    }

    public static ArgbColor GetBestSegmentColor(LayoutSettings layoutSettings)
    {
        if (!layoutSettings.UseRainbowColor)
        {
            return layoutSettings.BestSegmentColor;
        }

        // Lightweight HSV rainbow (matches upstream spirit; no System.Drawing).
        int hue = (int)DateTime.Now.TimeOfDay.TotalMilliseconds / 100 % 36 * 10;
        return FromHsv(hue, 1, 1, soften: true);
    }

    private static TimeSpan? GetSegmentTimeOrSegmentDelta(
        LiveSplitState state,
        int splitNumber,
        bool useCurrentTime,
        bool segmentTime,
        string comparison,
        TimingMethod method)
    {
        if (splitNumber < 0 || splitNumber >= state.Run.Count)
        {
            return null;
        }

        TimeSpan? currentTime = useCurrentTime
            ? state.CurrentTime[method]
            : state.Run[splitNumber].SplitTime[method];
        if (!currentTime.HasValue)
        {
            return null;
        }

        for (int x = splitNumber - 1; x >= 0; x--)
        {
            TimeSpan? splitTime = state.Run[x].SplitTime[method];
            if (!splitTime.HasValue)
            {
                continue;
            }

            if (segmentTime)
            {
                return currentTime - splitTime;
            }

            TimeSpan? cmpAtSplit = state.Run[splitNumber].Comparisons.TryGetValue(comparison, out SplitTime cSplit)
                ? cSplit[method]
                : null;
            TimeSpan? cmpAtPrev = state.Run[x].Comparisons.TryGetValue(comparison, out SplitTime cPrev)
                ? cPrev[method]
                : null;
            if (cmpAtSplit.HasValue && cmpAtPrev.HasValue)
            {
                return currentTime - cmpAtSplit - (splitTime - cmpAtPrev);
            }
        }

        if (segmentTime)
        {
            return currentTime;
        }

        TimeSpan? cmp = state.Run[splitNumber].Comparisons.TryGetValue(comparison, out SplitTime c)
            ? c[method]
            : null;
        return currentTime - cmp;
    }

    private static ArgbColor FromHsv(int hueDegrees, double s, double v, bool soften)
    {
        double h = (hueDegrees % 360) / 60.0;
        int i = (int)Math.Floor(h);
        double f = h - i;
        double p = v * (1 - s);
        double q = v * (1 - s * f);
        double t = v * (1 - s * (1 - f));
        (double r, double g, double b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };

        byte R = (byte)Math.Clamp((int)(r * 255), 0, 255);
        byte G = (byte)Math.Clamp((int)(g * 255), 0, 255);
        byte B = (byte)Math.Clamp((int)(b * 255), 0, 255);
        if (soften)
        {
            R = (byte)((R * 2 + 255) / 3);
            G = (byte)((G * 2 + 255) / 3);
            B = (byte)((B * 2 + 255) / 3);
        }

        return ArgbColor.FromArgb(255, R, G, B);
    }
}
