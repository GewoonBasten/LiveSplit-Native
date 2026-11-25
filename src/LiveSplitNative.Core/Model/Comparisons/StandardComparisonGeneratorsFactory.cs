namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// Port of StandardComparisonGeneratorsFactory @ 93a11b85 plus the short names it registers
/// (upstream keeps them on CompositeComparisons). <see cref="Create"/> is what a new Run starts
/// with; Windows then switches generators on and off from settings.cfg ComparisonGeneratorStates
/// (Choose Active Comparisons), which is not wired up here yet.
/// </summary>
public static class StandardComparisonGeneratorsFactory
{
    private static readonly Dictionary<string, string> ShortComparisonNames = new(StringComparer.Ordinal)
    {
        [BestSegmentsComparisonGenerator.ComparisonName] = BestSegmentsComparisonGenerator.ShortComparisonName,
        [Run.PersonalBestComparisonName] = "PB",
        [AverageSegmentsComparisonGenerator.ComparisonName] = AverageSegmentsComparisonGenerator.ShortComparisonName,
        [MedianSegmentsComparisonGenerator.ComparisonName] = MedianSegmentsComparisonGenerator.ShortComparisonName,
        [WorstSegmentsComparisonGenerator.ComparisonName] = WorstSegmentsComparisonGenerator.ShortComparisonName,
        [PercentileComparisonGenerator.ComparisonName] = PercentileComparisonGenerator.ShortComparisonName,
        [LatestRunComparisonGenerator.ComparisonName] = LatestRunComparisonGenerator.ShortComparisonName,
        [HCPComparisonGenerator.ComparisonName] = HCPComparisonGenerator.ShortComparisonName,
    };

    /// <summary>The generators a new Run gets: Best Segments and Average Segments.</summary>
    public static IEnumerable<IComparisonGenerator> Create(Run run)
    {
        yield return new BestSegmentsComparisonGenerator(run);
        yield return new AverageSegmentsComparisonGenerator(run);
    }

    /// <summary>Every standard generator, in upstream's order.</summary>
    public static IEnumerable<IComparisonGenerator> GetAllGenerators(Run run)
    {
        yield return new BestSegmentsComparisonGenerator(run);
        yield return new BestSplitTimesComparisonGenerator(run);
        yield return new AverageSegmentsComparisonGenerator(run);
        yield return new MedianSegmentsComparisonGenerator(run);
        yield return new WorstSegmentsComparisonGenerator(run);
        yield return new PercentileComparisonGenerator(run);
        yield return new LatestRunComparisonGenerator(run);
        yield return new HCPComparisonGenerator(run);
        yield return new NoneComparisonGenerator(run);
    }

    /// <summary>Upstream CompositeComparisons.AddShortComparisonName (racetime.gg's "[Race] name" → name).</summary>
    public static void AddShortComparisonName(string longName, string shortName) =>
        ShortComparisonNames[longName] = shortName;

    /// <summary>Upstream CompositeComparisons.GetShortComparisonName: unknown names come back unchanged.</summary>
    public static string GetShortComparisonName(string longName) =>
        ShortComparisonNames.TryGetValue(longName, out string? shortName) ? shortName : longName;
}
