using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;
using SkiaSharp;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// The layout's Image background (LayoutSettingsControl's Background group and TimerForm's
/// baked background, 93a11b85).
/// </summary>
[Collection(MainWindowCollection.Name)]
public class LayoutBackgroundImageAppTests
{
    /// <summary>A 64 x 32 PNG: the left half red, the right half blue.</summary>
    private static byte[] RedBluePng()
    {
        using var bitmap = new SKBitmap(64, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                bitmap.SetPixel(x, y, x < 32 ? SKColors.Red : SKColors.Blue);
            }
        }

        using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static (byte B, byte G, byte R, byte A) Pixel(Bitmap bitmap, int x, int y)
    {
        using ILockedFramebuffer frame = ((WriteableBitmap)bitmap).Lock();
        byte[] row = new byte[frame.RowBytes];
        System.Runtime.InteropServices.Marshal.Copy(frame.Address + (y * frame.RowBytes), row, 0, row.Length);
        return (row[x * 4], row[(x * 4) + 1], row[(x * 4) + 2], row[(x * 4) + 3]);
    }

    [AvaloniaFact]
    public void Bake_Crops_The_Middle_To_The_Window_And_Dims_It_Over_Black()
    {
        using var baker = new BackgroundImageBaker();
        // A tall window shows the middle 16 columns of the wide image: red left, blue right.
        Bitmap? baked = baker.Bake(RedBluePngCache, 0.5f, 0f, new PixelSize(100, 200), 1);

        Assert.NotNull(baked);
        Assert.Equal(new PixelSize(100, 200), baked!.PixelSize);
        (byte b, byte g, byte r, byte a) = Pixel(baked, 10, 100);
        Assert.Equal(255, a);
        Assert.InRange(r, 120, 135);
        Assert.InRange(b, 0, 4);
        Assert.InRange(g, 0, 4);
        (b, _, r, _) = Pixel(baked, 90, 100);
        Assert.InRange(b, 120, 135);
        Assert.InRange(r, 0, 4);

        // Full blur: the red and blue run into each other across the middle.
        Bitmap? blurred = baker.Bake(RedBluePngCache, 1f, 1f, new PixelSize(100, 200), 1);
        Assert.NotNull(blurred);
        (b, _, r, _) = Pixel(blurred!, 50, 100);
        Assert.True(r > 20 && b > 20, $"middle r={r} b={b}");

        // Not an image: no bake (the window shows black).
        Assert.Null(baker.Bake([1, 2, 3], 1f, 0f, new PixelSize(100, 200), 1));
    }

    private static readonly byte[] RedBluePngCache = RedBluePng();

    [AvaloniaFact]
    public void Same_Image_And_Settings_Reuse_The_Bake()
    {
        using var baker = new BackgroundImageBaker();
        Bitmap? first = baker.Bake(RedBluePngCache, 0.7f, 0.2f, new PixelSize(80, 40), 2);
        Assert.Same(first, baker.Bake(RedBluePngCache, 0.7f, 0.2f, new PixelSize(80, 40), 2));
        Assert.NotSame(first, baker.Bake(RedBluePngCache, 0.7f, 0.2f, new PixelSize(81, 40), 2));
    }

    [AvaloniaFact]
    public void Background_Group_Has_The_Colors_With_The_Image_Over_Them()
    {
        var dialog = new LayoutSettingsWindow(StandardLayoutFactory.Create());
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        Control tab = (Control)((TabItem)dialog.SettingsTabs.Items[0]!).Content!;
        ComboBox type = tab.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.Items.OfType<string>().Contains("Vertical Gradient"));
        Assert.Equal(["Solid Color", "Vertical Gradient", "Horizontal Gradient"], type.Items.OfType<string>());
        Assert.Equal("Solid Color", type.SelectedItem);

