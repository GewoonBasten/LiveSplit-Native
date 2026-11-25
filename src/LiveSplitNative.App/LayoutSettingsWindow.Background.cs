using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace LiveSplitNative.App;

/// <summary>
/// LayoutSettingsControl's Background group (93a11b85): "Color:" with the colors' type and one
/// color (Solid Color) or two (gradients); "Image:" with the image button and Show Image, which
/// shows the image instead of the colors (Windows' Image background type; the colors are kept
/// for when it is off, and greyed while it shows); and Image Opacity (5-100) and Image Blur
/// (0-100), on while it shows.
/// </summary>
public partial class LayoutSettingsWindow
{
    private static readonly string[] BackgroundTypeLabels = ["Solid Color", "Vertical Gradient", "Horizontal Gradient"];

    private SettingsField? _backgroundField;
    private CheckBox? _showImageBox;
    private Image? _backgroundThumbnail;
    private Control[] _imageOnly = [];

    private Control BuildBackgroundGroup()
    {
        _bg1 = new ColorSwatchButton { ArgbColor = _settings.BackgroundColor };
        _bg2 = new ColorSwatchButton { ArgbColor = _settings.BackgroundColor2 };
        WireLivePreview(_bg1, "BackgroundColor", c => _settings.BackgroundColor = c);
        WireLivePreview(_bg2, "BackgroundColor2", c => _settings.BackgroundColor2 = c);

        // btnBackground2 in Image mode: the image, zoomed into the button; a click picks a file.
        _backgroundThumbnail = new Image { Stretch = Stretch.Uniform };
        _backgroundImageButton = new Button
        {
            Width = 20,
            Height = 20,
            MinWidth = 20,
            MinHeight = 20,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Black,
            Background = Brushes.Transparent,
            Content = _backgroundThumbnail,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _backgroundImageButton.Click += async (_, _) => await ChooseBackgroundImageAsync();
        ShowThumbnail(_settings.BackgroundImage);

        _backgroundTypeBox = new ComboBox
        {
            Width = 180,
            ItemsSource = BackgroundTypeLabels,
            SelectedItem = _settings.ColorLayerType switch
            {
                "VerticalGradient" => "Vertical Gradient",
                "HorizontalGradient" => "Horizontal Gradient",
                _ => "Solid Color",
            },
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "settingsCombo" },
        };
        _showImageBox = new CheckBox
        {
            Content = "Show Image",
            IsChecked = string.Equals(_settings.BackgroundType, "Image", StringComparison.OrdinalIgnoreCase),
            VerticalAlignment = VerticalAlignment.Center,
        };

        _imageOpacitySlider = ImageSlider(5, Math.Clamp(Math.Round(_settings.ImageOpacity * 100), 5, 100));
        _imageBlurSlider = ImageSlider(0, Math.Clamp(Math.Round(_settings.ImageBlur * 100), 0, 100));
        _imageOpacitySlider.Width = 200;
        _imageBlurSlider.Width = 200;
        var opacityField = new SettingsField { Label = "Image Opacity:", Field = _imageOpacitySlider };
        var blurField = new SettingsField { Label = "Image Blur:", Field = _imageBlurSlider };
        _imageOnly = [opacityField, blurField, _imageOpacitySlider, _imageBlurSlider];

        // The colors, then the image that replaces them, with its opacity and blur.
        _backgroundField = new SettingsField
        {
            Label = "Color:",
            Field = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _backgroundTypeBox, _bg1, _bg2 },
            },
        };
        var imageField = new SettingsField
        {
            Label = "Image:",
            Field = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _backgroundImageButton, _showImageBox },
            },
        };

        _backgroundTypeBox.SelectionChanged += (_, _) => ShowBackgroundType();
        _showImageBox.IsCheckedChanged += async (_, _) =>
        {
            // Show Image without an image: one is picked first (none picked: not shown).
            if (_showImageBox.IsChecked == true && _settings.BackgroundImage is null)
            {
                await ChooseBackgroundImageAsync();
                if (_settings.BackgroundImage is null)
                {
                    _showImageBox.IsChecked = false;
                }
            }

            ShowBackgroundType();
            NotifyLayoutPreview();
        };
        ShowBackgroundType();
        return Group("Background", _backgroundField, imageField, opacityField, blurField);
    }

    /// <summary>The Background type as saved: Image while the image shows, else the colors' type.</summary>
    internal (string BackgroundType, string ColorType) BackgroundTypes()
    {
        string colors = (_backgroundTypeBox?.SelectedItem as string ?? "Solid Color").Replace(" ", "", StringComparison.Ordinal);
        return (_showImageBox?.IsChecked == true ? "Image" : colors, colors);
    }

    private Slider ImageSlider(double minimum, double value)
    {
        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = 100,
            Value = value,
            MinWidth = 100,
            VerticalAlignment = VerticalAlignment.Center,
        };
        SliderChrome.Compact(slider);
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                NotifyLayoutPreview();
            }
        };
        return slider;
    }

    /// <summary>cmbGradientType_SelectedIndexChanged: the second color for gradients; the image's sliders while it shows.</summary>
    private void ShowBackgroundType()
    {
        string type = _backgroundTypeBox?.SelectedItem as string ?? "Solid Color";
        _bg2!.IsVisible = type is "Vertical Gradient" or "Horizontal Gradient";
        bool image = _showImageBox?.IsChecked == true;
        foreach (Control control in _imageOnly)
        {
            control.IsEnabled = image;
        }

        // The image replaces the colors: they can't be changed while it shows.
        _backgroundField!.IsEnabled = !image;
    }

    private void ShowThumbnail(byte[]? image)
    {
        if (_backgroundThumbnail is null)
        {
            return;
        }

        Bitmap? thumbnail = null;
        if (image is not null)
        {
            try
            {
                using var stream = new MemoryStream(image);
                thumbnail = Bitmap.DecodeToWidth(stream, 40);
            }
            catch (Exception)
            {
                thumbnail = null;
            }
        }

        _backgroundThumbnail.Source = thumbnail;
    }

    /// <summary>
    /// The picked image as the background (Settings.BackgroundImage = Image.FromFile); false,
    /// and nothing changed, when the file is not an image.
    /// </summary>
    internal bool UseBackgroundImage(byte[] bytes)
    {
        using SkiaSharp.SKBitmap? decoded = BackgroundImageBaker.Decode(bytes);
        if (decoded is null)
        {
            return false;
        }

        _settings.BackgroundImage = bytes;
        ShowThumbnail(bytes);
        // An image that is added shows (instead of the colors).
        if (_showImageBox is not null)
        {
            _showImageBox.IsChecked = true;
        }

        NotifyLayoutPreview();
        return true;
    }

    /// <summary>BackgroundColorButtonClick in Image mode: "Set Background Image...", then "Could not load image!" on failure.</summary>
    private async Task ChooseBackgroundImageAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Set Background Image...",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image Files") { Patterns = ["*.bmp", "*.jpg", "*.gif", "*.jpeg", "*.png"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] },
            ],
        });
        if (files.Count == 0)
        {
            return;
        }

        byte[] bytes;
        try
        {
            await using Stream stream = await files[0].OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            bytes = memory.ToArray();
        }
        catch (Exception)
        {
            bytes = [];
        }

        if (!UseBackgroundImage(bytes))
        {
            await ShowCouldNotLoadImageAsync();
        }
    }

    private async Task ShowCouldNotLoadImageAsync()
    {
        var ok = new Button { Content = "OK", IsDefault = true };
        var dialog = DialogChrome.Message("Error", "Could not load image!", 320, ok);
        ok.Click += (_, _) => dialog.Close();
        await OwnedDialog.ShowAsync(this, dialog);
    }
}
