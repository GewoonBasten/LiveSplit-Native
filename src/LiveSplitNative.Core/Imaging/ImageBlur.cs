namespace LiveSplitNative.Core.Imaging;

/// <summary>
/// Port of LiveSplit.UI.ImageBlur (93a11b85): the layout background's blur. The image is first
/// scaled down by 1.5 x sigma + 1, then blurred with a Gaussian kernel in two passes (edge
/// pixels repeat). The caller scales (<see cref="ScaledSize"/>) and hands over the pixels.
/// </summary>
public static class ImageBlur
{
    public static double ScaleFactor(double sigma) => (1.5 * sigma) + 1;

    public static int KernelWidth(double sigma) => (int)Math.Round(3 * sigma / ScaleFactor(sigma));

    /// <summary>The size the image is scaled to before blurring; null when the blur does nothing (Generate returns the image).</summary>
    public static (int Width, int Height)? ScaledSize(int width, int height, double sigma)
    {
        if (KernelWidth(sigma) == 0)
        {
            return null;
        }

        double scale = ScaleFactor(sigma);
        return (Math.Max(1, (int)Math.Round(width / scale)), Math.Max(1, (int)Math.Round(height / scale)));
    }

    /// <summary>Blurs straight-alpha BGRA pixels (already at <see cref="ScaledSize"/>) in place.</summary>
    public static void Blur(byte[] bgra, int width, int height, double sigma)
    {
        int kernelWidth = KernelWidth(sigma);
        if (kernelWidth == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        double scaleFactor = ScaleFactor(sigma);
        double[] kernel = new double[kernelWidth];
        for (int i = 0; i < kernelWidth; i++)
        {
            double x = i * scaleFactor;
            kernel[i] = Math.Exp(-x * x / (2 * sigma * sigma)) / (sigma * Math.Sqrt(2 * Math.PI));
        }

        double overallWeight = kernel[0];
        for (int i = 1; i < kernelWidth; i++)
        {
            overallWeight += 2 * kernel[i];
        }

        // BlurColor keeps R, G, B, A as doubles; the passes are horizontal, then vertical.
        double[] source = new double[width * height * 4];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = bgra[i];
        }

        double[] temp = new double[source.Length];
        Pass(source, temp, width, height, kernel, overallWeight, horizontal: true);
        double[] result = new double[source.Length];
        Pass(temp, result, width, height, kernel, overallWeight, horizontal: false);

        // ActualColor: each channel clamped to 0-255 and rounded.
        for (int i = 0; i < result.Length; i++)
        {
            bgra[i] = (byte)Math.Round(Math.Clamp(result[i], 0, 255));
        }
    }

    private static void Pass(
        double[] from,
        double[] to,
        int width,
        int height,
        double[] kernel,
        double overallWeight,
        bool horizontal)
    {
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int target = ((y * width) + x) * 4;
                for (int channel = 0; channel < 4; channel++)
                {
                    double sum = kernel[0] * from[target + channel];
                    for (int i = 1; i < kernel.Length; i++)
                    {
                        int before = horizontal ? Index(x - i, y, width, height) : Index(x, y - i, width, height);
                        int after = horizontal ? Index(x + i, y, width, height) : Index(x, y + i, width, height);
                        sum += kernel[i] * (from[before + channel] + from[after + channel]);
                    }

                    to[target + channel] = sum / overallWeight;
                }
            }
        });
    }

    /// <summary>GetColor: coordinates outside the image take the nearest edge pixel.</summary>
    private static int Index(int x, int y, int width, int height)
    {
        x = Math.Max(0, Math.Min(width - 1, x));
        y = Math.Max(0, Math.Min(height - 1, y));
        return ((y * width) + x) * 4;
    }
}
