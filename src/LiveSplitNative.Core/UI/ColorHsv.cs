namespace LiveSplitNative.Core.UI;

/// <summary>
/// Port of LiveSplit.UI.ColorExtensions ToHSV / FromHSV (System.Drawing.Color.GetHue semantics).
/// </summary>
public static class ColorHsv
{
    public static void ToHsv(ArgbColor color, out double hue, out double saturation, out double value)
    {
        int max = Math.Max(color.R, Math.Max(color.G, color.B));
        int min = Math.Min(color.R, Math.Min(color.G, color.B));

        hue = GetHue(color);
        saturation = max == 0 ? 0 : 1d - (1d * min / max);
        value = max / 255d;
    }

    public static ArgbColor FromHsv(double hue, double saturation, double value, byte alpha = 255)
    {
        int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
        double f = (hue / 60) - Math.Floor(hue / 60);

        value *= 255;
        int v = Convert.ToInt32(value);
        int p = Convert.ToInt32(value * (1 - saturation));
        int q = Convert.ToInt32(value * (1 - (f * saturation)));
        int t = Convert.ToInt32(value * (1 - ((1 - f) * saturation)));

        return hi switch
        {
            0 => ArgbColor.FromArgb(alpha, ClampByte(v), ClampByte(t), ClampByte(p)),
            1 => ArgbColor.FromArgb(alpha, ClampByte(q), ClampByte(v), ClampByte(p)),
            2 => ArgbColor.FromArgb(alpha, ClampByte(p), ClampByte(v), ClampByte(t)),
            3 => ArgbColor.FromArgb(alpha, ClampByte(p), ClampByte(q), ClampByte(v)),
            4 => ArgbColor.FromArgb(alpha, ClampByte(t), ClampByte(p), ClampByte(v)),
            _ => ArgbColor.FromArgb(alpha, ClampByte(v), ClampByte(p), ClampByte(q)),
        };
    }

    /// <summary>Matches System.Drawing.Color.GetHue (degrees 0–360).</summary>
    public static double GetHue(ArgbColor color)
    {
        if (color.R == color.G && color.G == color.B)
        {
            return 0;
        }

        double r = color.R / 255d;
        double g = color.G / 255d;
        double b = color.B / 255d;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double hue;
        if (Math.Abs(r - max) < double.Epsilon)
        {
            hue = (g - b) / delta;
        }
        else if (Math.Abs(g - max) < double.Epsilon)
        {
            hue = 2 + ((b - r) / delta);
        }
        else
        {
            hue = 4 + ((r - g) / delta);
        }

        hue *= 60;
        if (hue < 0)
        {
            hue += 360;
        }

        return hue;
    }

    private static byte ClampByte(int v) => (byte)Math.Clamp(v, 0, 255);
}
