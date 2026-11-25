namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// Port of MedianSegmentsComparisonGenerator @ 93a11b85: the weighted median of each segment's
/// history (weight 0.75 per older attempt), summed into split times. Unlike Average Segments,
/// an attempt's history stops at its first segment without an entry.
/// </summary>
public sealed class MedianSegmentsComparisonGenerator(Run run) : IComparisonGenerator
{
    public const string ComparisonName = "Median Segments";
    public const string ShortComparisonName = "Median";
    public const double Weight = 0.75;

    public Run Run { get; } = run;

    public string Name => ComparisonName;

    private static TimeSpan CalculateMedian(List<TimeSpan> curList)
    {
        int elementCount = curList.Count;
        var weightedList = curList
            .Select((x, i) => new KeyValuePair<double, TimeSpan>(GetWeight(i, elementCount), x))
            .OrderBy(x => x.Value)
            .ToList();
        double totalWeights = weightedList.Sum(x => x.Key);
        double halfTotalWeights = totalWeights / 2;
        double curTotal = 0.0;
        foreach (KeyValuePair<double, TimeSpan> element in weightedList)
        {
            curTotal += element.Key;
            if (curTotal >= halfTotalWeights)
            {
                return element.Value;
            }
        }

        return TimeSpan.Zero;
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
                    break;
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
                totalTime += CalculateMedian(curList);
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
