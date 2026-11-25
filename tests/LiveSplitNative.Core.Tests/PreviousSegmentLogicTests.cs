using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>PreviousSegment.Update (LiveSplit.PreviousSegment, 93a11b85).</summary>
public class PreviousSegmentLogicTests
{
    [Fact]
    public void Shows_The_Previous_Segment_Delta_And_Possible_Time_Save()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        var run = new Run();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(20));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(8));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(9));
        var state = new LiveSplitState(run);
        var model = new TimerModel { CurrentState = state };
        var layout = new LayoutSettings();
        var settings = new PreviousSegmentComponentSettings { ShowPossibleTimeSave = true, TimeSaveAccuracy = TimeAccuracy.Seconds };

        // Not running: a dash, and no time save.
        PreviousSegmentView idle = PreviousSegmentLogic.Compute(state, settings, layout);
        Assert.Equal("Previous Segment", idle.Name);
        Assert.Equal("- / -", idle.Value);

        model.Start();
        clock.Advance(TimeSpan.FromSeconds(11));
        model.Split();
        // Segment A took 11 s against the PB's 10: +1.0; PB segment 10 - best 8 = 2 s to save.
        PreviousSegmentView after = PreviousSegmentLogic.Compute(state, settings, layout);
        Assert.Equal("+1.0 / 2", after.Value);
        Assert.Contains("Prev. Seg.", after.AlternateNames);

        // With a chosen comparison the name says which.
        settings.Comparison = "Best Segments";
        Assert.Equal("Previous Segment (Best)", PreviousSegmentLogic.Compute(state, settings, layout).Name);
    }
}
