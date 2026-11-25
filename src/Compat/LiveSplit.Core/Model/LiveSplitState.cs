// LiveSplit.Model.LiveSplitState as components see it, over the app's timer: the timer's values
// are read live, the run is the mirrored LiveSplit.Model.Run, and the timer's events are raised
// as LiveSplitState's (Compat/HostedTimer subscribes to the app's TimerModel).
using System;
using LiveSplit.Model.Input;
using LiveSplit.Options;
using LiveSplit.UI;
using Native = LiveSplitNative.Core;

namespace LiveSplit.Model
{
    public class LiveSplitState : ICloneable
    {
        private readonly Func<Native.Model.LiveSplitState> _state;
        private IRun _run;

        internal LiveSplitState(Func<Native.Model.LiveSplitState> state, IRun run, LayoutSettings layoutSettings)
        {
            _state = state;
            _run = run;
            LayoutSettings = layoutSettings;
        }

        /// <summary>The app's timer, which TimerModel drives (set by the compat runtime).</summary>
        internal Func<Native.Model.TimerModel> NativeModel { get; set; }

        public event EventHandler OnSplit;
        public event EventHandler OnUndoSplit;
        public event EventHandler OnSkipSplit;
        public event EventHandler OnStart;
        public event EventHandlerT<TimerPhase> OnReset;
        public event EventHandler OnPause;
        public event EventHandler OnUndoAllPauses;
        public event EventHandler OnResume;
        public event EventHandler OnScrollUp;
        public event EventHandler OnScrollDown;
        public event EventHandler OnSwitchComparisonPrevious;
        public event EventHandler OnSwitchComparisonNext;
        public event EventHandler RunManuallyModified;
        public event EventHandler ComparisonRenamed;

        private Native.Model.LiveSplitState N => _state();

        public IRun Run
        {
            get => _run;
            set => _run = value;
        }

        /// <summary>
        /// The layout: its settings and mode, and as Components the layout's components that come
        /// from component DLLs (Compat/HostedLayout).
        /// </summary>
        public ILayout Layout { get; set; }

        public LayoutSettings LayoutSettings { get; set; }

        /// <summary>The app's settings (settings.cfg): hotkey profiles, Simple Sum of Best and the rest.</summary>
        public ISettings Settings { get; set; }

        public System.Windows.Forms.Form Form { get; set; }

        public string CurrentHotkeyProfile { get; set; } = "Default";

        public AtomicDateTime AttemptStarted => Convert(N.AttemptStarted);

        public AtomicDateTime AttemptEnded => Convert(N.AttemptEnded);

        public TimeSpan TimePausedAt => N.TimePausedAt;

        public TimeSpan? GameTimePauseTime => N.GameTimePauseTime;

        public TimerPhase CurrentPhase => (TimerPhase)(int)N.CurrentPhase;

        public string CurrentComparison
        {
            get => N.CurrentComparison;
            set => N.CurrentComparison = value;
        }

        public TimingMethod CurrentTimingMethod
        {
            get => (TimingMethod)(int)N.CurrentTimingMethod;
            set => N.CurrentTimingMethod = (Native.Model.TimingMethod)(int)value;
        }

        public int CurrentSplitIndex => N.CurrentSplitIndex;

        public ISegment CurrentSplit => CurrentSplitIndex >= 0 && CurrentSplitIndex < Run.Count ? Run[CurrentSplitIndex] : null;

        public Time CurrentTime => Convert(N.CurrentTime);

        public TimeSpan? PauseTime => N.PauseTime;

        public TimeSpan CurrentAttemptDuration => N.CurrentAttemptDuration;

        public TimeSpan LoadingTimes
        {
            get => N.LoadingTimes;
            set => N.LoadingTimes = value;
        }

        public bool IsGameTimeInitialized
        {
            get => N.IsGameTimeInitialized;
            set => N.IsGameTimeInitialized = value;
        }

        public bool IsGameTimePaused
        {
            get => N.IsGameTimePaused;
            set => N.IsGameTimePaused = value;
        }

        public void SetGameTime(TimeSpan? gameTime) => N.SetGameTime(gameTime);

        public LiveSplitState Clone() => (LiveSplitState)MemberwiseClone();

        object ICloneable.Clone() => Clone();

        public void CallRunManuallyModified() => RunManuallyModified?.Invoke(this, EventArgs.Empty);

        public void CallComparisonRenamed(EventArgs e) => ComparisonRenamed?.Invoke(this, e);

        internal void RaiseSplit() => OnSplit?.Invoke(this, EventArgs.Empty);

        internal void RaiseUndoSplit() => OnUndoSplit?.Invoke(this, EventArgs.Empty);

        internal void RaiseSkipSplit() => OnSkipSplit?.Invoke(this, EventArgs.Empty);

        internal void RaiseStart() => OnStart?.Invoke(this, EventArgs.Empty);

        internal void RaiseReset(TimerPhase phase) => OnReset?.Invoke(this, phase);

        internal void RaisePause() => OnPause?.Invoke(this, EventArgs.Empty);

        internal void RaiseResume() => OnResume?.Invoke(this, EventArgs.Empty);

        internal void RaiseUndoAllPauses() => OnUndoAllPauses?.Invoke(this, EventArgs.Empty);

        internal void RaiseScrollUp() => OnScrollUp?.Invoke(this, EventArgs.Empty);

        internal void RaiseScrollDown() => OnScrollDown?.Invoke(this, EventArgs.Empty);

        internal void RaiseSwitchComparisonPrevious() => OnSwitchComparisonPrevious?.Invoke(this, EventArgs.Empty);

        internal void RaiseSwitchComparisonNext() => OnSwitchComparisonNext?.Invoke(this, EventArgs.Empty);

        internal static Time Convert(Native.Model.SplitTime time) => new(time.RealTime, time.GameTime);

        internal static AtomicDateTime Convert(Native.Timing.AtomicDateTime time) => new(time.Time, time.SyncedWithAtomicClock);
    }
}
