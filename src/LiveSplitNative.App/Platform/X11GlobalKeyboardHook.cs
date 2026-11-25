using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Input;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Global hotkeys on Linux with X11 (Windows: LowLevelKeyboardHook): X11's RECORD extension
/// reports every key press to the X server, whichever app has the keyboard, without taking it
/// from that app. On a Wayland desktop LiveSplit runs through XWayland and sees the keys typed in
/// X11 apps (most games, Proton and Wine among them), not those of native Wayland apps.
/// Keeping keys from other programs (Deactivate Hotkeys For Other Programs) is not done here.
/// </summary>
internal sealed class X11GlobalKeyboardHook : IGlobalKeyboardHook
{
    private const string X11 = "libX11.so.6";
    private const string Xtst = "libXtst.so.6";
    private const int KeyPress = 2;
    private const int RecordFromServer = 0;
    private const ulong RecordAllClients = 3;
    private const int ShiftMask = 0x01;
    private const int ControlMask = 0x04;
    private const int Mod1Mask = 0x08;
    private const int Mod2Mask = 0x10;
    private const int Mod4Mask = 0x40;

    private readonly object _lock = new();
    private GCHandle _self;
    private Thread? _thread;
    private nint _control;
    private nint _data;
    private ulong _context;
    private volatile bool _active;

    /// <summary>Per key code: the key without and with Num Lock (keypad digits), as read at start.</summary>
    private (Key Plain, Key NumLock)[] _keys = [];

    public event Action<string>? KeyPressed;

    public Func<string, bool>? ShouldConsume { get; set; }

    public bool IsActive => _active;

    /// <summary>Linux with an X11 display (X11, or XWayland on a Wayland desktop).</summary>
    public static bool IsSupported =>
        OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"));

