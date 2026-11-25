using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Session;

public readonly record struct ControlMenuItem(string Label, TimerCommand Command, bool IsEnabled);

/// <summary>
/// The timer items of the context menu's Control submenu, as data, so the menu and the hotkeys
/// demonstrably send the same commands. Labels and enabled rules are the ones the app used
/// before F1.
/// </summary>
public static class TimerControlMenu
{
    public static IReadOnlyList<ControlMenuItem> Build(LiveSplitState state)
    {
        TimerPhase phase = state.CurrentPhase;
        return
        [
            new(phase == TimerPhase.NotRunning ? "Start" : "Split", TimerCommand.StartOrSplit, true),
            new("Reset", TimerCommand.Reset, phase != TimerPhase.NotRunning),
            new("Undo Split", TimerCommand.UndoSplit, state.CurrentSplitIndex > 0 || phase == TimerPhase.Ended),
            new("Skip Split", TimerCommand.SkipSplit, phase is TimerPhase.Running or TimerPhase.Paused),
            new(phase == TimerPhase.Paused ? "Resume" : "Pause", TimerCommand.Pause, phase is TimerPhase.Running or TimerPhase.Paused),
            new("Undo All Pauses", TimerCommand.UndoAllPauses, phase is TimerPhase.Running or TimerPhase.Paused or TimerPhase.Ended),
        ];
    }
}
