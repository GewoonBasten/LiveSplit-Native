using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// Port of HCPComparisonGenerator @ 93a11b85 ("Golf HCP"): from the latest finished attempts
/// (settings.cfg HcpHistorySize, default 20) it takes the best ones (HcpNBestRuns, default 8),
/// averages each segment with a small variance penalty and scales the result to their average
/// total, never going below the best segment among them. TotalSeconds and FromSeconds use
/// .NET Framework arithmetic (<see cref="FrameworkTimeSpan"/>): FromSeconds rounds every
/// segment to whole milliseconds there, as in Windows LiveSplit.
/// </summary>
public sealed class HCPComparisonGenerator : IComparisonGenerator
{
    public const string ComparisonName = "Golf HCP";
    public const string ShortComparisonName = "HCP";

    private int _numberOfLatestRunsToInclude = 20;
    private int _maximumNumberOfBestRunsToInclude = 8;

    private readonly List<double> _totalTimes;
    private readonly List<List<double>> _segmentTimesPerRun;
    private readonly List<int> _bestRunIndices;

    public HCPComparisonGenerator(Run run)
    {
        Run = run;
        _totalTimes = new(_numberOfLatestRunsToInclude);
        _segmentTimesPerRun = new(_numberOfLatestRunsToInclude);
        _bestRunIndices = new(_maximumNumberOfBestRunsToInclude);
    }

    public Run Run { get; }

    public string Name => ComparisonName;

    public void Generate(TimingMethod method)
    {
        int segmentCount = Run.Count;
        if (segmentCount == 0)
        {
            return;
        }

        ResetReusableCollections();

        InitializeSegmentComparisons(segmentCount, method);

        CollectValidRuns(method, segmentCount);

        if (_totalTimes.Count == 0)
        {
            return;
        }

        SelectBestRuns();

        int bestRunsCount = _bestRunIndices.Count;
        double avgTotal = bestRunsCount > 0
            ? _bestRunIndices.Average(index => _totalTimes[index])
            : 0;

        if (avgTotal <= 0)
        {
            return;
        }

        (double[] segmentHCPTimes, bool[] hasValidTimes, double[] bestSegmentFromSelectedRuns) = CalculateSegmentHCPs(segmentCount);

        double totalHCPSegmentTime = segmentHCPTimes.Where(time => time > 0).Sum();

        if (totalHCPSegmentTime > 0)
        {
            ApplyHCPComparisons(segmentCount, avgTotal, totalHCPSegmentTime, segmentHCPTimes, hasValidTimes, bestSegmentFromSelectedRuns, method);
        }
    }

    private void ResetReusableCollections()
    {
        _totalTimes.Clear();
        if (_totalTimes.Capacity < _numberOfLatestRunsToInclude)
        {
            _totalTimes.Capacity = _numberOfLatestRunsToInclude;
        }

        _segmentTimesPerRun.Clear();
        if (_segmentTimesPerRun.Capacity < _numberOfLatestRunsToInclude)
        {
            _segmentTimesPerRun.Capacity = _numberOfLatestRunsToInclude;
        }

        _bestRunIndices.Clear();
        if (_bestRunIndices.Capacity < _maximumNumberOfBestRunsToInclude)
        {
            _bestRunIndices.Capacity = _maximumNumberOfBestRunsToInclude;
        }
    }

    private void InitializeSegmentComparisons(int segmentCount, TimingMethod method)
    {
        for (int i = 0; i < segmentCount; i++)
        {
            SplitTime time = ComparisonValues.Get(Run[i], Name);
            time[method] = null;
            Run[i].Comparisons[Name] = time;
        }
    }

    private void CollectValidRuns(TimingMethod method, int segmentCount)
    {
        List<Attempt> attemptHistory = Run.AttemptHistory;
        int attemptCount = attemptHistory.Count;
        int lastSegmentIndex = segmentCount - 1;
        Segment lastSegment = Run[lastSegmentIndex];

        int validAttemptsFound = 0;
        for (int i = attemptCount - 1; i >= 0 && validAttemptsFound < _numberOfLatestRunsToInclude; i--)
        {
            Attempt attempt = attemptHistory[i];

            if (!lastSegment.SegmentHistory.TryGetValue(attempt.Index, out SplitTime lastSegmentTime)
                || lastSegmentTime[method] is null)
            {
                continue;
            }

            double totalTime;
            if (attempt.Time[method] is { } attemptTime)
            {
                totalTime = FrameworkTimeSpan.TotalSeconds(attemptTime);
            }
            else if (lastSegmentTime[method] is { } lastTime)
            {
                totalTime = FrameworkTimeSpan.TotalSeconds(lastTime);
            }
            else
            {
                continue;
            }

            if (totalTime <= 0)
            {
                continue;
            }

            var segmentTimes = new List<double>(segmentCount);
            bool hasValidSegments = false;

            for (int j = 0; j < segmentCount; j++)
            {
                double segmentTime = 0;
                if (Run[j].SegmentHistory.TryGetValue(attempt.Index, out SplitTime segmentHistory)
                    && segmentHistory[method] is { } historyTime)
                {
                    segmentTime = FrameworkTimeSpan.TotalSeconds(historyTime);
                    if (segmentTime > 0)
                    {
                        hasValidSegments = true;
                    }
                }

                segmentTimes.Add(segmentTime);
            }

            if (hasValidSegments)
            {
                _totalTimes.Add(totalTime);
                _segmentTimesPerRun.Add(segmentTimes);
                validAttemptsFound++;
            }
        }
    }

