namespace LiveSplitNative.Core.Imaging;

/// <summary>
/// Decodes game/segment icon payloads from .lss Icon/GameIcon elements.
/// Supports portable raw PNG/JPEG/GIF/BMP base64 and extracts complete image payloads
/// from classic LiveSplit BinaryFormatter Image blobs (trim to IEND / JPEG EOI / validated BMP).
/// Full NRBF Image deserialize is not available on modern .NET — undecodable BF stays opaque.
/// </summary>
public static class IconBlobDecoder
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Gif87 = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'7', (byte)'a'];
    private static readonly byte[] Gif89 = [(byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a'];
    private static readonly byte[] Bmp = [(byte)'B', (byte)'M'];
    private static readonly byte[] PngIend = [(byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xAE, 0x42, 0x60, 0x82];
    private static readonly byte[] JpegEoi = [0xFF, 0xD9];

    /// <summary>Returns image bytes (PNG/JPEG/GIF/BMP) or null if undecodable.</summary>
    public static byte[]? TryDecodeImageBytes(string? iconXmlOrBase64)
    {
        if (string.IsNullOrWhiteSpace(iconXmlOrBase64))
        {
            return null;
        }

        string text = NormalizeBase64Payload(iconXmlOrBase64);
        byte[] raw;
        try
        {
            raw = Convert.FromBase64String(text);
        }
        catch (FormatException)
        {
            return null;
        }

        if (LooksLikeImage(raw))
        {
            return TrimCompleteImage(raw, 0) ?? raw;
        }

        // Prefer PNG (common in LiveSplit BF Bitmap blobs — e.g. Celeste sample @ offset 161).
        int png = IndexOf(raw, Png);
        if (png >= 0)
        {
            byte[]? trimmed = TrimCompleteImage(raw, png);
            if (trimmed is not null)
            {
                return trimmed;
            }
        }

        int jpeg = IndexOf(raw, Jpeg);
        if (jpeg >= 0)
        {
            byte[]? trimmed = TrimCompleteImage(raw, jpeg);
            if (trimmed is not null)
            {
                return trimmed;
            }
        }

        int gif = IndexOf(raw, Gif89);
        if (gif < 0)
        {
            gif = IndexOf(raw, Gif87);
        }

        if (gif >= 0)
        {
            byte[]? trimmed = TrimCompleteImage(raw, gif);
            if (trimmed is not null)
            {
                return trimmed;
            }
        }

        // BMP last — PNG payloads often contain incidental "BM" bytes.
        int bmp = IndexOf(raw, Bmp);
        while (bmp >= 0)
        {
            byte[]? trimmed = TrimCompleteImage(raw, bmp);
            if (trimmed is not null)
            {
                return trimmed;
            }

            bmp = IndexOf(raw, Bmp, bmp + 1);
        }

        return null;
    }

    /// <summary>Encode PNG/JPEG/etc. bytes as portable CDATA base64 for .lss Icon elements.</summary>
    public static string ToPortableIconXml(byte[] imageBytes)
    {
        string b64 = Convert.ToBase64String(imageBytes);
        return $"<![CDATA[{b64}]]>";
    }

    public static bool LooksLikeImage(ReadOnlySpan<byte> data) =>
        StartsWith(data, Png)
        || StartsWith(data, Jpeg)
        || StartsWith(data, Gif87)
        || StartsWith(data, Gif89)
        || StartsWith(data, Bmp);

    private static string NormalizeBase64Payload(string iconXmlOrBase64)
    {
        string text = iconXmlOrBase64.Trim();
        if (text.StartsWith("<![CDATA[", StringComparison.Ordinal))
        {
            int end = text.IndexOf("]]>", StringComparison.Ordinal);
            text = end > 9 ? text[9..end] : text[9..];
        }

        if (text.Contains('<', StringComparison.Ordinal))
        {
            int cdata = text.IndexOf("<![CDATA[", StringComparison.Ordinal);
            if (cdata >= 0)
            {
                int cdataEnd = text.IndexOf("]]>", cdata, StringComparison.Ordinal);
                text = cdataEnd > cdata ? text[(cdata + 9)..cdataEnd] : text;
            }
            else
            {
                int start = text.IndexOf('>') + 1;
                if (start > 0 && start < text.Length)
                {
                    int close = text.LastIndexOf('<');
                    text = close > start ? text[start..close] : text[start..];
                }
            }
        }

        // LiveSplit CDATA may include whitespace/newlines inside base64.
        return string.Concat(text.Where(static c => !char.IsWhiteSpace(c)));
    }

    private static byte[]? TrimCompleteImage(byte[] raw, int start)
    {
        if (start < 0 || start >= raw.Length)
        {
            return null;
        }

        ReadOnlySpan<byte> slice = raw.AsSpan(start);
        if (StartsWith(slice, Png))
        {
            int iend = IndexOf(raw, PngIend, start);
            if (iend < 0)
            {
                return null;
            }

            // IEND marker + 4-byte CRC already included in PngIend pattern length… pattern includes CRC.
            int end = iend + PngIend.Length;
            return raw[start..end];
        }

        if (StartsWith(slice, Jpeg))
        {
            int eoi = IndexOf(raw, JpegEoi, start + 2);
            if (eoi < 0)
            {
                return null;
            }

            return raw[start..(eoi + 2)];
        }

        if (StartsWith(slice, Gif87) || StartsWith(slice, Gif89))
        {
            int trailer = IndexOf(raw, [0x3B], start + 6);
            if (trailer < 0)
            {
                return null;
            }

            return raw[start..(trailer + 1)];
        }

        if (StartsWith(slice, Bmp) && slice.Length >= 14)
        {
            int size = BitConverter.ToInt32(raw, start + 2);
            if (size < 14 || size > raw.Length - start || size > 16 * 1024 * 1024)
            {
                return null;
            }

            return raw[start..(start + size)];
        }

        return null;
    }

    private static bool StartsWith(ReadOnlySpan<byte> data, byte[] magic)
    {
        if (data.Length < magic.Length)
        {
            return false;
        }

        for (int i = 0; i < magic.Length; i++)
        {
            if (data[i] != magic[i])
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start = 0)
    {
        for (int i = Math.Max(0, start); i <= haystack.Length - needle.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                return i;
            }
        }

        return -1;
    }
}
