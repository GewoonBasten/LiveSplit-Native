namespace LiveSplitNative.Core.Model;

/// <summary>Port of LiveSplit.Model.SumOfWorst @ 93a11b85 (used by the Worst Segments comparison).</summary>
public static class SumOfWorst
{
    private static void PopulatePrediction(IList<TimeSpan?> predictions, TimeSpan? predictedTime, int segmentIndex)
    {
        if (predictedTime.HasValue && (!predictions[segmentIndex].HasValue || predictedTime > predictions[segmentIndex]!.Value))
        {
            predictions[segmentIndex] = predictedTime;
        }
    }

    private static void PopulatePredictions(
        Run run,
        TimeSpan? currentTime,
        int segmentIndex,
        IList<TimeSpan?> predictions,
        bool useCurrentRun,
        TimingMethod method)
    {
        if (currentTime is null)
        {
            return;
        }

        PopulatePrediction(predictions, currentTime + run[segmentIndex].BestSegmentTime[method], segmentIndex + 1);
        foreach (KeyValuePair<int, SplitTime> segment in run[segmentIndex].SegmentHistory)
        {
            if (segmentIndex == 0
                || !run[segmentIndex - 1].SegmentHistory.TryGetValue(segment.Key, out SplitTime segmentTime)
                || segmentTime[method] != null)
            {
                IndexedTime prediction = SumOfSegmentsHelper.TrackBranch(run, currentTime, segmentIndex, segment.Key, method);
                PopulatePrediction(predictions, prediction.Time[method], prediction.Index);
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

    /// <remarks>
    /// Upstream walks segment indexes from 0 regardless of <paramref name="startIndex"/>;
    /// kept as is, since every caller passes 0.
    /// </remarks>
    public static TimeSpan? CalculateSumOfWorst(
        Run run,
        int startIndex,
        int endIndex,
        IList<TimeSpan?> predictions,
        bool useCurrentRun = true,
        TimingMethod method = TimingMethod.RealTime)
    {
        int segmentIndex = 0;
        predictions[startIndex] = TimeSpan.Zero;
        int count = Math.Max(0, Math.Min(endIndex - startIndex + 1, run.Count - startIndex));
        for (int i = 0; i < count; i++)
        {
            TimeSpan? currentTime = predictions[segmentIndex];
            PopulatePredictions(run, currentTime, segmentIndex, predictions, useCurrentRun, method);
            segmentIndex++;
        }

        return predictions[endIndex + 1];
    }

    public static TimeSpan? CalculateSumOfWorst(Run run, bool useCurrentRun = true, TimingMethod method = TimingMethod.RealTime)
    {
        var predictions = new TimeSpan?[run.Count + 1];
        return CalculateSumOfWorst(run, 0, run.Count - 1, predictions, useCurrentRun, method);
    }
}
