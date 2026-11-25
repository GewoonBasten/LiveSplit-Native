using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Platform seam for borderless timer move (Windows TimerForm FormBorderStyle.None).
/// Resize uses <see cref="BorderlessWindowGeometry.ApplyResize"/> from pointer deltas —
/// Avalonia BeginResizeDrag is a no-op on macOS SystemDecorations=None.
/// </summary>
public interface INativeWindowFrame
{
    string BackendName { get; }

    void BeginMove(Window window, PointerPressedEventArgs e);

    void SetMousePassThrough(Window window, bool enabled);

    bool? IsMousePassThrough(Window window);

    /// <summary>
    /// F2: the window frame and mouse in screen points with y up, set in one call (AppKit frame /
    /// setFrame:display:). False where the platform has no such call; the caller then falls
    /// back to Avalonia Width / Height / Position.
    /// </summary>
    bool SupportsScreenFrames { get; }

    bool TryGetScreenFrame(Window window, out ScreenFrame frame);

    void SetScreenFrame(Window window, ScreenFrame frame);

    bool TryGetMouseLocation(out double x, out double y);

    /// <summary>The top of the window's screen below the menu bar, in the same points; null if unknown.</summary>
    double? VisibleTop(Window window);

    /// <summary>
    /// An owned dialog's window level: one above its owner's and above the floating level, so
    /// an Always-on-Top timer never covers its dialogs and is never lowered for them.
    /// </summary>
    void PlaceAbove(Window child, Window owner);

    /// <summary>The window's AppKit level and whether any of it is on screen, for the self-tests.</summary>
    string DescribeLevel(Window window);

    /// <summary>
    /// Tags the window's colors as sRGB (Windows LiveSplit's colors), so wide-gamut and HDR
    /// displays convert them instead of showing them oversaturated. Returns the color space the
    /// window then has, or null where there is nothing to set.
    /// </summary>
    string? UseSrgbColorSpace(Window window);
}

public static class NativeWindowFrameFactory
{
    private static INativeWindowFrame? _cached;

    public static INativeWindowFrame Current => _cached ??=
        OperatingSystem.IsMacOS()
            ? new MacOsNativeWindowFrame()
            : new AvaloniaNativeWindowFrame();
}

internal sealed class AvaloniaNativeWindowFrame : INativeWindowFrame
{
    public string BackendName => "Avalonia";

    public void BeginMove(Window window, PointerPressedEventArgs e) =>
        window.BeginMoveDrag(e);

    public void SetMousePassThrough(Window window, bool enabled)
    {
        // Portable seam for a future Linux implementation.
    }

    public bool? IsMousePassThrough(Window window) => null;

    public bool SupportsScreenFrames => false;

    public bool TryGetScreenFrame(Window window, out ScreenFrame frame)
    {
        frame = default;
        return false;
    }

    public void SetScreenFrame(Window window, ScreenFrame frame)
    {
    }

    public bool TryGetMouseLocation(out double x, out double y)
    {
        x = y = 0;
        return false;
    }

    public double? VisibleTop(Window window) => null;

    public void PlaceAbove(Window child, Window owner)
    {
    }

    public string DescribeLevel(Window window) => "level=?";

    public string? UseSrgbColorSpace(Window window) => null;
}

internal sealed class MacOsNativeWindowFrame : INativeWindowFrame
{
    private const string ObjC = "/usr/lib/libobjc.A.dylib";

    public string BackendName => "AppKit";

    public void BeginMove(Window window, PointerPressedEventArgs e) =>
        window.BeginMoveDrag(e);