    public void Start(bool consume)
    {
        if (!IsSupported)
        {
            return;
        }

        lock (_lock)
        {
            if (_thread is not null)
            {
                return;
            }

            try
            {
                if (!Open())
                {
                    Close();
                    return;
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                Console.WriteLine($"Global hotkeys: X11 libraries missing ({ex.Message}).");
                Close();
                return;
            }

            if (!_self.IsAllocated)
            {
                _self = GCHandle.Alloc(this);
            }

            _thread = new Thread(Run) { IsBackground = true, Name = "Global hotkeys" };
            _thread.Start();
            bool wayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
            Console.WriteLine(wayland
                ? "Global hotkeys: listening (X11 RECORD through XWayland: keys typed in X11 apps and games)."
                : "Global hotkeys: listening (X11 RECORD).");
            if (consume)
            {
                Console.WriteLine("Global hotkeys: keeping keys from other programs is not available on Linux; they also reach the other program.");
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_thread is not null && _control != 0 && _context != 0)
            {
                XRecordDisableContext(_control, _context);
                XFlush(_control);
                _thread.Join(TimeSpan.FromSeconds(2));
            }

            _thread = null;
            Close();
        }
    }

    public void Dispose()
    {
        Stop();
        if (_self.IsAllocated)
        {
            _self.Free();
        }
    }

    /// <summary>Two connections (RECORD's control and data), the key table, the context.</summary>
    private bool Open()
    {
        _control = XOpenDisplay(0);
        _data = XOpenDisplay(0);
        if (_control == 0 || _data == 0)
        {
            Console.WriteLine("Global hotkeys: the X11 display could not be opened.");
            return false;
        }

        if (XRecordQueryVersion(_control, out _, out _) == 0)
        {
            Console.WriteLine("Global hotkeys: the X server has no RECORD extension.");
            return false;
        }

        _keys = ReadKeyTable(_control);
        nint range = XRecordAllocRange();
        if (range == 0)
        {
            return false;
        }

        try
        {
            // XRecordRange.device_events (first, last) at bytes 18 and 19: key presses only.
            Marshal.WriteByte(range, 18, KeyPress);
            Marshal.WriteByte(range, 19, KeyPress);
            ulong clients = RecordAllClients;
            _context = XRecordCreateContext(_control, 0, ref clients, 1, ref range, 1);
        }
        finally
        {
            XFree(range);
        }

        if (_context == 0)
        {
            Console.WriteLine("Global hotkeys: X11 RECORD refused the context.");
            return false;
        }

        XSync(_control, false);
        return true;
    }

    private void Close()
    {
        if (_control != 0 && _context != 0)
        {
            XRecordFreeContext(_control, _context);
        }

        _context = 0;
        if (_data != 0)
        {
            XCloseDisplay(_data);
            _data = 0;
        }

        if (_control != 0)
        {
            XCloseDisplay(_control);
            _control = 0;
        }

        _active = false;
    }

    private unsafe void Run()
    {
        _active = true;
        try
        {
            // Blocks, calling Callback for every key press, until Stop disables the context.
            if (XRecordEnableContext(_data, _context, &Callback, GCHandle.ToIntPtr(_self)) == 0)
            {
                Console.WriteLine("Global hotkeys: X11 RECORD could not be enabled.");
            }
        }
        finally
        {
            _active = false;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Callback(nint closure, nint recorded)
    {
        try
        {
            // XRecordInterceptData: category (int) at 24, data (pointer) at 32, data_len at 40.
            if (Marshal.ReadInt32(recorded, 24) == RecordFromServer
                && Marshal.ReadInt64(recorded, 40) > 0
                && GCHandle.FromIntPtr(closure).Target is X11GlobalKeyboardHook hook)
            {
                nint data = Marshal.ReadIntPtr(recorded, 32);
                // The core event: type, detail (the key code), ..., state (16 bits) at 28.
                if ((Marshal.ReadByte(data, 0) & 0x7F) == KeyPress)
                {
                    string chord = hook.ChordFor(Marshal.ReadByte(data, 1), (ushort)Marshal.ReadInt16(data, 28));
                    if (chord.Length > 0)
                    {
                        hook.KeyPressed?.Invoke(chord);
                    }
                }
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            XRecordFreeData(recorded);
        }
    }

    /// <summary>The chord for a key code and X11 modifier state ("" for a modifier or unknown key).</summary>
    internal string ChordFor(int keyCode, int state)
    {
        if ((uint)keyCode >= (uint)_keys.Length)
        {
            return "";
        }

        (Key plain, Key numLock) = _keys[keyCode];
        Key key = (state & Mod2Mask) != 0 && numLock != Key.None ? numLock : plain;
        return key == Key.None ? "" : HotkeyChord.Format(key, Modifiers(state));
    }

    internal static KeyModifiers Modifiers(int state)
    {
        KeyModifiers modifiers = KeyModifiers.None;
        if ((state & ControlMask) != 0)
        {
            modifiers |= KeyModifiers.Control;
        }

        if ((state & Mod1Mask) != 0)
        {
            modifiers |= KeyModifiers.Alt;
        }

        if ((state & ShiftMask) != 0)
        {
            modifiers |= KeyModifiers.Shift;
        }

        if ((state & Mod4Mask) != 0)
        {
            modifiers |= KeyModifiers.Meta;
        }

        return modifiers;
    }

    private static (Key, Key)[] ReadKeyTable(nint display)
    {
        var table = new (Key, Key)[256];
        for (int code = 8; code < 256; code++)
        {
            Key plain = KeyForKeysym(XkbKeycodeToKeysym(display, (byte)code, 0, 0));
            Key shifted = KeyForKeysym(XkbKeycodeToKeysym(display, (byte)code, 0, 1));
            // The keypad: its digits (second level) with Num Lock on, as Windows' VK_NUMPAD0..9.
            table[code] = (plain, IsKeypadDigit(shifted) ? shifted : Key.None);
        }

        return table;
    }

    private static bool IsKeypadDigit(Key key) => key is >= Key.NumPad0 and <= Key.NumPad9 or Key.Decimal;

    /// <summary>X11 keysyms (keysymdef.h) as Avalonia names the keys (Avalonia.X11's key transform).</summary>
    internal static Key KeyForKeysym(ulong keysym)
    {
        if (keysym is >= 0x61 and <= 0x7A)
        {
            return Key.A + (int)(keysym - 0x61);
        }

        if (keysym is >= 0x41 and <= 0x5A)
        {
            return Key.A + (int)(keysym - 0x41);
        }

        if (keysym is >= 0x30 and <= 0x39)
        {
            return Key.D0 + (int)(keysym - 0x30);
        }

        if (keysym is >= 0xFFBE and <= 0xFFD5)
        {
            return Key.F1 + (int)(keysym - 0xFFBE);
        }

        if (keysym is >= 0xFFB0 and <= 0xFFB9)
        {
            return Key.NumPad0 + (int)(keysym - 0xFFB0);
        }

        return keysym switch
        {
            0x20 => Key.Space,
            0x27 => Key.OemQuotes,
            0x2C => Key.OemComma,
            0x2D => Key.OemMinus,
            0x2E => Key.OemPeriod,
            0x2F => Key.OemQuestion,
            0x3B => Key.OemSemicolon,
            0x3D => Key.OemPlus,
            0x5B => Key.OemOpenBrackets,
            0x5C => Key.OemPipe,
            0x5D => Key.OemCloseBrackets,
            0x60 => Key.OemTilde,
            0xFF08 => Key.Back,
            0xFF09 => Key.Tab,
            0xFF0D => Key.Return,
            0xFF13 => Key.Pause,
            0xFF14 => Key.Scroll,
            0xFF1B => Key.Escape,
            0xFF50 => Key.Home,
            0xFF51 => Key.Left,
            0xFF52 => Key.Up,
            0xFF53 => Key.Right,
            0xFF54 => Key.Down,
            0xFF55 => Key.PageUp,
            0xFF56 => Key.PageDown,
            0xFF57 => Key.End,
            0xFF61 => Key.PrintScreen,
            0xFF63 => Key.Insert,
            0xFF7F => Key.NumLock,
            0xFF8D => Key.Return,
            0xFF95 => Key.Home,
            0xFF96 => Key.Left,
            0xFF97 => Key.Up,
            0xFF98 => Key.Right,
            0xFF99 => Key.Down,
            0xFF9A => Key.PageUp,
            0xFF9B => Key.PageDown,
            0xFF9C => Key.End,
            0xFF9D => Key.Clear,
            0xFF9E => Key.Insert,
            0xFF9F => Key.Delete,
            0xFFAA => Key.Multiply,
            0xFFAB => Key.Add,
            0xFFAC => Key.Separator,
            0xFFAD => Key.Subtract,
            0xFFAE => Key.Decimal,
            0xFFAF => Key.Divide,
            0xFFE5 => Key.CapsLock,
            0xFFFF => Key.Delete,
            // Shift, Control, Caps, Meta, Alt, Super, Hyper (0xFFE1..0xFFEE) and the rest: none.
            _ => Key.None,
        };
    }

    [DllImport(X11)]
    private static extern nint XOpenDisplay(nint name);

    [DllImport(X11)]
    private static extern int XCloseDisplay(nint display);

    [DllImport(X11)]
    private static extern int XFlush(nint display);

    [DllImport(X11)]
    private static extern int XSync(nint display, [MarshalAs(UnmanagedType.Bool)] bool discard);

    [DllImport(X11)]
    private static extern int XFree(nint data);

    [DllImport(X11)]
    private static extern ulong XkbKeycodeToKeysym(nint display, byte keycode, int group, int level);

    [DllImport(Xtst)]
    private static extern int XRecordQueryVersion(nint display, out int major, out int minor);

    [DllImport(Xtst)]
    private static extern nint XRecordAllocRange();

    [DllImport(Xtst)]
    private static extern ulong XRecordCreateContext(nint display, int datumFlags, ref ulong clients, int clientCount, ref nint ranges, int rangeCount);

    [DllImport(Xtst)]
    private static extern unsafe int XRecordEnableContext(nint display, ulong context, delegate* unmanaged[Cdecl]<nint, nint, void> callback, nint closure);

    [DllImport(Xtst)]
    private static extern int XRecordDisableContext(nint display, ulong context);

    [DllImport(Xtst)]
    private static extern int XRecordFreeContext(nint display, ulong context);

    [DllImport(Xtst)]
    private static extern void XRecordFreeData(nint data);
}
