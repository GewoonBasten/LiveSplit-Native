using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.Tests;

/// <summary>Run.FixSplits cases that once differed from upstream IRun.FixSplits (93a11b85).</summary>
public class RunFixSplitsTests
{
    [Fact]
    public void Imported_Real_Time_Is_Only_A_Duplicate_Of_Real_Times()
    {
        // Attempt 1 took 5 s real time and 2 s game time. The PB segment imported at
        // index 0 has 2 s real time: it equals no attempt's real time, so upstream keeps
        // it, even though an attempt's game time is also 2 s. Found by the differential test.
        var run = new Run();
        run.AddSegment("A");
        Segment segment = run[0];
        segment.BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2)), null, null, null));
        segment.SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2));
        segment.SegmentHistory[0] = new SplitTime(TimeSpan.FromSeconds(2), null);

        run.FixSplits();

        Assert.True(segment.SegmentHistory.ContainsKey(0));
        Assert.Equal(TimeSpan.FromSeconds(2), segment.SegmentHistory[0].RealTime);
    }

    [Fact]
    public void Imported_Real_Time_Equal_To_An_Attempt_Is_Removed()
    {
        var run = new Run();
        run.AddSegment("A");
        Segment segment = run[0];
        segment.BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(5), null);
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(TimeSpan.FromSeconds(5), null), null, null, null));
        segment.SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(5), null);
        segment.SegmentHistory[0] = new SplitTime(TimeSpan.FromSeconds(5), null);

        run.FixSplits();

        Assert.False(segment.SegmentHistory.ContainsKey(0));
        Assert.True(segment.SegmentHistory.ContainsKey(1));
    }
}
