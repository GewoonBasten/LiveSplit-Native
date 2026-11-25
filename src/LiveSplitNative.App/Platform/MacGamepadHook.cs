using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using LiveSplitNative.Core.Input;

namespace LiveSplitNative.App.Platform;

/// <summary>Where gamepad button presses come from (the Mac's controllers; tests use their own).</summary>
internal interface IGamepadSource
{
    /// <summary>A button was pressed: its hotkey ("Buttons0 Wireless Controller 054C-09CC"), on any thread.</summary>
    event Action<string>? ButtonPressed;

    /// <summary>Listens to the controllers until the returned lease is disposed (leases are counted).</summary>
    IDisposable Acquire();
}

/// <summary>The gamepad source the app uses.</summary>
internal static class Gamepads
{
    /// <summary>This computer's controllers: the Mac's, or Linux's input devices (tests replace it).</summary>
    public static IGamepadSource Current { get; set; } =
        OperatingSystem.IsLinux() ? new LinuxGamepadHook() : new MacGamepadHook();
}

/// <summary>
/// CompositeHook's GamepadHook on macOS: IOKit's HID manager reports every value of the connected
/// joysticks, gamepads, multi-axis controllers and mice (Windows: every DirectInput device but the
/// keyboard), which <see cref="GamepadInput"/> turns into LiveSplit's button names. It runs on its
/// own thread while something needs it (Allow Gamepads as Hotkeys, or a hotkey field waiting for
/// a key in Settings); controllers plugged in later are picked up by themselves.
/// </summary>
internal sealed class MacGamepadHook : IGamepadSource
{
    private const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint OptionsNone = 0;
    private const int NumberSInt32 = 3;
    private const int NumberSInt64 = 4;
    private const uint Utf8 = 0x08000100;

    private readonly object _lock = new();
    private readonly GamepadInput _input = new();
    private GCHandle _self;
    private Thread? _thread;
    private nint _runLoop;
    private int _leases;

    public event Action<string>? ButtonPressed;

    public IDisposable Acquire()
    {
        lock (_lock)
        {
            _leases++;
            if (_leases == 1 && OperatingSystem.IsMacOS())
            {
                if (!_self.IsAllocated)
                {
                    _self = GCHandle.Alloc(this);
                }

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
                if (_runLoop != 0)
                {
                    CFRunLoopStop(_runLoop);
                }

                thread = _thread;
                _thread = null;
            }
        }

        thread?.Join(TimeSpan.FromSeconds(2));
    }

