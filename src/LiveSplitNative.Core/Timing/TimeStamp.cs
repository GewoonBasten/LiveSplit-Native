namespace LiveSplitNative.Core.Timing;

/// <summary>
/// Port of LiveSplit.Model.TimeStamp: a reading of a monotonic clock. Elapsed time is
/// always the difference of two readings, never derived from UI frames.
/// The clock comes from <see cref="TimeSources.CreateDefault"/>, corrected by the drift
/// LiveSplit measured against an atomic clock (NTP) in earlier sessions
/// (<see cref="AtomicClock.PersistentDrift"/>, kept in settings.cfg as TimerDrift), as Windows
/// divides QueryPerformanceCounter by it. Tests replace the clock per async flow with
/// <see cref="ManualTimeSource.Install"/>, so parallel tests never see each other's clocks;
/// a replaced clock is read as it is.
/// </summary>
public sealed class TimeStamp
{
    private static readonly ITimeSource DefaultSource = TimeSources.CreateDefault();
    private static readonly AsyncLocal<ITimeSource?> OverrideSource = new();

    private readonly TimeSpan _value;

    private TimeStamp(TimeSpan value) => _value = value;

    /// <summary>The clock <see cref="Now"/> reads in the current async flow.</summary>
    public static ITimeSource Source => OverrideSource.Value ?? DefaultSource;

    /// <summary>The drift correction and the atomic clock sync (TimeStamp's static state upstream).</summary>
    public static AtomicClock Clock { get; } = new();

    /// <summary>TimeStamp.Now: the clock divided by the persistent drift.</summary>
    public static TimeStamp Now => OverrideSource.Value is { } source
        ? new(source.Now)
        : new(Clock.Correct(DefaultSource.Now));

    /// <summary>The OS clock as it is, without the drift (upstream's qpc.Elapsed).</summary>
    public static TimeSpan RawNow => DefaultSource.Now;

    /// <summary>
    /// TimeStamp.CurrentDateTime: the time of day from the atomic clock once it has been
    /// reached (synced), else the system's (not synced). Attempts keep it as their start and end.
    /// </summary>
    public static AtomicDateTime CurrentDateTime =>
        OverrideSource.Value is null ? Clock.CurrentDateTime(Now.Value) : new AtomicDateTime(DateTime.UtcNow, false);

    /// <summary>
    /// The RefreshDrift loop upstream starts with the class: in the background, against
    /// time.windows.com, for the rest of the session (once; later calls do nothing).
    /// </summary>
    public static void StartAtomicClockSync()
    {
        lock (Clock)
        {
            if (_syncThread is not null)
            {
                return;
            }

            var refresher = new DriftRefresher(Clock, () => RawNow, () => Now.Value, Ntp.Now, Thread.Sleep);
            _syncThread = new Thread(refresher.Run) { IsBackground = true, Name = "LiveSplit atomic clock sync" };
            _syncThread.Start();
        }
    }

    private static Thread? _syncThread;

    public TimeSpan Value => _value;

    /// <summary>
    /// Replaces the clock for the current async flow until the returned scope is disposed.
    /// Intended for tests and diagnostics; production code never calls it.
    /// </summary>
    public static IDisposable UseSource(ITimeSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ITimeSource? previous = OverrideSource.Value;
        OverrideSource.Value = source;
        return new Restore(previous);
    }

    public static TimeSpan operator -(TimeStamp a, TimeStamp b) => a._value - b._value;

    public static TimeStamp operator -(TimeStamp a, TimeSpan b) => new(a._value - b);

    public static bool operator ==(TimeStamp? a, TimeStamp? b)
    {
        if (a is null)
        {
            return b is null;
        }

        return b is not null && a._value == b._value;
    }

    public static bool operator !=(TimeStamp? a, TimeStamp? b) => !(a == b);

    public override bool Equals(object? obj) => obj is TimeStamp other && _value == other._value;

    public override int GetHashCode() => _value.GetHashCode();

    public override string ToString() => _value.ToString();

    private sealed class Restore(ITimeSource? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            OverrideSource.Value = previous;
        }
    }
}
