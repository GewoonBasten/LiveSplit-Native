using System.Runtime.InteropServices;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App.Platform;

/// <summary>An audio output device: its name (shown in the settings) and Core Audio UID.</summary>
internal sealed record SoundOutputDevice(string Name, string Uid);

/// <summary>
/// Sound Effects' player on macOS (Windows: NAudio WaveOut + AudioFileReader): AVAudioPlayer,
/// one sound at a time. The device list starts with the system's default output device (as
/// WaveOut device 0 is Windows' default), then the other output devices; device 0 follows the
/// system default, and an index this Mac does not have plays on the default too.
/// </summary>
internal sealed class MacOsSoundPlayer : ISoundPlayer
{
    private readonly object _lock = new();
    private nint _player;
    private int _generation;

    public void Stop()
    {
        lock (_lock)
        {
            _generation++;
            ReleasePlayer();
        }
    }

    public void Play(string path, float volume, int outputDevice)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        int generation;
        lock (_lock)
        {
            generation = ++_generation;
        }

        // PlaySound: the file is opened and played off the timer's thread.
        Task.Run(() =>
        {
            nint pool = objc_autoreleasePoolPush();
            try
            {
                LoadAvFoundation();
                nint url = Send(objc_getClass("NSURL"), Sel("fileURLWithPath:"), NsString(path));
                nint player = Send(Send(objc_getClass("AVAudioPlayer"), Sel("alloc")), Sel("initWithContentsOfURL:error:"), url, 0);
                if (player == 0)
                {
                    Console.Error.WriteLine("Sound Effects: could not open " + path);
                    return;
                }

                SendFloat(player, Sel("setVolume:"), Math.Clamp(volume, 0f, 1f));
                if (outputDevice > 0
                    && OutputDevices() is { } devices
                    && outputDevice < devices.Count
                    && SendBool(player, Sel("respondsToSelector:"), Sel("setCurrentDevice:")))
                {
                    SendVoid(player, Sel("setCurrentDevice:"), NsString(devices[outputDevice].Uid));
                }

                lock (_lock)
                {
                    if (generation != _generation)
                    {
                        SendVoid(player, Sel("release"));
                        return;
                    }

                    ReleasePlayer();
                    _player = player;
                    SendBool(player, Sel("prepareToPlay"));
                    SendBool(player, Sel("play"));
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Sound Effects: " + e.Message);
            }
            finally
            {
                objc_autoreleasePoolPop(pool);
            }
        });
    }

    /// <summary>For the proofs: whether AVAudioPlayer is playing now (null: no player yet).</summary>
    internal bool? IsPlaying
    {
        get
        {
            lock (_lock)
            {
                return _player == 0 ? null : SendBool(_player, Sel("isPlaying"));
            }
        }
    }

    private void ReleasePlayer()
    {
        if (_player == 0)
        {
            return;
        }

        nint player = _player;
        _player = 0;
        try
        {
            SendVoid(player, Sel("stop"));
            SendVoid(player, Sel("release"));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Sound Effects: " + e.Message);
        }
    }

