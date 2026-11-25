namespace LiveSplitNative.Core.Timing;

/// <summary>
/// TimeSpan conversions exactly as .NET Framework 4.8.1 does them, which is what Windows
/// LiveSplit runs on. .NET Core changed both: TotalSeconds divides by 10^7 instead of
/// multiplying by 1/10^7 (results can differ in the last bit, which after truncation is a
/// whole tick), and FromSeconds keeps every tick instead of rounding to the millisecond.
/// Use these only where upstream code feeds TotalSeconds or FromSeconds into arithmetic.
/// Source: referencesource mscorlib/system/timespan.cs.
/// </summary>
public static class FrameworkTimeSpan
{
    private const double SecondsPerTick = 1.0 / TimeSpan.TicksPerSecond;
    private const int MillisPerSecond = 1000;
    private const long MaxMilliseconds = long.MaxValue / TimeSpan.TicksPerMillisecond;
    private const long MinMilliseconds = long.MinValue / TimeSpan.TicksPerMillisecond;

    /// <summary>.NET Framework TimeSpan.TotalSeconds: ticks × (1 / TicksPerSecond).</summary>
    public static double TotalSeconds(TimeSpan time) => time.Ticks * SecondsPerTick;

    /// <summary>.NET Framework TimeSpan.FromSeconds: rounded half away from zero to whole milliseconds.</summary>
    public static TimeSpan FromSeconds(double value) => Interval(value, MillisPerSecond);

    private static TimeSpan Interval(double value, int scale)
    {
        if (double.IsNaN(value))
        {
            throw new ArgumentException("TimeSpan does not accept floating point Not-a-Number values.", nameof(value));
        }

        double tmp = value * scale;
        double millis = tmp + (value >= 0 ? 0.5 : -0.5);
        if (millis > MaxMilliseconds || millis < MinMilliseconds)
        {
            throw new OverflowException("TimeSpan overflowed because the duration is too long.");
        }

        return new TimeSpan((long)millis * TimeSpan.TicksPerMillisecond);
    }
}
