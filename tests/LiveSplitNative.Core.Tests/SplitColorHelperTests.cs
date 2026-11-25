using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class SplitColorHelperTests
{
    [Fact]
    public void GetSplitColor_AheadGaining_And_AheadLosing()
    {
        (LiveSplitState state, LayoutSettings layout) = SeedTwoSplits();
        // Split 0: −2s vs PB
        state.Run[0].SplitTime = new SplitTime(TimeSpan.FromSeconds(28));
        TimeSpan? d0 = LiveSplitStateHelper.GetLastDelta(state, 0, Run.PersonalBestComparisonName, TimingMethod.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(-2), d0);

        ArgbColor? c0 = LiveSplitStateHelper.GetSplitColor(
            state, layout, d0, 0, showSegmentDeltas: true, showBestSegments: false,
            Run.PersonalBestComparisonName, TimingMethod.RealTime);
        Assert.Equal(layout.AheadGainingTimeColor, c0);

        // Split 1: −1s vs PB (still ahead, but worse than last delta → Ahead Losing)
        state.Run[1].SplitTime = new SplitTime(TimeSpan.FromSeconds(69));
        TimeSpan? d1 = TimeSpan.FromSeconds(-1);
        ArgbColor? c1 = LiveSplitStateHelper.GetSplitColor(
            state, layout, d1, 1, showSegmentDeltas: true, showBestSegments: false,
            Run.PersonalBestComparisonName, TimingMethod.RealTime);
        Assert.Equal(layout.AheadLosingTimeColor, c1);
    }

    [Fact]
    public void GetSplitColor_BehindGaining_And_BehindLosing()
    {
        (LiveSplitState state, LayoutSettings layout) = SeedTwoSplits();
        state.Run[0].SplitTime = new SplitTime(TimeSpan.FromSeconds(34)); // +4
        TimeSpan? d0 = TimeSpan.FromSeconds(4);
        Assert.Equal(
            layout.BehindLosingTimeColor,
            LiveSplitStateHelper.GetSplitColor(
                state, layout, d0, 0, true, false, Run.PersonalBestComparisonName, TimingMethod.RealTime));

        state.Run[1].SplitTime = new SplitTime(TimeSpan.FromSeconds(72)); // +2 vs PB, gaining vs last
        TimeSpan? d1 = TimeSpan.FromSeconds(2);
        Assert.Equal(
            layout.BehindGainingTimeColor,
            LiveSplitStateHelper.GetSplitColor(
                state, layout, d1, 1, true, false, Run.PersonalBestComparisonName, TimingMethod.RealTime));
    }

    [Fact]
    public void GetSplitColor_BestSegment_Overrides_When_Enabled()
    {
        (LiveSplitState state, LayoutSettings layout) = SeedTwoSplits();
        layout.ShowBestSegments = true;
        state.Run[0].SplitTime = new SplitTime(TimeSpan.FromSeconds(28));
        state.Run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(30));
        // No Best Segments comparison → CheckBestSegment uses BestSegmentTime (cur < best).
        ArgbColor? color = LiveSplitStateHelper.GetSplitColor(
            state, layout, TimeSpan.FromSeconds(-2), 0,
            showSegmentDeltas: true, showBestSegments: true,
            Run.PersonalBestComparisonName, TimingMethod.RealTime);
        Assert.Equal(layout.BestSegmentColor, color);
    }

    [Fact]
    public void LayoutSettings_StandardColors_Match_DefaultAppearance()
    {
        LayoutSettings s = LayoutSettings.CreateStandard();
        Assert.Equal(ArgbColor.FromTuple(LiveSplitNative.Core.Appearance.DefaultLayoutAppearance.AheadGainingTimeColor), s.AheadGainingTimeColor);
        Assert.Equal(ArgbColor.FromTuple(LiveSplitNative.Core.Appearance.DefaultLayoutAppearance.AheadLosingTimeColor), s.AheadLosingTimeColor);
        Assert.Equal(ArgbColor.FromTuple(LiveSplitNative.Core.Appearance.DefaultLayoutAppearance.BehindGainingTimeColor), s.BehindGainingTimeColor);
        Assert.Equal(ArgbColor.FromTuple(LiveSplitNative.Core.Appearance.DefaultLayoutAppearance.BehindLosingTimeColor), s.BehindLosingTimeColor);
    }

    private static (LiveSplitState State, LayoutSettings Layout) SeedTwoSplits()
    {
        Run run = Run.CreateStandard();
        run.ClearSegments();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(30));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(70));
        return (new LiveSplitState(run), LayoutSettings.CreateStandard());
    }
}