    /// <summary>The output devices, the system default first (the settings' Output Device list).</summary>
    public static IReadOnlyList<SoundOutputDevice> OutputDevices()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return [];
        }

        try
        {
            var address = new AudioObjectPropertyAddress(DevicesSelector, GlobalScope);
            if (AudioObjectGetPropertyDataSize(SystemObject, ref address, 0, 0, out uint size) != 0 || size == 0)
            {
                return [];
            }

            uint[] ids = new uint[size / sizeof(uint)];
            GCHandle handle = GCHandle.Alloc(ids, GCHandleType.Pinned);
            try
            {
                if (AudioObjectGetPropertyData(SystemObject, ref address, 0, 0, ref size, handle.AddrOfPinnedObject()) != 0)
                {
                    return [];
                }
            }
            finally
            {
                handle.Free();
            }

            uint defaultId = DefaultOutputDevice();
            var devices = new List<(uint Id, SoundOutputDevice Device)>();
            foreach (uint id in ids)
            {
                var streams = new AudioObjectPropertyAddress(StreamsSelector, OutputScope);
                if (AudioObjectGetPropertyDataSize(id, ref streams, 0, 0, out uint streamsSize) != 0 || streamsSize == 0)
                {
                    continue;
                }

                string? name = StringProperty(id, NameSelector);
                string? uid = StringProperty(id, UidSelector);
                if (name is not null && uid is not null)
                {
                    devices.Add((id, new SoundOutputDevice(name, uid)));
                }
            }

            return devices
                .OrderBy(d => d.Id == defaultId ? 0 : 1)
                .Select(d => d.Device)
                .ToList();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Sound Effects: no output devices: " + e.Message);
            return [];
        }
    }

    private static uint DefaultOutputDevice()
    {
        var address = new AudioObjectPropertyAddress(DefaultOutputSelector, GlobalScope);
        uint size = sizeof(uint);
        nint data = Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            return AudioObjectGetPropertyData(SystemObject, ref address, 0, 0, ref size, data) == 0
                ? (uint)Marshal.ReadInt32(data)
                : 0;
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static string? StringProperty(uint device, uint selector)
    {
        var address = new AudioObjectPropertyAddress(selector, GlobalScope);
        uint size = (uint)nint.Size;
        nint data = Marshal.AllocHGlobal(nint.Size);
        try
        {
            if (AudioObjectGetPropertyData(device, ref address, 0, 0, ref size, data) != 0)
            {
                return null;
            }

            nint cfString = Marshal.ReadIntPtr(data);
            if (cfString == 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(Send(cfString, Sel("UTF8String")));
            }
            finally
            {
                CFRelease(cfString);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(data);
        }
    }

    private static bool s_avFoundationLoaded;

    private static void LoadAvFoundation()
    {
        if (!s_avFoundationLoaded)
        {
            NativeLibrary.Load("/System/Library/Frameworks/AVFoundation.framework/AVFoundation");
            s_avFoundationLoaded = true;
        }
    }

    private static nint Sel(string name) => sel_registerName(name);

    /// <summary>An autoreleased NSString (the caller's pool releases it).</summary>
    private static nint NsString(string value)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value + "\0");
        nint ptr = Marshal.AllocHGlobal(utf8.Length);
        try
        {
            Marshal.Copy(utf8, 0, ptr, utf8.Length);
            return Send(objc_getClass("NSString"), Sel("stringWithUTF8String:"), ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioObjectPropertyAddress(uint selector, uint scope)
    {
        public uint Selector = selector;
        public uint Scope = scope;
        public uint Element = 0; // kAudioObjectPropertyElementMain
    }

    private const uint SystemObject = 1; // kAudioObjectSystemObject
    private const uint DevicesSelector = 0x64657623; // 'dev#' kAudioHardwarePropertyDevices
    private const uint DefaultOutputSelector = 0x644F7574; // 'dOut' kAudioHardwarePropertyDefaultOutputDevice
    private const uint StreamsSelector = 0x73746D23; // 'stm#' kAudioDevicePropertyStreams
    private const uint NameSelector = 0x6C6E616D; // 'lnam' kAudioObjectPropertyName
    private const uint UidSelector = 0x75696420; // 'uid ' kAudioDevicePropertyDeviceUID
    private const uint GlobalScope = 0x676C6F62; // 'glob'
    private const uint OutputScope = 0x6F757470; // 'outp'

    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string CoreAudio = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreAudio)]
    private static extern int AudioObjectGetPropertyDataSize(uint objectId, ref AudioObjectPropertyAddress address, uint qualifierSize, nint qualifier, out uint dataSize);

    [DllImport(CoreAudio)]
    private static extern int AudioObjectGetPropertyData(uint objectId, ref AudioObjectPropertyAddress address, uint qualifierSize, nint qualifier, ref uint dataSize, nint data);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(nint value);

    [DllImport(ObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(ObjC)]
    private static extern nint objc_autoreleasePoolPush();

    [DllImport(ObjC)]
    private static extern void objc_autoreleasePoolPop(nint pool);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint Send(nint receiver, nint selector, nint arg1, nint arg2);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendVoid(nint receiver, nint selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void SendFloat(nint receiver, nint selector, float arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SendBool(nint receiver, nint selector, nint arg);
}
