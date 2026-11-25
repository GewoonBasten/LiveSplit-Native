namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// Port of LatestRunComparisonGenerator @ 93a11b85: the split times of the newest attempt that
/// reached the furthest segment. With no such attempt the comparison is left as it was.
/// </summary>
public sealed class LatestRunComparisonGenerator(Run run) : IComparisonGenerator
{
    public const string ComparisonName = "Latest Run";
    public const string ShortComparisonName = "Latest";

    public Run Run { get; } = run;

    public string Name => ComparisonName;

    private int GetAttemptIndex()
    {
        // Upstream Attempt is a struct, so LastOrDefault() on an empty history reads index 0.
        int maxIndex = Run.AttemptHistory.Count == 0 ? 0 : Run.AttemptHistory[^1].Index;
        for (int segmentIndex = Run.Count - 1; segmentIndex >= 0; segmentIndex--)
        {
            Segment segment = Run[segmentIndex];
            for (int attemptIndex = maxIndex; attemptIndex >= 1; attemptIndex--)
            {
                if (segment.SegmentHistory.ContainsKey(attemptIndex))
                {
                    return attemptIndex;
                }
            }
        }

        return 0;
    }

    public void Generate(TimingMethod method)
    {
        int attemptIndex = GetAttemptIndex();

        if (attemptIndex > 0)
        {
            TimeSpan? totalTime = TimeSpan.Zero;
            for (int ind = 0; ind < Run.Count; ind++)
            {
                TimeSpan? segmentTime = null;
                if (Run[ind].SegmentHistory.TryGetValue(attemptIndex, out SplitTime history))
                {
                    segmentTime = history[method];
                }
                else
                {
                    totalTime = null;
                }

                SplitTime time = ComparisonValues.Get(Run[ind], Name);
                if (totalTime is not null && segmentTime is not null)
                {
                    totalTime += segmentTime;
                    time[method] = totalTime;
                }
                else
                {
                    time[method] = null;
                }

                Run[ind].Comparisons[Name] = time;
            }
        }
    }

    public void Generate(ComparisonGeneratorSettings settings)
    {
        Generate(TimingMethod.RealTime);
        Generate(TimingMethod.GameTime);
    }
}
