namespace LiveSplitNative.Core.UI;

/// <summary>
/// Bottom color-picker preview: shade ramp of the <em>selected</em> color
/// (white → tints → selected → darker → black), matching the classic WinForms
/// "Shades:" strip — not a pure B&amp;W gray ramp, not a solid fill, not a Basic-colors grid.
/// </summary>
public static class ColorPreviewShades
{
    public const int DefaultStopCount = 11;

    public readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>
    /// Shade stops for <paramref name="selected"/> using the same HSV mix as the
    /// former Layout Editor <c>RebuildShades</c> strip (commit e05d145).
    /// </summary>
    public static IReadOnlyList<Rgb> ShadeStops(ArgbColor selected, int stopCount = DefaultStopCount)
    {
        int count = Math.Clamp(stopCount, 3, 64);
        ColorHsv.ToHsv(selected, out double h, out double s, out double _);
        byte alpha = selected.A == 0 ? (byte)255 : selected.A;
        var stops = new Rgb[count];
        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? 0 : (double)i / (count - 1);
            ArgbColor shade;
            if (t <= 0.5)
            {
                double u = t / 0.5;
                shade = ColorHsv.FromHsv(h, s * u, 1.0, alpha);
            }
            else
            {
                double u = (t - 0.5) / 0.5;
                shade = ColorHsv.FromHsv(h, s, 1.0 - u, alpha);
            }

            stops[i] = new Rgb(shade.R, shade.G, shade.B);
        }

        return stops;
    }

    /// <summary>True when every stop is achromatic (R=G=B) — the rejected B&amp;W ramp.</summary>
    public static bool IsPureBlackWhiteRamp(IReadOnlyList<Rgb> stops)
    {
        if (stops.Count < 2)
        {
            return false;
        }

        foreach (Rgb stop in stops)
        {
            if (stop.R != stop.G || stop.G != stop.B)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when the ramp has multiple distinct colors (not a solid fill).</summary>
    public static bool IsMultiStopRamp(IReadOnlyList<Rgb> stops)
    {
        if (stops.Count < 3)
        {
            return false;
        }

        return stops.Distinct().Count() >= 3;
    }

    /// <summary>
    /// Two selected hues must produce different mid-ramp colors (color-dependent shades).
    /// </summary>
    public static bool MidStopsDependOnSelectedColor(ArgbColor a, ArgbColor b)
    {
        IReadOnlyList<Rgb> sa = ShadeStops(a);
        IReadOnlyList<Rgb> sb = ShadeStops(b);
        int mid = sa.Count / 2;
        return sa[mid] != sb[mid];
    }

    /// <summary>Session helper for live preview + OK commit / Cancel restore (no disk I/O).</summary>
    public sealed class LivePreviewSession
    {
        public LivePreviewSession(ArgbColor original)
        {
            Original = original;
            Current = original;
        }

        public ArgbColor Original { get; }
        public ArgbColor Current { get; private set; }
        public bool Accepted { get; private set; }

        public void Preview(ArgbColor color) => Current = color;

        public ArgbColor Finish(bool accept)
        {
            Accepted = accept;
            return accept ? Current : Original;
        }
    }
}
