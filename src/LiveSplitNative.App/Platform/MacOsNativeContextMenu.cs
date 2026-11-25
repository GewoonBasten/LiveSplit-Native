using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Real AppKit <c>NSMenu</c> / <c>NSMenuItem</c> context menu via libobjc.
/// Appearance (light/dark, fonts, hover, shadows) comes from the system — no custom chrome.
/// </summary>
internal sealed class MacOsNativeContextMenu : INativeContextMenu
{
    private static readonly ConcurrentDictionary<nint, Action> s_clickHandlers = new();
    private static nint s_targetClass;
    private static nint s_targetInstance;
    private static readonly object s_initLock = new();
    private static bool s_initFailed;

    // Keep strong refs for the duration of popUp tracking.
    private readonly List<object> _lifetime = [];

    public bool IsSystemNative => true;
    public string BackendName => "AppKit.NSMenu";

    public static bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsMacOS() || s_initFailed)
            {
                return false;
            }

            try
            {
                EnsureTarget();
                return s_targetInstance != 0;
            }
            catch
            {
                s_initFailed = true;
                return false;
            }
        }
    }

    private static readonly AvaloniaFallbackContextMenu s_fallback = new();

    public void Show(Window owner, PixelPoint screenPoint, NativeMenuModel model)
    {
        // AppKit only on its main thread and for a real NSWindow; headless test hosts have
        // neither (an NSMenu there aborts the process), so they get the Avalonia menu.
        if (owner.TryGetPlatformHandle()?.HandleDescriptor?.Contains("NSWindow", StringComparison.OrdinalIgnoreCase) != true)
        {
            s_fallback.Show(owner, screenPoint, model);
            return;
        }

        EnsureTarget();
        _lifetime.Clear();

        nint menu = BuildNsMenu(model.Items);
        _lifetime.Add(menu);

        NSPoint location = ToAppKitScreenPoint(owner, screenPoint);

        // popUpMenuPositioningItem:atLocation:inView: — view nil ⇒ screen coordinates (AppKit).
        nint sel = sel_registerName("popUpMenuPositioningItem:atLocation:inView:");
        objc_msgSend_popUp(menu, sel, 0, location, 0);

        // Drop handlers for items from this tree after tracking ends.
        // (Handlers keyed by NSMenuItem*; items are released with the menu.)
        foreach (nint key in s_clickHandlers.Keys.ToArray())
        {
            // Leave handlers that may still be referenced; clear all — we own the session.
            s_clickHandlers.TryRemove(key, out _);
        }

        objc_msgSend_void(menu, sel_registerName("release"));
        _lifetime.Clear();
        _ = owner;
    }

    private nint BuildNsMenu(IReadOnlyList<NativeMenuEntry> entries)
    {
        nint menuClass = objc_getClass("NSMenu");
        nint menu = objc_msgSend_ret(objc_msgSend_ret(menuClass, sel_registerName("alloc")), sel_registerName("init"));
        objc_msgSend_bool(menu, sel_registerName("setAutoenablesItems:"), false);

        foreach (NativeMenuEntry entry in entries)
        {
            nint item = BuildNsItem(entry);
            objc_msgSend_id(menu, sel_registerName("addItem:"), item);
            // addItem: retains; balance our alloc/copy.
            objc_msgSend_void(item, sel_registerName("release"));
        }

        return menu;
    }

    private nint BuildNsItem(NativeMenuEntry entry)
    {
        if (entry is NativeMenuSeparator)
        {
            nint sepClass = objc_getClass("NSMenuItem");
            nint sep = objc_msgSend_ret(sepClass, sel_registerName("separatorItem"));
            // separatorItem is a shared singleton — retain so addItem:/release pairing is safe.
            return objc_msgSend_ret(sep, sel_registerName("retain"));
        }

        var action = (NativeMenuAction)entry;
        // stringWithUTF8String: returns autoreleased NSString — do not release.
        nint title = NsString(action.Header);
        nint empty = NsString("");
        nint itemClass = objc_getClass("NSMenuItem");
        nint item = objc_msgSend_initTitle(
            objc_msgSend_ret(itemClass, sel_registerName("alloc")),
            sel_registerName("initWithTitle:action:keyEquivalent:"),
            title,
            sel_registerName("lsnInvoke:"),
            empty);

        objc_msgSend_bool(item, sel_registerName("setEnabled:"), action.IsEnabled);
        if (action.IsChecked)
        {
            // NSControlStateValueOn = 1
            objc_msgSend_nint(item, sel_registerName("setState:"), 1);
        }

        if (action.Children is not null)
        {
            // Empty category submenus (Windows Media/Control) still get an NSMenu.
            nint sub = BuildNsMenu(action.Children);
            objc_msgSend_id(item, sel_registerName("setSubmenu:"), sub);
            objc_msgSend_void(sub, sel_registerName("release"));
        }
        else if (action.IsEnabled && (action.OnClick is not null || action.OnClickAsync is not null))
        {
            objc_msgSend_id(item, sel_registerName("setTarget:"), s_targetInstance);
            Action invoke = action.Invoke;
            // Run the item after the menu's tracking loop has ended: an item that opens a dialog
            // or a file panel must not start it from inside the menu's modal loop.
            s_clickHandlers[item] = () => Dispatcher.UIThread.Post(invoke);
            // Keep managed action graph alive while menu is open.
            _lifetime.Add(action);
        }

        return item;
    }

    private static NSPoint ToAppKitScreenPoint(Window owner, PixelPoint screenPoint)
    {
        // Avalonia PixelPoint is top-left; AppKit screen coords are bottom-left (main display).
        // Prefer the caller point (right-click / proof anchor) over NSEvent.mouseLocation so
        // programmatic Show() is not skewed by a distant cursor.
        try
        {
            Screen? screen = owner.Screens?.ScreenFromPoint(screenPoint)
                ?? owner.Screens?.Primary
                ?? owner.Screens?.All.FirstOrDefault();
            if (screen is not null)
            {
                // Convert within the screen frame, then to global AppKit space.
                double localTopY = screenPoint.Y - screen.Bounds.Y;
                double appKitLocalY = screen.Bounds.Height - localTopY;
                // AppKit global: origin at bottom-left of primary; Avalonia screen.Bounds.Y
                // is top-left based. For primary (Y≈0) globalY = appKitLocalY works.
                double primaryHeight = owner.Screens?.Primary?.Bounds.Height
                    ?? screen.Bounds.Height;
                double globalY = primaryHeight - screenPoint.Y;
                _ = appKitLocalY;
                return new NSPoint { X = screenPoint.X, Y = globalY };
            }
        }
        catch
        {
            // fall through
        }

        try
        {
            nint nsEvent = objc_getClass("NSEvent");
            if (nsEvent != 0)
            {
                return objc_msgSend_point(nsEvent, sel_registerName("mouseLocation"));
            }
        }
        catch
        {
            // ignore
        }

        return new NSPoint { X = screenPoint.X, Y = 600 };
    }

    private static nint NsString(string value)
    {
        nint nsString = objc_getClass("NSString");
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value + "\0");
        nint ptr = Marshal.AllocHGlobal(utf8.Length);
        try
        {
            Marshal.Copy(utf8, 0, ptr, utf8.Length);
            // stringWithUTF8String: copies bytes into an autoreleased NSString.
            return objc_msgSend_ret(nsString, sel_registerName("stringWithUTF8String:"), ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void EnsureTarget()
    {
        if (s_targetInstance != 0 || s_initFailed)
        {
            return;
        }

        lock (s_initLock)
        {
            if (s_targetInstance != 0 || s_initFailed)
            {
                return;
            }

            try
            {
                nint nsObject = objc_getClass("NSObject");
                s_targetClass = objc_allocateClassPair(nsObject, "LSNNativeMenuTarget", 0);
                if (s_targetClass == 0)
                {
                    // Already registered from a prior load.
                    s_targetClass = objc_getClass("LSNNativeMenuTarget");
                }
                else
                {
                    nint imp = Marshal.GetFunctionPointerForDelegate(s_invokeDelegate);
                    if (!class_addMethod(s_targetClass, sel_registerName("lsnInvoke:"), imp, "v@:@"))
                    {
                        throw new InvalidOperationException("class_addMethod(lsnInvoke:) failed");
                    }

                    objc_registerClassPair(s_targetClass);
                }

                s_targetInstance = objc_msgSend_ret(
                    objc_msgSend_ret(s_targetClass, sel_registerName("alloc")),
                    sel_registerName("init"));
            }
            catch
            {
                s_initFailed = true;
                throw;
            }
        }
    }

    private static readonly ObjCInvoke s_invokeDelegate = OnInvoke;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ObjCInvoke(nint self, nint cmd, nint sender);

    private static void OnInvoke(nint self, nint cmd, nint sender)
    {
        if (s_clickHandlers.TryGetValue(sender, out Action? action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("NSMenu click handler failed: " + ex);
            }
        }

        _ = self;
        _ = cmd;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NSPoint
    {
        public double X;
        public double Y;
    }

    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    [DllImport(ObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(ObjC)]
    private static extern nint objc_allocateClassPair(nint superclass, string name, nint extraBytes);

    [DllImport(ObjC)]
    private static extern void objc_registerClassPair(nint cls);

    [DllImport(ObjC)]
    private static extern bool class_addMethod(nint cls, nint name, nint imp, string types);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_ret(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_ret(nint receiver, nint selector, nint arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_id(nint receiver, nint selector, nint arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(nint receiver, nint selector, bool arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_nint(nint receiver, nint selector, nint arg1);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_initTitle(
        nint receiver, nint selector, nint title, nint action, nint keyEquivalent);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern bool objc_msgSend_popUp(
        nint receiver, nint selector, nint item, NSPoint location, nint view);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern NSPoint objc_msgSend_point(nint receiver, nint selector);
}
