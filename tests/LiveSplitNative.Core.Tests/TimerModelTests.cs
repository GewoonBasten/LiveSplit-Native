using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

public class TimerModelTests
{
    private static (TimerModel model, LiveSplitState state, Run run) Create()
    {
        var run = new Run();
        run.AddSegment("A");
        run.AddSegment("B");
        run.AddSegment("C");
        var state = new LiveSplitState(run);
        var model = new TimerModel { CurrentState = state };
        return (model, state, run);
    }

    [Fact]
    public void Start_Sets_Running_And_Index_Zero()
    {
        var (model, state, run) = Create();
        int attempts = run.AttemptCount;
        model.Start();
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
        Assert.Equal(0, state.CurrentSplitIndex);
        Assert.Equal(attempts + 1, run.AttemptCount);
    }

    [Fact]
    public void Split_Advances_Only_When_RealTime_Positive()
    {
        var (model, state, _) = Create();
        model.Start();
        // Busy-wait until CurrentTime is positive (upstream requires RealTime > 0).
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (state.CurrentTime.RealTime <= TimeSpan.Zero && sw.ElapsedMilliseconds < 200)
        {
            Thread.Sleep(1);
        }

        Assert.True(state.CurrentTime.RealTime > TimeSpan.Zero);
        model.Split();
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.NotNull(state.Run[0].SplitTime.RealTime);
    }

    [Fact]
    public void Skip_Undo_Pause_Resume_Reset_Match_Phases()
    {
        var (model, state, run) = Create();
        model.Start();
        Thread.Sleep(5);
        model.Split();
        model.SkipSplit();
        Assert.Equal(2, state.CurrentSplitIndex);

        model.UndoSplit();
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.Null(state.Run[1].SplitTime.RealTime);

        model.Pause();
        Assert.Equal(TimerPhase.Paused, state.CurrentPhase);
        TimeSpan pausedAt = state.CurrentTime.RealTime!.Value;
        Thread.Sleep(15);
        Assert.Equal(pausedAt, state.CurrentTime.RealTime);

        model.Pause(); // resume
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);

        model.Reset();
        Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);
        Assert.Equal(-1, state.CurrentSplitIndex);
        Assert.True(run.AttemptHistory.Count >= 1);
        Assert.All(run.Segments, s => Assert.Null(s.SplitTime.RealTime));
    }

    [Fact]
    public void Pause_From_NotRunning_Starts()
    {
        var (model, state, _) = Create();
        model.Pause();
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
    }

    [Fact]
    public void CurrentTime_Uses_Monotonic_AdjustedStart()
    {
        var (model, state, _) = Create();
        model.Start();
        TimeStamp a = TimeStamp.Now;
        Thread.Sleep(20);
        TimeStamp b = TimeStamp.Now;
        Assert.True(b - a >= TimeSpan.FromMilliseconds(15));
        Assert.True(state.CurrentTime.RealTime > TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void Ending_Run_Sets_Ended_Phase()
    {
        var (model, state, _) = Create();
        model.Start();
        Thread.Sleep(5);
        model.Split();
        Thread.Sleep(5);
        model.Split();
        Thread.Sleep(5);
        model.Split();
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.Equal(3, state.CurrentSplitIndex);
    }
}
