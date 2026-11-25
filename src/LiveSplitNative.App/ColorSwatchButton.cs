using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// WinForms-like color button: small swatch that opens a compact Choose Color dialog
/// (upstream uses ColorPickerDialog from Fetze.WinFormsColor).
/// Live-previews while picking; Cancel restores the original; no disk I/O here.
/// </summary>
public sealed class ColorSwatchButton : Button
{
    public static readonly StyledProperty<ArgbColor> ArgbColorProperty =
        AvaloniaProperty.Register<ColorSwatchButton, ArgbColor>(nameof(ArgbColor));

    /// <summary>
    /// Optional live target update (e.g. layout settings color → layout redraw).
    /// Invoked during drag and again with the original on Cancel.
    /// </summary>
    public Action<ArgbColor>? LivePreview { get; set; }

    public ArgbColor ArgbColor
    {
        get => GetValue(ArgbColorProperty);
        set => SetValue(ArgbColorProperty, value);
    }

    public ColorSwatchButton()
    {
        Classes.Add("colorSwatch");
        Width = 20;
        Height = 20;
        MinWidth = 20;
        MinHeight = 20;
        Padding = new Thickness(0);
        CornerRadius = new CornerRadius(0);
        BorderThickness = new Thickness(1);
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00));
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Click += async (_, _) => await PickAsync();
        RefreshSwatch();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ArgbColorProperty)
        {
            RefreshSwatch();
        }
    }

    private void RefreshSwatch()
    {
        var c = ArgbColor;
        Background = c.A == 0
            ? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
            : new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));
    }

    private async Task PickAsync()
    {
        Window? owner = TopLevel.GetTopLevel(this) as Window;
        ArgbColor original = ArgbColor;
        ArgbColor? picked = await ColorPickerDialog.PickAsync(owner, original, live =>
        {
            ArgbColor = live;
            LivePreview?.Invoke(live);
        });

        if (picked is { } color)
        {
            ArgbColor = color;
            LivePreview?.Invoke(color);
        }
        else
        {
            ArgbColor = original;
            LivePreview?.Invoke(original);
        }
    }
}
