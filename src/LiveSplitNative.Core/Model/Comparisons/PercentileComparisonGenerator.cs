namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// Port of PercentileComparisonGenerator @ 93a11b85 ("Balanced PB"): picks one percentile of
/// every segment's weighted history (weight 0.9375 per older attempt) so the splits add up to
/// the PB, found by bisection in at most 50 steps. Works on ticks only, so no
/// .NET Framework-specific arithmetic is involved.
/// </summary>
public sealed class PercentileComparisonGenerator(Run run) : IComparisonGenerator
{
    public const string ComparisonName = "Balanced PB";
    public const string ShortComparisonName = "Balanced";
    public const double Weight = 0.9375;

    public Run Run { get; } = run;

    public string Name => ComparisonName;

    private static double GetWeight(int index, int count) => Math.Pow(Weight, count - index - 1);

    private static double ReWeight(double a, double b, double c) => (a - b) / c;

    private static TimeSpan Calculate(double perc, TimeSpan value1, double key1, TimeSpan value2, double key2)
    {
        double percDn = (key1 - perc) * value2.Ticks / (key1 - key2);
        double percUp = (perc - key2) * value1.Ticks / (key1 - key2);
        return TimeSpan.FromTicks(Convert.ToInt64(percUp + percDn));
    }

    private TimeSpan? GetGoalTime(TimingMethod method) => Run[^1].PersonalBestSplitTime[method];

    public void Generate(TimingMethod method)
    {
        var allHistory = new List<List<(TimeSpan Time, int Index)>>(Run.Count);
        for (int i = 0; i < Run.Count; i++)
        {
            allHistory.Add([]);
        }

        foreach (Attempt attempt in Run.AttemptHistory)
        {
            int ind = attempt.Index;
            int historyStartingIndex = -1;
            for (int currentIndex = 0; currentIndex < Run.Count; currentIndex++)
            {
                if (Run[currentIndex].SegmentHistory.TryGetValue(ind, out SplitTime history))
                {
                    if (history[method] is { } historyTime)
                    {
                        allHistory[currentIndex].Add((historyTime, historyStartingIndex));
                        historyStartingIndex = currentIndex;
                    }
                }
                else
                {
                    historyStartingIndex = currentIndex;
                }
            }
        }

        var weightedLists = new List<List<KeyValuePair<double, TimeSpan>>?>(Run.Count);
        int overallStartingIndex = -1;

        for (int curIndex = 0; curIndex < allHistory.Count; curIndex++)
        {
            List<(TimeSpan Time, int Index)> currentList = allHistory[curIndex];
            bool nullSegment = false;
            TimeSpan? curPBTime = Run[curIndex].PersonalBestSplitTime[method];
            TimeSpan? previousPBTime = overallStartingIndex >= 0 ? Run[overallStartingIndex].PersonalBestSplitTime[method] : null;
            var finalList = new List<TimeSpan>();

            int startingIndex = overallStartingIndex;
            var matchingSegmentHistory = currentList.Where(x => x.Index == startingIndex).ToList();
            if (matchingSegmentHistory.Count > 0)
            {
                finalList = matchingSegmentHistory.Select(x => x.Time).ToList();
                overallStartingIndex = curIndex;
            }
            else if (curPBTime is not null && previousPBTime is not null)
            {
                finalList.Add(curPBTime.Value - previousPBTime.Value);
                overallStartingIndex = curIndex;
            }
            else
            {
                nullSegment = true;
            }

            if (!nullSegment)
            {
                var tempList = finalList
                    .Select((x, i) => new KeyValuePair<double, TimeSpan>(GetWeight(i, finalList.Count), x))
                    .ToList();
                var weightedList = new List<KeyValuePair<double, TimeSpan>>();
                if (tempList.Count > 1)
                {
                    tempList = [.. tempList.OrderBy(x => x.Value)];
                    double totalWeight = tempList.Sum(x => x.Key);
                    double smallestWeight = tempList[0].Key;
                    double rangeWeight = totalWeight - smallestWeight;
                    double aggWeight = 0.0;
                    foreach (KeyValuePair<double, TimeSpan> value in tempList)
                    {
                        aggWeight += value.Key;
                        weightedList.Add(new KeyValuePair<double, TimeSpan>(ReWeight(aggWeight, smallestWeight, rangeWeight), value.Value));
                    }

                    weightedList = [.. weightedList.OrderBy(x => x.Value)];
                }
                else
                {
                    weightedList.Add(new KeyValuePair<double, TimeSpan>(1.0, tempList[0].Value));
                }

                weightedLists.Add(weightedList);
            }
            else
            {
                weightedLists.Add(null);
            }
        }

        TimeSpan? goalTime = GetGoalTime(method);
        TimeSpan runSum;
        var outputSplits = new List<TimeSpan>(Run.Count);
        double percentile;
        double percMax = 1.0;
        double percMin = 0.0;
        int loopProtection = 0;

        do
        {
            runSum = TimeSpan.Zero;
            outputSplits.Clear();
            percentile = (0.5 * (percMax - percMin)) + percMin;
            foreach (List<KeyValuePair<double, TimeSpan>>? weightedList in weightedLists)
            {
                if (weightedList is not null)
                {
                    TimeSpan curValue = TimeSpan.Zero;
                    if (weightedList.Count > 1)
                    {
                        for (int n = 0; n < weightedList.Count; n++)
                        {
                            if (weightedList[n].Key > percentile)
                            {
                                curValue = Calculate(percentile, weightedList[n].Value, weightedList[n].Key, weightedList[n - 1].Value, weightedList[n - 1].Key);
                                break;
                            }

                            if (weightedList[n].Key == percentile)
                            {
                                curValue = weightedList[n].Value;
                                break;
                            }
                        }
                    }
                    else
                    {
                        curValue = weightedList[0].Value;
                    }

                    outputSplits.Add(curValue);
                    runSum += curValue;
                }
                else
                {
                    outputSplits.Add(TimeSpan.Zero);
                }
            }

            if (runSum > goalTime)
            {
                percMax = percentile;
            }
            else
            {
                percMin = percentile;
            }

            loopProtection += 1;
        }
        while (goalTime is { } goal && runSum - goal != TimeSpan.Zero && loopProtection < 50);

        TimeSpan totalTime = TimeSpan.Zero;
        for (int ind = 0; ind < Run.Count; ind++)
        {
            totalTime += outputSplits[ind];
            TimeSpan? useTime = outputSplits[ind] == TimeSpan.Zero ? null : totalTime;

            SplitTime time = ComparisonValues.Get(Run[ind], Name);
            time[method] = useTime;
            Run[ind].Comparisons[Name] = time;
        }
    }

    public void Generate(ComparisonGeneratorSettings settings)
    {
        Generate(TimingMethod.RealTime);
        Generate(TimingMethod.GameTime);
    }
}
