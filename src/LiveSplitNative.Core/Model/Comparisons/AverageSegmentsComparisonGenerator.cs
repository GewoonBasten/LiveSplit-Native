using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// Port of AverageSegmentsComparisonGenerator @ 93a11b85: a weighted mean of each segment's
/// history (weight 0.75 per older attempt), summed into split times. TotalSeconds uses
/// .NET Framework arithmetic (<see cref="FrameworkTimeSpan"/>), as Windows LiveSplit computes it.
/// </summary>
public sealed class AverageSegmentsComparisonGenerator(Run run) : IComparisonGenerator
{
    public const string ComparisonName = "Average Segments";
    public const string ShortComparisonName = "Average";
    public const double Weight = 0.75;

    public Run Run { get; } = run;

    public string Name => ComparisonName;

    private static TimeSpan CalculateAverage(List<TimeSpan> curList)
    {
        int elementCount = curList.Count;
        var weightedList = curList
            .Select((x, i) => new KeyValuePair<double, TimeSpan>(GetWeight(i, elementCount), x))
            .OrderBy(x => x.Value)
            .ToList();
        double totalWeights = weightedList.Sum(x => x.Key);
        double averageTime = weightedList.Sum(x => x.Key * FrameworkTimeSpan.TotalSeconds(x.Value)) / totalWeights;
        return TimeSpan.FromTicks((long)(averageTime * TimeSpan.TicksPerSecond));
    }

    private static double GetWeight(int index, int count) => Math.Pow(Weight, count - index - 1);

    public void Generate(TimingMethod method)
    {
        var allHistory = new List<List<TimeSpan>>(Run.Count);
        for (int i = 0; i < Run.Count; i++)
        {
            allHistory.Add([]);
        }

        foreach (Attempt attempt in Run.AttemptHistory)
        {
            int ind = attempt.Index;
            bool ignoreNextHistory = false;
            for (int segmentIndex = 0; segmentIndex < Run.Count; segmentIndex++)
            {
                if (Run[segmentIndex].SegmentHistory.TryGetValue(ind, out SplitTime history))
                {
                    if (history[method] is null)
                    {
                        ignoreNextHistory = true;
                    }
                    else if (!ignoreNextHistory)
                    {
                        allHistory[segmentIndex].Add(history[method]!.Value);
                    }
                    else
                    {
                        ignoreNextHistory = false;
                    }
                }
                else
                {
                    ignoreNextHistory = false;
                }
            }
        }

        TimeSpan? totalTime = TimeSpan.Zero;
        for (int ind = 0; ind < Run.Count; ind++)
        {
            List<TimeSpan> curList = allHistory[ind];
            if (curList.Count == 0)
            {
                totalTime = null;
            }

            if (totalTime is not null)
            {
                totalTime += CalculateAverage(curList);
            }

            SplitTime time = ComparisonValues.Get(Run[ind], Name);
            time[method] = totalTime;
            Run[ind].Comparisons[Name] = time;
        }
    }

    public void Generate(ComparisonGeneratorSettings settings)
    {
        Generate(TimingMethod.RealTime);
        Generate(TimingMethod.GameTime);
    }
}
