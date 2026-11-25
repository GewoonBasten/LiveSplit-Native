using System.Text.RegularExpressions;
using System.Xml;
using LiveSplitNative.Core.Imaging;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>The layout's Image background: BackgroundImage, ImageOpacity, ImageBlur (93a11b85).</summary>
public class LayoutBackgroundImageTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    /// <summary>A Bitmap blob Windows wrote (the game icon of Basten's splits).</summary>
    private static byte[] WindowsBlob()
    {
        string lss = File.ReadAllText(Fixture("sms-any-gcn-no-ace-9-full.lss"));
        Match match = Regex.Match(lss, @"<GameIcon><!\[CDATA\[(.*?)\]\]>", RegexOptions.Singleline);
        Assert.True(match.Success);
        return Convert.FromBase64String(match.Groups[1].Value);
    }

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Bitmap_Blob_Is_Written_Byte_For_Byte_As_Windows_Writes_It()
    {
        byte[] blob = WindowsBlob();
        byte[]? png = WindowsBitmapBlob.Decode(blob);

        Assert.NotNull(png);
        Assert.Equal(PngMagic, png![..8]);
        Assert.Equal(blob, WindowsBitmapBlob.Encode(png));
    }

    [Fact]
    public void Image_Background_Round_Trips_In_Windows_Order()
    {
        byte[] png = WindowsBitmapBlob.Decode(WindowsBlob())!;
        Layout layout = StandardLayoutFactory.Create();
        layout.Settings.BackgroundType = "Image";
        layout.Settings.BackgroundImage = png;
        layout.Settings.ImageOpacity = 0.35f;
        layout.Settings.ImageBlur = 0.2f;

        using var stream = new MemoryStream();
        XmlLayoutSerializer.Save(layout, stream);
        stream.Position = 0;
        var document = new XmlDocument();
        document.Load(stream);
        XmlElement settings = document["Layout"]!["Settings"]!;
        List<string> names = settings.ChildNodes.OfType<XmlElement>().Select(e => e.Name).ToList();
        int type = names.IndexOf("BackgroundType");
        Assert.Equal(["BackgroundType", "BackgroundImage", "ImageOpacity", "ImageBlur", "Opacity"], names.GetRange(type, 5));
        Assert.Equal("Image", settings["BackgroundType"]!.InnerText);
        Assert.IsType<XmlCDataSection>(settings["BackgroundImage"]!.FirstChild);
        Assert.Equal(WindowsBitmapBlob.Encode(png), Convert.FromBase64String(settings["BackgroundImage"]!.InnerText));
        Assert.Equal("0.35", settings["ImageOpacity"]!.InnerText);
        Assert.Equal("0.2", settings["ImageBlur"]!.InnerText);

        stream.Position = 0;
        Layout loaded = XmlLayoutSerializer.Load(stream);
        Assert.Equal("Image", loaded.Settings.BackgroundType);
        Assert.Equal(png, loaded.Settings.BackgroundImage);
        Assert.Equal(0.35f, loaded.Settings.ImageOpacity);
        Assert.Equal(0.2f, loaded.Settings.ImageBlur);
    }

    [Fact]
    public void The_Colors_Under_The_Image_Are_Kept_For_LiveSplitNative_Only()
    {
        Layout layout = StandardLayoutFactory.Create();
        layout.Settings.BackgroundType = "Image";
        layout.Settings.BackgroundColorType = "HorizontalGradient";
        using var stream = new MemoryStream();
        XmlLayoutSerializer.Save(layout, stream);
        stream.Position = 0;
        var document = new XmlDocument();
        document.Load(stream);
        XmlElement settings = document["Layout"]!["Settings"]!;
        // Windows reads Image (the image alone) and skips the extra element.
        Assert.Equal("Image", settings["BackgroundType"]!.InnerText);
        Assert.Equal("HorizontalGradient", settings["BackgroundColorType"]!.InnerText);
        stream.Position = 0;
        Layout loaded = XmlLayoutSerializer.Load(stream);
        Assert.Equal(("Image", "HorizontalGradient", "HorizontalGradient"), (loaded.Settings.BackgroundType, loaded.Settings.BackgroundColorType, loaded.Settings.ColorLayerType));

        // A solid color under the image, or no image: nothing extra (as Windows writes it).
        layout.Settings.BackgroundColorType = "SolidColor";
        using var plain = new MemoryStream();
        XmlLayoutSerializer.Save(layout, plain);
        Assert.DoesNotContain("BackgroundColorType", System.Text.Encoding.UTF8.GetString(plain.ToArray()));
        layout.Settings.BackgroundType = "VerticalGradient";
        Assert.Equal("VerticalGradient", layout.Settings.ColorLayerType);
    }

    [Fact]
    public void No_Image_Is_An_Empty_Element()
    {
        // GetImageFromElement reads only a non-empty element; <BackgroundImage></BackgroundImage>
        // would make Windows deserialize nothing and fail to load the layout.
        Layout layout = StandardLayoutFactory.Create();
        using var stream = new MemoryStream();
        XmlLayoutSerializer.Save(layout, stream);
        stream.Position = 0;
        var document = new XmlDocument();
        document.Load(stream);
        XmlElement image = document["Layout"]!["Settings"]!["BackgroundImage"]!;

        Assert.True(image.IsEmpty);
        Assert.Equal("1", document["Layout"]!["Settings"]!["ImageOpacity"]!.InnerText);
        Assert.Equal("0", document["Layout"]!["Settings"]!["ImageBlur"]!.InnerText);
    }

    [Fact]
    public void Older_Layout_Has_No_Image_Full_Opacity_And_No_Blur()
    {
        Layout layout = XmlLayoutSerializer.LoadFile(Fixture("default-layout.lsl"));

        Assert.Null(layout.Settings.BackgroundImage);
        Assert.Equal(1f, layout.Settings.ImageOpacity);
        Assert.Equal(0f, layout.Settings.ImageBlur);
    }

    [Fact]
    public void Clone_And_Assign_Keep_The_Image()
    {
        var settings = LayoutSettings.CreateStandard();
        settings.BackgroundImage = [1, 2, 3];
        settings.ImageOpacity = 0.5f;
        settings.ImageBlur = 0.3f;

        LayoutSettings clone = settings.Clone();
        var assigned = LayoutSettings.CreateStandard();
        assigned.Assign(settings);

        foreach (LayoutSettings copy in new[] { clone, assigned })
        {
            Assert.Equal(new byte[] { 1, 2, 3 }, copy.BackgroundImage);
            Assert.Equal(0.5f, copy.ImageOpacity);
            Assert.Equal(0.3f, copy.ImageBlur);
        }
    }

    [Theory]
    [InlineData(0.0, 1000, 500, null, null)]
    [InlineData(0.1, 1000, 500, null, null)]
    [InlineData(1.0, 1000, 500, 400, 200)]
    [InlineData(10.0, 1600, 900, 100, 56)]
    public void Blur_Scales_The_Image_Down_First(double sigma, int width, int height, int? scaledWidth, int? scaledHeight)
    {
        (int Width, int Height)? size = ImageBlur.ScaledSize(width, height, sigma);

        if (scaledWidth is null)
        {
            Assert.Null(size);
        }
        else
        {
            Assert.Equal((scaledWidth.Value, scaledHeight!.Value), size);
        }
    }

    [Fact]
    public void Blur_Spreads_A_Pixel_Evenly_And_Keeps_A_Flat_Image()
    {
        const int size = 9;
        byte[] dot = new byte[size * size * 4];
        int center = ((4 * size) + 4) * 4;
        dot[center] = dot[center + 1] = dot[center + 2] = dot[center + 3] = 255;
        ImageBlur.Blur(dot, size, size, 10);

        byte Value(int x, int y) => dot[((y * size) + x) * 4];
        Assert.True(Value(4, 4) < 255);
        Assert.True(Value(4, 4) > Value(3, 4));
        Assert.Equal(Value(3, 4), Value(5, 4));
        Assert.Equal(Value(4, 3), Value(4, 5));
        Assert.Equal(Value(3, 4), Value(4, 3));

        byte[] flat = Enumerable.Repeat((byte)120, size * size * 4).ToArray();
        ImageBlur.Blur(flat, size, size, 10);
        Assert.All(flat, value => Assert.Equal(120, value));
    }
}
