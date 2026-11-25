using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class TimerPaintLogicTests
{
    [Fact]
    public void DigitGradientColors_Match_Windows_Hsv_Formula()
    {
        // Gray timer color (default 170,170,170): s=0, v≈0.667
        var original = ArgbColor.FromArgb(255, 170, 170, 170);
        var (top, bottom) = TimerPaintLogic.DigitGradientColors(original);

        ColorHsv.ToHsv(original, out _, out double s, out double v);
        Assert.Equal(0, s, precision: 5);
        var expectedBottom = ColorHsv.FromHsv(0, s, 0.8 * v);
        var expectedTop = ColorHsv.FromHsv(0, 0.5 * s, Math.Min(1, (1.5 * v) + 0.1));
        Assert.Equal(expectedTop, top);
        Assert.Equal(expectedBottom, bottom);
        // Top is lighter than bottom for gray.
        Assert.True(top.R > bottom.R);
    }

    [Fact]
    public void DigitGradientColors_Saturated_Green_Keeps_Hue_Family()
    {
        var green = ArgbColor.FromArgb(255, 0, 204, 75);
        var (top, bottom) = TimerPaintLogic.DigitGradientColors(green);
        // Both ends should stay in the green channel dominant family (not crude RGB*0.55).
        Assert.True(top.G >= top.R && top.G >= top.B);
        Assert.True(bottom.G >= bottom.R && bottom.G >= bottom.B);
        Assert.True(top.G > bottom.G || top.R + top.G + top.B > bottom.R + bottom.G + bottom.B);
    }

    [Theory]
    [InlineData("PlainWithDeltaColor", true)]
    [InlineData("Vertical With Delta Color", true)]
    [InlineData("HorizontalWithDeltaColor", true)]
    [InlineData("Plain", false)]
    [InlineData("Vertical", false)]
    public void UsesDeltaColor_Detects_Variants(string type, bool expected) =>
        Assert.Equal(expected, TimerPaintLogic.UsesDeltaColor(type));

    [Fact]
    public void ResolveBackgroundColors_PlainWithDelta_Uses_TimerColor_Tint()
    {
        var timer = ArgbColor.FromArgb(255, 0, 204, 75);
        var (c1, c2, plain) = TimerPaintLogic.ResolveBackgroundColors(
            "PlainWithDeltaColor",
            timer,
            ArgbColor.FromArgb(0, 0, 0, 0),
            ArgbColor.FromArgb(0, 0, 0, 0));

        Assert.True(plain);
        Assert.Equal(c1, c2);
        // A = timer.A * 7 / 12
        Assert.Equal((byte)(255 * 7 / 12), c1.A);
        // Tint is darkened/desaturated green family (v*0.25, s*0.5).
        Assert.True(c1.G > c1.R);
        Assert.True(c1.G > c1.B);
    }

    [Fact]
    public void ResolveBackgroundColors_VerticalWithDelta_Gradient_Ends()
    {
        var timer = ArgbColor.FromArgb(255, 204, 51, 51);
        var (c1, c2, plain) = TimerPaintLogic.ResolveBackgroundColors(
            "VerticalWithDeltaColor",
            timer,
            ArgbColor.FromArgb(0, 0, 0, 0),
            ArgbColor.FromArgb(0, 0, 0, 0));

        Assert.False(plain);
        Assert.Equal((byte)(255 / 6), c1.A);
        Assert.Equal(255, c2.A);
        Assert.Equal(c1.R, c2.R);
        Assert.Equal(c1.G, c2.G);
        Assert.Equal(c1.B, c2.B);
    }

    [Fact]
    public void ResolveBackgroundColors_Plain_Keeps_Settings_Colors()
    {
        var cSettings1 = ArgbColor.FromArgb(255, 10, 20, 30);
        var cSettings2 = ArgbColor.FromArgb(255, 40, 50, 60);
        var (c1, c2, plain) = TimerPaintLogic.ResolveBackgroundColors(
            "Plain",
            ArgbColor.FromArgb(255, 0, 255, 0),
            cSettings1,
            cSettings2);
        Assert.True(plain);
        Assert.Equal(cSettings1, c1);
        Assert.Equal(cSettings1, c2);
    }
}
