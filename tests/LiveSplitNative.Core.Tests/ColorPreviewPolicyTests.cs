using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class ColorPreviewPolicyTests
{
    [Fact]
    public void PermanentRule_Is_NonEmpty_Authority_Text()
    {
        Assert.Contains("segmented strip", ColorPreviewPolicy.PermanentRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must never be replaced", ColorPreviewPolicy.PermanentRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("continuous grayscale", ColorPreviewPolicy.PermanentRule, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("solid-color rectangle", ColorPreviewPolicy.PermanentRule, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Forbidden_Custom_Preview_Tags_Detected()
    {
        Assert.True(ColorPreviewPolicy.IsForbiddenCustomPreviewTag("GrayscalePreview"));
        Assert.True(ColorPreviewPolicy.IsForbiddenCustomPreviewTag("ShadePreview"));
        Assert.False(ColorPreviewPolicy.IsForbiddenCustomPreviewTag("ColorPreviewer"));
        Assert.False(ColorPreviewPolicy.IsForbiddenCustomPreviewTag(null));
    }

    [Fact]
    public void Rejected_Continuous_BlackWhite_Ramp_Detected()
    {
        Assert.True(ColorPreviewPolicy.IsRejectedContinuousBlackWhiteRamp(
        [
            (255, 255, 255),
            (128, 128, 128),
            (0, 0, 0),
        ]));
        Assert.False(ColorPreviewPolicy.IsRejectedContinuousBlackWhiteRamp(
        [
            (255, 255, 255),
            (0x29, 0xCC, 0x54),
            (0, 0, 0),
        ]));
        // Solid single color (even gray) is not a "ramp"
        Assert.False(ColorPreviewPolicy.IsRejectedContinuousBlackWhiteRamp(
        [
            (0x1A, 0x1A, 0x1A),
            (0x1A, 0x1A, 0x1A),
        ]));
    }

    [Fact]
    public void LivePreviewSession_Still_Ok_Cancel()
    {
        var original = ArgbColor.FromArgb(255, 0x1A, 0x3A, 0x6E);
        var session = new ColorPreviewShades.LivePreviewSession(original);
        session.Preview(ArgbColor.FromArgb(255, 0x29, 0xCC, 0x54));
        Assert.Equal(original, session.Finish(accept: false));
    }
}
