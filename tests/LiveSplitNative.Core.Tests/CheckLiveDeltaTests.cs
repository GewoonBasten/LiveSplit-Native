using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class CheckLiveDeltaTests
{
    [Fact]
    public void CheckLiveDelta_Null_While_Ahead_Of_Comparison()
    {
        (LiveSplitState state, LayoutSettings layout) = SeedRunning(behindBySeconds: null, aheadBySeconds: 5);
        Assert.Null(LiveSplitStateHelper.CheckLiveDelta(
            state, layout, splitDelta: true, Run.PersonalBestComparisonName, TimingMethod.RealTime));
        Assert.Null(LiveSplitStateHelper.CheckLiveDelta(
            state, layout, splitDelta: false, Run.PersonalBestComparisonName, TimingMethod.RealTime));
    }

    [Fact]
    public void CheckLiveDelta_SplitDelta_When_Behind_Comparison_Split()
    {
        (LiveSplitState state, LayoutSettings layout) = SeedRunning(behindBySeconds: 8, aheadBySeconds: null);
        TimeSpan? live = LiveSplitStateHelper.CheckLiveDelta(
            state, layout, splitDelta: true, Run.PersonalBestComparisonName, TimingMethod.RealTime);
        Assert.NotNull(live);
        Assert.True(live > TimeSpan.FromSeconds(6));
        Assert.True(live < TimeSpan.FromSeconds(12));
    }

    [Fact]
    public void CheckLiveDelta_SegmentDelta_When_Behind_Comparison_Pace()
    {
        (LiveSplitState state, LayoutSettings layout) = SeedRunning(behindBySeconds: 8, aheadBySeconds: null);
        TimeSpan? live = LiveSplitStateHelper.CheckLiveDelta(
            state, layout, splitDelta: false, Run.PersonalBestComparisonName, TimingMethod.RealTime);
        Assert.NotNull(live);
        Assert.True(live > TimeSpan.Zero);
    }

    [Fact]
    public void GetTimerColor_Ended_PersonalBest_Vs_Behind()
    {
        Run run = Run.CreateStandard();
        run.ClearSegments();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(30));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(70));
        LayoutSettings layout = LayoutSettings.CreateStandard();
        var state = new LiveSplitState(run)
        {
            CurrentPhase = TimerPhase.Ended,
            CurrentSplitIndex = 2,
        };

        run[0].SplitTime = new SplitTime(TimeSpan.FromSeconds(28));
        run[1].SplitTime = new SplitTime(TimeSpan.FromSeconds(65)); // under PB finish
        Assert.Equal(layout.PersonalBestColor, LiveSplitStateHelper.GetTimerColor(state, layout));

        run[1].SplitTime = new SplitTime(TimeSpan.FromSeconds(80)); // over PB finish
        Assert.Equal(layout.BehindLosingTimeColor, LiveSplitStateHelper.GetTimerColor(state, layout));
    }

    [Fact]
    public void GetTimerColor_NotRunning_And_Paused()
    {
        (LiveSplitState state, LayoutSettings layout) = SeedRunning(behindBySeconds: null, aheadBySeconds: 1);
        state.CurrentPhase = TimerPhase.NotRunning;
        state.CurrentSplitIndex = -1;
        Assert.Equal(layout.NotRunningColor, LiveSplitStateHelper.GetTimerColor(state, layout));

        state.CurrentPhase = TimerPhase.Paused;
        state.CurrentSplitIndex = 0;
        state.TimePausedAt = TimeSpan.FromSeconds(12);
        Assert.Equal(layout.PausedColor, LiveSplitStateHelper.GetTimerColor(state, layout));
    }

    /// <summary>
    /// Puts the run in Running on split 0 with CurrentTime ≈ PB±offset via AdjustedStartTime.
    /// </summary>
    private static (LiveSplitState State, LayoutSettings Layout) SeedRunning(double? behindBySeconds, double? aheadBySeconds)
    {
        Run run = Run.CreateStandard();
        run.ClearSegments();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(30));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(70));
        LayoutSettings layout = LayoutSettings.CreateStandard();
        var state = new LiveSplitState(run)
        {
            CurrentPhase = TimerPhase.Running,
            CurrentSplitIndex = 0,
        };

        double offset = behindBySeconds ?? -(aheadBySeconds ?? 0);
        // CurrentTime = Now - AdjustedStartTime ≈ 30 + offset
        TimeSpan target = TimeSpan.FromSeconds(30 + offset);
        state.AdjustedStartTime = TimeStamp.Now - target;
        state.StartTimeWithOffset = state.AdjustedStartTime;
        state.StartTime = state.AdjustedStartTime;
        return (state, layout);
    }
}
