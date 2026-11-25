using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LiveSplitNative.Core.Timing;

/// <summary>
/// A monotonic clock: elapsed time since an arbitrary, fixed origin.
/// <see cref="TimeStamp"/> reads it; nothing else in the timer may read a clock directly.
/// </summary>
public interface ITimeSource
{
    /// <summary>Short description for logs and diagnostics, e.g. "CLOCK_MONOTONIC_RAW".</summary>
    string Name { get; }

    /// <summary>True when the clock keeps counting while the machine sleeps.</summary>
    bool CountsDuringSleep { get; }

    /// <summary>Elapsed time since the clock's origin. Never decreases.</summary>
    TimeSpan Now { get; }
}

/// <summary>Chooses the OS clock the timer uses.</summary>
public static class TimeSources
{
    /// <summary>
    /// Windows LiveSplit uses QueryPerformanceCounter, which keeps counting through sleep.
    /// .NET's <see cref="Stopwatch"/> does not on macOS (CLOCK_UPTIME_RAW) or Linux
    /// (CLOCK_MONOTONIC), so a run spanning a system sleep would lose that time.
    /// Like livesplit-core (src/platform/normal/mod.rs) we read clocks that do count it:
    /// CLOCK_MONOTONIC_RAW on macOS and CLOCK_BOOTTIME on Linux. Any failure falls back
    /// to <see cref="Stopwatch"/>, which is still monotonic.
    /// </summary>
    public static ITimeSource CreateDefault()
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var mac = new MacMonotonicRawTimeSource();
                _ = mac.Now;
                return mac;
            }

            if (OperatingSystem.IsLinux())
            {
                var linux = new LinuxBootTimeSource();
                _ = linux.Now;
                return linux;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            // Fall through to Stopwatch.
        }

        return new StopwatchTimeSource();
    }
}

/// <summary>
/// <see cref="Stopwatch"/>: QueryPerformanceCounter on Windows (counts through sleep),
/// CLOCK_UPTIME_RAW on macOS and CLOCK_MONOTONIC on Linux (both stop during sleep).
/// </summary>
public sealed class StopwatchTimeSource : ITimeSource
{
    private readonly long _origin = Stopwatch.GetTimestamp();

    public string Name => "Stopwatch";

    public bool CountsDuringSleep => OperatingSystem.IsWindows();

    public TimeSpan Now => Stopwatch.GetElapsedTime(_origin);
}

/// <summary>macOS clock_gettime_nsec_np(CLOCK_MONOTONIC_RAW): nanoseconds, keeps counting while asleep.</summary>
public sealed class MacMonotonicRawTimeSource : ITimeSource
{
    // <time.h> on Darwin: _CLOCK_MONOTONIC_RAW = 4 (CLOCK_UPTIME_RAW = 8 is the one that stops in sleep).
    private const int ClockMonotonicRaw = 4;

    private readonly ulong _origin;

    public MacMonotonicRawTimeSource()
    {
        _origin = Read();
    }

    public string Name => "CLOCK_MONOTONIC_RAW";

    public bool CountsDuringSleep => true;

    public TimeSpan Now
    {
        get
        {
            ulong elapsedNs = Read() - _origin;
            return TimeSpan.FromTicks((long)(elapsedNs / 100));
        }
    }

    private static ulong Read()
    {
        ulong ns = clock_gettime_nsec_np(ClockMonotonicRaw);
        if (ns == 0)
        {
            throw new InvalidOperationException("clock_gettime_nsec_np(CLOCK_MONOTONIC_RAW) failed.");
        }

        return ns;
    }

    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern ulong clock_gettime_nsec_np(int clockId);
}

/// <summary>Linux clock_gettime(CLOCK_BOOTTIME): like CLOCK_MONOTONIC but includes suspend.</summary>
public sealed class LinuxBootTimeSource : ITimeSource
{
    private const int ClockBootTime = 7;

    private readonly long _originNs;

    public LinuxBootTimeSource()
    {
        _originNs = ReadNs();
    }

    public string Name => "CLOCK_BOOTTIME";

    public bool CountsDuringSleep => true;

    public TimeSpan Now => TimeSpan.FromTicks((ReadNs() - _originNs) / 100);

    private static long ReadNs()
    {
        if (clock_gettime(ClockBootTime, out Timespec ts) != 0)
        {
            throw new InvalidOperationException("clock_gettime(CLOCK_BOOTTIME) failed.");
        }

        return (ts.Seconds * 1_000_000_000L) + ts.Nanoseconds;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long Seconds;
        public long Nanoseconds;
    }

    // glibc's soname; on musl the import fails and TimeSources falls back to Stopwatch.
    [DllImport("libc.so.6", SetLastError = true)]
    private static extern int clock_gettime(int clockId, out Timespec timespec);
}

/// <summary>
/// A clock that only moves when told to, for exact-value tests:
/// <c>using var clock = ManualTimeSource.Install(); model.Start(); clock.Advance(...); model.Split();</c>
/// </summary>
public sealed class ManualTimeSource : ITimeSource
{
    private TimeSpan _now;

    public ManualTimeSource(TimeSpan start = default)
    {
        _now = start;
    }

    public string Name => "Manual";

    public bool CountsDuringSleep => true;

    public TimeSpan Now => _now;

    /// <summary>Makes this clock the one <see cref="TimeStamp.Now"/> reads, for the current async flow only.</summary>
    public static ManualTimeSourceScope Install(TimeSpan start = default)
    {
        var clock = new ManualTimeSource(start);
        return new ManualTimeSourceScope(clock, TimeStamp.UseSource(clock));
    }

    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(by), "A monotonic clock cannot go backwards.");
        }

        _now += by;
    }

    public void AdvanceMilliseconds(double ms) => Advance(TimeSpan.FromMilliseconds(ms));
}

/// <summary>Disposing restores the previous clock.</summary>
public sealed class ManualTimeSourceScope : IDisposable
{
    private readonly IDisposable _restore;

    internal ManualTimeSourceScope(ManualTimeSource clock, IDisposable restore)
    {
        Clock = clock;
        _restore = restore;
    }

    public ManualTimeSource Clock { get; }

    public void Advance(TimeSpan by) => Clock.Advance(by);

    public void AdvanceMilliseconds(double ms) => Clock.AdvanceMilliseconds(ms);

    public void Dispose() => _restore.Dispose();
}
