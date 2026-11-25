using LiveSplitNative.Core.Model.Comparisons;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// The Comparison dropdowns of the component settings, as each upstream settings control fills
/// them (93a11b85): "Current Comparison", then the run's comparisons minus the ones that make no
/// sense there, then the saved value if it is not in the list (so it is never silently changed).
/// </summary>
public static class ComparisonChoices
{
    private const string Current = ComparisonSwitching.CurrentComparisonSetting;

    /// <summary>PreviousSegmentSettings: no Best Split Times, no None.</summary>
    public static List<string> PreviousSegment(IEnumerable<string> runComparisons, string saved) =>
        Build(runComparisons, saved, BestSplitTimesComparisonGenerator.ComparisonName, NoneComparisonGenerator.ComparisonName);

    /// <summary>DetailedTimerSettings, both comparisons: no Best Split Times, no None.</summary>
    public static List<string> DetailedTimer(IEnumerable<string> runComparisons, string saved) =>
        Build(runComparisons, saved, BestSplitTimesComparisonGenerator.ComparisonName, NoneComparisonGenerator.ComparisonName);

    /// <summary>PossibleTimeSaveSettings: no Best Split Times, no Best Segments, no None.</summary>
    public static List<string> PossibleTimeSave(IEnumerable<string> runComparisons, string saved) =>
        Build(
            runComparisons,
            saved,
            BestSplitTimesComparisonGenerator.ComparisonName,
            BestSegmentsComparisonGenerator.ComparisonName,
            NoneComparisonGenerator.ComparisonName);

    /// <summary>RunPredictionSettings and ComparisonTimeSettings: no Best Split Times, no None.</summary>
    public static List<string> RunPrediction(IEnumerable<string> runComparisons, string saved) =>
        Build(runComparisons, saved, BestSplitTimesComparisonGenerator.ComparisonName, NoneComparisonGenerator.ComparisonName);

    /// <summary>GraphSettings: no Best Split Times, no None.</summary>
    public static List<string> Graph(IEnumerable<string> runComparisons, string saved) =>
        Build(runComparisons, saved, BestSplitTimesComparisonGenerator.ComparisonName, NoneComparisonGenerator.ComparisonName);

    /// <summary>DeltaSettings: everything but None.</summary>
    public static List<string> Delta(IEnumerable<string> runComparisons, string saved) =>
        Build(runComparisons, saved, NoneComparisonGenerator.ComparisonName);

    /// <summary>Subsplits SplitsSettings section header: everything but None.</summary>
    public static List<string> SubsplitsHeader(IEnumerable<string> runComparisons, string saved) =>
        Build(runComparisons, saved, NoneComparisonGenerator.ComparisonName);

    /// <summary>
    /// Splits and Subsplits ColumnSettings: split-time and delta columns offer everything but None;
    /// segment columns also leave out Best Split Times, and a column set to it becomes Current
    /// Comparison (returned as <paramref name="saved"/>).
    /// </summary>
    public static List<string> Column(IEnumerable<string> runComparisons, ColumnType type, ref string saved)
    {
        if (type is ColumnType.Delta or ColumnType.DeltaorSplitTime or ColumnType.SplitTime)
        {
            return Build(runComparisons, saved, NoneComparisonGenerator.ComparisonName);
        }

        if (saved == BestSplitTimesComparisonGenerator.ComparisonName)
        {
            saved = Current;
        }

        return Build(runComparisons, saved, BestSplitTimesComparisonGenerator.ComparisonName, NoneComparisonGenerator.ComparisonName);
    }

    private static List<string> Build(IEnumerable<string> runComparisons, string saved, params string[] excluded)
    {
        var items = new List<string> { Current };
        items.AddRange(runComparisons.Where(x => !excluded.Contains(x, StringComparer.Ordinal)));
        if (!items.Contains(saved, StringComparer.Ordinal))
        {
            items.Add(saved);
        }

        return items;
    }
}
