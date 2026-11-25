using System.Runtime.InteropServices;
using System.Text;
using LiveSplitNative.Core.Input;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// CompositeHook's GamepadHook on Linux: the kernel's input devices (/dev/input/event*) that are
/// joysticks or gamepads (they have joystick or gamepad buttons), read on a thread of their own
/// while something needs them. Buttons, sticks, triggers and the D-pad become LiveSplit's names
/// through <see cref="GamepadInput"/>, as the HID values do on macOS; controllers plugged in later
/// are picked up within two seconds. Desktops let the person at the computer read controllers
/// (systemd's uaccess); a controller that cannot be opened is passed over.
/// </summary>
internal sealed class LinuxGamepadHook : IGamepadSource
{
    private const string LibC = "libc";
    private const int OpenReadOnly = 0;
    private const int OpenNonBlocking = 0x800;
    private const short PollIn = 1;
    private const ushort EvKey = 1;
    private const ushort EvAbs = 3;
    private const int BtnJoystick = 0x120;
    private const int BtnGamepad = 0x130;
    private const int BtnTriggerHappy = 0x2C0;
    private const int AbsHat0X = 0x10;
    private const int AbsHat0Y = 0x11;
    private const int InputEventSize = 24;

    private readonly object _lock = new();
    private readonly GamepadInput _input = new();
    private Thread? _thread;
    private volatile bool _stopping;
    private int _leases;

    public event Action<string>? ButtonPressed;

    public IDisposable Acquire()
    {
        lock (_lock)
        {
            _leases++;
            if (_leases == 1 && OperatingSystem.IsLinux())
            {
                _stopping = false;
                _thread = new Thread(Run) { IsBackground = true, Name = "Gamepad hotkeys" };
                _thread.Start();
            }
        }

        return new Lease(this);
    }

    private void Release()
    {
        Thread? thread = null;
        lock (_lock)
        {
            _leases = Math.Max(0, _leases - 1);
            if (_leases == 0 && _thread is not null)
            {
                _stopping = true;
                thread = _thread;
                _thread = null;
            }
        }

        thread?.Join(TimeSpan.FromSeconds(2));
    }

    private sealed class Device
    {
        public required int Fd { get; init; }

        public required string Path { get; init; }

        public required string Name { get; init; }

        public Dictionary<int, (int Min, int Max)> Axes { get; } = [];

        public int HatX { get; set; }

        public int HatY { get; set; }
    }

