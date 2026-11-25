using System.Net;
using System.Net.Sockets;

namespace LiveSplitNative.Core.Timing;

/// <summary>
/// The state LiveSplit's TimeStamp keeps statically (93a11b85): the drift the timer's clock is
/// divided by this session (<see cref="PersistentDrift"/>, from settings.cfg), the drift measured
/// now (<see cref="NewDrift"/>, saved for the next session), and the last and first atomic clock
/// readings with the clock's reading at the same moment.
/// </summary>
public sealed class AtomicClock
{
    private readonly object _lock = new();
    private double _persistentDrift = 1.0;
    private double _newDrift = 1.0;
    private TimeSpan _firstRaw = TimeSpan.Zero;
    private DateTime _firstNtp = DateTime.MinValue;
    private TimeSpan _lastRaw = TimeSpan.Zero;
    private DateTime _lastNtp = DateTime.MinValue;

    /// <summary>
    /// The drift the clock is divided by. Set from settings.cfg when LiveSplit starts (before any
    /// time is taken) and not changed during the session, so times never jump.
    /// </summary>
    public double PersistentDrift
    {
        get
        {
            lock (_lock)
            {
                return _persistentDrift;
            }
        }

        set
        {
            lock (_lock)
            {
                _persistentDrift = value;
            }
        }
    }

    /// <summary>The drift measured this session (settings.cfg's TimerDrift when saved).</summary>
    public double NewDrift
    {
        get
        {
            lock (_lock)
            {
                return _newDrift;
            }
        }

        set
        {
            lock (_lock)
            {
                _newDrift = value;
            }
        }
    }

    /// <summary>TimeStamp.IsSyncedWithAtomicClock: the atomic clock was read this session.</summary>
    public bool IsSynced
    {
        get
        {
            lock (_lock)
            {
                return _lastRaw != TimeSpan.Zero;
            }
        }
    }

    /// <summary>
    /// XMLSettingsFactory.LoadDrift: a saved drift more than 1 % from 1 is reset to 1; both drifts
    /// start from it.
    /// </summary>
    public void LoadDrift(double loadedDrift)
    {
        if (double.IsNaN(loadedDrift) || Math.Abs(loadedDrift - 1) > 0.01)
        {
            loadedDrift = 1;
        }

        lock (_lock)
        {
            _persistentDrift = _newDrift = loadedDrift;
        }
    }

    /// <summary>TimeStamp.Now: the clock's reading divided by the persistent drift.</summary>
    public TimeSpan Correct(TimeSpan raw)
    {
        double drift = PersistentDrift;
        return drift == 1.0 ? raw : TimeSpan.FromTicks((long)(raw.Ticks / drift));
    }

    /// <summary>
    /// TimeStamp.CurrentDateTime: the last atomic clock reading plus the time since (Now minus the
    /// clock's raw reading then, as upstream subtracts them); the system's time until synced.
    /// </summary>
    public AtomicDateTime CurrentDateTime(TimeSpan now)
    {
        lock (_lock)
        {
            if (_lastRaw != TimeSpan.Zero)
            {
                return new AtomicDateTime(_lastNtp.Add(now - _lastRaw), true);
            }
        }

        return new AtomicDateTime(DateTime.UtcNow, false);
    }

    internal (TimeSpan FirstRaw, DateTime FirstNtp) First
    {
        get
        {
            lock (_lock)
            {
                return (_firstRaw, _firstNtp);
            }
        }
    }

    internal void SetLast(TimeSpan raw, DateTime ntp)
    {
        lock (_lock)
        {
            _lastRaw = raw;
            _lastNtp = ntp;
        }
    }

    internal (TimeSpan Raw, DateTime Ntp) Last
    {
        get
        {
            lock (_lock)
            {
                return (_lastRaw, _lastNtp);
            }
        }
    }

    internal void SetFirst(TimeSpan raw, DateTime ntp)
    {
        lock (_lock)
        {
            _firstRaw = raw;
            _firstNtp = ntp;
        }
    }
}

