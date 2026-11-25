using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.UI;

/// <summary>What the Timer component shows (LiveSplit.Timer Timer.GetTime / Update, 93a11b85).</summary>
public static class TimerComponentLogic
{
    /// <summary>
    /// Timer.GetTime: before an attempt the timer shows the run's Start Timer value (Run.Offset,
    /// negative for a countdown), otherwise the current time. The offset is not part of
    /// <see cref="LiveSplitState.CurrentTime"/> while the timer is not running; TimerModel.Start
    /// puts it into the start time, so the attempt continues from it.
    /// </summary>
    public static TimeSpan? GetTime(LiveSplitState state, TimingMethod method) =>
        state.CurrentPhase == TimerPhase.NotRunning
            ? state.Run.Offset
            : state.CurrentTime[method];

    /// <summary>Timer.Update: Game Time without a value (not initialized) shows Real Time.</summary>
    public static TimeSpan? DisplayedTime(LiveSplitState state, TimingMethod method) =>
        GetTime(state, method)
        ?? (method == TimingMethod.GameTime ? GetTime(state, TimingMethod.RealTime) : null);

    /// <summary>Timer.Update: the component's Timing Method setting, or the run's current one.</summary>
    public static TimingMethod ResolveTimingMethod(string setting, TimingMethod current) =>
        setting switch
        {
            "Real Time" => TimingMethod.RealTime,
            "Game Time" => TimingMethod.GameTime,
            _ => current,
        };
}