    private void Run()
    {
        var devices = new Dictionary<string, Device>();
        DateTime nextScan = DateTime.MinValue;
        byte[] buffer = new byte[InputEventSize * 64];
        try
        {
            while (!_stopping)
            {
                if (DateTime.UtcNow >= nextScan)
                {
                    Scan(devices);
                    nextScan = DateTime.UtcNow.AddSeconds(2);
                }

                if (devices.Count == 0)
                {
                    Thread.Sleep(250);
                    continue;
                }

                Device[] list = [.. devices.Values];
                var polls = new PollFd[list.Length];
                for (int i = 0; i < list.Length; i++)
                {
                    polls[i] = new PollFd { Fd = list[i].Fd, Events = PollIn };
                }

                if (poll(polls, (ulong)polls.Length, 250) <= 0)
                {
                    continue;
                }

                for (int i = 0; i < list.Length; i++)
                {
                    if (polls[i].Revents == 0)
                    {
                        continue;
                    }

                    long read = ReadEvents(list[i], buffer);
                    if (read < 0)
                    {
                        // Unplugged.
                        close(list[i].Fd);
                        devices.Remove(list[i].Path);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Console.WriteLine("Gamepad hotkeys: not available (" + ex.Message + ").");
        }
        finally
        {
            foreach (Device device in devices.Values)
            {
                close(device.Fd);
            }
        }
    }

    private long ReadEvents(Device device, byte[] buffer)
    {
        long read = read_(device.Fd, buffer, (ulong)buffer.Length);
        if (read <= 0)
        {
            return Marshal.GetLastPInvokeError() == 11 ? 0 : -1; // EAGAIN: nothing more for now.
        }

        for (int offset = 0; offset + InputEventSize <= read; offset += InputEventSize)
        {
            ushort type = BitConverter.ToUInt16(buffer, offset + 16);
            ushort code = BitConverter.ToUInt16(buffer, offset + 18);
            int value = BitConverter.ToInt32(buffer, offset + 20);
            if (Translate(device, type, code, value) is { } hotkey)
            {
                ButtonPressed?.Invoke(hotkey);
            }
        }

        return read;
    }

    /// <summary>An input event as the HID value GamepadInput reads (the pressed name, or null).</summary>
    private string? Translate(Device device, ushort type, ushort code, int value)
    {
        if (type == EvKey)
        {
            int? button = ButtonIndex(code);
            return button is { } index
                ? _input.Value(device.Path, device.Name, code, GamepadInput.ButtonPage, index + 1, value, 0, 1, isMouse: false)
                : null;
        }

        if (type != EvAbs)
        {
            return null;
        }

        if (code is AbsHat0X or AbsHat0Y)
        {
            if (code == AbsHat0X)
            {
                device.HatX = Math.Sign(value);
            }
            else
            {
                device.HatY = Math.Sign(value);
            }

            // The D-pad as a hat switch: 0 (up) to 7 clockwise; 8 is centered.
            int hat = (device.HatX, device.HatY) switch
            {
                (0, -1) => 0,
                (1, -1) => 1,
                (1, 0) => 2,
                (1, 1) => 3,
                (0, 1) => 4,
                (-1, 1) => 5,
                (-1, 0) => 6,
                (-1, -1) => 7,
                _ => 8,
            };
            return _input.Value(device.Path, device.Name, AbsHat0X, GamepadInput.GenericDesktopPage, 0x39, hat, 0, 7, isMouse: false);
        }

        int? usage = code switch
        {
            0 => 0x30, // X
            1 => 0x31, // Y
            2 => 0x32, // Z (a trigger on most gamepads)
            3 => 0x33, // RX
            4 => 0x34, // RY
            5 => 0x35, // RZ (the other trigger)
            6 => 0x36, // throttle: slider 0
            7 => 0x37, // rudder: slider 1
            _ => null,
        };
        if (usage is null || !device.Axes.TryGetValue(code, out (int Min, int Max) range))
        {
            return null;
        }

        return _input.Value(device.Path, device.Name, 0x100u + code, GamepadInput.GenericDesktopPage, usage.Value, value, range.Min, range.Max, isMouse: false);
    }

    /// <summary>A joystick or gamepad button's number (DirectInput's order as far as the kernel keeps it).</summary>
    internal static int? ButtonIndex(int code) => code switch
    {
        >= BtnGamepad and < BtnGamepad + 15 => code - BtnGamepad,
        >= BtnJoystick and < BtnJoystick + 16 => code - BtnJoystick,
        >= BtnTriggerHappy and < BtnTriggerHappy + 40 => 16 + code - BtnTriggerHappy,
        _ => null,
    };

    /// <summary>Opens the joysticks and gamepads not open yet.</summary>
    private static void Scan(Dictionary<string, Device> devices)
    {
        string[] paths;
        try
        {
            paths = Directory.GetFiles("/dev/input", "event*");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (string path in paths)
        {
            if (devices.ContainsKey(path))
            {
                continue;
            }

            int fd = open(path, OpenReadOnly | OpenNonBlocking);
            if (fd < 0)
            {
                continue;
            }

            if (!IsController(fd))
            {
                close(fd);
                continue;
            }

            var device = new Device { Fd = fd, Path = path, Name = DeviceName(fd) };
            for (int code = 0; code < 8; code++)
            {
                int[] info = new int[6];
                if (ioctl(fd, Ioc(2, 0x40 + code, 24), info) >= 0 && info[2] > info[1])
                {
                    device.Axes[code] = (info[1], info[2]);
                }
            }

            devices[path] = device;
            Console.WriteLine($"Gamepad hotkeys: listening to {device.Name}.");
        }
    }

    /// <summary>A device with joystick or gamepad buttons (EVIOCGBIT for EV_KEY).</summary>
    private static bool IsController(int fd)
    {
        byte[] keys = new byte[(0x2FF / 8) + 1];
        if (ioctl(fd, Ioc(2, 0x20 + EvKey, keys.Length), keys) < 0)
        {
            return false;
        }

        bool Has(int code) => (keys[code / 8] & (1 << (code % 8))) != 0;
        return Has(BtnJoystick) || Has(BtnGamepad) || Has(BtnGamepad + 1);
    }

    /// <summary>EVIOCGNAME and EVIOCGID: the name and IDs, as LiveSplit names a controller.</summary>
    private static string DeviceName(int fd)
    {
        byte[] name = new byte[256];
        int length = ioctl(fd, Ioc(2, 0x06, name.Length), name);
        string product = length > 0 ? Encoding.UTF8.GetString(name, 0, Array.IndexOf(name, (byte)0) is var end and >= 0 ? end : length) : "";
        ushort[] id = new ushort[4];
        bool hasId = ioctl(fd, Ioc(2, 0x02, 8), id) >= 0;
        return GamepadInput.DeviceName(product, hasId ? id[1] : 0, hasId ? id[2] : 0);
    }

    /// <summary>Linux's _IOC for the 'E' (evdev) requests.</summary>
    private static ulong Ioc(int direction, int number, int size) =>
        ((ulong)direction << 30) | ((ulong)size << 16) | ((ulong)'E' << 8) | (ulong)number;

    private sealed class Lease(LinuxGamepadHook hook) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                hook.Release();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }

    [DllImport(LibC, SetLastError = true)]
    private static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport(LibC)]
    private static extern int close(int fd);

    [DllImport(LibC, EntryPoint = "read", SetLastError = true)]
    private static extern long read_(int fd, byte[] buffer, ulong count);

    [DllImport(LibC, SetLastError = true)]
    private static extern int ioctl(int fd, ulong request, byte[] data);

    [DllImport(LibC, SetLastError = true)]
    private static extern int ioctl(int fd, ulong request, int[] data);

    [DllImport(LibC, SetLastError = true)]
    private static extern int ioctl(int fd, ulong request, ushort[] data);

    [DllImport(LibC, SetLastError = true)]
    private static extern int poll([In, Out] PollFd[] fds, ulong count, int timeout);
}
