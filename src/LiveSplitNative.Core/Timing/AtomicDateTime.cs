namespace LiveSplitNative.Core.Timing;

/// <summary>Port of LiveSplit.Model.AtomicDateTime (simplified).</summary>
public readonly struct AtomicDateTime
{
    public AtomicDateTime(DateTime time, bool syncedWithAtomicClock)
    {
        Time = time;
        SyncedWithAtomicClock = syncedWithAtomicClock;
    }

    public DateTime Time { get; }
    public bool SyncedWithAtomicClock { get; }

    public static TimeSpan operator -(AtomicDateTime a, AtomicDateTime b) => a.Time - b.Time;
}
