using LiveSplitNative.Core.Options;

namespace LiveSplitNative.Core.Session;

/// <summary>What a pressed key chord asks for, per the hotkey profile.</summary>
public readonly record struct HotkeyResolution(TimerCommand? Command, bool ToggleGlobalHotkeys)
{
    public bool IsEmpty => Command is null && !ToggleGlobalHotkeys;
}

/// <summary>
/// Windows TimerForm.hook_KeyOrButtonPressed: the first matching binding in the order Split,
/// Undo, Skip, Reset, Pause, Switch Comparison (Previous), Switch Comparison (Next) gives the
/// command; Toggle Global Hotkeys is checked separately, so one key can do both.
/// </summary>
public static class HotkeyCommands
{
    public static HotkeyResolution Resolve(HotkeyProfileStub profile, string chord)
    {
        if (string.IsNullOrEmpty(chord))
        {
            return default;
        }

        TimerCommand? command =
            Matches(chord, profile.SplitKey) ? TimerCommand.StartOrSplit
            : Matches(chord, profile.UndoKey) ? TimerCommand.UndoSplit
            : Matches(chord, profile.SkipKey) ? TimerCommand.SkipSplit
            : Matches(chord, profile.ResetKey) ? TimerCommand.Reset
            : Matches(chord, profile.PauseKey) ? TimerCommand.Pause
            : Matches(chord, profile.SwitchComparisonPrevious) ? TimerCommand.SwitchComparisonPrevious
            : Matches(chord, profile.SwitchComparisonNext) ? TimerCommand.SwitchComparisonNext
            : null;
        return new HotkeyResolution(command, Matches(chord, profile.ToggleGlobalHotkeys));
    }

    private static bool Matches(string chord, string? binding) =>
        !string.IsNullOrEmpty(binding) && string.Equals(chord, binding, StringComparison.OrdinalIgnoreCase);
}
