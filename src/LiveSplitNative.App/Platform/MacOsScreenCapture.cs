using System.Runtime.InteropServices;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// In-process desktop / window capture via CoreGraphics. Needed because agent-side
/// <c>screencapture</c> / external Quartz often cannot see this process's windows (TCC).
/// </summary>
internal static class MacOsScreenCapture
{
    public static bool TryCaptureOnScreenPng(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        try
        {
            // Full on-screen composite including this app's NSMenu windows.
            nint image = CGWindowListCreateImage(
                CGRect.Null,
                kCGWindowListOptionOnScreenOnly,
                kCGNullWindowID,
                kCGWindowImageBoundsIgnoreFraming | kCGWindowImageShouldBeOpaque);

            if (image == 0)
            {
                return false;
            }

            try
            {
                return SavePng(image, path);
            }
            finally
            {
                CGImageRelease(image);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("MacOsScreenCapture failed: " + ex.Message);
            return false;
        }
    }

    private static bool SavePng(nint image, string path)
    {
        nint url = CFURLFromPath(path);
        if (url == 0)
        {
            return false;
        }

        try
        {
            nint type = CFString("public.png");
            nint dest = CGImageDestinationCreateWithURL(url, type, 1, 0);
            CFRelease(type);
            if (dest == 0)
            {
                return false;
            }

            try
            {
                CGImageDestinationAddImage(dest, image, 0);
                return CGImageDestinationFinalize(dest);
            }
            finally
            {
                CFRelease(dest);
            }
        }
        finally
        {
            CFRelease(url);
        }
    }

    private static nint CFString(string value)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value + "\0");
        nint ptr = Marshal.AllocHGlobal(utf8.Length);
        try
        {
            Marshal.Copy(utf8, 0, ptr, utf8.Length);
            // kCFStringEncodingUTF8 = 0x08000100
            return CFStringCreateWithCString(0, ptr, 0x08000100);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static nint CFURLFromPath(string path)
    {
        nint cfPath = CFString(path);
        try
        {
            // isDirectory = false
            return CFURLCreateWithFileSystemPath(0, cfPath, kCFURLPOSIXPathStyle, false);
        }
        finally
        {
            CFRelease(cfPath);
        }
    }

    private const uint kCGWindowListOptionOnScreenOnly = 1 << 0;
    private const uint kCGWindowImageBoundsIgnoreFraming = 1 << 0;
    private const uint kCGWindowImageShouldBeOpaque = 1 << 1;
    private const uint kCGNullWindowID = 0;
    private const uint kCFURLPOSIXPathStyle = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct CGRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public static CGRect Null => new() { X = double.PositiveInfinity, Y = double.PositiveInfinity, Width = 0, Height = 0 };
    }

    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string ImageIO = "/System/Library/Frameworks/ImageIO.framework/ImageIO";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [DllImport(CoreGraphics)]
    private static extern nint CGWindowListCreateImage(CGRect screenBounds, uint windowOption, uint windowID, uint imageOption);

    [DllImport(CoreGraphics)]
    private static extern void CGImageRelease(nint image);

    [DllImport(ImageIO)]
    private static extern nint CGImageDestinationCreateWithURL(nint url, nint type, nint count, nint options);

    [DllImport(ImageIO)]
    private static extern void CGImageDestinationAddImage(nint dest, nint image, nint properties);

    [DllImport(ImageIO)]
    private static extern bool CGImageDestinationFinalize(nint dest);

    [DllImport(CoreFoundation)]
    private static extern nint CFStringCreateWithCString(nint alloc, nint cStr, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern nint CFURLCreateWithFileSystemPath(nint allocator, nint filePath, uint pathStyle, bool isDirectory);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(nint cf);
}
