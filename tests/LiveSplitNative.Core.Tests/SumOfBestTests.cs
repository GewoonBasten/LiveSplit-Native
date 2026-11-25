using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.Tests;

public class SumOfBestTests
{
    [Fact]
    public void Simple_SumOfBest_Sums_Gold_Segments()
    {
        var run = new Run();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(20));

        TimeSpan? sob = SumOfBest.CalculateSumOfBest(run, simpleCalculation: true, useCurrentRun: false);
        Assert.Equal(TimeSpan.FromSeconds(30), sob);
    }

    [Fact]
    public void SumOfBest_Uses_Pb_When_Better_Than_Golds_Alone()
    {
        var run = new Run();
        run.AddSegment("A");
        run.AddSegment("B");
        // Artificial: golds are slow
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(50));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(50));
        // PB is faster overall (cumulative)
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(8));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(18));

        TimeSpan? sob = SumOfBest.CalculateSumOfBest(run, simpleCalculation: true, useCurrentRun: false);
        // Predictions take min of gold path and PB path — PB finishes at 18.
        Assert.Equal(TimeSpan.FromSeconds(18), sob);
    }
}
