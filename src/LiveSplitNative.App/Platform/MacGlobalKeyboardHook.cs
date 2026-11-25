using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.Input;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Global hotkeys on macOS (Windows: LowLevelKeyboardHook / RegisterHotKey in CompositeHook): a
/// Quartz event tap sees every key press, whichever app has the keyboard, and reports it as a
/// hotkey chord. Listening needs the Input Monitoring permission; keeping a hotkey from the other
/// programs (Deactivate For Other Programs) needs an active tap, which needs Accessibility. A
/// tap the system refuses is tried again every few seconds, so granting the permission is
/// picked up without a restart when macOS allows it. Without the permission no listening tap is
/// made (making one brings up macOS's own question); LiveSplit asks with its own window
/// (MainWindow.HotkeyPermission.cs).
/// </summary>
internal sealed class MacGlobalKeyboardHook : IGlobalKeyboardHook
{
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
    private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    private const uint HidRequestListenEvent = 1;
    private const uint HidAccessGranted = 0;
    private const uint HidAccessDenied = 1;
    private const int SessionEventTap = 1;
    private const int HeadInsertEventTap = 0;
    private const int TapOptionDefault = 0;
    private const int TapOptionListenOnly = 1;
    private const uint KeyDown = 10;
    private const uint TapDisabledByTimeout = 0xFFFFFFFE;
    private const uint TapDisabledByUserInput = 0xFFFFFFFF;
    private const int KeyboardEventKeycode = 9;
    private const ulong FlagShift = 0x20000;
    private const ulong FlagControl = 0x40000;
    private const ulong FlagAlternate = 0x80000;
    private const ulong FlagCommand = 0x100000;

    private readonly object _lock = new();
    private GCHandle _self;
    private Thread? _thread;
    private nint _tap;
    private nint _runLoop;
    private volatile bool _stopping;
    private bool _consuming;

    /// <summary>A key chord was pressed (raised on the hook's own thread).</summary>
    public event Action<string>? KeyPressed;

    /// <summary>Whether a chord is kept from the app that has the keyboard (asked on the hook's thread).</summary>
    public Func<string, bool>? ShouldConsume { get; set; }

    /// <summary>Whether the tap is running (key presses anywhere are seen).</summary>
    public bool IsActive => _tap != 0;

    /// <summary>Whether the running tap can keep keys from other programs.</summary>
    public bool CanConsume => _tap != 0 && _consuming;

    public static bool IsSupported => OperatingSystem.IsMacOS();

    /// <summary>
    /// Whether macOS lets this app see key presses in other apps: Input Monitoring is on for it, or
    /// it has Accessibility (which allows that too). Always true where there is no such permission.
    /// </summary>
    public static bool CanListen => !IsSupported || ListenPermission == KeyListenPermission.Granted || IsAccessibilityTrusted();

    /// <summary>The Input Monitoring permission: on, off, or never asked for (the app is not in the list yet).</summary>
    public static KeyListenPermission ListenPermission
    {
        get
        {
            if (!IsSupported)
            {
                return KeyListenPermission.Granted;
            }

            try
            {
                return IOHIDCheckAccess(HidRequestListenEvent) switch
                {
                    HidAccessGranted => KeyListenPermission.Granted,
                    HidAccessDenied => KeyListenPermission.Denied,
                    _ => KeyListenPermission.NotAsked,
                };
            }
            catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
            {
                return KeyListenPermission.NotAsked;
            }
        }
    }

    /// <summary>
    /// Asks macOS for Input Monitoring: puts LiveSplit in System Settings' list, and the first time
    /// shows macOS's own question (whose Open System Settings goes to that list).
    /// </summary>
    public static void RequestListenPermission()
    {
        if (!IsSupported)
        {
            return;
        }

        try
        {
            CGRequestListenEventAccess();
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    /// <summary>Opens System Settings at Privacy &amp; Security, Input Monitoring.</summary>
    public static void OpenInputMonitoringSettings()
    {
        if (!IsSupported)
        {
            return;
        }

        try
        {
            using var open = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "/usr/bin/open", ["x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent"]) { UseShellExecute = false });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
        }
    }