        List<ColorSwatchButton> swatches = tab.GetLogicalDescendants().OfType<ColorSwatchButton>().Take(2).ToList();
        List<Slider> imageSliders = tab.GetLogicalDescendants().OfType<Slider>()
            .Where(s => s.GetLogicalParent() is Grid grid && grid.Children.OfType<TextBlock>().Any(t => LabelText.Is(t, "Image Opacity:") || LabelText.Is(t, "Image Blur:")))
            .ToList();
        Assert.Equal(2, imageSliders.Count);
        Button imageButton = tab.GetLogicalDescendants().OfType<Button>().Single(b => b.Content is Image);
        CheckBox show = tab.GetLogicalDescendants().OfType<CheckBox>().Single(c => Equals(c.Content, "Show Image"));

        Assert.True(swatches[0].IsVisible);
        Assert.False(swatches[1].IsVisible);
        Assert.True(imageButton.IsVisible);
        Assert.False(show.IsChecked);
        Assert.All(imageSliders, s => Assert.False(s.IsEnabled));

        type.SelectedItem = "Vertical Gradient";
        Assert.True(swatches[0].IsVisible && swatches[1].IsVisible);

        // An image that is added shows instead of the colors, which are kept but greyed.
        Assert.False(dialog.UseBackgroundImage([1, 2, 3]));
        Assert.False(show.IsChecked);
        Assert.True(dialog.UseBackgroundImage(RedBluePngCache));
        Assert.True(show.IsChecked);
        Assert.False(type.IsEffectivelyEnabled || swatches[0].IsEffectivelyEnabled);
        Assert.All(imageSliders, s => Assert.True(s.IsEnabled));
        Assert.Equal(5, imageSliders[0].Minimum);
        Assert.Equal(0, imageSliders[1].Minimum);
        imageSliders[0].Value = 35;
        imageSliders[1].Value = 20;
        Assert.Equal(("Image", "VerticalGradient"), dialog.BackgroundTypes());

        // Not shown: the colors are back (and can be changed); the image is kept.
        show.IsChecked = false;
        Assert.True(type.IsEffectivelyEnabled && swatches[1].IsEffectivelyEnabled);
        Assert.All(imageSliders, s => Assert.False(s.IsEnabled));
        Assert.Equal(("VerticalGradient", "VerticalGradient"), dialog.BackgroundTypes());
        show.IsChecked = true;
        dialog.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "OK"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        LayoutSettings result = dialog.Result!.Settings;
        Assert.Equal("Image", result.BackgroundType);
        Assert.Equal("VerticalGradient", result.BackgroundColorType);
        Assert.Equal(RedBluePngCache, result.BackgroundImage);
        Assert.Equal(0.35f, result.ImageOpacity);
        Assert.Equal(0.2f, result.ImageBlur);
    }

    [AvaloniaFact]
    public void Timer_Window_Shows_The_Image_And_Black_Without_One()
    {
        string dir = HeadlessUi.NewTempDir();
        string settingsPath = Path.Combine(dir, "settings.cfg");
        XmlAppSettingsSerializer.SaveFile(new AppSettings(), settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Layout layout = window.CurrentLayout.Clone();
        layout.Settings.BackgroundType = "Image";
        layout.Settings.BackgroundColor = ArgbColor.FromArgb(255, 15, 15, 15);
        window.ApplyLayoutForTests(layout);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Colors.Black, Assert.IsAssignableFrom<ISolidColorBrush>(window.Background).Color);

        layout = window.CurrentLayout.Clone();
        layout.Settings.BackgroundImage = RedBluePngCache;
        window.ApplyLayoutForTests(layout);
        Dispatcher.UIThread.RunJobs();
        ImageBrush brush = Assert.IsType<ImageBrush>(window.Background);
        var source = Assert.IsAssignableFrom<Bitmap>(brush.Source);
        Assert.Equal(
            new PixelSize((int)Math.Round(window.ClientSize.Width * window.RenderScaling), (int)Math.Round(window.ClientSize.Height * window.RenderScaling)),
            source.PixelSize);

        window.CurrentLayout.HasChanged = false;
        window.Session.State.Run.HasChanged = false;
        window.Close();
        HeadlessUi.Delete(dir);
    }
}
