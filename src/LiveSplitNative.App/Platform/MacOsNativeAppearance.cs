using System.Runtime.InteropServices;
using LiveSplitNative.Core.Options;

namespace LiveSplitNative.App.Platform;

/// <summary>Sets NSApp.appearance so NSMenu / native panels match System/Light/Dark.</summary>
internal sealed class MacOsNativeAppearance : INativeAppearance
{
    public string BackendName => "AppKit.NSAppearance";

    public static bool IsAvailable => OperatingSystem.IsMacOS();

    public void Apply(AppAppearanceMode mode)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            nint nsAppClass = objc_getClass("NSApplication");
            nint shared = objc_msgSend_ret(nsAppClass, sel_registerName("sharedApplication"));
            nint appearance = mode switch
            {
                AppAppearanceMode.Light => AppearanceNamed("NSAppearanceNameAqua"),
                AppAppearanceMode.Dark => AppearanceNamed("NSAppearanceNameDarkAqua"),
                _ => nint.Zero, // System — nil clears override
            };
            objc_msgSend_nint(shared, sel_registerName("setAppearance:"), appearance);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("NSAppearance apply failed: " + ex.Message);
        }
    }

    private static nint AppearanceNamed(string name)
    {
        nint nsAppearance = objc_getClass("NSAppearance");
        nint nsName = CFString(name);
        return objc_msgSend_ret_nint(nsAppearance, sel_registerName("appearanceNamed:"), nsName);
    }

    private static nint CFString(string value) =>
        CFStringCreateWithCString(nint.Zero, value, kCFStringEncodingUTF8);

    private const string ObjC = "/usr/lib/libobjc.A.dylib";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint kCFStringEncodingUTF8 = 0x08000100;

    [DllImport(ObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_ret(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_ret_nint(nint receiver, nint selector, nint arg);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_nint(nint receiver, nint selector, nint arg);

    [DllImport(CoreFoundation)]
    private static extern nint CFStringCreateWithCString(nint alloc, string str, uint encoding);
}
