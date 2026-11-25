using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// LiveSplit.ManualGameTime: game time is paused at 0 when the run starts and is then set by
/// hand, one split at a time, in the "Enter Game Time" window (ShitSplitter).
/// </summary>
public static class ManualGameTimeLogic
{
    /// <summary>ManualGameTimeComponent.state_OnStart: game time paused, at 0.</summary>
    public static void Start(LiveSplitState state)
    {
        state.IsGameTimePaused = true;
        state.SetGameTime(TimeSpan.Zero);
    }

    /// <summary>ManualGameTimeComponent.State_OnUndoSplit: back to the previous split's game time.</summary>
    public static void UndoSplit(LiveSplitState state)
    {
        int index = state.CurrentSplitIndex;
        state.SetGameTime(index > 0 ? state.Run[index - 1].SplitTime.GameTime : TimeSpan.Zero);
    }

    /// <summary>
    /// ShitSplitter.txtGameTime_KeyPress (Enter): the times typed (spaces ignored, "+" adds them
    /// up, e.g. "1:02.5 + 0:30") become the game time, added to the game time so far with Segment
    /// Times. True when the timer should split now; false (nothing changed) when a time does not
    /// parse.
    /// </summary>
    public static bool Enter(LiveSplitState state, ManualGameTimeComponentSettings settings, string text)
    {
        TimeSpan total = TimeSpan.Zero;
        try
        {
            foreach (string time in text.Replace(" ", "", StringComparison.Ordinal).Split('+'))
            {
                total += TimeSpanParser.Parse(time);
            }
        }
        catch (Exception)
        {
            return false;
        }

        state.SetGameTime(total + (settings.UseSegmentTimes ? state.CurrentTime.GameTime : TimeSpan.Zero));
        return true;
    }
}
