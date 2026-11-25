using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// The timer's drift correction against an atomic clock, as LiveSplit's TimeStamp.RefreshDrift,
/// NTP and XMLSettingsFactory.LoadDrift do it (93a11b85), on a made-up clock and time server.
/// </summary>
public class AtomicClockTests
{
    /// <summary>A Mac whose clock runs <paramref name="rate"/> times as fast as real time, and a time server.</summary>
    private sealed class World(double rate)
    {
        public DateTime Real { get; set; } = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        public TimeSpan Raw { get; set; } = TimeSpan.FromHours(3);

        public bool Offline { get; set; }

        public int Readings { get; private set; }

        public void Sleep(TimeSpan time) => Pass(time);

        public void Pass(TimeSpan time)
        {
            Real += time;
            Raw += TimeSpan.FromTicks((long)(time.Ticks * rate));
        }

        public DateTime Ntp()
        {
            Readings++;
            return Offline ? throw new System.Net.Sockets.SocketException() : Real;
        }

        public DriftRefresher Refresher(AtomicClock clock) =>
            new(clock, () => Raw, () => clock.Correct(Raw), Ntp, Sleep);
    }

    [Fact]
    public void The_First_Round_Syncs_The_Time_Of_Day_And_Waits_An_Hour()
    {
        var clock = new AtomicClock();
        var world = new World(1.0);
        Assert.False(clock.IsSynced);
        Assert.False(clock.CurrentDateTime(world.Raw).SyncedWithAtomicClock);

        Assert.Equal(TimeSpan.FromHours(1), world.Refresher(clock).Step());
        Assert.Equal(10, world.Readings);
        Assert.True(clock.IsSynced);
        world.Pass(TimeSpan.FromMinutes(7));
        AtomicDateTime now = clock.CurrentDateTime(world.Raw);
        Assert.True(now.SyncedWithAtomicClock);
        // Upstream averages the offsets as a double: exact to well under a millisecond.
        Assert.True((now.Time - world.Real).Duration() < TimeSpan.FromMilliseconds(1));
        Assert.Equal(1.0, clock.NewDrift);
    }

    [Fact]
    public void Later_Rounds_Measure_The_Drift_Weighted_By_Time()
    {
        // The clock gains 100 µs per second (100 ppm).
        var clock = new AtomicClock();
        var world = new World(1.0001);
        DriftRefresher refresher = world.Refresher(clock);
        refresher.Wait(refresher.Step());
        Assert.Equal(TimeSpan.FromMinutes(30), refresher.Step());

        // The two points are an hour and 45 s apart (the second round's readings): 0.95^1.0125 of
        // the persistent drift (1), the rest of the measured one (1.0001).
        double weight = Math.Pow(0.95, 1.0125);
        double expected = Math.Pow(1.0001, 1 - weight) * Math.Pow(1.0, weight);
        Assert.Equal(expected, clock.NewDrift, 9);
        Assert.Equal(1.0, clock.PersistentDrift);

        // After many hours the measured drift wins.
        for (int i = 0; i < 200; i++)
        {
            refresher.Wait(refresher.Step());
        }

        Assert.InRange(clock.NewDrift, 1.000099, 1.0001001);
    }

    [Fact]
    public void A_Drift_Of_One_Percent_Or_More_Is_Ignored()
    {
        var clock = new AtomicClock();
        var world = new World(1.02);
        DriftRefresher refresher = world.Refresher(clock);
        refresher.Wait(refresher.Step());
        refresher.Step();
        Assert.Equal(1.0, clock.NewDrift);
    }

    [Fact]
    public void Without_Five_Readings_Nothing_Is_Synced_And_It_Tries_Again_In_Half_An_Hour()
    {
        var clock = new AtomicClock();
        var world = new World(1.0) { Offline = true };
        Assert.Equal(TimeSpan.FromMinutes(30), world.Refresher(clock).Step());
        Assert.False(clock.IsSynced);
    }

    [Fact]
    public void A_Sleep_Of_The_Mac_Starts_The_Measurement_Over()
    {
        var clock = new AtomicClock();
        var world = new World(1.0001);
        DriftRefresher refresher = world.Refresher(clock);
        refresher.Step();
        // The wait took an hour longer than asked (the Mac slept): the first point is dropped.
        var sleeper = new DriftRefresher(clock, () => world.Raw, () => world.Raw, world.Ntp, t => world.Pass(t + TimeSpan.FromHours(1)));
        sleeper.Wait(TimeSpan.FromHours(1));
        Assert.Equal(TimeSpan.FromHours(1), refresher.Step());
        Assert.Equal(1.0, clock.NewDrift);
    }

    [Fact]
    public void The_Clock_Is_Divided_By_The_Persistent_Drift()
    {
        var clock = new AtomicClock();
        clock.LoadDrift(1.0001);
        Assert.Equal((1.0001, 1.0001), (clock.PersistentDrift, clock.NewDrift));
        Assert.Equal(TimeSpan.FromTicks((long)(TimeSpan.FromHours(1).Ticks / 1.0001)), clock.Correct(TimeSpan.FromHours(1)));
        clock.LoadDrift(1.02);
        Assert.Equal(1.0, clock.PersistentDrift);
        Assert.Equal(TimeSpan.FromHours(1), clock.Correct(TimeSpan.FromHours(1)));
    }

    [Fact]
    public void Ntp_Reads_The_Transmit_Time_And_Adds_Half_The_Round_Trip()
    {
        // 2026-10-30 12:00:00.250 UTC in NTP's format (seconds since 1900, big-endian, and a fraction).
        DateTime time = new(2026, 10, 30, 12, 0, 0, 250, DateTimeKind.Utc);
        ulong seconds = (ulong)(time - new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        byte[] reply = new byte[48];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(reply.AsSpan(40), (uint)seconds);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(reply.AsSpan(44), 0x40000000); // 0.25 s
        Assert.Equal(time.AddMilliseconds(15), Ntp.FromReply(reply, TimeSpan.FromMilliseconds(15), time));
        Assert.Throws<InvalidDataException>(() => Ntp.FromReply(reply, TimeSpan.Zero, time.AddHours(2)));
    }

    [Fact]
    public void Attempts_Are_Not_Marked_Synced_Under_A_Test_Clock()
    {
        using ManualTimeSourceScope scope = ManualTimeSource.Install();
        Assert.False(TimeStamp.CurrentDateTime.SyncedWithAtomicClock);
    }
}
