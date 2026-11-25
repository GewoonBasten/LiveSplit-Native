using System.Globalization;
using System.Text;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

/// <summary>F1: the timer session, its commands, events and input paths.</summary>
public class TimerSessionTests
{
    private static readonly TimerCommand[] AllCommands = Enum.GetValues<TimerCommand>();

    [Fact]
    public void StartOrSplit_Follows_Windows_TimerForm()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        TimerSession session = NewSession(segments: 2);

        session.Execute(TimerCommand.StartOrSplit);
        Assert.Equal(TimerPhase.Running, session.State.CurrentPhase);

        clock.Advance(TimeSpan.FromSeconds(5));
        session.Execute(TimerCommand.StartOrSplit);
        Assert.Equal(1, session.State.CurrentSplitIndex);

        session.Execute(TimerCommand.Pause);
        session.Execute(TimerCommand.StartOrSplit); // paused: resumes, does not split
        Assert.Equal(TimerPhase.Running, session.State.CurrentPhase);
        Assert.Equal(1, session.State.CurrentSplitIndex);

        clock.Advance(TimeSpan.FromSeconds(5));
        session.Execute(TimerCommand.StartOrSplit);
        Assert.Equal(TimerPhase.Ended, session.State.CurrentPhase);

        session.Execute(TimerCommand.StartOrSplit); // ended: resets
        Assert.Equal(TimerPhase.NotRunning, session.State.CurrentPhase);
        Assert.Equal(1, session.State.Run.AttemptHistory.Count);
    }

    [Fact]
    public void Every_Input_Path_Gives_The_Same_State()
    {
        // Direct model calls (Windows StartOrSplit written out here), session commands, and key
        // chords resolved through the hotkey profile must end in identical state after every step.
        HotkeyProfileStub profile = BoundProfile();
        Dictionary<TimerCommand, string> chords = new()
        {
            [TimerCommand.StartOrSplit] = profile.SplitKey,
            [TimerCommand.Reset] = profile.ResetKey,
            [TimerCommand.UndoSplit] = profile.UndoKey,
            [TimerCommand.SkipSplit] = profile.SkipKey,
            [TimerCommand.Pause] = profile.PauseKey,
            [TimerCommand.SwitchComparisonPrevious] = profile.SwitchComparisonPrevious,
            [TimerCommand.SwitchComparisonNext] = profile.SwitchComparisonNext,
        };

        for (int seed = 0; seed < 200; seed++)
        {
            var rng = new Random(seed);
            using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1 + rng.Next(50)));
            int segments = 1 + rng.Next(5);
            TimerSession viaSession = NewSession(segments);
            TimerSession viaHotkeys = NewSession(segments);
            TimerSession direct = NewSession(segments);
            var trace = new StringBuilder($"seed {seed}, {segments} segments:");

            for (int step = 0; step < 150; step++)
            {
                if (rng.Next(3) == 0)
                {
                    var by = TimeSpan.FromMilliseconds(1 + rng.Next(20_000));
                    clock.Advance(by);
                    trace.Append(CultureInfo.InvariantCulture, $" +{by.TotalMilliseconds}ms");
                    continue;
                }

                TimerCommand command = AllCommands[rng.Next(AllCommands.Length)];
                trace.Append(' ').Append(command);
                viaSession.Execute(command);
                if (chords.TryGetValue(command, out string? chord))
                {
                    HotkeyResolution resolved = HotkeyCommands.Resolve(profile, chord.ToLowerInvariant());
                    Assert.Equal(command, resolved.Command);
                    viaHotkeys.Execute(resolved.Command!.Value);
                }
                else
                {
                    viaHotkeys.Execute(command); // no hotkey for this one (menu or server only)
                }

                ApplyDirect(direct, command);

                string expected = Snapshot(direct);
                Assert.True(expected == Snapshot(viaSession), trace + "\nsession differs:\n" + Snapshot(viaSession) + "\ndirect:\n" + expected);
                Assert.True(expected == Snapshot(viaHotkeys), trace + "\nhotkeys differ:\n" + Snapshot(viaHotkeys) + "\ndirect:\n" + expected);
            }
        }
    }

    [Fact]
    public void Control_Menu_Items_Send_Their_Command_And_Follow_The_Old_Enabled_Rules()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        TimerSession session = NewSession(segments: 2);

        Assert.Equal(
            [("Start", true), ("Reset", false), ("Undo Split", false), ("Skip Split", false), ("Pause", false), ("Undo All Pauses", false)],
            TimerControlMenu.Build(session.State).Select(i => (i.Label, i.IsEnabled)));

        Click(session, "Start");
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal(
            [("Split", true), ("Reset", true), ("Undo Split", false), ("Skip Split", true), ("Pause", true), ("Undo All Pauses", true)],
            TimerControlMenu.Build(session.State).Select(i => (i.Label, i.IsEnabled)));

        Click(session, "Split");
        Click(session, "Pause");
        Assert.Equal("Resume", TimerControlMenu.Build(session.State)[4].Label);
        Assert.True(TimerControlMenu.Build(session.State)[2].IsEnabled);
        Click(session, "Resume");
        Assert.Equal(TimerPhase.Running, session.State.CurrentPhase);
        Click(session, "Reset");
        Assert.Equal(TimerPhase.NotRunning, session.State.CurrentPhase);
    }

    [Fact]
    public void Hotkeys_Resolve_In_Windows_Order_And_Toggle_Separately()
    {
        var profile = new HotkeyProfileStub { SplitKey = "Space", ResetKey = "Space", ToggleGlobalHotkeys = "Space", PauseKey = "P" };

        HotkeyResolution both = HotkeyCommands.Resolve(profile, "space");
        Assert.Equal(TimerCommand.StartOrSplit, both.Command);
        Assert.True(both.ToggleGlobalHotkeys);

        Assert.Equal(TimerCommand.Pause, HotkeyCommands.Resolve(profile, "P").Command);
        Assert.True(HotkeyCommands.Resolve(profile, "Q").IsEmpty);
        Assert.True(HotkeyCommands.Resolve(profile, "").IsEmpty);
        Assert.True(HotkeyCommands.Resolve(new HotkeyProfileStub(), "Space").IsEmpty);
    }

    [Fact]
    public void Only_Split_And_Pause_Hotkeys_Wait_For_The_Hotkey_Delay()
    {
        Assert.Equal(
            [TimerCommand.StartOrSplit, TimerCommand.Pause],
            AllCommands.Where(TimerSession.UsesHotkeyDelay));
    }

    [Fact]
    public async Task Delayed_Hotkey_Acts_After_The_Delay()
    {
        TimerSession session = NewSession(segments: 1);
        var changed = new TaskCompletionSource<SessionChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Changed += (_, e) => changed.TrySetResult(e.Change);

        session.ExecuteFromHotkey(TimerCommand.StartOrSplit, TimeSpan.FromMilliseconds(150));
        Assert.Equal(TimerPhase.NotRunning, session.State.CurrentPhase);

        Task finished = await Task.WhenAny(changed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(changed.Task, finished);
        Assert.Equal(TimerPhase.Running, session.State.CurrentPhase);
    }

    [Fact]
    public void Changes_Are_Reported_Once_Per_Call_With_What_Changed()
    {
        TimerSession session = NewSession(segments: 2);
        var seen = new List<SessionChange>();
        session.Changed += (_, e) => seen.Add(e.Change);

        session.Execute(TimerCommand.StartOrSplit);
        session.Execute(TimerCommand.SwitchComparisonNext);
        session.SetTimingMethod(TimingMethod.GameTime);
        session.SetRun(new Run(), TimerPhase.NotRunning, -1);

        Assert.Equal(
            [
                SessionChange.Timer | SessionChange.Comparison,
                SessionChange.Comparison,
                SessionChange.Comparison,
                SessionChange.Run | SessionChange.Comparison | SessionChange.Timer,
            ],
            seen);
    }

    [Fact]
    public void Commands_From_Another_Thread_Wait_For_The_Dispatcher()
    {
        var dispatcher = new QueuedDispatcher();
        var state = new LiveSplitState(RunWith(1));
        var session = new TimerSession(state, () => new ComparisonGeneratorSettings(), dispatcher);

        session.Execute(TimerCommand.StartOrSplit);
        Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);

        dispatcher.RunAll();
        Assert.Equal(TimerPhase.Running, state.CurrentPhase);
    }

    [Fact]
    public void Reset_Through_The_Session_Regenerates_Comparisons()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        TimerSession session = NewSession(segments: 1);

        session.Execute(TimerCommand.StartOrSplit);
        clock.Advance(TimeSpan.FromSeconds(42));
        session.Execute(TimerCommand.StartOrSplit);
        session.Execute(TimerCommand.Reset);

        Assert.Equal(TimeSpan.FromSeconds(42), session.State.Run[0].Comparisons["Best Segments"].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(42), session.State.Run[0].Comparisons["Average Segments"].RealTime);
    }

    private static void Click(TimerSession session, string label)
    {
        ControlMenuItem item = TimerControlMenu.Build(session.State).Single(i => i.Label == label);
        Assert.True(item.IsEnabled, label);
        session.Execute(item.Command);
    }

    /// <summary>What Windows TimerForm does per command, written against the model directly.</summary>
    private static void ApplyDirect(TimerSession twin, TimerCommand command)
    {
        TimerModel model = twin.Model;
        LiveSplitState state = twin.State;
        switch (command)
        {
            case TimerCommand.StartOrSplit when state.CurrentPhase == TimerPhase.Running:
                model.Split();
                break;
            case TimerCommand.StartOrSplit when state.CurrentPhase == TimerPhase.Paused:
                model.Pause();
                break;
            case TimerCommand.StartOrSplit when state.CurrentPhase == TimerPhase.NotRunning:
                model.Start();
                break;
            case TimerCommand.StartOrSplit:
                model.Reset();
                break;
            case TimerCommand.Reset:
                model.Reset();
                break;
            case TimerCommand.ResetWithoutSaving:
                model.Reset(false);
                break;
            case TimerCommand.UndoSplit:
                model.UndoSplit();
                break;
            case TimerCommand.SkipSplit:
                model.SkipSplit();
                break;
            case TimerCommand.Pause:
                model.Pause();
                break;
            case TimerCommand.UndoAllPauses:
                model.UndoAllPauses();
                break;
            case TimerCommand.SwitchComparisonPrevious:
                model.SwitchComparisonPrevious();
                break;
            case TimerCommand.SwitchComparisonNext:
                model.SwitchComparisonNext();
                break;
        }
    }

    private static string Snapshot(TimerSession session)
    {
        LiveSplitState s = session.State;
        var b = new StringBuilder();
        b.AppendLine(CultureInfo.InvariantCulture, $"{s.CurrentPhase} {s.CurrentSplitIndex} {s.CurrentComparison} {s.CurrentTimingMethod}");
        b.AppendLine(CultureInfo.InvariantCulture, $"time {s.CurrentTime} pause {s.PauseTime} attempts {s.Run.AttemptCount}");
        for (int i = 0; i < s.Run.Count; i++)
        {
            Segment seg = s.Run[i];
            b.Append(CultureInfo.InvariantCulture, $"[{i}] {seg.SplitTime} pb {seg.PersonalBestSplitTime} gold {seg.BestSegmentTime} hist");
            foreach ((int id, SplitTime t) in seg.SegmentHistory)
            {
                b.Append(CultureInfo.InvariantCulture, $" {id}={t}");
            }

            foreach (string name in s.Run.Comparisons)
            {
                b.Append(CultureInfo.InvariantCulture, $" {name}={ComparisonOrEmpty(seg, name)}");
            }

            b.AppendLine();
        }

        foreach (Attempt a in s.Run.AttemptHistory)
        {
            b.AppendLine(CultureInfo.InvariantCulture, $"attempt {a.Index} {a.Time} pause {a.PauseTime}");
        }

        return b.ToString();
    }

    private static string ComparisonOrEmpty(Segment segment, string name) =>
        segment.Comparisons.TryGetValue(name, out SplitTime t) ? t.ToString() : "-";

    private static TimerSession NewSession(int segments)
    {
        Run run = RunWith(segments);
        run.CustomComparisons.Add("Rival");
        return new TimerSession(new LiveSplitState(run), () => new ComparisonGeneratorSettings());
    }

    private static Run RunWith(int segments)
    {
        var run = new Run();
        for (int i = 0; i < segments; i++)
        {
            run.AddSegment("S" + i.ToString(CultureInfo.InvariantCulture));
        }

        return run;
    }

    private static HotkeyProfileStub BoundProfile() => new()
    {
        SplitKey = "Space",
        ResetKey = "R",
        UndoKey = "U",
        SkipKey = "K",
        PauseKey = "P",
        SwitchComparisonPrevious = "Shift+Left",
        SwitchComparisonNext = "Shift+Right",
        ToggleGlobalHotkeys = "G",
    };

    private sealed class QueuedDispatcher : ISessionDispatcher
    {
        private readonly Queue<Action> _queue = new();

        public bool CheckAccess() => false;

        public void Post(Action action) => _queue.Enqueue(action);

        public void RunAll()
        {
            while (_queue.TryDequeue(out Action? action))
            {
                action();
            }
        }
    }
}
