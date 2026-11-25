using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace LiveSplitNative.App;

/// <summary>
/// Windows LiveSplit draws icons in a fixed square (IconSize×IconSize).
/// Wrap <see cref="Image"/> so layout is always square; UniformToFill covers the box
/// (uniform scale, preserve aspect — no non-uniform stretch; Border clips overflow).
/// </summary>
internal static class SquareIconSlot
{
    public static Border Create(double size, Thickness? margin = null)
    {
        var image = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = size,
            Height = size,
        };
        return new Border
        {
            Width = size,
            Height = size,
            Margin = margin ?? default,
            ClipToBounds = true,
            Background = Brushes.Transparent,
            Child = image,
            Tag = image,
        };
    }

    public static Image ImageOf(Border slot) =>
        slot.Tag as Image
        ?? slot.Child as Image
        ?? throw new InvalidOperationException("Square icon slot missing Image child.");

    public static void SetSource(Border slot, Bitmap? bitmap, bool hideWhenEmpty = true)
    {
        Image image = ImageOf(slot);
        image.Source = bitmap;
        // Split rows keep the square box even when empty (Windows icon column).
        slot.IsVisible = !hideWhenEmpty || bitmap is not null;
    }

    public static Control? TryCreateFromIconXml(string? iconXml, double size, Thickness? margin = null)
    {
        byte[]? bytes = LiveSplitNative.Core.Imaging.IconBlobDecoder.TryDecodeImageBytes(iconXml);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var ms = new MemoryStream(bytes);
            Border slot = Create(size, margin);
            ImageOf(slot).Source = new Bitmap(ms);
            return slot;
        }
        catch
        {
            return null;
        }
    }
}
