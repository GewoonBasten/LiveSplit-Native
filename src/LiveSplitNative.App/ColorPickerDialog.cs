using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// Compact light "Choose Color" dialog (WinForms ColorPickerDialog density).
/// ColorView spectrum + value/alpha sliders + centered mode icons +
/// Avalonia <c>ColorPreviewer</c> (segmented accent strip + raised selected color) + OK/Cancel.
/// No Basic colors / Shades: grid / Color–New dual boxes.
/// No custom continuous GrayscalePreview / ShadePreview bars.
/// </summary>
internal static class ColorPickerDialog
{
    public static ColorView CreateCompactColorView(ArgbColor current)
    {
        var colorView = new ColorView
        {
            Color = Color.FromArgb(
                current.A == 0 ? (byte)255 : current.A,
                current.R,
                current.G,
                current.B),
            IsAlphaEnabled = true,
            IsAlphaVisible = true,
            // Accent segments beside the raised selected-color preview (authority: 72 / swatch-strip).
            IsAccentColorsVisible = true,
            IsColorPaletteVisible = true,
            IsColorSpectrumVisible = true,
            IsColorSpectrumSliderVisible = true,
            IsComponentSliderVisible = true,
            // Permanent: Avalonia ColorPreviewer — never replace with custom gradient/solid bars.
            IsColorPreviewVisible = true,
            Background = Brushes.Transparent,
            Width = 380,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
        };

        colorView.AttachedToVisualTree += async (_, _) =>
        {
            await Task.Delay(32);
            colorView.UpdateLayout();
            PackModeIcons(colorView);
            EnsureColorPreviewerVisible(colorView);

            foreach (Border border in colorView.GetVisualDescendants().OfType<Border>())
            {
                string? name = border.Name;
                if (name is "TabBackgroundBorder" or "ContentBackgroundBorder")
                {
                    border.Background = Brushes.Transparent;
                    border.BorderBrush = Brushes.Transparent;
                    border.BorderThickness = new Thickness(0);
                }
            }

            foreach (TabControl tabs in colorView.GetVisualDescendants().OfType<TabControl>())
            {
                tabs.Background = Brushes.Transparent;
            }

            foreach (ColorSpectrum spectrum in colorView.GetVisualDescendants().OfType<ColorSpectrum>())
            {
                spectrum.MinWidth = 220;
                spectrum.MinHeight = 220;
                spectrum.Width = 256;
                spectrum.Height = 256;
                spectrum.HorizontalAlignment = HorizontalAlignment.Center;
            }

            foreach (ColorSlider slider in colorView.GetVisualDescendants().OfType<ColorSlider>())
            {
                if (slider.Bounds.Height < 8 || slider.Bounds.Width < 8)
                {
                    slider.MinWidth = Math.Max(slider.MinWidth, 28);
                    slider.MinHeight = Math.Max(slider.MinHeight, 200);
                }

                HsvColor hsv = slider.HsvColor;
                slider.HsvColor = new HsvColor(hsv.A, hsv.H, hsv.S, Math.Clamp(hsv.V + 0.0001, 0, 1));
                slider.HsvColor = hsv;
                slider.InvalidateVisual();
            }

            UseSquarePaletteSwatches(colorView);
            colorView.InvalidateVisual();
            colorView.UpdateLayout();
        };

        return colorView;
    }

    /// <summary>Side of one palette swatch: 8 rows fit the palette tab's 266 px content height.</summary>
    public const double PaletteSwatchSize = 32;

