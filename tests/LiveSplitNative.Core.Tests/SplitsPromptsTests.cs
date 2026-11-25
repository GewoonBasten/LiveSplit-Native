using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// Windows TimerForm's rules for "Save as Personal Best?", "Update Times?" and what SaveSplits
/// writes mid-attempt (a reset copy). The copy itself is checked against upstream in the
/// Upstream tests; these cover the decisions case by case.
/// </summary>
public class SplitsPromptsTests
{
    [Fact]
    public void Pb_Prompt_While_Running_Or_Paused_Not_When_Idle()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewTimer(2);
        Assert.False(SplitsPrompts.ShouldOfferSaveAsPersonalBest(state));

        model.Start();
        Assert.True(SplitsPrompts.ShouldOfferSaveAsPersonalBest(state));
        clock.Advance(TimeSpan.FromSeconds(1));
        model.Pause();
        Assert.True(SplitsPrompts.ShouldOfferSaveAsPersonalBest(state));
    }

    [Fact]
    public void Pb_Prompt_After_The_End_Only_When_The_Pb_Was_Not_Beaten()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewTimer(1);

        // No PB yet: finishing is a PB, nothing to ask.
        Finish(model, clock, TimeSpan.FromSeconds(10));
        Assert.False(SplitsPrompts.ShouldOfferSaveAsPersonalBest(state));
        model.Reset();

        // Slower than the 10 s PB, and equal to it: ask.
        Finish(model, clock, TimeSpan.FromSeconds(12));
        Assert.True(SplitsPrompts.ShouldOfferSaveAsPersonalBest(state));
        model.Reset();
        Finish(model, clock, TimeSpan.FromSeconds(10));
        Assert.True(SplitsPrompts.ShouldOfferSaveAsPersonalBest(state));
        model.Reset();

        // Faster: a new PB, nothing to ask.
        Finish(model, clock, TimeSpan.FromSeconds(9));
        Assert.False(SplitsPrompts.ShouldOfferSaveAsPersonalBest(state));
    }

    [Fact]
    public void Reset_Warning_For_A_Gold_A_First_Pb_Or_A_Better_Pb_Only()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewTimer(2);
        Assert.False(SplitsPrompts.ShouldWarnBeforeReset(state));

        // Started, nothing split: nothing beaten.
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(SplitsPrompts.ShouldWarnBeforeReset(state));

        // First split of a fresh run is a gold.
        model.Split();
        Assert.True(SplitsPrompts.ShouldWarnBeforeReset(state));
        clock.Advance(TimeSpan.FromSeconds(4));
        model.Split();
        Assert.True(SplitsPrompts.ShouldWarnBeforeReset(state));
        model.Reset();

        // 5 s + 5 s: slower on both segments and overall, nothing to warn about.
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(5));
        model.Split();
        Assert.False(SplitsPrompts.ShouldWarnBeforeReset(state));
        clock.Advance(TimeSpan.FromSeconds(5));
        model.Split();
        Assert.False(SplitsPrompts.ShouldWarnBeforeReset(state));
        model.Reset();

        // Gold on segment 2 (3 s < 4 s) though the run is slower overall: warn.
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(6));
        model.Split();
        clock.Advance(TimeSpan.FromSeconds(3));
        model.Split();
        Assert.True(SplitsPrompts.ShouldWarnBeforeReset(state));
    }

    [Fact]
    public void Reset_Warning_For_A_Better_Pb_Without_Any_Gold()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewTimer(3);
        model.Start();
        for (int i = 0; i < 3; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(4));
            model.Split();
        }

        model.Reset(); // PB 12 s, golds 4 s each

        // 5 s, skip, 11 s: segment 3 counts as 6 s (no gold) but 11 s beats the 12 s PB.
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(5));
        model.Split();
        model.SkipSplit();
        clock.Advance(TimeSpan.FromSeconds(6));
        model.Split();
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.False(Enumerable.Range(0, 3).Any(i => LiveSplitStateHelper.CheckBestSegment(state, i, TimingMethod.RealTime)));
        Assert.True(SplitsPrompts.ShouldWarnBeforeReset(state));
    }

    [Fact]
    public void Reset_Warning_Uses_The_Current_Timing_Method()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewTimer(1);
        state.CurrentTimingMethod = TimingMethod.GameTime;

        // Game time never started: the split has no game time, so nothing counts as beaten.
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(4));
        model.Split();
        Assert.Null(state.Run[0].SplitTime.GameTime);
        Assert.False(SplitsPrompts.ShouldWarnBeforeReset(state));

        state.CurrentTimingMethod = TimingMethod.RealTime;
        Assert.True(SplitsPrompts.ShouldWarnBeforeReset(state));
    }

    [Fact]
    public void Reset_Copy_Records_The_Attempt_And_Leaves_The_Live_Timer_Alone()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewTimer(3);
        state.Run.Metadata.RunId = "abc";
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(7));
        model.Split();
        clock.Advance(TimeSpan.FromSeconds(1));

        Run saved = SplitsPrompts.ResetCopyForSaving(state);

        Assert.Single(saved.AttemptHistory);
        Assert.Equal(TimeSpan.FromSeconds(7), saved[0].BestSegmentTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(7), saved[0].SegmentHistory[1].RealTime);
        Assert.Null(saved[0].SplitTime.RealTime);
        Assert.Equal("abc", saved.Metadata.RunId);

        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.Equal(TimeSpan.FromSeconds(7), state.Run[0].SplitTime.RealTime);
        Assert.Empty(state.Run.AttemptHistory);
        Assert.Null(state.Run[0].BestSegmentTime.RealTime);
        Assert.Empty(state.Run[0].SegmentHistory);
        Assert.Equal(TimeSpan.FromSeconds(8), state.CurrentTime.RealTime);

        // Finishing the live attempt afterwards still works and is the run's first attempt.
        model.Split();
        model.Split();
        model.Reset();
        Assert.Single(state.Run.AttemptHistory);
    }

    [Fact]
    public void Run_Clone_Is_Deep()
    {
        var run = new Run { GameName = "G", CategoryName = "C", AttemptCount = 3, HasChanged = true };
        run.AddSegment("A");
        run[0].Comparisons["Personal Best"] = new SplitTime(TimeSpan.FromSeconds(1));
        run[0].SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(1));
        run[0].CustomVariableValues["x"] = "1";
        run[0].UnknownOuterXml.Add("<Foo />");
        run.CustomComparisons.Add("Custom");
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(TimeSpan.FromSeconds(1)), null, null, null));
        run.Metadata.VariableValueNames["Platform"] = "Wii";
        run.UnknownRootOuterXml.Add("<Bar />");

        Run copy = run.Clone();
        copy.GameName = "changed";
        copy[0].Name = "changed";
        copy[0].Comparisons["Personal Best"] = default;
        copy[0].SegmentHistory.Clear();
        copy[0].CustomVariableValues.Clear();
        copy[0].UnknownOuterXml.Clear();
        copy.CustomComparisons.Clear();
        copy.AttemptHistory[0].Index = 99;
        copy.Metadata.VariableValueNames.Clear();
        copy.UnknownRootOuterXml.Clear();

        Assert.Equal("G", run.GameName);
        Assert.Equal("A", run[0].Name);
        Assert.Equal(TimeSpan.FromSeconds(1), run[0].PersonalBestSplitTime.RealTime);
        Assert.Single(run[0].SegmentHistory);
        Assert.Single(run[0].CustomVariableValues);
        Assert.Single(run[0].UnknownOuterXml);
        Assert.Equal(new[] { "Personal Best", "Custom" }, run.CustomComparisons);
        Assert.Equal(1, run.AttemptHistory[0].Index);
        Assert.Single(run.Metadata.VariableValueNames);
        Assert.Single(run.UnknownRootOuterXml);
        Assert.True(copy.HasChanged);
        Assert.Equal(3, copy.AttemptCount);
        Assert.Equal(run.ComparisonGenerators, copy.ComparisonGenerators);
    }

    private static (LiveSplitState State, TimerModel Model) NewTimer(int segments)
    {
        var run = new Run();
        for (int i = 0; i < segments; i++)
        {
            run.AddSegment("S" + i);
        }

        var state = new LiveSplitState(run);
        return (state, new TimerModel { CurrentState = state });
    }

    private static void Finish(TimerModel model, ManualTimeSourceScope clock, TimeSpan time)
    {
        model.Start();
        clock.Advance(time);
        model.Split();
        Assert.Equal(TimerPhase.Ended, model.CurrentState!.CurrentPhase);
    }
}
