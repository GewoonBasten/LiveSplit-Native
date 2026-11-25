using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>IconShadow.Generate (LiveSplit.Core, 93a11b85).</summary>
public class IconShadowTests
{
    [Fact]
    public void An_Opaque_Icon_Gives_A_Soft_Shadow_In_The_Shadow_Color()
    {
        byte[] alpha = Enumerable.Repeat((byte)255, IconShadow.SourceSize * IconShadow.SourceSize).ToArray();
        ArgbColor color = ArgbColor.FromArgb(128, 0, 0, 0);
        byte[] shadow = IconShadow.Generate(alpha, color);
        Assert.Equal(IconShadow.Size * IconShadow.Size * 4, shadow.Length);
        byte Alpha(int x, int y) => shadow[(((y * IconShadow.Size) + x) * 4) + 3];
        // Strongest in the middle, fading to nothing at the corners, symmetric.
        Assert.True(Alpha(15, 15) > Alpha(3, 15));
        Assert.Equal(0, Alpha(0, 0));
        Assert.Equal(Alpha(10, 12), Alpha(19, 12));
        // Middle: upstream applies the shadow alpha (255 x ((128/255 - 1)^3 + 1) = 223.5) in both
        // passes: horizontal 223, vertical 0.8 x 223.5 x 223/255 = 156.
        Assert.Equal(156, Alpha(15, 15));
    }
}
