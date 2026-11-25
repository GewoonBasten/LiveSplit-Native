using System.Diagnostics;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

public class TimeSourceTests
{
    [Fact]
    public void Default_Clock_Counts_During_Sleep_On_Every_Desktop_OS()
    {
        ITimeSource source = TimeSources.CreateDefault();
        if (OperatingSystem.IsMacOS())
        {
            Assert.Equal("CLOCK_MONOTONIC_RAW", source.Name);
        }
        else if (OperatingSystem.IsLinux())
        {
            Assert.Equal("CLOCK_BOOTTIME", source.Name);
        }
        else
        {
            Assert.Equal("Stopwatch", source.Name);
        }

        Assert.True(source.CountsDuringSleep);
    }

    [Fact]
    public void Default_Clock_Never_Goes_Backwards()
    {
        ITimeSource source = TimeSources.CreateDefault();
        TimeSpan previous = source.Now;
        for (int i = 0; i < 100_000; i++)
        {
            TimeSpan now = source.Now;
            Assert.True(now >= previous, $"clock went from {previous} to {now}");
            previous = now;
        }
    }

    [Fact]
    public void Default_Clock_Runs_At_The_Same_Rate_As_Stopwatch()
    {
        // While awake, the sleep-proof clock and Stopwatch must agree closely.
        ITimeSource source = TimeSources.CreateDefault();
        TimeSpan start = source.Now;
        var sw = Stopwatch.StartNew();
        Thread.Sleep(200);
        TimeSpan elapsed = source.Now - start;
        sw.Stop();

        double drift = Math.Abs((elapsed - sw.Elapsed).TotalMilliseconds);
        Assert.True(drift < 5, $"clock {elapsed.TotalMilliseconds} ms vs Stopwatch {sw.Elapsed.TotalMilliseconds} ms");
    }

    [Fact]
    public void Manual_Clock_Refuses_To_Go_Backwards()
    {
        var clock = new ManualTimeSource();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromTicks(-1)));
    }

    [Theory]
    [InlineData(40, 25)]
    [InlineData(60, 16)]
    [InlineData(300, 3)]
    [InlineData(1000, 3)]
    [InlineData(7, 50)]
    [InlineData(0, 50)]
    public void Refresh_Rate_Is_Clamped_Like_Windows(int hz, int expectedMs)
    {
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), AppSettings.RefreshInterval(hz));
    }
}
