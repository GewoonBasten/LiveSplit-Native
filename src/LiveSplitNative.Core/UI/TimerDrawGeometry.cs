namespace LiveSplitNative.Core.UI;

/// <summary>
/// Where and how big the Timer component draws its digits (LiveSplit.Timer Timer.DrawGeneral /
/// DrawUnscaled, 93a11b85). The Detailed Timer draws its main timer and its segment timer with
/// this same code, each in its own band.
/// <list type="bullet">
/// <item>The digits are laid out once, unscaled, in a box 45 high and as wide as "88:88:88" in
/// the timer font plus the decimals plus 11: the widest time the timer can show, so the scale does
/// not change while the time grows.</item>
/// <item>That box is scaled uniformly by min(height / 45, (width - 14) / (box width - 14)): the
/// height decides until the box would not fit the width.</item>
/// <item>The decimals font is TimerFont.Size / 50 x Decimals Font Size (50 = as big as the
/// digits). They are part of the box width, so bigger decimals can make the whole timer smaller
/// in a narrow component, but never change its height.</item>
/// <item>Not centered, the text ends at width - 7 + scale (the box's right edge at width - 7,
/// the text 6 units in from it); the font cell (GDI ascent + descent) is centered vertically
/// in the band.</item>
/// </list>
/// </summary>
public static class TimerDrawGeometry
{
    /// <summary>The unscaled box height (Timer.DrawGeneral unscaledHeight).</summary>
    public const double UnscaledHeight = 45;

    /// <summary>Decimals Font Size that makes the decimals as big as the digits.</summary>
    public const double FullDecimalsSize = 50;

    /// <summary>TimerDecimalPlacesFont: TimerFont.Size / 50 x Decimals Font Size.</summary>
    public static double DecimalsFontSize(double timerFontSize, double decimalsSize) =>
        timerFontSize / FullDecimalsSize * decimalsSize;

    /// <summary>unscaledWidth = max(10, "88:88:88" width + decimals width + 11).</summary>
    public static double UnscaledWidth(double measureWidth, double decimalsWidth) =>
        Math.Max(10, measureWidth + decimalsWidth + 11);

    /// <summary>scale = min((width - 14) / (unscaledWidth - 14), height / 45).</summary>
    public static double Scale(double width, double height, double unscaledWidth)
    {
        double widthFactor = (width - 14) / (unscaledWidth - 14);
        double heightFactor = height / UnscaledHeight;
        // Windows would draw mirrored below 14 px wide; the component minimum is 20.
        return Math.Max(0.01, Math.Min(widthFactor, heightFactor));
    }

    /// <summary>
    /// The digits' baseline in a band: DrawUnscaled centers the font cell (GDI cell ascent +
    /// descent) in the 45-high box, so the baseline sits (ascent - descent) / 2 em below the
    /// band's middle. <paramref name="emSize"/> is the scaled font size.
    /// </summary>
    public static double BaselineY(double bandTop, double bandHeight, double emSize, double cellAscentPerEm, double cellDescentPerEm) =>
        bandTop + (bandHeight / 2) + ((cellAscentPerEm - cellDescentPerEm) / 2 * emSize);

    /// <summary>
    /// The x where the drawn text ends, from the component's left edge. Not centered: the
    /// transform puts the box's right edge at width - 7 and the text ends 6 units inside it.
    /// Centered: the whole box (with its "88:88:88" room) is centered.
    /// </summary>
    public static double TextRight(double width, double unscaledWidth, double scale, bool centerTimer) =>
        centerTimer
            ? ((width + (unscaledWidth * scale)) / 2) - (6 * scale)
            : width - 7 + scale;
}