    private unsafe void Run()
    {
        nint manager = 0;
        nint matching = 0;
        try
        {
            manager = IOHIDManagerCreate(0, OptionsNone);
            if (manager == 0)
            {
                return;
            }

            matching = CFArrayCreateMutable(0, 0, Export("kCFTypeArrayCallBacks"));
            foreach (int usage in new[] { GamepadInput.JoystickUsage, GamepadInput.GamePadUsage, GamepadInput.MultiAxisControllerUsage, GamepadInput.MouseUsage })
            {
                nint dictionary = Matching(GamepadInput.GenericDesktopPage, usage);
                CFArrayAppendValue(matching, dictionary);
                CFRelease(dictionary);
            }

            IOHIDManagerSetDeviceMatchingMultiple(manager, matching);
            IOHIDManagerRegisterInputValueCallback(manager, &Callback, GCHandle.ToIntPtr(_self));
            nint runLoop = CFRunLoopGetCurrent();
            nint mode = Export("kCFRunLoopDefaultMode") is var symbol && symbol != 0 ? Marshal.ReadIntPtr(symbol) : 0;
            IOHIDManagerScheduleWithRunLoop(manager, runLoop, mode);
            int opened = IOHIDManagerOpen(manager, OptionsNone);
            Console.WriteLine(opened == 0
                ? $"Gamepad hotkeys: listening ({string.Join(", ", DeviceNames(manager))})."
                : $"Gamepad hotkeys: not every device could be opened ({opened:X}); listening to {string.Join(", ", DeviceNames(manager))}.");
            lock (_lock)
            {
                if (_thread != Thread.CurrentThread)
                {
                    return;
                }

                _runLoop = runLoop;
            }

            CFRunLoopRun();
            IOHIDManagerUnscheduleFromRunLoop(manager, runLoop, mode);
            IOHIDManagerClose(manager, OptionsNone);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Console.WriteLine("Gamepad hotkeys: not available (" + ex.Message + ").");
        }
        finally
        {
            lock (_lock)
            {
                _runLoop = 0;
            }

            if (matching != 0)
            {
                CFRelease(matching);
            }

            if (manager != 0)
            {
                CFRelease(manager);
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Callback(nint context, int result, nint sender, nint value)
    {
        try
        {
            if (GCHandle.FromIntPtr(context).Target is not MacGamepadHook hook || value == 0)
            {
                return;
            }

            nint element = IOHIDValueGetElement(value);
            nint device = IOHIDElementGetDevice(element);
            int page = (int)IOHIDElementGetUsagePage(element);
            int usage = (int)IOHIDElementGetUsage(element);
            if (usage == -1 || page > 0xFF)
            {
                return; // vendor-defined
            }

            bool isMouse = IOHIDDeviceConformsTo(device, GamepadInput.GenericDesktopPage, GamepadInput.MouseUsage)
                && !IOHIDDeviceConformsTo(device, GamepadInput.GenericDesktopPage, GamepadInput.GamePadUsage)
                && !IOHIDDeviceConformsTo(device, GamepadInput.GenericDesktopPage, GamepadInput.JoystickUsage);
            string deviceName = GamepadInput.DeviceName(
                StringProperty(device, "Product"),
                IntProperty(device, "VendorID"),
                IntProperty(device, "ProductID"));
            string? pressed = hook._input.Value(
                device.ToString("X"),
                deviceName,
                IOHIDElementGetCookie(element),
                page,
                usage,
                IOHIDValueGetIntegerValue(value),
                IOHIDElementGetLogicalMin(element),
                IOHIDElementGetLogicalMax(element),
                isMouse);
            if (pressed is not null)
            {
                hook.ButtonPressed?.Invoke(pressed);
            }
        }
        catch (Exception)
        {
            // A device that went away mid-report: nothing to do.
        }
    }

    /// <summary>The devices the manager has now, by their hotkey names (for the log).</summary>
    private static List<string> DeviceNames(nint manager)
    {
        var names = new List<string>();
        nint set = IOHIDManagerCopyDevices(manager);
        if (set == 0)
        {
            return names;
        }

        try
        {
            nint count = CFSetGetCount(set);
            nint[] devices = new nint[count];
            CFSetGetValues(set, devices);
            foreach (nint device in devices)
            {
                names.Add(GamepadInput.DeviceName(StringProperty(device, "Product"), IntProperty(device, "VendorID"), IntProperty(device, "ProductID")));
            }
        }
        finally
        {
            CFRelease(set);
        }

        return names;
    }

    private static nint Matching(int page, int usage)
    {
        nint dictionary = CFDictionaryCreateMutable(0, 0, Export("kCFTypeDictionaryKeyCallBacks"), Export("kCFTypeDictionaryValueCallBacks"));
        SetNumber(dictionary, "DeviceUsagePage", page);
        SetNumber(dictionary, "DeviceUsage", usage);
        return dictionary;
    }

    private static void SetNumber(nint dictionary, string key, int number)
    {
        nint cfKey = CFStringCreateWithCString(0, key, Utf8);
        nint cfNumber = CFNumberCreate(0, NumberSInt32, ref number);
        CFDictionarySetValue(dictionary, cfKey, cfNumber);
        CFRelease(cfKey);
        CFRelease(cfNumber);
    }

    private static string? StringProperty(nint device, string key)
    {
        nint cfKey = CFStringCreateWithCString(0, key, Utf8);
        try
        {
            nint value = IOHIDDeviceGetProperty(device, cfKey);
            if (value == 0 || CFGetTypeID(value) != CFStringGetTypeID())
            {
                return null;
            }

            byte[] buffer = new byte[512];
            return CFStringGetCString(value, buffer, buffer.Length, Utf8)
                ? Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0) is var end and >= 0 ? end : buffer.Length)
                : null;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    private static int IntProperty(nint device, string key)
    {
        nint cfKey = CFStringCreateWithCString(0, key, Utf8);
        try
        {
            nint value = IOHIDDeviceGetProperty(device, cfKey);
            long number = 0;
            return value != 0 && CFGetTypeID(value) == CFNumberGetTypeID() && CFNumberGetValue(value, NumberSInt64, ref number)
                ? (int)number
                : 0;
        }
        finally
        {
            CFRelease(cfKey);
        }
    }

    private static nint Export(string name)
    {
        nint handle = NativeLibrary.Load(CoreFoundation);
        return NativeLibrary.GetExport(handle, name);
    }

    private sealed class Lease(MacGamepadHook hook) : IDisposable
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

    [DllImport(IOKit)]
    private static extern nint IOHIDManagerCreate(nint allocator, uint options);

    [DllImport(IOKit)]
    private static extern void IOHIDManagerSetDeviceMatchingMultiple(nint manager, nint multiple);

    [DllImport(IOKit)]
    private static extern unsafe void IOHIDManagerRegisterInputValueCallback(
        nint manager, delegate* unmanaged[Cdecl]<nint, int, nint, nint, void> callback, nint context);

    [DllImport(IOKit)]
    private static extern void IOHIDManagerScheduleWithRunLoop(nint manager, nint runLoop, nint mode);

    [DllImport(IOKit)]
    private static extern void IOHIDManagerUnscheduleFromRunLoop(nint manager, nint runLoop, nint mode);

    [DllImport(IOKit)]
    private static extern int IOHIDManagerOpen(nint manager, uint options);

    [DllImport(IOKit)]
    private static extern int IOHIDManagerClose(nint manager, uint options);

    [DllImport(IOKit)]
    private static extern nint IOHIDManagerCopyDevices(nint manager);

    [DllImport(CoreFoundation)]
    private static extern nint CFSetGetCount(nint set);

    [DllImport(CoreFoundation)]
    private static extern void CFSetGetValues(nint set, [Out] nint[] values);

    [DllImport(IOKit)]
    private static extern nint IOHIDValueGetElement(nint value);

    [DllImport(IOKit)]
    private static extern long IOHIDValueGetIntegerValue(nint value);

    [DllImport(IOKit)]
    private static extern nint IOHIDElementGetDevice(nint element);

    [DllImport(IOKit)]
    private static extern uint IOHIDElementGetUsagePage(nint element);

    [DllImport(IOKit)]
    private static extern uint IOHIDElementGetUsage(nint element);

    [DllImport(IOKit)]
    private static extern uint IOHIDElementGetCookie(nint element);

    [DllImport(IOKit)]
    private static extern long IOHIDElementGetLogicalMin(nint element);

    [DllImport(IOKit)]
    private static extern long IOHIDElementGetLogicalMax(nint element);

    [DllImport(IOKit)]
    private static extern nint IOHIDDeviceGetProperty(nint device, nint key);

    [DllImport(IOKit)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool IOHIDDeviceConformsTo(nint device, int usagePage, int usage);

    [DllImport(CoreFoundation)]
    private static extern nint CFRunLoopGetCurrent();

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopRun();

    [DllImport(CoreFoundation)]
    private static extern void CFRunLoopStop(nint runLoop);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(nint value);

    [DllImport(CoreFoundation)]
    private static extern nint CFArrayCreateMutable(nint allocator, nint capacity, nint callbacks);

    [DllImport(CoreFoundation)]
    private static extern void CFArrayAppendValue(nint array, nint value);

    [DllImport(CoreFoundation)]
    private static extern nint CFDictionaryCreateMutable(nint allocator, nint capacity, nint keyCallbacks, nint valueCallbacks);

    [DllImport(CoreFoundation)]
    private static extern void CFDictionarySetValue(nint dictionary, nint key, nint value);

    [DllImport(CoreFoundation)]
    private static extern nint CFNumberCreate(nint allocator, int type, ref int value);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFNumberGetValue(nint number, int type, ref long value);

    [DllImport(CoreFoundation)]
    private static extern nint CFStringCreateWithCString(nint allocator, string text, uint encoding);

    [DllImport(CoreFoundation)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(nint text, byte[] buffer, nint size, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern nuint CFGetTypeID(nint value);

    [DllImport(CoreFoundation)]
    private static extern nuint CFStringGetTypeID();

    [DllImport(CoreFoundation)]
    private static extern nuint CFNumberGetTypeID();
}