    private static bool IsAccessibilityTrusted()
    {
        try
        {
            return AXIsProcessTrusted();
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    /// <summary>Starts (or restarts) the tap; <paramref name="consume"/> asks for an active tap.</summary>
    public void Start(bool consume)
    {
        if (!IsSupported)
        {
            return;
        }

        lock (_lock)
        {
            if (_thread is not null && _consuming == consume)
            {
                return;
            }

            StopLocked();
            _stopping = false;
            _consuming = consume;
            if (!_self.IsAllocated)
            {
                _self = GCHandle.Alloc(this);
            }

            _thread = new Thread(Run) { IsBackground = true, Name = "Global hotkeys" };
            _thread.Start();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            StopLocked();
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

    private void StopLocked()
    {
        _stopping = true;
        if (_runLoop != 0)
        {
            CFRunLoopStop(_runLoop);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    private unsafe void Run()
    {
        bool waiting = false;
        while (!_stopping)
        {
            bool consume = _consuming;
            // A listening tap only with the permission: trying one without it brings up macOS's
            // own question, while LiveSplit asks with its own window.
            nint tap = consume || CanListen
                ? CGEventTapCreate(
                    SessionEventTap,
                    HeadInsertEventTap,
                    consume ? TapOptionDefault : TapOptionListenOnly,
                    1UL << (int)KeyDown,
                    &Callback,
                    GCHandle.ToIntPtr(_self))
                : 0;
            if (tap == 0 && consume)
            {
                // No Accessibility permission: the keys are still seen, only not kept.
                Console.WriteLine("Global hotkeys: no Accessibility permission, the keys also reach other programs.");
                _consuming = false;
                continue;
            }

            if (tap == 0)
            {
                if (!waiting)
                {
                    waiting = true;
                    Console.WriteLine("Global hotkeys: waiting for the Input Monitoring permission.");
                }

                for (int i = 0; i < 30 && !_stopping; i++)
                {
                    Thread.Sleep(100);
                }

                continue;
            }

            nint source = CFMachPortCreateRunLoopSource(0, tap, 0);
            _runLoop = CFRunLoopGetCurrent();
            nint mode = CommonModes();
            CFRunLoopAddSource(_runLoop, source, mode);
            CGEventTapEnable(tap, true);
            _tap = tap;
            Console.WriteLine($"Global hotkeys: listening{(consume ? " (keys can be kept from other programs)" : "")}.");
            if (!_stopping)
            {
                CFRunLoopRun();
            }

            _tap = 0;
            CGEventTapEnable(tap, false);
            CFRunLoopRemoveSource(_runLoop, source, mode);
            CFRelease(source);
            CFRelease(tap);
            _runLoop = 0;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static nint Callback(nint proxy, uint type, nint cgEvent, nint userInfo)
    {
        try
        {
            if (GCHandle.FromIntPtr(userInfo).Target is not MacGlobalKeyboardHook hook)
            {
                return cgEvent;
            }

            if (type is TapDisabledByTimeout or TapDisabledByUserInput)
            {
                if (hook._tap != 0)
                {
                    CGEventTapEnable(hook._tap, true);
                }

                return cgEvent;
            }

            if (type != KeyDown)
            {
                return cgEvent;
            }

            string chord = ChordFor(CGEventGetIntegerValueField(cgEvent, KeyboardEventKeycode), CGEventGetFlags(cgEvent));
            if (chord.Length == 0)
            {
                return cgEvent;
            }

            bool consume = hook._consuming && hook.ShouldConsume?.Invoke(chord) == true;
            hook.KeyPressed?.Invoke(chord);
            return consume ? 0 : cgEvent;
        }
        catch (Exception)
        {
            return cgEvent;
        }
    }

    /// <summary>The chord for a virtual key code and Quartz modifier flags ("" for an unknown or modifier key).</summary>
    internal static string ChordFor(long keyCode, ulong flags)
    {
        if (!KeyCodes.TryGetValue(keyCode, out Key key))
        {
            return "";
        }

        KeyModifiers modifiers = KeyModifiers.None;
        if ((flags & FlagControl) != 0)
        {
            modifiers |= KeyModifiers.Control;
        }

        if ((flags & FlagAlternate) != 0)
        {
            modifiers |= KeyModifiers.Alt;
        }

        if ((flags & FlagShift) != 0)
        {
            modifiers |= KeyModifiers.Shift;
        }

        if ((flags & FlagCommand) != 0)
        {
            modifiers |= KeyModifiers.Meta;
        }

        return HotkeyChord.Format(key, modifiers);
    }

    /// <summary>macOS virtual key codes (kVK_*) as Avalonia names the keys (Avalonia.Native KeyTransform).</summary>
    internal static readonly IReadOnlyDictionary<long, Key> KeyCodes = new Dictionary<long, Key>
    {
        [0x00] = Key.A, [0x01] = Key.S, [0x02] = Key.D, [0x03] = Key.F, [0x04] = Key.H, [0x05] = Key.G,
        [0x06] = Key.Z, [0x07] = Key.X, [0x08] = Key.C, [0x09] = Key.V, [0x0B] = Key.B, [0x0C] = Key.Q,
        [0x0D] = Key.W, [0x0E] = Key.E, [0x0F] = Key.R, [0x10] = Key.Y, [0x11] = Key.T,
        [0x12] = Key.D1, [0x13] = Key.D2, [0x14] = Key.D3, [0x15] = Key.D4, [0x16] = Key.D6, [0x17] = Key.D5,
        [0x18] = Key.OemPlus, [0x19] = Key.D9, [0x1A] = Key.D7, [0x1B] = Key.OemMinus, [0x1C] = Key.D8, [0x1D] = Key.D0,
        [0x1E] = Key.OemCloseBrackets, [0x1F] = Key.O, [0x20] = Key.U, [0x21] = Key.OemOpenBrackets, [0x22] = Key.I,
        [0x23] = Key.P, [0x24] = Key.Return, [0x25] = Key.L, [0x26] = Key.J, [0x27] = Key.OemQuotes, [0x28] = Key.K,
        [0x29] = Key.OemSemicolon, [0x2A] = Key.OemPipe, [0x2B] = Key.OemComma, [0x2C] = Key.OemQuestion,
        [0x2D] = Key.N, [0x2E] = Key.M, [0x2F] = Key.OemPeriod, [0x30] = Key.Tab, [0x31] = Key.Space,
        [0x32] = Key.OemTilde, [0x33] = Key.Back, [0x35] = Key.Escape, [0x39] = Key.CapsLock,
        [0x40] = Key.F17, [0x41] = Key.Decimal, [0x43] = Key.Multiply, [0x45] = Key.Add, [0x47] = Key.Clear,
        [0x4B] = Key.Divide, [0x4C] = Key.Return, [0x4E] = Key.Subtract, [0x4F] = Key.F18, [0x50] = Key.F19,
        [0x52] = Key.NumPad0, [0x53] = Key.NumPad1, [0x54] = Key.NumPad2, [0x55] = Key.NumPad3, [0x56] = Key.NumPad4,
        [0x57] = Key.NumPad5, [0x58] = Key.NumPad6, [0x59] = Key.NumPad7, [0x5A] = Key.F20, [0x5B] = Key.NumPad8,
        [0x5C] = Key.NumPad9, [0x60] = Key.F5, [0x61] = Key.F6, [0x62] = Key.F7, [0x63] = Key.F3, [0x64] = Key.F8,
        [0x65] = Key.F9, [0x67] = Key.F11, [0x69] = Key.F13, [0x6A] = Key.F16, [0x6B] = Key.F14, [0x6D] = Key.F10,
        [0x6F] = Key.F12, [0x71] = Key.F15, [0x72] = Key.Insert, [0x73] = Key.Home, [0x74] = Key.PageUp,
        [0x75] = Key.Delete, [0x76] = Key.F4, [0x77] = Key.End, [0x78] = Key.F2, [0x79] = Key.PageDown, [0x7A] = Key.F1,
        [0x7B] = Key.Left, [0x7C] = Key.Right, [0x7D] = Key.Down, [0x7E] = Key.Up,
    };

    private static nint CommonModes()
    {
        nint handle = NativeLibrary.Load(CoreFoundation);
        nint symbol = NativeLibrary.GetExport(handle, "kCFRunLoopCommonModes");
        return Marshal.ReadIntPtr(symbol);
    }

    [DllImport(CoreGraphics)]
    private static extern unsafe nint CGEventTapCreate(
        int tap,
        int place,
        int options,
        ulong eventsOfInterest,
        delegate* unmanaged[Cdecl]<nint, uint, nint, nint, nint> callback,
        nint userInfo);

    [DllImport(CoreGraphics)]
    private static extern void CGEventTapEnable(nint tap, [MarshalAs(UnmanagedType.I1)] bool enable);

    [DllImport(CoreGraphics)]
    private static extern long CGEventGetIntegerValueField(nint cgEvent, int field);

    [DllImport(CoreGraphics)]
    private static extern ulong CGEventGetFlags(nint cgEvent);

    [DllImport(CoreGraphics)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGRequestListenEventAccess();

    [DllImport(IOKit)]
    private static extern uint IOHIDCheckAccess(uint requestType);

    [DllImport(ApplicationServices)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrusted();

    [DllImport(CoreFoundation)]
    private static extern nint CFMachPortCreateRunLoopSource(nint allocator, nint port, nint order);

    [DllImport(CoreFoundation)]
    private static extern nint CFRunLoopGetCurrent();

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopAddSource(nint runLoop, nint source, nint mode);

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopRemoveSource(nint runLoop, nint source, nint mode);

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopRun();

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopStop(nint runLoop);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(nint value);
}

/// <summary>macOS's Input Monitoring permission for this app.</summary>
internal enum KeyListenPermission
{
    Granted,
    Denied,
    NotAsked,
}
