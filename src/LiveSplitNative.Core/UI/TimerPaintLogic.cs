namespace LiveSplitNative.Core.UI;

/// <summary>
/// Port of LiveSplit.Timer Timer.DrawBackground + digit ShowGradient HSV dual-brush math.
/// </summary>
public static class TimerPaintLogic
{
    /// <summary>
    /// Resolves background fill colors for Plain / Vertical / Horizontal / *WithDeltaColor.
    /// Mirrors <c>Timer.DrawBackground</c> (timerColor → HSV darkened tint when delta variants).
    /// </summary>
    public static (ArgbColor Color1, ArgbColor Color2, bool IsPlain) ResolveBackgroundColors(
        string? gradientType,
        ArgbColor timerColor,
        ArgbColor settingsColor1,
        ArgbColor settingsColor2)
    {
        string normalized = NormalizeGradientType(gradientType);
        ArgbColor background1 = settingsColor1;
        ArgbColor background2 = settingsColor2;

        bool withDelta = normalized is "PlainWithDeltaColor"
            or "HorizontalWithDeltaColor"
            or "VerticalWithDeltaColor";

        if (withDelta)
        {
            ColorHsv.ToHsv(timerColor, out double h, out double s, out double v);
            ArgbColor newColor = ColorHsv.FromHsv(h, s * 0.5, v * 0.25, alpha: 255);

            if (normalized == "PlainWithDeltaColor")
            {
                background1 = ArgbColor.FromArgb((byte)(timerColor.A * 7 / 12), newColor.R, newColor.G, newColor.B);
                background2 = background1;
            }
            else
            {
                background1 = ArgbColor.FromArgb((byte)(timerColor.A / 6), newColor.R, newColor.G, newColor.B);
                background2 = ArgbColor.FromArgb(timerColor.A, newColor.R, newColor.G, newColor.B);
            }
        }

        bool plain = normalized is "Plain" or "PlainWithDeltaColor";
        return (background1, plain ? background1 : background2, plain);
    }

    public static bool IsHorizontalGradient(string? gradientType)
    {
        string n = NormalizeGradientType(gradientType);
        return n is "Horizontal" or "HorizontalWithDeltaColor";
    }

    public static bool UsesDeltaColor(string? gradientType)
    {
        string n = NormalizeGradientType(gradientType);
        return n is "PlainWithDeltaColor" or "VerticalWithDeltaColor" or "HorizontalWithDeltaColor";
    }

    /// <summary>
    /// Windows Timer.cs ShowGradient: top = HSV(h, 0.5s, min(1, 1.5v+0.1)), bottom = HSV(h, s, 0.8v).
    /// </summary>
    public static (ArgbColor Top, ArgbColor Bottom) DigitGradientColors(ArgbColor original)
    {
        ColorHsv.ToHsv(original, out double h, out double s, out double v);
        ArgbColor bottom = ColorHsv.FromHsv(h, s, 0.8 * v, original.A);
        ArgbColor top = ColorHsv.FromHsv(h, 0.5 * s, Math.Min(1, (1.5 * v) + 0.1), original.A);
        return (top, bottom);
    }

    public static bool ShouldPaintBackground(ArgbColor color1, ArgbColor color2, string? gradientType)
    {
        string n = NormalizeGradientType(gradientType);
        return color1.A > 0 || (n is not "Plain" and not "PlainWithDeltaColor" && color2.A > 0)
            || UsesDeltaColor(gradientType);
    }

    public static string NormalizeGradientType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "Plain";
        }

        return type.Replace(" ", "", StringComparison.Ordinal);
    }
}
