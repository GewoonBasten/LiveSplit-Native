namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>Port of WorstSegmentsComparisonGenerator @ 93a11b85: cumulative Sum of Worst predictions.</summary>
public sealed class WorstSegmentsComparisonGenerator(Run run) : IComparisonGenerator
{
    public const string ComparisonName = "Worst Segments";
    public const string ShortComparisonName = "Worst";

    public Run Run { get; } = run;

    public string Name => ComparisonName;

    public void Generate(ComparisonGeneratorSettings settings)
    {
        var realTimePredictions = new TimeSpan?[Run.Count + 1];
        var gameTimePredictions = new TimeSpan?[Run.Count + 1];
        SumOfWorst.CalculateSumOfWorst(Run, 0, Run.Count - 1, realTimePredictions, false, TimingMethod.RealTime);
        SumOfWorst.CalculateSumOfWorst(Run, 0, Run.Count - 1, gameTimePredictions, false, TimingMethod.GameTime);
        for (int i = 0; i < Run.Count; i++)
        {
            Run[i].Comparisons[Name] = new SplitTime(realTimePredictions[i + 1], gameTimePredictions[i + 1]);
        }
    }
}
