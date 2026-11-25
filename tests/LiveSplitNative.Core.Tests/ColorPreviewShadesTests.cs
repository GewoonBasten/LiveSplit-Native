using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class ColorPreviewShadesTests
{
    [Fact]
    public void ShadeStops_Are_MultiStop_And_Not_Pure_BlackWhite_For_Chromatic_Color()
    {
        var green = ArgbColor.FromArgb(255, 0x29, 0xCC, 0x54);
        IReadOnlyList<ColorPreviewShades.Rgb> stops = ColorPreviewShades.ShadeStops(green);
        Assert.True(stops.Count >= 3);
        Assert.True(ColorPreviewShades.IsMultiStopRamp(stops));
        Assert.False(ColorPreviewShades.IsPureBlackWhiteRamp(stops));
        // Ends: white-ish then black
        Assert.Equal(new ColorPreviewShades.Rgb(255, 255, 255), stops[0]);
        Assert.Equal(new ColorPreviewShades.Rgb(0, 0, 0), stops[^1]);
    }

    [Fact]
    public void ShadeStops_Depend_On_Selected_Hue()
    {
        var green = ArgbColor.FromArgb(255, 0x29, 0xCC, 0x54);
        var navy = ArgbColor.FromArgb(255, 0x1A, 0x3A, 0x6E);
        Assert.True(ColorPreviewShades.MidStopsDependOnSelectedColor(green, navy));

        IReadOnlyList<ColorPreviewShades.Rgb> gStops = ColorPreviewShades.ShadeStops(green);
        IReadOnlyList<ColorPreviewShades.Rgb> nStops = ColorPreviewShades.ShadeStops(navy);
        int mid = gStops.Count / 2;
        Assert.NotEqual(gStops[mid], nStops[mid]);
    }

    [Fact]
    public void PureGraySelected_May_Be_Achromatic_But_Still_MultiStop()
    {
        var gray = ArgbColor.FromArgb(255, 128, 128, 128);
        IReadOnlyList<ColorPreviewShades.Rgb> stops = ColorPreviewShades.ShadeStops(gray);
        Assert.True(ColorPreviewShades.IsMultiStopRamp(stops));
        // Gray selection legitimately yields an achromatic ramp — not the bug case.
        Assert.True(ColorPreviewShades.IsPureBlackWhiteRamp(stops));
    }

    [Fact]
    public void Rejected_BwRamp_Helper_Detects_Equal_Rgb_Stops()
    {
        ColorPreviewShades.Rgb[] bw =
        [
            new(255, 255, 255),
            new(128, 128, 128),
            new(0, 0, 0),
        ];
        Assert.True(ColorPreviewShades.IsPureBlackWhiteRamp(bw));
        Assert.False(ColorPreviewShades.IsPureBlackWhiteRamp(
        [
            new(255, 255, 255),
            new(0x29, 0xCC, 0x54),
            new(0, 0, 0),
        ]));
    }

    [Fact]
    public void LivePreviewSession_Cancel_Restores_Original()
    {
        var original = ArgbColor.FromArgb(255, 0x1A, 0x3A, 0x6E);
        var session = new ColorPreviewShades.LivePreviewSession(original);
        session.Preview(ArgbColor.FromArgb(255, 0x00, 0xFF, 0x00));
        ArgbColor result = session.Finish(accept: false);
        Assert.Equal(original, result);
        Assert.False(session.Accepted);
    }

    [Fact]
    public void LivePreviewSession_Ok_Commits_Current()
    {
        var original = ArgbColor.FromArgb(255, 0x1A, 0x3A, 0x6E);
        var picked = ArgbColor.FromArgb(255, 0x29, 0xCC, 0x54);
        var session = new ColorPreviewShades.LivePreviewSession(original);
        session.Preview(picked);
        ArgbColor result = session.Finish(accept: true);
        Assert.Equal(picked, result);
        Assert.True(session.Accepted);
    }
}
