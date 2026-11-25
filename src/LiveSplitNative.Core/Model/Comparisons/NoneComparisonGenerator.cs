namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>Port of NoneComparisonGenerator @ 93a11b85: an always-empty comparison.</summary>
public sealed class NoneComparisonGenerator(Run run) : IComparisonGenerator
{
    public const string ComparisonName = "None";

    public Run Run { get; } = run;

    public string Name => ComparisonName;

    public void Generate(ComparisonGeneratorSettings settings)
    {
        foreach (Segment segment in Run.Segments)
        {
            segment.Comparisons[Name] = default;
        }
    }
}
