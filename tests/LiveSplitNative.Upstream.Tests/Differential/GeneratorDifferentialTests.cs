using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using N = LiveSplitNative.Core.Model;
using NC = LiveSplitNative.Core.Model.Comparisons;
using U = LiveSplit.Model;
using UC = LiveSplit.Model.Comparisons;

namespace LiveSplitNative.Upstream.Tests.Differential;

/// <summary>
/// The nine standard comparison generators, ours against upstream's, on runs the timer alone
/// rarely produces: the .lss fixtures (real attempt histories) and random histories with skipped
/// and missing segments, gaps in attempt ids, imported PB history at ids &lt;= 0, null PB splits
/// and golds. Tolerances as in <see cref="GeneratorComparer"/>; everything else must match exactly.
/// </summary>
public class GeneratorDifferentialTests
{
    private const int RandomSeeds = 3000;

    private static readonly (bool Simple, int HcpHistorySize, int HcpNBestRuns)[] FixtureSettings =
    {
        (false, 20, 8),
        (true, 20, 8),
        (false, 5, 3),
        (false, 1, 1),
        (true, 60, 20),
    };

    public static IEnumerable<object[]> Fixtures() =>
        Directory.GetFiles(FixtureDirectory, "*.lss")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name });

    private static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "fixtures");

    [Fact]
    public void Generator_Lists_Line_Up()
    {
        var native = new N.Run();
        var upstream = new U.Run(new UC.StandardComparisonGeneratorsFactory());

        Assert.Equal(
            GeneratorComparer.UpstreamGenerators(upstream).Select(g => g.Name),
            GeneratorComparer.NativeGenerators(native).Select(g => g.Name));
        Assert.Equal(upstream.ComparisonGenerators.Select(g => g.Name), native.ComparisonGenerators.Select(g => g.Name));
        Assert.Equal(upstream.Comparisons, native.Comparisons);
        foreach (string name in upstream.Comparisons.Concat(GeneratorComparer.UpstreamGenerators(upstream).Select(g => g.Name)))
        {
            Assert.Equal(
                UC.CompositeComparisons.GetShortComparisonName(name),
                NC.StandardComparisonGeneratorsFactory.GetShortComparisonName(name));
        }
    }

    [Fact]
    public void Fixtures_Are_Found()
    {
        Assert.True(Fixtures().Count() >= 5, FixtureDirectory);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_Generated_Comparisons_Match_Upstream(string file)
    {
        N.Run native = LiveSplitNative.Core.Serialization.XmlRunSerializer.LoadFile(Path.Combine(FixtureDirectory, file));
        U.Run upstream = RunCopier.ToUpstream(native);
        List<UC.IComparisonGenerator> upstreamGenerators = GeneratorComparer.UpstreamGenerators(upstream);
        List<NC.IComparisonGenerator> nativeGenerators = GeneratorComparer.NativeGenerators(native);

        foreach ((bool simple, int hcpHistorySize, int hcpNBestRuns) in FixtureSettings)
        {
            string report = Regenerate(upstream, upstreamGenerators, native, nativeGenerators, simple, hcpHistorySize, hcpNBestRuns, out _);
            Assert.True(
                report is null,
                $"{file}, SimpleSumOfBest {simple}, HcpHistorySize {hcpHistorySize}, HcpNBestRuns {hcpNBestRuns}:\n{report}");
        }
    }

    [Fact]
    public void Large_Fixture_Gives_Every_Generator_Values()
    {
        // Guards against the fixture test passing because both sides produce nothing.
        N.Run native = LiveSplitNative.Core.Serialization.XmlRunSerializer.LoadFile(Path.Combine(FixtureDirectory, "livesplit1.6.lss"));
        List<NC.IComparisonGenerator> generators = GeneratorComparer.NativeGenerators(native);
        foreach (NC.IComparisonGenerator generator in generators)
        {
            generator.Generate(new NC.ComparisonGeneratorSettings());
        }

        foreach (NC.IComparisonGenerator generator in generators.Where(g => g.Name != NC.NoneComparisonGenerator.ComparisonName))
        {
            Assert.True(
                native.Segments.Any(s => s.Comparisons.TryGetValue(generator.Name, out N.SplitTime t) && t.RealTime is not null),
                generator.Name);
        }
    }

    [Fact]
    public void Random_Histories_Match_Upstream()
    {
        var divergences = new Dictionary<string, string>(StringComparer.Ordinal);
        int divergingSeeds = 0;

        for (int seed = 0; seed < RandomSeeds; seed++)
        {
            var rng = new Random(seed);
            N.Run native = RandomRun(rng, out string description);
            U.Run upstream = RunCopier.ToUpstream(native);
            bool simple = rng.Next(2) == 0;
            int hcpHistorySize = rng.Next(26);
            int hcpNBestRuns = rng.Next(11);

            string report = Regenerate(
                upstream,
                GeneratorComparer.UpstreamGenerators(upstream),
                native,
                GeneratorComparer.NativeGenerators(native),
                simple,
                hcpHistorySize,
                hcpNBestRuns,
                out string category);
            if (report is null)
            {
                continue;
            }

            divergingSeeds++;
            if (!divergences.ContainsKey(category))
            {
                divergences[category] =
                    $"seed {seed}: {description}, SimpleSumOfBest {simple}, HcpHistorySize {hcpHistorySize}, HcpNBestRuns {hcpNBestRuns}\n{report}";
            }
        }

        if (divergences.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine(CultureInfo.InvariantCulture, $"{divergingSeeds} of {RandomSeeds} random runs diverge from upstream, in {divergences.Count} distinct ways. First example of each:");
        foreach (KeyValuePair<string, string> pair in divergences.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            message.AppendLine();
            message.AppendLine("=== " + pair.Key);
            message.AppendLine(pair.Value);
        }

        Assert.Fail(message.ToString());
    }

    private static string Regenerate(
        U.Run upstream,
        List<UC.IComparisonGenerator> upstreamGenerators,
        N.Run native,
        List<NC.IComparisonGenerator> nativeGenerators,
        bool simple,
        int hcpHistorySize,
        int hcpNBestRuns,
        out string category)
    {
        var upstreamSettings = new LiveSplit.Options.ShimSettings
        {
            SimpleSumOfBest = simple,
            HcpHistorySize = hcpHistorySize,
            HcpNBestRuns = hcpNBestRuns,
        };
        var nativeSettings = new NC.ComparisonGeneratorSettings
        {
            SimpleSumOfBest = simple,
            HcpHistorySize = hcpHistorySize,
            HcpNBestRuns = hcpNBestRuns,
        };

        // One generator at a time, so an exception names the generator that threw.
        for (int i = 0; i < upstreamGenerators.Count; i++)
        {
            string upstreamThrew = TwinTimer.Try(() => upstreamGenerators[i].Generate(upstreamSettings));
            string nativeThrew = TwinTimer.Try(() => nativeGenerators[i].Generate(nativeSettings));
            if (upstreamThrew != nativeThrew)
            {
                category = $"Throws[{upstreamGenerators[i].Name}]";
                return $"upstream: {upstreamGenerators[i].Name} throws {upstreamThrew ?? "nothing"}\n"
                    + $"native:   {nativeGenerators[i].Name} throws {nativeThrew ?? "nothing"}";
            }
        }

        return GeneratorComparer.Compare(upstream, native, nativeGenerators.Select(g => g.Name), out category);
    }

    /// <summary>A run with a random history that keeps the structure a real .lss can have.</summary>
    private static N.Run RandomRun(Random rng, out string description)
    {
        int segmentCount = 1 + rng.Next(8);
        var run = new N.Run();
        for (int i = 0; i < segmentCount; i++)
        {
            run.AddSegment("S" + i.ToString(CultureInfo.InvariantCulture));
        }

        int attempts = rng.Next(41);
        int index = 0;
        for (int a = 0; a < attempts; a++)
        {
            index += 1 + (rng.Next(6) == 0 ? rng.Next(3) : 0);
            int reached = rng.Next(segmentCount + 2);
            TimeSpan? realTotal = TimeSpan.Zero;
            TimeSpan? gameTotal = TimeSpan.Zero;
            for (int s = 0; s < segmentCount; s++)
            {
                int mode = rng.Next(20);
                if ((s >= reached && mode < 18) || mode == 0)
                {
                    realTotal = null;
                    gameTotal = null;
                    continue;
                }

                N.SplitTime time = mode == 1 ? default : RandomSegmentTime(rng);
                run[s].SegmentHistory[index] = time;
                realTotal += time.RealTime;
                gameTotal += time.GameTime;
            }

            N.SplitTime attemptTime = rng.Next(5) switch
            {
                0 => default,
                1 => RandomSegmentTime(rng),
                _ => new N.SplitTime(realTotal, gameTotal),
            };
            run.AttemptHistory.Add(new N.Attempt(index, attemptTime, null, null, null));
        }

        int imported = rng.Next(4);
        for (int k = 0; k < imported; k++)
        {
            for (int s = 0; s < segmentCount; s++)
            {
                if (rng.Next(4) != 0)
                {
                    run[s].SegmentHistory[-k] = rng.Next(8) == 0 ? default : RandomSegmentTime(rng);
                }
            }
        }

        if (rng.Next(5) != 0)
        {
            TimeSpan realPb = TimeSpan.Zero;
            TimeSpan gamePb = TimeSpan.Zero;
            for (int s = 0; s < segmentCount; s++)
            {
                N.SplitTime segment = RandomSegmentTime(rng);
                realPb += segment.RealTime ?? TimeSpan.Zero;
                gamePb += segment.GameTime ?? TimeSpan.Zero;
                run[s].PersonalBestSplitTime = rng.Next(6) == 0
                    ? default
                    : new N.SplitTime(realPb, segment.GameTime is null ? null : (TimeSpan?)gamePb);
            }
        }

        for (int s = 0; s < segmentCount; s++)
        {
            if (rng.Next(5) != 0)
            {
                run[s].BestSegmentTime = RandomSegmentTime(rng);
            }
        }

        run.AttemptCount = index;
        description = $"{segmentCount} segments, {attempts} attempts (last id {index}), {imported} imported history ids";
        return run;
    }

    private static N.SplitTime RandomSegmentTime(Random rng)
    {
        TimeSpan real = rng.Next(40) switch
        {
            0 => TimeSpan.Zero,
            1 => TimeSpan.FromTicks(-1 - rng.Next(10_000_000)),
            < 14 => TimeSpan.FromMilliseconds(500 * (1 + rng.Next(20))),
            _ => TimeSpan.FromTicks(1 + rng.Next(200_000_000)),
        };
        TimeSpan? game = rng.Next(3) == 0 ? null : real - TimeSpan.FromMilliseconds(rng.Next(3000));
        return new N.SplitTime(real, game);
    }
}
