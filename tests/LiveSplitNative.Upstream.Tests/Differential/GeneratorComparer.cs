using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using N = LiveSplitNative.Core.Model;
using NC = LiveSplitNative.Core.Model.Comparisons;
using U = LiveSplit.Model;
using UC = LiveSplit.Model.Comparisons;

namespace LiveSplitNative.Upstream.Tests.Differential;

/// <summary>
/// Compares every generated comparison of an upstream run and a LiveSplitNative run, segment
/// by segment, in both timing methods. Most generators must match to the tick. Average Segments
/// and Golf HCP get a tolerance, because the vendored upstream code runs on .NET 10, where
/// TimeSpan.TotalSeconds divides instead of multiplying and FromSeconds keeps every tick, while
/// our port does what .NET Framework (Windows LiveSplit) does:
/// <list type="bullet">
/// <item>Average Segments: TotalSeconds can land one bit apart, so the tick truncation can differ
/// by one tick per segment; split times add up, so split i allows i + 1 ticks.</item>
/// <item>Golf HCP: .NET Framework rounds each segment to whole milliseconds, .NET 10 truncates to
/// ticks, so each segment can differ by up to half a millisecond (plus a tick or two from the
/// TotalSeconds bit); split i allows i + 1 of those.</item>
/// </list>
/// </summary>
internal static class GeneratorComparer
{
    private static readonly TimeSpan HalfMillisecond = TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond / 2);

    public static TimeSpan TolerancePerSegment(string name) => name switch
    {
        NC.AverageSegmentsComparisonGenerator.ComparisonName => TimeSpan.FromTicks(1),
        NC.HCPComparisonGenerator.ComparisonName => HalfMillisecond + TimeSpan.FromTicks(2),
        _ => TimeSpan.Zero,
    };

    public static List<UC.IComparisonGenerator> UpstreamGenerators(U.IRun run) =>
        new UC.StandardComparisonGeneratorsFactory().GetAllGenerators(run).ToList();

    public static List<NC.IComparisonGenerator> NativeGenerators(N.Run run) =>
        NC.StandardComparisonGeneratorsFactory.GetAllGenerators(run).ToList();

    /// <summary>Returns null when everything matches, else a readable line pair; category names the generator.</summary>
    public static string Compare(U.IRun upstream, N.Run native, IEnumerable<string> names, out string category)
    {
        category = null;
        if (upstream.Count != native.Count)
        {
            category = "Generated.SegmentCount";
            return $"upstream: {upstream.Count} segments\nnative:   {native.Count} segments";
        }

        foreach (string name in names)
        {
            TimeSpan perSegment = TolerancePerSegment(name);
            for (int i = 0; i < upstream.Count; i++)
            {
                U.Time u = upstream[i].Comparisons[name];
                N.SplitTime n = native[i].Comparisons.TryGetValue(name, out N.SplitTime value) ? value : default;
                TimeSpan tolerance = TimeSpan.FromTicks(perSegment.Ticks * (i + 1));
                string report = CompareTimes(name, i, "RealTime", u.RealTime, n.RealTime, tolerance)
                    ?? CompareTimes(name, i, "GameTime", u.GameTime, n.GameTime, tolerance);
                if (report is not null)
                {
                    category = $"Generated[{name}]";
                    return report;
                }
            }
        }

        return null;
    }

    private static string CompareTimes(string name, int index, string method, TimeSpan? u, TimeSpan? n, TimeSpan tolerance)
    {
        if (u is null && n is null)
        {
            return null;
        }

        if (u is { } a && n is { } b && (a - b).Duration() <= tolerance)
        {
            return null;
        }

        string allowed = tolerance == TimeSpan.Zero
            ? "exact"
            : "within " + tolerance.Ticks.ToString(CultureInfo.InvariantCulture) + " ticks";
        return $"upstream: {name} [{index}] {method} = {TwinTimer.T(u)}\n"
            + $"native:   {name} [{index}] {method} = {TwinTimer.T(n)} ({allowed})";
    }
}
