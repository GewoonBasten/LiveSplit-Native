using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Session;

/// <summary>
/// The decisions Windows TimerForm makes before it saves splits or resets (ported from 93a11b85).
/// The app shows the dialogs; the rules for when to ask, and what gets saved, live here.
/// </summary>
public static class SplitsPrompts
{
    /// <summary>
    /// TimerForm.SaveSplits(promptPBMessage: true) asks "Save as Personal Best?" when the attempt
    /// ended without beating the PB, or is still running or paused.
    /// </summary>
    public static bool ShouldOfferSaveAsPersonalBest(LiveSplitState state)
    {
        TimingMethod method = state.CurrentTimingMethod;
        return (state.CurrentPhase == TimerPhase.Ended
                && state.Run[^1].PersonalBestSplitTime[method] != null
                && state.Run[^1].SplitTime[method] >= state.Run[^1].PersonalBestSplitTime[method])
            || state.CurrentPhase == TimerPhase.Running
            || state.CurrentPhase == TimerPhase.Paused;
    }

    /// <summary>
    /// What TimerForm.SaveSplits writes when the attempt is not saved as a PB: a copy of the state,
    /// reset as the timer would be, so the attempt lands in the file while the live timer keeps
    /// going. When the timer is not running this is just a copy of the run.
    /// </summary>
    public static Run ResetCopyForSaving(LiveSplitState state)
    {
        LiveSplitState copy = state.Clone();
        new TimerModel { CurrentState = copy }.Reset();
        return copy.Run;
    }

    /// <summary>
    /// TimerForm.WarnAboutResetting: with Warn On Reset on, "Update Times?" is asked when the
    /// attempt has a gold, or finished with a first or better PB.
    /// </summary>
    public static bool ShouldWarnBeforeReset(LiveSplitState state)
    {
        TimingMethod method = state.CurrentTimingMethod;
        bool warnUser = false;
        for (int index = 0; index < state.Run.Count; index++)
        {
            if (LiveSplitStateHelper.CheckBestSegment(state, index, method))
            {
                warnUser = true;
                break;
            }
        }

        Segment last = state.Run[^1];
        if ((!warnUser && last.SplitTime[method] != null && last.PersonalBestSplitTime[method] == null)
            || last.SplitTime[method] < last.PersonalBestSplitTime[method])
        {
            warnUser = true;
        }

        return warnUser;
    }
}
