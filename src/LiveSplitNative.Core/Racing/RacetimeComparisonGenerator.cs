using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;

namespace LiveSplitNative.Core.Racing;

/// <summary>
/// LiveSplit.Racetime RacetimeComparisonGenerator: an entrant's "[Race] name" comparison. It
/// generates nothing; the entrant's split times are written in as racetime.gg sends them.
/// </summary>
public sealed class RacetimeComparisonGenerator(Run run, string name) : IComparisonGenerator
{
    public Run Run { get; } = run;

    public string Name { get; } = name;

    public void Generate(ComparisonGeneratorSettings settings)
    {
    }

    public static string GetRaceComparisonName(string user) => "[Race] " + user;

    public static bool IsRaceComparison(string comparison) => comparison.StartsWith("[Race] ", StringComparison.Ordinal);
}
