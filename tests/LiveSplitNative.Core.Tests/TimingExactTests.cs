using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// Exact-value timing tests on a manual clock. Every expected value follows upstream
/// LiveSplit TimerModel / LiveSplitState semantics (93a11b85), down to the 100 ns tick.
/// </summary>
public class TimingExactTests
{
    private static (TimerModel Model, LiveSplitState State, Run Run) Create(params string[] segments)
    {
        var run = new Run();
        foreach (string name in segments.Length == 0 ? new[] { "A", "B", "C" } : segments)
        {
            run.AddSegment(name);
        }

        var state = new LiveSplitState(run);
        return (new TimerModel { CurrentState = state }, state, run);
    }

    private static TimeSpan Ms(double ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Split_Records_The_Clock_Exactly()
    {
        using var clock = ManualTimeSource.Install(TimeSpan.FromHours(3));
        var (model, state, run) = Create();

        model.Start();
        clock.Advance(TimeSpan.FromTicks(12_345_678)); // 1.2345678 s
        model.Split();

        Assert.Equal(TimeSpan.FromTicks(12_345_678), run[0].SplitTime.RealTime);
        Assert.Equal(1, state.CurrentSplitIndex);
    }

    [Fact]
    public void Reading_The_Time_Does_Not_Change_It()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, _) = Create();
        model.Start();
        clock.AdvanceMilliseconds(500);

