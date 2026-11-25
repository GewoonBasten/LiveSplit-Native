using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// Generator behaviour that the differential tests cannot check, because the vendored upstream
/// code runs on .NET 10 arithmetic while Windows LiveSplit (and our port) uses .NET Framework's.
/// </summary>
public class ComparisonGeneratorTests
{
    [Fact]
    public void New_Run_Has_Best_And_Average_Segments_Generators()
    {
        var run = new Run();

        Assert.Equal(
            [BestSegmentsComparisonGenerator.ComparisonName, AverageSegmentsComparisonGenerator.ComparisonName],
            run.ComparisonGenerators.Select(g => g.Name));
        Assert.Equal(
            [Run.PersonalBestComparisonName, "Best Segments", "Average Segments"],
            run.Comparisons);
        Assert.Equal([Run.PersonalBestComparisonName], run.CustomComparisons);
    }

    [Fact]
    public void All_Generators_In_Upstream_Order_With_Short_Names()
    {
        var run = new Run();

        Assert.Equal(
            [
                "Best Segments", "Best Split Times", "Average Segments", "Median Segments", "Worst Segments",
                "Balanced PB", "Latest Run", "Golf HCP", "None",
            ],
            StandardComparisonGeneratorsFactory.GetAllGenerators(run).Select(g => g.Name));
        Assert.Equal("Best", StandardComparisonGeneratorsFactory.GetShortComparisonName("Best Segments"));
        Assert.Equal("PB", StandardComparisonGeneratorsFactory.GetShortComparisonName(Run.PersonalBestComparisonName));
        Assert.Equal("HCP", StandardComparisonGeneratorsFactory.GetShortComparisonName("Golf HCP"));
        Assert.Equal("Best Split Times", StandardComparisonGeneratorsFactory.GetShortComparisonName("Best Split Times"));
        Assert.Equal("Rival", StandardComparisonGeneratorsFactory.GetShortComparisonName("Rival"));
    }

    [Fact]
    public void FrameworkTimeSpan_FromSeconds_Rounds_Half_Away_From_Zero_To_Milliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(2063), FrameworkTimeSpan.FromSeconds(2.0625));
        Assert.Equal(TimeSpan.FromMilliseconds(-2063), FrameworkTimeSpan.FromSeconds(-2.0625));
        Assert.Equal(TimeSpan.FromMilliseconds(1), FrameworkTimeSpan.FromSeconds(0.0005));
        Assert.Equal(TimeSpan.Zero, FrameworkTimeSpan.FromSeconds(0.0004999));
    }

    [Fact]
    public void FrameworkTimeSpan_TotalSeconds_Multiplies_By_SecondsPerTick()
    {
        // 13 ticks: 13 * (1 / 10^7) is one bit below 13 / 10^7, which .NET 10 returns.
        Assert.Equal(1.2999999999999998e-06, FrameworkTimeSpan.TotalSeconds(TimeSpan.FromTicks(13)));
        Assert.Equal(1.3e-06, TimeSpan.FromTicks(13).TotalSeconds);
    }

    [Fact]
    public void Average_Segments_Truncates_As_Dotnet_Framework_Does()
    {
        // One attempt of 1:23.4567894: TotalSeconds lands one bit low on .NET Framework, so the
        // tick truncation in CalculateAverage gives ...893 on Windows (.NET 10 would give ...894).
        Run run = SingleSegmentRun(TimeSpan.FromTicks(834_567_894));

        new AverageSegmentsComparisonGenerator(run).Generate(new ComparisonGeneratorSettings());

        Assert.Equal(TimeSpan.FromTicks(834_567_893), run[0].Comparisons["Average Segments"].RealTime);
        Assert.Null(run[0].Comparisons["Average Segments"].GameTime);
    }

    [Theory]
    [InlineData(20_625_000L, 20_630_000L)] // 2.0625 s rounds half up to 2.063 s
    [InlineData(25_005_000L, 25_000_000L)] // 2.5005 s: TotalSeconds is just below, so 2.500 s
    public void Golf_HCP_Rounds_Segments_To_Milliseconds_As_Dotnet_Framework_Does(long segmentTicks, long expectedTicks)
    {
        Run run = SingleSegmentRun(TimeSpan.FromTicks(segmentTicks));

        new HCPComparisonGenerator(run).Generate(new ComparisonGeneratorSettings());

        Assert.Equal(TimeSpan.FromTicks(expectedTicks), run[0].Comparisons["Golf HCP"].RealTime);
        Assert.Null(run[0].Comparisons["Golf HCP"].GameTime);
    }

    [Fact]
    public void Generated_Comparisons_Are_Not_Saved_To_The_Lss()
    {
        Run run = XmlRunSerializer.LoadFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "livesplit1.6.lss"));
        List<string> customBefore = [.. run.CustomComparisons];
        run.ComparisonGenerators.Clear();
        run.ComparisonGenerators.AddRange(StandardComparisonGeneratorsFactory.GetAllGenerators(run));
        run.RegenerateComparisons(new ComparisonGeneratorSettings());
        Assert.NotNull(run[^1].Comparisons["Best Segments"].RealTime);

        using var stream = new MemoryStream();
        XmlRunSerializer.Save(run, stream);
        stream.Position = 0;
        var document = new XmlDocument();
        document.Load(stream);
        IEnumerable<string> savedNames = document.SelectNodes("//SplitTime/@name")!
            .Cast<XmlAttribute>()
            .Select(a => a.Value)
            .Distinct();

        Assert.Equal(customBefore, run.CustomComparisons);
        Assert.Equal(customBefore.Order(StringComparer.Ordinal), savedNames.Order(StringComparer.Ordinal));
    }

    private static Run SingleSegmentRun(TimeSpan segmentTime)
    {
        var run = new Run();
        run.AddSegment("A");
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(segmentTime), null, null, null));
        run[0].SegmentHistory[1] = new SplitTime(segmentTime);
        return run;
    }
}
