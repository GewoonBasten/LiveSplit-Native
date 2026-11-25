// LiveSplit.Core UI/IconShadow.cs @ 93a11b85 (MIT): the blurred shadow drawn under a split's icon.
// Images here carry no pixels yet (Graphics.DrawImage draws nothing), so the shadow is an empty
// image of Windows' size.
using System.Drawing;

namespace LiveSplit.UI;

public class IconShadow
{
    public static readonly float[] Kernel = [0.398942f, 0.241971f, 0.053991f, 0.00443185f];

    public static Bitmap Generate(Image image, Color shadowColor) => new(30, 30);
}
