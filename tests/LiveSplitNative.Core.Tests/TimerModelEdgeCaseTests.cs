using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

public class TimerModelEdgeCaseTests
{
    private static (TimerModel model, LiveSplitState state, Run run) Create(int segments = 3)
    {
        var run = new Run();
        for (int i = 0; i < segments; i++)
        {
            run.AddSegment($"S{i + 1}");
        }

        var state = new LiveSplitState(run);
        var model = new TimerModel { CurrentState = state };
        return (model, state, run);
    }

    private static void WaitPositive(LiveSplitState state, int timeoutMs = 200)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (state.CurrentTime.RealTime <= TimeSpan.Zero && sw.ElapsedMilliseconds < timeoutMs)
        {
            Thread.Sleep(1);
        }

        Assert.True(state.CurrentTime.RealTime > TimeSpan.Zero);
    }

    [Fact]
    public void DoubleStart_Does_Not_Double_Count_Attempts()
    {
        var (model, state, run) = Create();
        model.Start();
        int attempts = run.AttemptCount;
        int index = state.CurrentSplitIndex;
        model.Start();
        Assert.Equal(attempts, run.AttemptCount);
        Assert.Equal(index, state.CurrentSplitIndex);
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
    }

    [Fact]
    public void Undo_On_First_Split_Is_NoOp()
    {
        var (model, state, _) = Create();
        model.Start();
        model.UndoSplit();
        Assert.Equal(0, state.CurrentSplitIndex);
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
    }

    [Fact]
    public void Skip_Then_Undo_Restores_Index_And_Clears_Skipped()
    {
        var (model, state, run) = Create();
        model.Start();
        WaitPositive(state);
        model.SkipSplit();
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.Null(run[0].SplitTime.RealTime);
        model.UndoSplit();
        Assert.Equal(0, state.CurrentSplitIndex);
        Assert.Null(run[0].SplitTime.RealTime);
    }

    [Fact]
    public void Pause_Resume_Freezes_Then_Continues_Monotonic()
    {
        var (model, state, _) = Create();
        model.Start();
        WaitPositive(state);
        model.Pause();
        Assert.Equal(TimerPhase.Paused, state.CurrentPhase);
        TimeSpan frozen = state.CurrentTime.RealTime!.Value;
        Thread.Sleep(25);
        Assert.Equal(frozen, state.CurrentTime.RealTime);
        model.Pause();
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
        Thread.Sleep(15);
        Assert.True(state.CurrentTime.RealTime > frozen);
    }

    [Fact]
    public void AttemptCount_Increments_Only_On_Start()
    {
        var (model, _, run) = Create();
        Assert.Equal(0, run.AttemptCount);
        model.Start();
        Assert.Equal(1, run.AttemptCount);
        model.Reset(updateSplits: false);
        model.Start();
        Assert.Equal(2, run.AttemptCount);
    }

    [Fact]
    public void Reset_Without_Update_Skips_History_And_Golds()
    {
        var (model, state, run) = Create();
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(5));
        model.Start();
        WaitPositive(state);
        model.Split();
        WaitPositive(state);
        model.Split();
        WaitPositive(state);
        model.Split();
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        int historyBefore = run.AttemptHistory.Count;
        SplitTime goldBefore = run[0].BestSegmentTime;
        model.Reset(updateSplits: false);
        Assert.Equal(historyBefore, run.AttemptHistory.Count);
        Assert.Equal(goldBefore.RealTime, run[0].BestSegmentTime.RealTime);
        Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);
    }

    [Fact]
    public void Reset_With_Update_Records_Attempt_And_Can_Update_Golds()
    {
        var (model, state, run) = Create();
        model.Start();
        WaitPositive(state);
        // Force a known elapsed by sleeping then splitting all.
        Thread.Sleep(30);
        model.Split();
        Thread.Sleep(20);
        model.Split();
        Thread.Sleep(20);
        model.Split();
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        model.Reset(updateSplits: true);
        Assert.True(run.AttemptHistory.Count >= 1);
        Attempt last = run.AttemptHistory[^1];
        Assert.NotNull(last.Time.RealTime);
        Assert.True(run[0].BestSegmentTime.RealTime.HasValue);
    }

    [Fact]
    public void Completed_Run_Faster_Than_Pb_Promotes_Pb()
    {
        var (model, state, run) = Create(2);
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(25));
        model.Start();
        Thread.Sleep(15);
        model.Split();
        Thread.Sleep(15);
        model.Split();
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        model.Reset(updateSplits: true);
        Assert.True(run[1].PersonalBestSplitTime.RealTime < TimeSpan.FromSeconds(25));
    }

    [Fact]
    public void UndoAllPauses_Restores_AdjustedStart_To_StartWithOffset()
    {
        var (model, state, _) = Create();
        model.Start();
        WaitPositive(state);
        model.Pause();
        Thread.Sleep(20);
        model.Pause();
        Assert.NotEqual(state.StartTimeWithOffset, state.AdjustedStartTime);
        model.UndoAllPauses();
        Assert.Equal(state.StartTimeWithOffset, state.AdjustedStartTime);
    }

    [Fact]
    public void TimeStamp_Advances_Without_Ui_Frames()
    {
        TimeStamp a = TimeStamp.Now;
        Thread.Sleep(30);
        TimeStamp b = TimeStamp.Now;
        Assert.True(b - a >= TimeSpan.FromMilliseconds(20));
    }
}
