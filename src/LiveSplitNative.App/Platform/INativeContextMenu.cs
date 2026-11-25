using Avalonia;
using Avalonia.Controls;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// OS chrome context menu. macOS → real AppKit <c>NSMenu</c>; Linux later → GTK/Qt/XDG;
/// other platforms → Avalonia <see cref="ContextMenu"/> fallback (not claimed as native).
/// </summary>
public interface INativeContextMenu
{
    /// <summary>True when backed by a real OS menu (NSMenu today), not Avalonia chrome.</summary>
    bool IsSystemNative { get; }

    /// <summary>Diagnostic name, e.g. <c>AppKit.NSMenu</c> or <c>Avalonia.ContextMenu</c>.</summary>
    string BackendName { get; }

    /// <summary>
    /// Show the menu at a screen point (Avalonia top-left pixel coords).
    /// Blocks on macOS until the NSMenu tracking loop ends, so call it only when no mouse button
    /// is down and not from inside an input handler; the chosen item runs after it returns.
    /// </summary>
    void Show(Window owner, PixelPoint screenPoint, NativeMenuModel model);
}

public static class NativeContextMenuFactory
{
    private static INativeContextMenu? _cached;

    public static INativeContextMenu Current =>
        _cached ??= Create();

    public static INativeContextMenu Create()
    {
        if (OperatingSystem.IsMacOS() && MacOsNativeContextMenu.IsAvailable)
        {
            return new MacOsNativeContextMenu();
        }

        return new AvaloniaFallbackContextMenu();
    }
}
