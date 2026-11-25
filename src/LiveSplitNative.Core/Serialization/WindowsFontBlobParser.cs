using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace LiveSplitNative.Core.Serialization;

/// <summary>
/// Partial reader for classic LiveSplit <c>System.Drawing.Font</c> BinaryFormatter blobs
/// (Base64 / CDATA in .lsl). Full deserialize is unavailable on macOS; we extract Name,
/// Size (float), and Bold (FontStyle bit 0) from the known NRBF layout.
/// </summary>
public static class WindowsFontBlobParser
{
    public readonly record struct ParsedFont(string Family, float SizePx, bool Bold);

    public static bool TryParse(string? base64OrCdata, out ParsedFont font)
    {
        font = default;
        if (string.IsNullOrWhiteSpace(base64OrCdata))
        {
            return false;
        }

        string raw = base64OrCdata.Trim();
        if (raw.StartsWith("<![CDATA[", StringComparison.OrdinalIgnoreCase))
        {
            int start = raw.IndexOf('[', raw.IndexOf('[') + 1) + 1;
            int end = raw.LastIndexOf("]]>", StringComparison.Ordinal);
            if (start <= 0 || end <= start)
            {
                return false;
            }

            raw = raw[start..end];
        }

        byte[] data;
        try
        {
            data = Convert.FromBase64String(raw);
        }
        catch (FormatException)
        {
            return false;
        }

        return TryParseBytes(data, out font);
    }

    public static bool TryParseBytes(ReadOnlySpan<byte> data, out ParsedFont font)
    {
        font = default;
        if (data.Length < 40 || !LooksLikeFontBlob(data))
        {
            return false;
        }

        // Scan BinaryObjectString (0x06) records for the font family (not System.* metadata).
        for (int i = 0; i < data.Length - 6; i++)
        {
            if (data[i] != 0x06)
            {
                continue;
            }

            if (!TryReadBinaryObjectString(data, i, out string value, out int afterString))
            {
                continue;
            }

            if (value.Length is < 1 or > 64
                || value.StartsWith("System.", StringComparison.Ordinal)
                || value is "Name" or "Size" or "Style" or "Unit" or "value__"
                || !value.Any(char.IsLetter))
            {
                continue;
            }

            if (afterString + 4 > data.Length)
            {
                continue;
            }

            float size = BinaryPrimitives.ReadSingleLittleEndian(data[afterString..]);
            if (size is <= 0 or > 200 || float.IsNaN(size) || float.IsInfinity(size))
            {
                continue;
            }

            // FontStyle int follows first value__ + MemberPrimitiveUntyped marker 08 02 00 00 00.
            bool bold = false;
            ReadOnlySpan<byte> styleNeedle = "value__"u8;
            int styleSearch = afterString;
            int found = data[styleSearch..].IndexOf(styleNeedle);
            if (found >= 0)
            {
                int p = styleSearch + found;
                ReadOnlySpan<byte> marker = [0x08, 0x02, 0x00, 0x00, 0x00];
                int m = data[p..].IndexOf(marker);
                if (m >= 0 && p + m + 9 <= data.Length)
                {
                    int style = BinaryPrimitives.ReadInt32LittleEndian(data[(p + m + 5)..]);
                    bold = (style & 1) != 0; // FontStyle.Bold
                }
            }

            font = new ParsedFont(value, size, bold);
            return true;
        }

        return false;
    }

    private static bool LooksLikeFontBlob(ReadOnlySpan<byte> data)
    {
        // SerializationHeader + System.Drawing.Font type name appear in every LiveSplit font blob.
        string ascii = Encoding.ASCII.GetString(data);
        return ascii.Contains("System.Drawing.Font", StringComparison.Ordinal);
    }

    private static bool TryReadBinaryObjectString(
        ReadOnlySpan<byte> data,
        int offset,
        out string value,
        out int after)
    {
        value = "";
        after = offset;
        if (offset + 5 >= data.Length || data[offset] != 0x06)
        {
            return false;
        }

        int j = offset + 5; // skip record type + objectId
        if (!TryRead7BitEncodedInt(data, ref j, out int length)
            || length is <= 0 or > 128
            || j + length > data.Length)
        {
            return false;
        }

        value = Encoding.UTF8.GetString(data.Slice(j, length));
        after = j + length;
        return true;
    }

    private static bool TryRead7BitEncodedInt(ReadOnlySpan<byte> data, ref int index, out int value)
    {
        value = 0;
        int shift = 0;
        while (index < data.Length && shift < 35)
        {
            byte b = data[index++];
            value |= (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return true;
            }

            shift += 7;
        }

        return false;
    }

    /// <summary>Debug helper for tests.</summary>
    public static string Describe(ParsedFont font) =>
        $"{font.Family} {font.SizePx.ToString(CultureInfo.InvariantCulture)}{(font.Bold ? " Bold" : "")}";
}
