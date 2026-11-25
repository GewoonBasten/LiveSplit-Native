using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class DetailedTimerLogicTests
{
    [Theory]
    [InlineData("1", DigitsFormat.SingleDigitSeconds)]
    [InlineData("00:01", DigitsFormat.DoubleDigitMinutes)]
    [InlineData("0:00:01", DigitsFormat.SingleDigitHours)]
    [InlineData("00:00:01", DigitsFormat.DoubleDigitHours)]
    public void ParseDigitsFormat_Matches_Windows_Timer(string token, DigitsFormat expected) =>
        Assert.Equal(expected, DetailedTimerLogic.ParseDigitsFormat(token));

    [Theory]
    [InlineData("", TimeAccuracy.Seconds)]
    [InlineData(".2", TimeAccuracy.Tenths)]
    [InlineData(".23", TimeAccuracy.Hundredths)]
    [InlineData(".234", TimeAccuracy.Milliseconds)]
    public void ParseAccuracyToken_Matches_Windows_Timer(string token, TimeAccuracy expected) =>
        Assert.Equal(expected, DetailedTimerLogic.ParseAccuracyToken(token));

    [Fact]
    public void TimerFormat_RoundTrip()
    {
        var settings = new DetailedTimerComponentSettings
        {
            DigitsFormat = "00:01",
            Accuracy = TimeAccuracy.Tenths,
            SegmentDigitsFormat = "1",
            SegmentAccuracy = TimeAccuracy.Milliseconds,
            DisplayIcon = true,
            IconSize = 48,
            ShowSplitName = true,
            SegmentLabelsFontBold = true,
        };
        DetailedTimerComponentSettings again = DetailedTimerComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.Equal("00:01", again.DigitsFormat);
        Assert.Equal(TimeAccuracy.Tenths, again.Accuracy);
        Assert.Equal("1", again.SegmentDigitsFormat);
        Assert.Equal(TimeAccuracy.Milliseconds, again.SegmentAccuracy);
        Assert.True(again.DisplayIcon);
        Assert.Equal(48, again.IconSize, precision: 0);
        Assert.True(again.ShowSplitName);
        Assert.True(again.SegmentLabelsFontBold);
        Assert.Contains("TimerFormat", settings.ToInnerXml(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-Hall", 0, 3, "Hall")]
    [InlineData("{Temple} Entrance", 0, 3, "Entrance")]
    [InlineData("Done", 2, 3, "Done")]
    [InlineData("-Last", 2, 3, "-Last")] // last segment keeps leading dash
    public void FormatSplitName_Matches_Windows_Subsplit_Rules(string name, int index, int count, string expected) =>
        Assert.Equal(expected, DetailedTimerLogic.FormatSplitName(name, index, count));

    [Fact]
    public void ResolveComparisons_Hides_When_Same_Or_Missing()
    {
        var same = DetailedTimerLogic.ResolveComparisons(
            "Personal Best", "Personal Best", false, "Personal Best", ["Personal Best"]);
        Assert.True(same.HideComparison);

        var best = DetailedTimerLogic.ResolveComparisons(
            "Current Comparison", "Best Segments", false, "Personal Best", ["Personal Best", "Best Segments", "Average Segments"]);
        Assert.Equal("Personal Best", best.Comparison);
        Assert.Equal("Best Segments", best.Comparison2);
        Assert.False(best.HideComparison);

        // Best Segments switched off in Choose Active Comparisons: upstream hides comparison 2.
        var inactive = DetailedTimerLogic.ResolveComparisons(
            "Current Comparison", "Best Segments", false, "Personal Best", ["Personal Best", "Average Segments"]);
        Assert.Equal("Personal Best", inactive.Comparison);
        Assert.True(inactive.HideComparison);
    }

    [Fact]
    public void SegmentComparisonTime_Uses_Segment_Delta_Not_Split_Time()
    {
        var run = Run.CreateStandard();
        run.ClearSegments();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(25));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(9));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(14));

        // Current split index 1 → segment B PB delta = 25 - 10 = 15
        TimeSpan? pb = DetailedTimerLogic.SegmentComparisonTime(
            run, currentSplitIndex: 1, "Personal Best", TimingMethod.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(15), pb);

        TimeSpan? best = DetailedTimerLogic.SegmentComparisonTime(
            run, currentSplitIndex: 1, "Best Segments", TimingMethod.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(14), best);
    }

    [Fact]
    public void SegmentTimerTime_Uses_Engine_State_Not_Frames()
    {
        var run = Run.CreateStandard();
        run.ClearSegments();
        run.AddSegment("A");
        run.AddSegment("B");
        var state = new LiveSplitState(run)
        {
            CurrentPhase = TimerPhase.Running,
            CurrentSplitIndex = 1,
            // CurrentTime.RealTime = Now - AdjustedStartTime ≈ 40s
            AdjustedStartTime = TimeStamp.Now - TimeSpan.FromSeconds(40),
        };
        run[0].SplitTime = new SplitTime(TimeSpan.FromSeconds(12));

        TimeSpan? segment = DetailedTimerLogic.SegmentTimerTime(state, TimingMethod.RealTime);
        Assert.NotNull(segment);
        Assert.InRange(segment!.Value.TotalSeconds, 27.5, 28.5);
    }

    [Fact]
    public void ShortComparisonName_Matches_Standard_Generators()
    {
        Assert.Equal("PB", DetailedTimerLogic.ShortComparisonName("Personal Best"));
        Assert.Equal("Best", DetailedTimerLogic.ShortComparisonName("Best Segments"));
        Assert.Equal("Average", DetailedTimerLogic.ShortComparisonName("Average Segments"));
    }
}