/// <summary>
/// TimeStamp.RefreshDrift: ten atomic clock readings five seconds apart; with at least five, the
/// average offset is the clock's sync point. The first sync point is kept; every later one (an
/// hour, then every half hour) gives the drift between them, mixed with the persistent drift by
/// how long they are apart (0.95 per hour), unless it is 1 % or more from 1. A wait that took
/// two minutes longer than asked (the Mac slept) starts over from a new first point.
/// </summary>
public sealed class DriftRefresher(
    AtomicClock clock,
    Func<TimeSpan> rawNow,
    Func<TimeSpan> now,
    Func<DateTime> ntpNow,
    Action<TimeSpan> sleep)
{
    public static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan FirstInterval = TimeSpan.FromHours(1);
    public static readonly TimeSpan Interval = TimeSpan.FromHours(0.5);

    /// <summary>The loop; it runs for the rest of the session.</summary>
    public void Run()
    {
        while (true)
        {
            Wait(Step());
        }
    }

    /// <summary>One round of readings; the wait before the next.</summary>
    public TimeSpan Step()
    {
        var times = new List<long>();
        TimeSpan rawTime = TimeSpan.Zero;
        for (int count = 1; count <= 10; count++)
        {
            try
            {
                DateTime ntpTime = ntpNow();
                rawTime = rawNow();
                times.Add(ntpTime.Ticks - rawTime.Ticks);
            }
            catch (Exception)
            {
                // Offline, blocked or too far off: this reading doesn't count.
            }

            if (count < 10)
            {
                Wait(SampleInterval);
            }
        }

        if (times.Count < 5)
        {
            return Interval;
        }

        double averageDifference = times.Average();
        var lastNtp = new DateTime(rawTime.Ticks + (long)averageDifference, DateTimeKind.Utc);
        clock.SetLast(rawTime, lastNtp);
        (TimeSpan firstRaw, DateTime firstNtp) = clock.First;
        if (firstRaw == TimeSpan.Zero)
        {
            clock.SetFirst(rawTime, lastNtp);
            return FirstInterval;
        }

        TimeSpan rawDelta = rawTime - firstRaw;
        TimeSpan ntpDelta = lastNtp - firstNtp;
        double newDrift = rawDelta.TotalMilliseconds / ntpDelta.TotalMilliseconds;
        // Any drift that is too far from 1 is ignored.
        if (Math.Abs(newDrift - 1) < 0.01)
        {
            double weight = Math.Pow(0.95, ntpDelta.TotalHours);
            clock.NewDrift = Math.Pow(newDrift, 1 - weight) * Math.Pow(clock.PersistentDrift, weight);
        }

        return Interval;
    }

    /// <summary>TimeStamp.Wait: sleeps, and starts over when the sleep took much longer (the machine slept).</summary>
    public void Wait(TimeSpan waitTime)
    {
        TimeSpan before = now();
        sleep(waitTime);
        TimeSpan elapsed = now() - before;
        if (elapsed.TotalMinutes > waitTime.TotalMinutes + 2)
        {
            clock.SetFirst(TimeSpan.Zero, DateTime.MinValue);
        }
    }
}

/// <summary>
/// LiveSplit's NTP.Now: one SNTP request to time.windows.com (Windows' time server), the reply's
/// transmit time plus half the round trip. Throws when it can't be reached within three seconds
/// or the answer is more than an hour from the system's time.
/// </summary>
public static class Ntp
{
    public const string Server = "time.windows.com";

    public static DateTime Now()
    {
        byte[] data = new byte[48];
        data[0] = 0x1B; // LI 0, version 3, client mode
        IPAddress address = Dns.GetHostAddresses(Server).First(a => a.AddressFamily == AddressFamily.InterNetwork);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(new IPEndPoint(address, 123));
        socket.ReceiveTimeout = 3000;
        TimeStamp before = TimeStamp.Now;
        socket.Send(data);
        int received = socket.Receive(data);
        TimeStamp after = TimeStamp.Now;
        if (received < 48)
        {
            throw new InvalidDataException("The NTP answer is too short.");
        }

        return FromReply(data, TimeSpan.FromTicks((after - before).Ticks / 2), DateTime.UtcNow);
    }

    /// <summary>The reply's transmit timestamp (offset 40, big-endian seconds and fraction since 1900) plus the delay.</summary>
    public static DateTime FromReply(byte[] reply, TimeSpan delay, DateTime utcNow)
    {
        ulong intPart = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(reply.AsSpan(40, 4));
        ulong fractPart = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(reply.AsSpan(44, 4));
        ulong milliseconds = (intPart * 1000) + (fractPart * 1000 / 0x100000000L);
        DateTime networkDateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(milliseconds);
        DateTime resultingTime = networkDateTime + delay;
        if ((utcNow - resultingTime).Duration() > TimeSpan.FromHours(1))
        {
            throw new InvalidDataException("NTP time is too far off from local time");
        }

        return resultingTime;
    }
}
