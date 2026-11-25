namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// Port of LiveSplit.Model.Comparisons.IComparisonGenerator @ 93a11b85. A generator writes
/// one computed comparison (Best Segments, Average Segments, …) into every segment's
/// <see cref="Segment.Comparisons"/>. Generated comparisons are never saved to the .lss;
/// they are regenerated from the run's history, as Windows TimerForm.RegenerateComparisons does.
/// </summary>
public interface IComparisonGenerator
{
    Run Run { get; }

    string Name { get; }

    void Generate(ComparisonGeneratorSettings settings);
}

/// <summary>The settings.cfg values upstream generators read (ISettings subset); defaults as StandardSettingsFactory.</summary>
public sealed class ComparisonGeneratorSettings
{
    public bool SimpleSumOfBest { get; set; }

    public int HcpHistorySize { get; set; } = 20;

    public int HcpNBestRuns { get; set; } = 8;
}

internal static class ComparisonValues
{
    /// <summary>Upstream CompositeComparisons indexer: a missing comparison reads as an empty time.</summary>
    public static SplitTime Get(Segment segment, string name) =>
        segment.Comparisons.TryGetValue(name, out SplitTime time) ? time : default;
}
