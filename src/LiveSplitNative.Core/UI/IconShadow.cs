namespace LiveSplitNative.Core.UI;

/// <summary>
/// The blurred shadow Windows draws under split icons with Icon Shadows on (LiveSplit.Core
/// IconShadow.Generate, 93a11b85): the icon's alpha at 24 x 24, placed 3 px into a 30 x 30
/// image, blurred with a 7-tap Gaussian horizontally then vertically, in the shadow color.
/// </summary>
public static class IconShadow
{
    public const int SourceSize = 24;
    public const int Size = 30;

    private static readonly float[] Kernel = [0.398942f, 0.241971f, 0.053991f, 0.00443185f];

    /// <summary>
    /// <paramref name="iconAlpha"/>: the icon's alpha (0-255) at 24 x 24, row by row. Returns the
    /// 30 x 30 shadow as straight (not premultiplied) BGRA bytes.
    /// </summary>
    public static byte[] Generate(ReadOnlySpan<byte> iconAlpha, ArgbColor shadowColor)
    {
        if (iconAlpha.Length < SourceSize * SourceSize)
        {
            throw new ArgumentException("Need 24 x 24 alpha values.", nameof(iconAlpha));
        }

        const float shadowStrength = 0.8f;
        double alpha = 255f * (Math.Pow((shadowColor.A / 255f) - 1f, 3) + 1f);
        byte[] source = iconAlpha[..(SourceSize * SourceSize)].ToArray();
        var temp = new byte[Size * Size];
        var result = new byte[Size * Size * 4];

        float Get1(int x, int y)
        {
            x -= 3;
            y -= 3;
            return x < 0 || y < 0 || x >= SourceSize || y >= SourceSize ? 0f : source[(y * SourceSize) + x] / 255f;
        }

        float Get2(int x, int y) =>
            x < 0 || y < 0 || x >= Size || y >= Size ? 0f : temp[(y * Size) + x] / 255f;

        // Horizontal blur (the intermediate image keeps whole alpha values, as Bitmap.SetPixel).
        for (int x = 0; x < Size; ++x)
        {
            for (int y = 0; y < Size; ++y)
            {
                float sum = Kernel[0] * Get1(x, y);
                for (int i = 1; i < 4; ++i)
                {
                    sum += Kernel[i] * (Get1(x - i, y) + Get1(x + i, y));
                }

                temp[(y * Size) + x] = (byte)Math.Clamp((int)((alpha * sum) + 0.5f), 0, 255);
            }
        }

        // Vertical blur.
        for (int x = 0; x < Size; ++x)
        {
            for (int y = 0; y < Size; ++y)
            {
                float sum = Kernel[0] * Get2(x, y);
                for (int i = 1; i < 4; ++i)
                {
                    sum += Kernel[i] * (Get2(x, y - i) + Get2(x, y + i));
                }

                int offset = ((y * Size) + x) * 4;
                result[offset] = shadowColor.B;
                result[offset + 1] = shadowColor.G;
                result[offset + 2] = shadowColor.R;
                result[offset + 3] = (byte)Math.Clamp((int)((shadowStrength * alpha * sum) + 0.5f), 0, 255);
            }
        }

        return result;
    }
}
