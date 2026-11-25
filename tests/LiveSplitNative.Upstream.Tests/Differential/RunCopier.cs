using System.Collections.Generic;
using N = LiveSplitNative.Core.Model;
using NT = LiveSplitNative.Core.Timing;
using U = LiveSplit.Model;
using UC = LiveSplit.Model.Comparisons;

namespace LiveSplitNative.Upstream.Tests.Differential;

/// <summary>
/// Copies a LiveSplitNative run into an upstream run field by field: segments with their
/// comparisons, golds and history, the attempt history and the custom comparison names. The
/// vendored upstream code has no .lss parser, so fixture files are loaded with ours and copied.
/// </summary>
internal static class RunCopier
{
    public static U.Run ToUpstream(N.Run native)
    {
        var run = new U.Run(new UC.StandardComparisonGeneratorsFactory())
        {
            Offset = native.Offset,
            AttemptCount = native.AttemptCount,
            CustomComparisons = new List<string>(native.CustomComparisons),
        };

        foreach (N.Segment segment in native.Segments)
        {
            var copy = new U.Segment(segment.Name)
            {
                BestSegmentTime = T(segment.BestSegmentTime),
                SplitTime = T(segment.SplitTime),
            };
            foreach (KeyValuePair<string, N.SplitTime> comparison in segment.Comparisons)
            {
                copy.Comparisons[comparison.Key] = T(comparison.Value);
            }

            foreach (KeyValuePair<int, N.SplitTime> history in segment.SegmentHistory)
            {
                copy.SegmentHistory[history.Key] = T(history.Value);
            }

            run.Add(copy);
        }

        foreach (N.Attempt attempt in native.AttemptHistory)
        {
            run.AttemptHistory.Add(new U.Attempt(attempt.Index, T(attempt.Time), T(attempt.Started), T(attempt.Ended), attempt.PauseTime));
        }

        return run;
    }

    private static U.Time T(N.SplitTime time) => new(time.RealTime, time.GameTime);

    private static U.AtomicDateTime? T(NT.AtomicDateTime? time) =>
        time is { } t ? new U.AtomicDateTime(t.Time, t.SyncedWithAtomicClock) : null;
}
