namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>Port of BestSegmentsComparisonGenerator @ 93a11b85: cumulative Sum of Best predictions.</summary>
public sealed class BestSegmentsComparisonGenerator(Run run) : IComparisonGenerator
{
    public const string ComparisonName = "Best Segments";
    public const string ShortComparisonName = "Best";

    public Run Run { get; } = run;

    public string Name => ComparisonName;

    public void Generate(ComparisonGeneratorSettings settings)
    {
        var realTimePredictions = new TimeSpan?[Run.Count + 1];
        var gameTimePredictions = new TimeSpan?[Run.Count + 1];
        SumOfBest.CalculateSumOfBest(Run, 0, Run.Count - 1, realTimePredictions, settings.SimpleSumOfBest, false, TimingMethod.RealTime);
        SumOfBest.CalculateSumOfBest(Run, 0, Run.Count - 1, gameTimePredictions, settings.SimpleSumOfBest, false, TimingMethod.GameTime);
        for (int i = 0; i < Run.Count; i++)
        {
            Run[i].Comparisons[Name] = new SplitTime(realTimePredictions[i + 1], gameTimePredictions[i + 1]);
        }
    }
}