    /// <summary>
    /// The palette as a grid of square swatches (Basten, 12 Oct), centered in its tab. Fluent's
    /// palette stretches the swatches over the whole tab instead; the list's size follows the
    /// palette's columns and rows, so it never depends on the selected color.
    /// </summary>
    public static void UseSquarePaletteSwatches(ColorView colorView)
    {
        int columns = Math.Max(1, colorView.PaletteColumnCount);
        int count = colorView.PaletteColors?.Count() ?? 0;
        if (count == 0)
        {
            return;
        }

        int rows = (count + columns - 1) / columns;
        colorView.Styles.Add(new Style(x => x.OfType<ColorView>().Template().OfType<ListBox>())
        {
            Setters =
            {
                new Setter(Layoutable.WidthProperty, columns * PaletteSwatchSize),
                new Setter(Layoutable.HeightProperty, rows * PaletteSwatchSize),
                new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Center),
                new Setter(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center),
            },
        });
    }

    /// <summary>Force ColorPreviewer on — never hide for cosmetic workarounds.</summary>
    public static void EnsureColorPreviewerVisible(ColorView colorView)
    {
        colorView.IsColorPreviewVisible = true;
        colorView.IsAccentColorsVisible = true;
        foreach (Control previewer in EnumerateColorPreviewers(colorView))
        {
            previewer.IsVisible = true;
        }
    }

    public static IEnumerable<Control> EnumerateColorPreviewers(ColorView colorView) =>
        colorView.GetVisualDescendants().OfType<Control>()
            .Where(c => c.GetType().Name is "ColorPreviewer");

    /// <summary>
    /// Regression: dialog must not host custom continuous preview bars
    /// (Tags GrayscalePreview / ShadePreview).
    /// </summary>
    public static bool HasForbiddenCustomPreviewBar(Visual root) =>
        root.GetVisualDescendants().OfType<Control>()
            .Any(c => ColorPreviewPolicy.IsForbiddenCustomPreviewTag(c.Tag));

    public static bool HasVisibleColorPreviewer(ColorView colorView) =>
        colorView.IsColorPreviewVisible
        && colorView.IsAccentColorsVisible
        && EnumerateColorPreviewers(colorView).Any(c => c.IsVisible);

    public static void PackModeIcons(ColorView colorView)
    {
        foreach (TabItem tab in colorView.GetVisualDescendants().OfType<TabItem>())
        {
            tab.HorizontalAlignment = HorizontalAlignment.Center;
            tab.Width = 28;
            tab.Height = 28;
            tab.MinWidth = 28;
            tab.MinHeight = 28;
            tab.Margin = new Thickness(2, 6, 2, 6);
            tab.Padding = new Thickness(0);
        }

        // Only the mode tab strip is centered, never the TabControl: Fluent's TabControl template
        // passes its HorizontalAlignment to the Border around strip and content, so a centered
        // TabControl sized every tab to its content's own width. The spectrum has a fixed size and
        // looked right; the palette's UniformGrid shrank to 12 px swatches and the Components
        // sliders to stubs. The palette tab's swatch list is an ItemsPresenter
        // too: centering it measured its UniformGrid at the swatches' own (tiny) width, which
        // made them thin strips, and it happened again on every color change.
        foreach (ItemsPresenter presenter in TabStripPresenters(colorView))
        {
            presenter.HorizontalAlignment = HorizontalAlignment.Center;
            if (presenter.Panel is { } panel)
            {
                panel.HorizontalAlignment = HorizontalAlignment.Center;
            }
        }
    }

    /// <summary>The ItemsPresenter of each TabControl itself (its tab strip), nothing inside a tab.</summary>
    public static IEnumerable<ItemsPresenter> TabStripPresenters(ColorView colorView) =>
        colorView.GetVisualDescendants()
            .OfType<ItemsPresenter>()
            .Where(p => p.TemplatedParent is TabControl);

    public static Window Create(ArgbColor current, out ColorView colorView, out Func<bool> wasAccepted)
    {
        colorView = CreateCompactColorView(current);
        ColorView boundView = colorView;
        var ok = DialogChrome.Style(new Button { Content = "OK", IsDefault = true });
        var cancel = DialogChrome.Style(new Button { Content = "Cancel", IsCancel = true });
        bool accepted = false;
        wasAccepted = () => accepted;

        // A color change only changes the color: the layout was packed once when the view was
        // shown, and re-packing on every change is what reflowed the palette swatches.
        boundView.ColorChanged += (_, _) => EnsureColorPreviewerVisible(boundView);

        var dialog = DialogChrome.ConfigureLightDialog(new Window
        {
            Title = "Choose Color",
            CanResize = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        });

        ok.Click += (_, _) =>
        {
            accepted = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        dialog.Content = new Border
        {
            Background = AppThemeController.GetDialogBrush("LsDialogWindowBackground", dialog),
            Padding = new Thickness(8),
            Child = new DockPanel
            {
                LastChildFill = true,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 8, 0, 0),
                        [DockPanel.DockProperty] = Dock.Bottom,
                        Children = { cancel, ok },
                    },
                    boundView,
                },
            },
        };

        return dialog;
    }

    /// <summary>
    /// Compatibility overload — no custom preview Border; ColorPreviewer lives inside ColorView.
    /// </summary>
    public static Window Create(
        ArgbColor current,
        out ColorView colorView,
        out Func<bool> wasAccepted,
        out Border? unusedCustomPreview)
    {
        unusedCustomPreview = null;
        return Create(current, out colorView, out wasAccepted);
    }

    public static async Task<ArgbColor?> PickAsync(
        Window? owner,
        ArgbColor current,
        Action<ArgbColor>? onLivePreview = null)
    {
        var session = new ColorPreviewShades.LivePreviewSession(current);
        // A fully transparent color (a component's "no background") starts opaque: picking a
        // shade then gives that shade, instead of an invisible one. Cancel keeps the original.
        ArgbColor start = current.A == 0 ? ArgbColor.FromArgb(255, current.R, current.G, current.B) : current;
        var dialog = Create(start, out ColorView colorView, out Func<bool> wasAccepted);

        void OnColorChanged(object? _, ColorChangedEventArgs e)
        {
            Color c = e.NewColor;
            var argb = ArgbColor.FromArgb(c.A, c.R, c.G, c.B);
            session.Preview(argb);
            onLivePreview?.Invoke(argb);
        }

        colorView.ColorChanged += OnColorChanged;

        if (owner is not null)
        {
            await OwnedDialog.ShowAsync(owner, dialog);
        }
        else
        {
            dialog.Show();
        }

        colorView.ColorChanged -= OnColorChanged;

        ArgbColor result = session.Finish(wasAccepted());
        if (!wasAccepted())
        {
            onLivePreview?.Invoke(session.Original);
            return null;
        }

        return result;
    }
}