    public void SetMousePassThrough(Window window, bool enabled)
    {
        IPlatformHandle? handle = window.TryGetPlatformHandle();
        if (handle is null
            || handle.Handle == IntPtr.Zero
            || handle.HandleDescriptor?.Contains(
                "NSWindow",
                StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        objc_msgSend_bool(
            handle.Handle,
            sel_registerName("setIgnoresMouseEvents:"),
            enabled);
    }

    public bool? IsMousePassThrough(Window window)
    {
        IPlatformHandle? handle = window.TryGetPlatformHandle();
        if (handle is null
            || handle.Handle == IntPtr.Zero
            || handle.HandleDescriptor?.Contains(
                "NSWindow",
                StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        return objc_msgSend_getBool(
            handle.Handle,
            sel_registerName("ignoresMouseEvents"));
    }

    /// <summary>
    /// NSRect / NSPoint come back in registers through objc_msgSend only on arm64 (on x86_64 they
    /// need objc_msgSend_stret), so Intel Macs keep the Avalonia path.
    /// </summary>
    public bool SupportsScreenFrames => RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

    public bool TryGetScreenFrame(Window window, out ScreenFrame frame)
    {
        frame = default;
        if (!SupportsScreenFrames || NsWindow(window) is not { } ns)
        {
            return false;
        }

        NSRect r = objc_msgSend_rect(ns, sel_registerName("frame"));
        frame = new ScreenFrame(r.X, r.Y, r.Width, r.Height);
        return r.Width > 0 && r.Height > 0;
    }

    public void SetScreenFrame(Window window, ScreenFrame frame)
    {
        if (!SupportsScreenFrames || NsWindow(window) is not { } ns)
        {
            return;
        }

        // Origin and size in one call, so no in-between frame is ever shown.
        objc_msgSend_setFrame(
            ns,
            sel_registerName("setFrame:display:"),
            new NSRect { X = frame.X, Y = frame.Y, Width = frame.Width, Height = frame.Height },
            true);
    }

    public bool TryGetMouseLocation(out double x, out double y)
    {
        x = y = 0;
        if (!SupportsScreenFrames)
        {
            return false;
        }

        NSPoint p = objc_msgSend_point(objc_getClass("NSEvent"), sel_registerName("mouseLocation"));
        x = p.X;
        y = p.Y;
        return true;
    }

    public double? VisibleTop(Window window)
    {
        if (!SupportsScreenFrames || NsWindow(window) is not { } ns)
        {
            return null;
        }

        nint screen = objc_msgSend_ptr(ns, sel_registerName("screen"));
        if (screen == 0)
        {
            return null;
        }

        NSRect visible = objc_msgSend_rect(screen, sel_registerName("visibleFrame"));
        return visible.Height > 0 ? visible.Y + visible.Height : null;
    }

    /// <summary>
    /// [window setColorSpace:[NSColorSpace sRGBColorSpace]]: Avalonia hands AppKit its pixels
    /// without a color space, so on a wide-gamut / HDR display (Basten's OLED, 17 Oct) they were
    /// shown as the display's own, much more saturated, colors.
    /// </summary>
    public string? UseSrgbColorSpace(Window window)
    {
        if (NsWindow(window) is not { } ns)
        {
            return null;
        }

        nint srgb = objc_msgSend_ptr(objc_getClass("NSColorSpace"), sel_registerName("sRGBColorSpace"));
        if (srgb == 0)
        {
            return null;
        }

        objc_msgSend_setPtr(ns, sel_registerName("setColorSpace:"), srgb);
        nint now = objc_msgSend_ptr(ns, sel_registerName("colorSpace"));
        nint name = now == 0 ? 0 : objc_msgSend_ptr(now, sel_registerName("localizedName"));
        nint utf8 = name == 0 ? 0 : objc_msgSend_ptr(name, sel_registerName("UTF8String"));
        return utf8 == 0 ? "?" : Marshal.PtrToStringUTF8(utf8);
    }

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_setPtr(nint receiver, nint selector, nint value);

    private static nint? NsWindow(Window window)
    {
        IPlatformHandle? handle = window.TryGetPlatformHandle();
        return handle is not null
            && handle.Handle != IntPtr.Zero
            && handle.HandleDescriptor?.Contains("NSWindow", StringComparison.OrdinalIgnoreCase) == true
                ? handle.Handle
                : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NSPoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NSRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;
    }

    /// <summary>NSFloatingWindowLevel, the level Avalonia gives a Topmost window.</summary>
    private const long FloatingLevel = 3;

    public void PlaceAbove(Window child, Window owner)
    {
        if (NsWindow(child) is not { } childWindow || NsWindow(owner) is not { } ownerWindow)
        {
            return;
        }

        long ownerLevel = (long)objc_msgSend_ptr(ownerWindow, sel_registerName("level"));
        objc_msgSend_long(childWindow, sel_registerName("setLevel:"), Math.Max(ownerLevel, FloatingLevel) + 1);
    }

    public string DescribeLevel(Window window)
    {
        if (NsWindow(window) is not { } ns)
        {
            return "level=?";
        }

        long level = (long)objc_msgSend_ptr(ns, sel_registerName("level"));
        ulong occlusion = (ulong)objc_msgSend_ptr(ns, sel_registerName("occlusionState"));
        bool visible = objc_msgSend_getBool(ns, sel_registerName("isVisible"));
        // NSWindowOcclusionStateVisible: some of the window is on screen, not covered.
        return $"level={level} visible={visible} onScreen={(occlusion & 2) != 0}";
    }

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_long(nint receiver, nint selector, long value);

    [DllImport(ObjC)]
    private static extern nint objc_getClass(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_ptr(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern NSRect objc_msgSend_rect(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern NSPoint objc_msgSend_point(nint receiver, nint selector);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_setFrame(
        nint receiver,
        nint selector,
        NSRect frame,
        [MarshalAs(UnmanagedType.I1)] bool display);

    [DllImport(ObjC)]
    private static extern nint sel_registerName(string name);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_bool(
        nint receiver,
        nint selector,
        [MarshalAs(UnmanagedType.I1)] bool value);

    [DllImport(ObjC, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool objc_msgSend_getBool(
        nint receiver,
        nint selector);
}

/// <summary>Maps Core hit-test edges to Avalonia resize cursors.</summary>
internal static class TimerWindowHitTest
{
    public const double ResizeHandleDip = BorderlessWindowGeometry.ResizeHandleDip;

    public static BorderlessResizeEdge? HitResizeEdge(double localX, double localY, double width, double height) =>
        BorderlessWindowGeometry.HitResizeEdge(localX, localY, width, height, ResizeHandleDip);

    public static StandardCursorType? CursorForEdge(BorderlessResizeEdge? edge) =>
        edge switch
        {
            BorderlessResizeEdge.North or BorderlessResizeEdge.South => StandardCursorType.SizeNorthSouth,
            BorderlessResizeEdge.East or BorderlessResizeEdge.West => StandardCursorType.SizeWestEast,
            BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.SouthEast => StandardCursorType.TopLeftCorner,
            BorderlessResizeEdge.NorthEast or BorderlessResizeEdge.SouthWest => StandardCursorType.TopRightCorner,
            _ => null,
        };
}
