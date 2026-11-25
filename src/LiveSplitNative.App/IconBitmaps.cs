using Avalonia.Media.Imaging;
using LiveSplitNative.Core.Imaging;

namespace LiveSplitNative.App;

/// <summary>Decodes the icon payloads stored in .lss files into Avalonia bitmaps.</summary>
internal static class IconBitmaps
{
    /// <param name="decodeSize">Unused; kept so existing call sites read the same. The bitmap keeps its stored size.</param>
    public static Bitmap? TryLoad(string? iconXml, int decodeSize = 48)
    {
        byte[]? bytes = IconBlobDecoder.TryDecodeImageBytes(iconXml);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
