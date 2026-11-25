// LiveSplit.Model helpers components call (LiveSplit.Core @ 93a11b85, MIT): AbbreviationExtensions,
// TimeSpanParser and SumOfBest, worked out by the app's own ports of them; TimerModel over the
// app's timer.
using System;
using System.Collections.Generic;
using System.Linq;
using LiveSplit.Model.Input;
using Native = LiveSplitNative.Core;

namespace LiveSplit.Model
{
    public static class AbbreviationExtensions
    {
        public static IEnumerable<string> GetAbbreviations(this string name) =>
            Native.Model.AbbreviationExtensions.GetAbbreviations(name).ToArray();
    }

    public static class TimeSpanParser
    {
        public static TimeSpan? ParseNullable(string timeString) => Native.Model.TimeSpanParser.ParseNullable(timeString);

        public static TimeSpan Parse(string timeString) => Native.Model.TimeSpanParser.Parse(timeString);
    }

    /// <summary>
    /// SumOfBest over the app's run: a run a component got from LiveSplitState (or a clone of it)
    /// is the app's run mirrored, so the app's SumOfBest works it out; any other run gives null.
    /// </summary>
    public static class SumOfBest
    {
        public static TimeSpan? CalculateSumOfBest(
            IRun run,
            int startIndex,
            int endIndex,
            IList<TimeSpan?> predictions,
            bool simpleCalculation = false,
            bool useCurrentRun = true,
            TimingMethod method = TimingMethod.RealTime)
        {
            if (Source(run) is not { } source)
            {
                return null;
            }

            var native = new TimeSpan?[predictions.Count];
            for (int i = 0; i < predictions.Count; i++)
            {
                native[i] = predictions[i];
            }

            TimeSpan? result = Native.Model.SumOfBest.CalculateSumOfBest(source, startIndex, endIndex, native, simpleCalculation, useCurrentRun, (Native.Model.TimingMethod)(int)method);
            for (int i = 0; i < predictions.Count; i++)
            {
                predictions[i] = native[i];
            }

            return result;
        }

        public static TimeSpan? CalculateSumOfBest(
            IRun run,
            bool simpleCalculation = false,
            bool useCurrentRun = true,
            TimingMethod method = TimingMethod.RealTime) =>
            Source(run) is { } source
                ? Native.Model.SumOfBest.CalculateSumOfBest(source, simpleCalculation, useCurrentRun, (Native.Model.TimingMethod)(int)method)
                : null;

        private static Native.Model.Run Source(IRun run) => (run as Run)?.Source;
    }

    public interface ITimerModel
    {
        LiveSplitState CurrentState { get; set; }

        event EventHandler OnSplit;

        event EventHandler OnUndoSplit;

        event EventHandler OnSkipSplit;

        event EventHandler OnStart;

        event EventHandlerT<TimerPhase> OnReset;

        event EventHandler OnPause;

        event EventHandler OnUndoAllPauses;

        event EventHandler OnResume;

        event EventHandler OnScrollUp;

        event EventHandler OnScrollDown;

        event EventHandler OnSwitchComparisonPrevious;

        event EventHandler OnSwitchComparisonNext;

        void Start();

        void InitializeGameTime();

        void Split();

        void SkipSplit();

        void UndoSplit();

        void Reset();

        void Reset(bool updateSplits);

        void ResetAndSetAttemptAsPB();

        void Pause();

        void UndoAllPauses();

        void SwitchComparisonPrevious();

        void SwitchComparisonNext();

        void ScrollUp();

        void ScrollDown();
    }

    /// <summary>
    /// TimerModel: what a component does to the timer (Manual Game Time splitting, say) is done to
    /// the app's timer, as if the user had pressed the hotkey. Its events are LiveSplitState's.
    /// </summary>
    public class TimerModel : ITimerModel
    {
        public LiveSplitState CurrentState { get; set; }

        public event EventHandler OnSplit { add => CurrentState.OnSplit += value; remove => CurrentState.OnSplit -= value; }

        public event EventHandler OnUndoSplit { add => CurrentState.OnUndoSplit += value; remove => CurrentState.OnUndoSplit -= value; }

        public event EventHandler OnSkipSplit { add => CurrentState.OnSkipSplit += value; remove => CurrentState.OnSkipSplit -= value; }

        public event EventHandler OnStart { add => CurrentState.OnStart += value; remove => CurrentState.OnStart -= value; }

        public event EventHandlerT<TimerPhase> OnReset { add => CurrentState.OnReset += value; remove => CurrentState.OnReset -= value; }

        public event EventHandler OnPause { add => CurrentState.OnPause += value; remove => CurrentState.OnPause -= value; }

        public event EventHandler OnUndoAllPauses { add => CurrentState.OnUndoAllPauses += value; remove => CurrentState.OnUndoAllPauses -= value; }

        public event EventHandler OnResume { add => CurrentState.OnResume += value; remove => CurrentState.OnResume -= value; }

        public event EventHandler OnScrollUp { add => CurrentState.OnScrollUp += value; remove => CurrentState.OnScrollUp -= value; }

        public event EventHandler OnScrollDown { add => CurrentState.OnScrollDown += value; remove => CurrentState.OnScrollDown -= value; }

        public event EventHandler OnSwitchComparisonPrevious { add => CurrentState.OnSwitchComparisonPrevious += value; remove => CurrentState.OnSwitchComparisonPrevious -= value; }

        public event EventHandler OnSwitchComparisonNext { add => CurrentState.OnSwitchComparisonNext += value; remove => CurrentState.OnSwitchComparisonNext -= value; }

        private Native.Model.TimerModel Model => CurrentState?.NativeModel?.Invoke();

        public void Start() => Model?.Start();

        public void InitializeGameTime()
        {
            if (CurrentState is not null)
            {
                CurrentState.IsGameTimeInitialized = true;
            }
        }

        public void Split() => Model?.Split();

        public void SkipSplit() => Model?.SkipSplit();

        public void UndoSplit() => Model?.UndoSplit();

        public void Reset() => Model?.Reset();

        public void Reset(bool updateSplits) => Model?.Reset(updateSplits);

        public void ResetAndSetAttemptAsPB() => Model?.ResetAndSetAttemptAsPb();

        public void Pause() => Model?.Pause();

        public void UndoAllPauses() => Model?.UndoAllPauses();

        public void SwitchComparisonPrevious() => Model?.SwitchComparisonPrevious();

        public void SwitchComparisonNext() => Model?.SwitchComparisonNext();

        public void ScrollUp() => Model?.ScrollUp();

        public void ScrollDown() => Model?.ScrollDown();
    }
}