    private void SelectBestRuns()
    {
        var totalTimeWithIndices = _totalTimes
            .Select((time, index) => (Index: index, Time: time))
            .OrderBy(t => t.Time)
            .Take(_maximumNumberOfBestRunsToInclude)
            .ToList();

        _bestRunIndices.Clear();
        _bestRunIndices.AddRange(totalTimeWithIndices.Select(t => t.Index));
    }

    private (double[] SegmentHCPTimes, bool[] HasValidTimes, double[] BestSegmentFromSelectedRuns) CalculateSegmentHCPs(int segmentCount)
    {
        double[] segmentHCPTimes = new double[segmentCount];
        bool[] hasValidTimes = new bool[segmentCount];
        double[] bestSegmentFromSelectedRuns = new double[segmentCount];

        for (int segIndex = 0; segIndex < segmentCount; segIndex++)
        {
            var bestSegmentTimes = _bestRunIndices
                .Where(runIndex => runIndex < _segmentTimesPerRun.Count)
                .Select(runIndex => _segmentTimesPerRun[runIndex][segIndex])
                .Where(segTime => segTime > 0)
                .ToList();

            if (bestSegmentTimes.Count > 0)
            {
                bestSegmentFromSelectedRuns[segIndex] = bestSegmentTimes.Min();

                double mean = bestSegmentTimes.Average();

                if (bestSegmentTimes.Count > 1)
                {
                    double variance = bestSegmentTimes.Average(time => Math.Pow(time - mean, 2));
                    double stdDev = Math.Sqrt(variance);

                    // Penalty proportional to the spread, capped at 5% of the mean (upstream comment).
                    double variancePenalty = Math.Min(stdDev * 0.1, mean * 0.05);

                    segmentHCPTimes[segIndex] = mean + variancePenalty;
                }
                else
                {
                    segmentHCPTimes[segIndex] = mean;
                }

                hasValidTimes[segIndex] = true;
            }
            else
            {
                bestSegmentFromSelectedRuns[segIndex] = double.MaxValue;
                hasValidTimes[segIndex] = false;
            }
        }

        return (segmentHCPTimes, hasValidTimes, bestSegmentFromSelectedRuns);
    }

    private void ApplyHCPComparisons(
        int segmentCount,
        double avgTotal,
        double totalHCPSegmentTime,
        double[] segmentHCPTimes,
        bool[] hasValidTimes,
        double[] bestSegmentFromSelectedRuns,
        TimingMethod method)
    {
        const int MaxIterations = 10;
        bool[] clampedSegments = new bool[segmentCount];
        double scaleFactor = 1.0;
        int iteration = 0;
        bool anyNewClamps;

        do
        {
            anyNewClamps = false;
            double totalClampedTime = 0;
            double totalUnclampedTime = 0;

            for (int i = 0; i < segmentCount; i++)
            {
                if (hasValidTimes[i])
                {
                    if (clampedSegments[i])
                    {
                        totalClampedTime += bestSegmentFromSelectedRuns[i];
                    }
                    else
                    {
                        totalUnclampedTime += segmentHCPTimes[i];
                    }
                }
            }

            if (totalUnclampedTime > 0)
            {
                if (totalClampedTime < avgTotal)
                {
                    double remainingTarget = avgTotal - totalClampedTime;
                    scaleFactor = remainingTarget / totalUnclampedTime;
                }
                else
                {
                    // All budget consumed by clamped segments: clamp everything else.
                    scaleFactor = 1.0;
                    for (int i = 0; i < segmentCount; i++)
                    {
                        if (hasValidTimes[i] && !clampedSegments[i])
                        {
                            clampedSegments[i] = true;
                        }
                    }

                    break;
                }
            }
            else if (totalHCPSegmentTime > 0)
            {
                scaleFactor = avgTotal / totalHCPSegmentTime;
                break;
            }
            else
            {
                break;
            }

            for (int i = 0; i < segmentCount; i++)
            {
                if (hasValidTimes[i] && !clampedSegments[i])
                {
                    double bestSegSeconds = bestSegmentFromSelectedRuns[i];
                    double scaledTime = segmentHCPTimes[i] * scaleFactor;

                    if (scaledTime < bestSegSeconds)
                    {
                        clampedSegments[i] = true;
                        anyNewClamps = true;
                    }
                }
            }

            iteration++;
        }
        while (anyNewClamps && iteration < MaxIterations);

        TimeSpan cumulative = TimeSpan.Zero;

        for (int i = 0; i < segmentCount; i++)
        {
            SplitTime time = ComparisonValues.Get(Run[i], Name);

            if (hasValidTimes[i])
            {
                TimeSpan segmentDuration = clampedSegments[i]
                    ? FrameworkTimeSpan.FromSeconds(bestSegmentFromSelectedRuns[i])
                    : FrameworkTimeSpan.FromSeconds(Math.Max(0, segmentHCPTimes[i] * scaleFactor));

                cumulative += segmentDuration;
                time[method] = cumulative;
            }
            else
            {
                // No valid times for this segment: null, but the cumulative chain continues.
                time[method] = null;
            }

            Run[i].Comparisons[Name] = time;
        }
    }

    public void Generate(ComparisonGeneratorSettings settings)
    {
        _numberOfLatestRunsToInclude = settings.HcpHistorySize;
        _maximumNumberOfBestRunsToInclude = settings.HcpNBestRuns;
        Generate(TimingMethod.RealTime);
        Generate(TimingMethod.GameTime);
    }
}
