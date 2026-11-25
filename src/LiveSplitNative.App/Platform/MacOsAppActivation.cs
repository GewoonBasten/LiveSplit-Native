using System.Runtime.InteropServices;

namespace LiveSplitNative.App.Platform;

/// <summary>Bring the process to the foreground so NSMenu / screencapture see the timer window.</summary>
internal static class MacOsAppActivation
{
    public static void ActivateIgnoringOtherApps()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            // AppKit must only be driven from the main thread. Avalonia.Native and the screenshot
            // harness run their UI there; headless test hosts do not, and activating from another
            // thread deadlocks them (seen 11 Oct 2026), so skip it there.
            if (!objc_msgSend_getBool(objc_getClass("NSThread"), sel_registerName("isMainThread")))
            {
                return;
            }

            nint nsApp = objc_getClass("NSApplication");
            nint shared = objc_msgSend_ret(nsApp, sel_registerName("sharedApplication"));
            // NSApplicationActivationPolicyRegular = 0 — required for menus/windows from agent shells.
            objc_msgSend_nint(shared, sel_registerName("setActivationPolicy:"), 0);
            objc_msgSend_bool(shared, sel_registerName("activateIgnoringOtherApps:"), true);
            objc_msgSend_void(shared, sel_registerName("finishLaunching"));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("NSApp activate failed: " + ex.Message);
        }
    }

    /// <summary>
    /// NSApplication hide: (Hide LiveSplit Native, Cmd+H in other apps): every window of the app
    /// goes out of sight while it keeps running (global hotkeys and gamepads too); clicking its
    /// Dock icon brings them back, as macOS does for any hidden app. False when not on macOS.
    /// </summary>
    public static bool Hide()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            // AppKit only on the main thread (headless test hosts run their UI elsewhere).
            if (!objc_msgSend_getBool(objc_getClass("NSThread"), sel_registerName("isMainThread")))
            {
                return false;
            }

            nint shared = objc_msgSend_ret(objc_getClass("NSApplication"), sel_registerName("sharedApplication"));
            objc_msgSend_nint(shared, sel_registerName("hide:"), 0);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("NSApp hide failed: " + ex.Message);
            return false;
        }
    }

    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_ret(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_getBool(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(nint receiver, nint selector, bool arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_nint(nint receiver, nint selector, nint arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void(nint receiver, nint selector);
}
