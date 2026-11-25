namespace LiveSplitNative.Core.Model;

/// <summary>
/// Port of LiveSplit.Model.SumOfBest.CalculateSumOfBest (portable managed logic).
/// Source: upstream SumOfBest.cs + SumOfSegmentsHelper.cs @ 93a11b85.
/// </summary>
public static class SumOfBest
{
    private static void PopulatePrediction(IList<TimeSpan?> predictions, TimeSpan? predictedTime, int segmentIndex)
    {
        if (predictedTime.HasValue && (!predictions[segmentIndex].HasValue || predictedTime < predictions[segmentIndex]!.Value))
        {
            predictions[segmentIndex] = predictedTime;
        }
    }

    private static void PopulatePredictions(
        Run run,
        TimeSpan? currentTime,
        int segmentIndex,
        IList<TimeSpan?> predictions,
        bool simpleCalculation,
        bool useCurrentRun,
        TimingMethod method)
    {
        if (currentTime is null)
        {
            return;
        }

        PopulatePrediction(predictions, currentTime + run[segmentIndex].BestSegmentTime[method], segmentIndex + 1);
        if (!simpleCalculation)
        {
            foreach (KeyValuePair<int, SplitTime> nullSegment in run[segmentIndex].SegmentHistory.Where(x => !x.Value[method].HasValue))
            {
                if (segmentIndex == 0
                    || !run[segmentIndex - 1].SegmentHistory.TryGetValue(nullSegment.Key, out SplitTime segmentTime)
                    || segmentTime[method] != null)
                {
                    IndexedTime prediction = SumOfSegmentsHelper.TrackBranch(run, currentTime, segmentIndex + 1, nullSegment.Key, method);
                    PopulatePrediction(predictions, prediction.Time[method], prediction.Index);
                }
            }
        }

        if (useCurrentRun)
        {
            IndexedTime currentRunPrediction = SumOfSegmentsHelper.TrackCurrentRun(run, currentTime, segmentIndex, method);
            PopulatePrediction(predictions, currentRunPrediction.Time[method], currentRunPrediction.Index);
        }

        IndexedTime personalBestRunPrediction = SumOfSegmentsHelper.TrackPersonalBestRun(run, currentTime, segmentIndex, method);
        PopulatePrediction(predictions, personalBestRunPrediction.Time[method], personalBestRunPrediction.Index);
    }

    public static TimeSpan? CalculateSumOfBest(
        Run run,
        int startIndex,
        int endIndex,
        IList<TimeSpan?> predictions,
        bool simpleCalculation = false,
        bool useCurrentRun = true,
        TimingMethod method = TimingMethod.RealTime)
    {
        if (run.Count == 0 || endIndex < startIndex)
        {
            return null;
        }

        predictions[startIndex] = TimeSpan.Zero;
        for (int segmentIndex = startIndex; segmentIndex <= endIndex; segmentIndex++)
        {
            TimeSpan? currentTime = predictions[segmentIndex];
            PopulatePredictions(run, currentTime, segmentIndex, predictions, simpleCalculation, useCurrentRun, method);
        }

        return predictions[endIndex + 1];
    }

    public static TimeSpan? CalculateSumOfBest(
        Run run,
        bool simpleCalculation = false,
        bool useCurrentRun = true,
        TimingMethod method = TimingMethod.RealTime)
    {
        var predictions = new TimeSpan?[run.Count + 1];
        return CalculateSumOfBest(run, 0, run.Count - 1, predictions, simpleCalculation, useCurrentRun, method);
    }

    /// <summary>
    /// SumOfBest.Clean (Splits Editor, Other..., Clean Sum of Best): each segment-history time
    /// across skipped splits that beats the Sum of Best over the same stretch is offered to the
    /// callback, and removed when it answers true (no callback removes them all).
    /// </summary>
    public static void Clean(Run run, TimingMethod method, Func<CleanUpCallbackParameters, bool>? callback = null)
    {
        if (run.Count == 0)
        {
            return;
        }

        var predictions = new TimeSpan?[run.Count + 1];
        CalculateSumOfBest(run, 0, run.Count - 1, predictions, simpleCalculation: true, useCurrentRun: false, method);
        for (int segmentIndex = 0; segmentIndex < run.Count; segmentIndex++)
        {
            TimeSpan? currentTime = predictions[segmentIndex];
            foreach (KeyValuePair<int, SplitTime> nullSegment in run[segmentIndex].SegmentHistory.Where(x => !x.Value[method].HasValue).ToList())
            {
                IndexedTime prediction = SumOfSegmentsHelper.TrackBranch(run, currentTime, segmentIndex + 1, nullSegment.Key, method);
                CheckPrediction(run, predictions, prediction.Time[method], segmentIndex - 1, prediction.Index - 1, nullSegment.Key, method, callback);
            }
        }
    }

    /// <summary>SumOfBest.Clean for both timing methods, Real Time first.</summary>
    public static void Clean(Run run, Func<CleanUpCallbackParameters, bool>? callback = null)
    {
        Clean(run, TimingMethod.RealTime, callback);
        Clean(run, TimingMethod.GameTime, callback);
    }

    private static void CheckPrediction(
        Run run,
        TimeSpan?[] predictions,
        TimeSpan? predictedTime,
        int startingIndex,
        int endingIndex,
        int runIndex,
        TimingMethod method,
        Func<CleanUpCallbackParameters, bool>? callback)
    {
        if (predictedTime is not { } predicted
            || (predictions[endingIndex + 1] is { } best && predicted >= best)
            || !run[endingIndex].SegmentHistory.TryGetValue(runIndex, out SplitTime element)
            || element[method] is not { } timeBetween)
        {
            return;
        }

        var parameters = new CleanUpCallbackParameters(
            startingIndex >= 0 ? run[startingIndex] : null,
            run[endingIndex],
            timeBetween,
            predictions[endingIndex + 1] - predictions[startingIndex + 1],
            run.AttemptHistory.FirstOrDefault(x => x.Index == runIndex),
            method);
        if (callback is null || callback(parameters))
        {
            run[endingIndex].SegmentHistory.Remove(runIndex);
        }
    }

    /// <summary>SumOfBest.CleanUpCallbackParameters: one time Clean Sum of Best asks about.</summary>
    public sealed record CleanUpCallbackParameters(
        Segment? StartingSegment,
        Segment EndingSegment,
        TimeSpan TimeBetween,
        TimeSpan? CombinedSumOfBest,
        Attempt? Attempt,
        TimingMethod Method);
}