        // A display refreshing at any rate only reads; the value stays put until the clock moves.
        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(Ms(500), state.CurrentTime.RealTime);
        }
    }

    [Fact]
    public void Split_Is_Refused_Until_RealTime_Is_Positive()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, run) = Create();
        run.Offset = TimeSpan.FromSeconds(-2);

        model.Start();
        Assert.Equal(TimeSpan.FromSeconds(-2), state.CurrentTime.RealTime);

        clock.AdvanceMilliseconds(1999);
        model.Split();
        Assert.Equal(0, state.CurrentSplitIndex);

        clock.AdvanceMilliseconds(2);
        model.Split();
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.Equal(Ms(1), run[0].SplitTime.RealTime);
    }

    [Fact]
    public void Pause_Excludes_Paused_Time_And_Reports_It()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, run) = Create();

        model.Start();
        clock.AdvanceMilliseconds(1000);
        model.Pause();
        clock.AdvanceMilliseconds(5000);
        Assert.Equal(Ms(1000), state.CurrentTime.RealTime);
        Assert.Equal(Ms(5000), state.PauseTime);

        model.Pause(); // resume
        clock.AdvanceMilliseconds(500);
        model.Split();

        Assert.Equal(Ms(1500), run[0].SplitTime.RealTime);
        Assert.Equal(Ms(5000), state.PauseTime);
    }

    [Fact]
    public void UndoAllPauses_Adds_The_Paused_Time_Back()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, _) = Create();

        model.Start();
        clock.AdvanceMilliseconds(1000);
        model.Pause();
        clock.AdvanceMilliseconds(5000);
        model.Pause();
        clock.AdvanceMilliseconds(500);

        model.UndoAllPauses();

        Assert.Equal(Ms(6500), state.CurrentTime.RealTime);
        Assert.Null(state.PauseTime);
    }

    [Fact]
    public void A_Zero_Length_Pause_Still_Counts_As_A_Pause()
    {
        // Upstream compares TimeStamp references: after any resume PauseTime is 0, not
        // null, until UndoAllPauses. Found by the upstream differential test.
        using var clock = ManualTimeSource.Install();
        var (model, state, _) = Create();

        model.Start();
        clock.AdvanceMilliseconds(1000);
        Assert.Null(state.PauseTime);

        model.Pause();
        model.Pause();
        Assert.Equal(TimeSpan.Zero, state.PauseTime);

        model.UndoAllPauses();
        Assert.Null(state.PauseTime);
    }

    [Fact]
    public void UndoAllPauses_After_The_Run_Ended_Moves_The_Final_Split()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, run) = Create("Only");

        model.Start();
        clock.AdvanceMilliseconds(1000);
        model.Pause();
        clock.AdvanceMilliseconds(2000);
        model.Pause();
        clock.AdvanceMilliseconds(1000);
        model.Split();
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.Equal(Ms(2000), run[0].SplitTime.RealTime);

        model.UndoAllPauses();

        Assert.Equal(Ms(4000), run[0].SplitTime.RealTime);
    }

    [Fact]
    public void GameTime_Subtracts_Loading_Times()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, run) = Create();

        model.Start();
        state.IsGameTimeInitialized = true;
        clock.AdvanceMilliseconds(4000);
        state.LoadingTimes = Ms(300);
        model.Split();

        Assert.Equal(Ms(4000), run[0].SplitTime.RealTime);
        Assert.Equal(Ms(3700), run[0].SplitTime.GameTime);
    }

    [Fact]
    public void GameTime_Pause_Freezes_GameTime_Only()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, _) = Create();

        model.Start();
        state.IsGameTimeInitialized = true;
        clock.AdvanceMilliseconds(1000);
        state.IsGameTimePaused = true;
        clock.AdvanceMilliseconds(2000);
        Assert.Equal(Ms(3000), state.CurrentTime.RealTime);
        Assert.Equal(Ms(1000), state.CurrentTime.GameTime);

        state.IsGameTimePaused = false;
        Assert.Equal(Ms(2000), state.LoadingTimes);
        clock.AdvanceMilliseconds(1000);

        Assert.Equal(Ms(4000), state.CurrentTime.RealTime);
        Assert.Equal(Ms(2000), state.CurrentTime.GameTime);
    }

    [Fact]
    public void Uninitialised_GameTime_Is_Null()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, _) = Create();
        model.Start();
        clock.AdvanceMilliseconds(100);

        Assert.Null(state.CurrentTime.GameTime);
    }

    [Fact]
    public void Undo_After_The_End_Resumes_With_No_Time_Lost()
    {
        using var clock = ManualTimeSource.Install();
        var (model, state, run) = Create("A", "B");

        model.Start();
        clock.AdvanceMilliseconds(1000);
        model.Split();
        clock.AdvanceMilliseconds(1000);
        model.Split();
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);

        model.UndoSplit();
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
        Assert.Null(run[1].SplitTime.RealTime);

        clock.AdvanceMilliseconds(250);
        model.Split();
        Assert.Equal(Ms(2250), run[1].SplitTime.RealTime);
    }

    [Fact]
    public void First_Completed_Run_Becomes_PB_With_Golds_And_History()
    {
        using var clock = ManualTimeSource.Install();
        var (model, _, run) = Create("A", "B");

        model.Start();
        clock.AdvanceMilliseconds(10_000);
        model.Split();
        clock.AdvanceMilliseconds(15_000);
        model.Split();
        model.Reset();

        Assert.Equal(TimeSpan.FromSeconds(10), run[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(25), run[1].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(10), run[0].BestSegmentTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(15), run[1].BestSegmentTime.RealTime);

        Attempt attempt = Assert.Single(run.AttemptHistory);
        Assert.Equal(1, attempt.Index);
        Assert.Equal(TimeSpan.FromSeconds(25), attempt.Time.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(10), run[0].SegmentHistory[1].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(15), run[1].SegmentHistory[1].RealTime);
    }

    [Fact]
    public void Slower_Run_Keeps_PB_But_Takes_The_Faster_Gold()
    {
        using var clock = ManualTimeSource.Install();
        var (model, _, run) = Create("A", "B");

        model.Start();
        clock.AdvanceMilliseconds(10_000);
        model.Split();
        clock.AdvanceMilliseconds(15_000);
        model.Split();
        model.Reset();

        model.Start();
        clock.AdvanceMilliseconds(9_000);
        model.Split();
        clock.AdvanceMilliseconds(18_000);
        model.Split();
        model.Reset();

        Assert.Equal(TimeSpan.FromSeconds(25), run[1].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(9), run[0].BestSegmentTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(15), run[1].BestSegmentTime.RealTime);
        Assert.Equal(2, run.AttemptHistory.Count);
        Assert.Equal(TimeSpan.FromSeconds(27), run.AttemptHistory[^1].Time.RealTime);
    }

    [Fact]
    public void Reset_Without_Saving_Leaves_History_Untouched()
    {
        using var clock = ManualTimeSource.Install();
        var (model, _, run) = Create("A", "B");

        model.Start();
        clock.AdvanceMilliseconds(10_000);
        model.Split();
        model.Reset(updateSplits: false);

        Assert.Empty(run.AttemptHistory);
        Assert.Null(run[0].BestSegmentTime.RealTime);
        Assert.All(run.Segments, s => Assert.Null(s.SplitTime.RealTime));
    }

    [Fact]
    public void Skipped_Segment_Has_No_Time_And_No_Gold()
    {
        using var clock = ManualTimeSource.Install();
        var (model, _, run) = Create("A", "B", "C");

        model.Start();
        clock.AdvanceMilliseconds(10_000);
        model.SkipSplit();
        clock.AdvanceMilliseconds(5_000);
        model.Split();
        Assert.Null(run[0].SplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(15), run[1].SplitTime.RealTime);

        model.Reset();

        Assert.Null(run[0].BestSegmentTime.RealTime);
        // The segment after a skip measures from the last real split (the start here).
        Assert.Equal(TimeSpan.FromSeconds(15), run[1].BestSegmentTime.RealTime);
    }

    [Fact]
    public void Split_Copies_Custom_Variables_Into_The_Segment()
    {
        using var clock = ManualTimeSource.Install();
        var (model, _, run) = Create("A", "B");
        run.Metadata.PermanentCustomVariables["Deaths"] = "3";

        model.Start();
        clock.AdvanceMilliseconds(100);
        model.Split();
        run.Metadata.PermanentCustomVariables["Deaths"] = "5";
        clock.AdvanceMilliseconds(100);
        model.Split();

        Assert.Equal("3", run[0].CustomVariableValues["Deaths"]);
        Assert.Equal("5", run[1].CustomVariableValues["Deaths"]);
    }

    [Fact]
    public void Manual_Clock_Is_Removed_When_Disposed()
    {
        ITimeSource before = TimeStamp.Source;
        using (ManualTimeSource.Install())
        {
            Assert.Equal("Manual", TimeStamp.Source.Name);
        }

        Assert.Same(before, TimeStamp.Source);
    }
}
