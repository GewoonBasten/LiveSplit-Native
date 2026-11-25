using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using LiveSplitNative.Core.Imaging;
using SkiaSharp;

namespace LiveSplitNative.App;

/// <summary>The background colors under the image: a solid color, or a vertical or horizontal gradient.</summary>
internal readonly record struct BackgroundColors(int Direction, Core.UI.ArgbColor First, Core.UI.ArgbColor Second)
{
    public const int Solid = 0;
    public const int Vertical = 1;
    public const int Horizontal = 2;

    /// <summary>Windows' form under an image: black.</summary>
    public static BackgroundColors Black => new(Solid, Core.UI.ArgbColor.FromArgb(255, 0, 0, 0), Core.UI.ArgbColor.FromArgb(255, 0, 0, 0));
}

/// <summary>
/// TimerForm.CreateBakedBackground: the layout's background image at the window's pixel size,
/// blurred (ImageBlur.Generate with sigma ImageBlur x 10), cropped to the window's shape around
/// its center (never stretched), and drawn at ImageOpacity over the form's black (or the given
/// colors). The decoded and blurred image is kept until the image or the blur changes; the bake
/// until anything changes.
/// </summary>
internal sealed class BackgroundImageBaker : IDisposable
{
    private byte[]? _image;
    private SKBitmap? _decoded;
    private float _blur = float.NaN;
    private SKBitmap? _source;
    private (byte[] Image, float Opacity, float Blur, PixelSize Size, double Scaling, BackgroundColors Colors)? _bakedKey;
    private Bitmap? _baked;

    /// <summary>Decodes an image file as Windows' Image.FromFile would accept it; null if it is not an image.</summary>
    public static SKBitmap? Decode(byte[] bytes)
    {
        try
        {
            using var data = SKData.CreateCopy(bytes);
            using SKCodec? codec = SKCodec.Create(data);
            if (codec is null)
            {
                return null;
            }

            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            SKBitmap? bitmap = SKBitmap.Decode(codec, info);
            return bitmap is { Width: > 0, Height: > 0 } ? bitmap : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The baked background for a window of <paramref name="size"/> device pixels, or null without a usable image.</summary>
    public Bitmap? Bake(byte[]? image, float opacity, float blur, PixelSize size, double scaling) =>
        Bake(image, opacity, blur, size, scaling, BackgroundColors.Black);

    /// <summary>The baked background over <paramref name="colors"/>.</summary>
    public Bitmap? Bake(byte[]? image, float opacity, float blur, PixelSize size, double scaling, BackgroundColors colors)
    {
        if (image is null || size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }

        if (_bakedKey is { } key
            && ReferenceEquals(key.Image, image)
            && key.Opacity == opacity
            && key.Blur == blur
            && key.Size == size
            && key.Scaling == scaling
            && key.Colors == colors)
        {
            return _baked;
        }

        SKBitmap? source = Source(image, blur);
        // The previous bake may still be on screen until the window takes the new one: it is
        // left to the collector rather than disposed here.
        Bitmap? baked = source is null ? null : Draw(source, opacity, size, scaling, colors);
        _baked = baked;
        _bakedKey = (image, opacity, blur, size, scaling, colors);
        return baked;
    }

    private SKBitmap? Source(byte[] image, float blur)
    {
        if (!ReferenceEquals(image, _image))
        {
            ReleaseSource();
            _decoded?.Dispose();
            _decoded = Decode(image);
            _image = image;
            _blur = float.NaN;
        }

        if (_decoded is null)
        {
            return null;
        }

        if (blur != _blur)
        {
            ReleaseSource();
            _source = blur > 0 ? Blurred(_decoded, blur * 10) : _decoded;
            _blur = blur;
        }

        return _source;
    }

    /// <summary>ImageBlur.Generate: scaled down by 1.5 x sigma + 1 (new Bitmap(image, w, h), bilinear), then blurred.</summary>
    private static SKBitmap Blurred(SKBitmap image, double sigma)
    {
        if (ImageBlur.ScaledSize(image.Width, image.Height, sigma) is not { } size)
        {
            return image;
        }

        var scaled = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        image.ScalePixels(scaled, SKFilterQuality.Low);
        if (ImageBlur.KernelWidth(sigma) > 1)
        {
            // A one-wide kernel weighs only the pixel itself: nothing to blur.
            int rowBytes = size.Width * 4;
            byte[] pixels = new byte[rowBytes * size.Height];
            IntPtr address = scaled.GetPixels();
            for (int y = 0; y < size.Height; y++)
            {
                Marshal.Copy(address + (y * scaled.RowBytes), pixels, y * rowBytes, rowBytes);
            }

            ImageBlur.Blur(pixels, size.Width, size.Height, sigma);
            for (int y = 0; y < size.Height; y++)
            {
                Marshal.Copy(pixels, y * rowBytes, address + (y * scaled.RowBytes), rowBytes);
            }

            scaled.NotifyPixelsChanged();
        }

        return scaled;
    }

    private static Bitmap Draw(SKBitmap image, float opacity, PixelSize size, double scaling, BackgroundColors colors)
    {
        int width = size.Width;
        int height = size.Height;
        // The part of the image with the window's shape, around its center.
        float croppedWidth = image.Width;
        float croppedHeight = image.Height;
        if (image.Width / (float)image.Height > width / (float)height)
        {
            croppedWidth = image.Height * (width / (float)height);
        }
        else
        {
            croppedHeight = image.Width * (height / (float)width);
        }

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using SKSurface surface = SKSurface.Create(info);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);
        // The colors first (LinearGradientBrush from the top or the left edge), then the image.
        SKColor first = new(colors.First.R, colors.First.G, colors.First.B, colors.First.A);
        SKColor second = new(colors.Second.R, colors.Second.G, colors.Second.B, colors.Second.A);
        using (var fill = new SKPaint())
        {
            if (colors.Direction == BackgroundColors.Solid)
            {
                fill.Color = first;
            }
            else
            {
                fill.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    colors.Direction == BackgroundColors.Horizontal ? new SKPoint(width, 0) : new SKPoint(0, height),
                    [first, second],
                    SKShaderTileMode.Clamp);
            }

            canvas.DrawRect(SKRect.Create(0, 0, width, height), fill);
        }

        using var paint = new SKPaint
        {
            // ColorMatrix Matrix33 = opacity: the image's alpha times the opacity.
            Color = SKColors.White.WithAlpha((byte)Math.Round(Math.Clamp(opacity, 0f, 1f) * 255)),
            FilterQuality = SKFilterQuality.High,
        };
        canvas.DrawBitmap(
            image,
            SKRect.Create((image.Width - croppedWidth) / 2, (image.Height - croppedHeight) / 2, croppedWidth, croppedHeight),
            SKRect.Create(0, 0, width, height),
            paint);
        canvas.Flush();

        var baked = new WriteableBitmap(size, new Vector(96 * scaling, 96 * scaling), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using ILockedFramebuffer frame = baked.Lock();
        surface.ReadPixels(info, frame.Address, frame.RowBytes, 0, 0);
        return baked;
    }

    private void ReleaseSource()
    {
        if (_source is not null && !ReferenceEquals(_source, _decoded))
        {
            _source.Dispose();
        }

        _source = null;
    }

    public void Dispose()
    {
        ReleaseSource();
        _decoded?.Dispose();
        _decoded = null;
        _baked?.Dispose();
        _baked = null;
        _bakedKey = null;
    }
}
