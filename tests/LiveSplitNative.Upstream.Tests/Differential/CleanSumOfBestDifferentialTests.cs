using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using N = LiveSplitNative.Core.Model;
using U = LiveSplit.Model;

namespace LiveSplitNative.Upstream.Tests.Differential;

/// <summary>
/// Clean Sum of Best (Splits Editor, Other...): every .lss fixture is cleaned by upstream
/// SumOfBest.Clean (93a11b85) and by ours, answering the same questions the same way (yes, no,
/// yes, ...). Both must ask the same questions in the same order and leave the same segment
/// history.
/// </summary>
public class CleanSumOfBestDifferentialTests
{
    private readonly ITestOutputHelper _output;

    public CleanSumOfBestDifferentialTests(ITestOutputHelper output) => _output = output;

    private static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "fixtures");

    public static IEnumerable<object[]> Files() =>
        Directory.GetFiles(FixtureDirectory, "*.lss")
            .Concat(Directory.Exists(Path.Combine(FixtureDirectory, "corpus"))
                ? Directory.GetFiles(Path.Combine(FixtureDirectory, "corpus"), "*.lss")
                : Array.Empty<string>())
            .Select(path => Path.GetRelativePath(FixtureDirectory, path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Files))]
    public void Ours_Asks_And_Removes_As_Windows(string file)
    {
        N.Run ours;
        try
        {
            ours = LiveSplitNative.Core.Serialization.XmlRunSerializer.LoadFile(Path.Combine(FixtureDirectory, file));
        }
        catch (System.Xml.XmlException)
        {
            // The fuzz-crash files are not splits files: nothing to clean.
            _output.WriteLine($"{file}: not a splits file");
            return;
        }

        U.Run windows = RunCopier.ToUpstream(ours);

        var windowsAsked = new List<string>();
        U.SumOfBest.Clean(windows, p =>
        {
            windowsAsked.Add(string.Join(" | ",
                p.startingSegment?.Name ?? "(start)",
                p.endingSegment?.Name ?? "(none)",
                p.timeBetween.Ticks,
                p.combinedSumOfBest?.Ticks.ToString() ?? "-",
                // Upstream's Attempt is a struct: an attempt not in the history reads as index 0.
                p.attempt.Index,
                p.method));
            return windowsAsked.Count % 2 == 1;
        });

        var oursAsked = new List<string>();
        N.SumOfBest.Clean(ours, p =>
        {
            oursAsked.Add(string.Join(" | ",
                p.StartingSegment?.Name ?? "(start)",
                p.EndingSegment.Name,
                p.TimeBetween.Ticks,
                p.CombinedSumOfBest?.Ticks.ToString() ?? "-",
                p.Attempt?.Index ?? 0,
                p.Method == N.TimingMethod.RealTime ? "RealTime" : "GameTime"));
            return oursAsked.Count % 2 == 1;
        });

        _output.WriteLine($"{file}: {windowsAsked.Count} questions");
        Assert.Equal(windowsAsked, oursAsked);
        for (int i = 0; i < ours.Count; i++)
        {
            Assert.Equal(
                windows[i].SegmentHistory.Select(h => $"{h.Key}:{h.Value.RealTime?.Ticks}:{h.Value.GameTime?.Ticks}"),
                ours[i].SegmentHistory.Select(h => $"{h.Key}:{h.Value.RealTime?.Ticks}:{h.Value.GameTime?.Ticks}"));
        }
    }

    /// <summary>A run with an impossible time across a skipped split: it is asked about and removed.</summary>
    [Fact]
    public void A_Too_Fast_Time_Across_A_Skipped_Split_Is_Asked_About()
    {
        var ours = new N.Run();
        ours.AddSegment("A");
        ours.AddSegment("B");
        ours.AddSegment("C");
        ours[0].BestSegmentTime = new N.SplitTime(TimeSpan.FromSeconds(10));
        ours[1].BestSegmentTime = new N.SplitTime(TimeSpan.FromSeconds(10));
        ours[2].BestSegmentTime = new N.SplitTime(TimeSpan.FromSeconds(10));
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            ours[0].SegmentHistory[attempt] = new N.SplitTime(TimeSpan.FromSeconds(10));
            ours[2].SegmentHistory[attempt] = new N.SplitTime(TimeSpan.FromSeconds(10));
        }

        // Attempt 3 skipped B, then took 5 s for B and C together: faster than golds' 20 s.
        ours[0].SegmentHistory[3] = new N.SplitTime(TimeSpan.FromSeconds(10));
        ours[1].SegmentHistory[3] = new N.SplitTime(null, null);
        ours[2].SegmentHistory[3] = new N.SplitTime(TimeSpan.FromSeconds(5));
        ours[1].SegmentHistory[1] = new N.SplitTime(TimeSpan.FromSeconds(10));
        ours[1].SegmentHistory[2] = new N.SplitTime(TimeSpan.FromSeconds(10));

        var asked = new List<N.SumOfBest.CleanUpCallbackParameters>();
        N.SumOfBest.Clean(ours, N.TimingMethod.RealTime, p =>
        {
            asked.Add(p);
            return true;
        });

        N.SumOfBest.CleanUpCallbackParameters question = Assert.Single(asked);
        Assert.Equal("A", question.StartingSegment?.Name);
        Assert.Equal("C", question.EndingSegment.Name);
        Assert.Equal(TimeSpan.FromSeconds(5), question.TimeBetween);
        Assert.Equal(TimeSpan.FromSeconds(20), question.CombinedSumOfBest);
        Assert.False(ours[2].SegmentHistory.ContainsKey(3));
    }
}
