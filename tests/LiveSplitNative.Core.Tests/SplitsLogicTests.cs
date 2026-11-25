using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>LiveSplit.Splits SplitsComponent.Update (93a11b85): the rows' splits and the scroll position.</summary>
public class SplitsLogicTests
{
    private static LiveSplitState State(int splits, int currentSplit)
    {
        var run = new Run();
        for (int i = 0; i < splits; i++)
        {
            run.AddSegment($"Split {i}");
        }

        return new LiveSplitState(run)
        {
            CurrentPhase = currentSplit < 0 ? TimerPhase.NotRunning : TimerPhase.Running,
            CurrentSplitIndex = currentSplit,
        };
    }

    private static int[] Range(int from, int to) => Enumerable.Range(from, to - from + 1).ToArray();

    [Fact]
    public void Rows_Leave_Upcoming_Splits_Below_The_Current_One()
    {
        var settings = new SplitsComponentSettings { VisualSplitCount = 16, SplitPreviewCount = 1, AlwaysShowLastSplit = true };
        Assert.Equal([.. Range(0, 14), 19], new SplitsLogic().Update(State(20, -1), settings).ToArray());
        Assert.Equal([.. Range(0, 14), 19], new SplitsLogic().Update(State(20, 13), settings).ToArray());
        // Split 14 is current: the list moves so that split 15 still shows below it.
        Assert.Equal([.. Range(1, 15), 19], new SplitsLogic().Update(State(20, 14), settings).ToArray());
        Assert.Equal([.. Range(4, 18), 19], new SplitsLogic().Update(State(20, 20), settings).ToArray());

        settings.AlwaysShowLastSplit = false;
        Assert.Equal(Range(0, 15), new SplitsLogic().Update(State(20, 14), settings).ToArray());
        Assert.Equal(Range(1, 16), new SplitsLogic().Update(State(20, 15), settings).ToArray());

        // Fewer splits than rows: every split once.
        settings.AlwaysShowLastSplit = true;
        Assert.Equal([0, 1, 2], new SplitsLogic().Update(State(3, 1), settings).ToArray());
    }

    [Fact]
    public void Scrolling_Moves_The_List_Within_The_Run_And_Resets()
    {
        var settings = new SplitsComponentSettings { VisualSplitCount = 16, SplitPreviewCount = 1, AlwaysShowLastSplit = true };
        LiveSplitState state = State(20, 14);
        var logic = new SplitsLogic();
        for (int i = 0; i < 5; i++)
        {
            logic.ScrollUp();
        }

        Assert.Equal([.. Range(0, 14), 19], logic.Update(state, settings).ToArray());
        Assert.Equal(-1, logic.ScrollOffset); // clamped to the top of the run

        for (int i = 0; i < 10; i++)
        {
            logic.ScrollDown();
        }

        Assert.Equal([.. Range(4, 18), 19], logic.Update(state, settings).ToArray());
        Assert.Equal(3, logic.ScrollOffset); // clamped to the end of the run

        logic.ResetScroll();
        Assert.Equal([.. Range(1, 15), 19], logic.Update(state, settings).ToArray());
    }

    [Fact]
    public void Session_Scroll_Raises_The_Model_Events_And_Changes_Nothing_Else()
    {
        var run = new Run();
        run.AddSegment("A");
        var session = new TimerSession(new LiveSplitState(run), () => new ComparisonGeneratorSettings());
        var seen = new List<string>();
        session.Model.OnScrollUp += (_, _) => seen.Add("up");
        session.Model.OnScrollDown += (_, _) => seen.Add("down");
        var changes = new List<SessionChange>();
        session.Changed += (_, e) => changes.Add(e.Change);

        session.Scroll(up: true);
        session.Scroll(up: false);

        Assert.Equal(["up", "down"], seen.ToArray());
        Assert.Equal([SessionChange.None, SessionChange.None], changes.ToArray());
        Assert.Equal(TimerPhase.NotRunning, session.State.CurrentPhase);
    }
}
