namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// Port of BestSplitTimesComparisonGenerator @ 93a11b85: per split, the fastest cumulative time
/// any single attempt reached, starting from the PB splits (the first split starts from its gold).
/// </summary>
public sealed class BestSplitTimesComparisonGenerator(Run run) : IComparisonGenerator
{
    public const string ComparisonName = "Best Split Times";

    public Run Run { get; } = run;

    public string Name => ComparisonName;

    public void Generate(TimingMethod method)
    {
        int maxIndex = Run.AttemptHistory.Select(x => x.Index).DefaultIfEmpty(0).Max();
        for (int y = Run.GetMinSegmentHistoryIndex(); y <= maxIndex; y++)
        {
            TimeSpan time = TimeSpan.Zero;

            foreach (Segment segment in Run.Segments)
            {
                if (segment.SegmentHistory.TryGetValue(y, out SplitTime segmentHistoryElement))
                {
                    TimeSpan? segmentTime = segmentHistoryElement[method];
                    if (segmentTime is not null)
                    {
                        time += segmentTime.Value;

                        SplitTime current = ComparisonValues.Get(segment, Name);
                        if (current[method] is null || time < current[method])
                        {
                            current[method] = time;
                            segment.Comparisons[Name] = current;
                        }
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }

    public void Generate(ComparisonGeneratorSettings settings)
    {
        for (int i = 0; i < Run.Count; i++)
        {
            Segment segment = Run[i];
            segment.Comparisons[Name] = i > 0
                ? ComparisonValues.Get(segment, Run.PersonalBestComparisonName)
                : segment.BestSegmentTime;
        }

        Generate(TimingMethod.RealTime);
        Generate(TimingMethod.GameTime);
    }
}
