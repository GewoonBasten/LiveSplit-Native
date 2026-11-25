using System.Globalization;

namespace LiveSplitNative.Core.UI;

public readonly record struct ArgbColor(byte A, byte R, byte G, byte B)
{
    public static ArgbColor FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);

    public static ArgbColor FromTuple((byte A, byte R, byte G, byte B) c) => new(c.A, c.R, c.G, c.B);

    public static ArgbColor ParseHex(string? text, ArgbColor fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        text = text.Trim();
        if (text.StartsWith('#') || text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text.StartsWith('#') ? text[1..] : text[2..];
        }

        if (text.Length == 6
            && uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            return new ArgbColor(255, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
        }

        if (text.Length == 8
            && uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
        {
            return new ArgbColor(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }

        return fallback;
    }

    public string ToHex() => $"{A:X2}{R:X2}{G:X2}{B:X2}";

    public (byte A, byte R, byte G, byte B) ToTuple() => (A, R, G, B);
}
