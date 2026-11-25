using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace LiveSplitNative.App;

/// <summary>A layout font as LiveSplit's font dialog edits it: family, size in pixels, bold, italic.</summary>
public readonly record struct LayoutFontChoice(string Family, float Size, bool Bold, bool Italic)
{
    /// <summary>SettingsHelper.FormatFont: the family and System.Drawing's FontStyle name.</summary>
    public string Describe() =>
        Family + " " + (Bold, Italic) switch
        {
            (true, true) => "Bold, Italic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular",
        };
}

/// <summary>
/// Port of LiveSplit's CustomFontDialog.FontDialog (lib/CustomFontDialog, 93a11b85): the
/// installed fonts with a search box, a size box with the sizes between MinSize and MaxSize,
/// Bold and Italic as far as the family has them, and a sample. Every change raises
/// <see cref="FontChanged"/> (the layout previews it); Cancel goes back to the original font,
/// which raises it once more.
/// </summary>
public sealed class FontDialogWindow : Window
{
    private static readonly int[] Sizes = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72];

    private readonly LayoutFontChoice _original;
    private readonly TextBox _fontBox = new() { Width = 160, Height = 24 };
    private readonly ListBox _fontList = new() { Width = 160, Height = 286 };
    private readonly TextBox _sizeBox = new() { Width = 100, Height = 24 };
    private readonly ListBox _sizeList = new() { Width = 100, Height = 168 };
    private readonly CheckBox _bold = new() { Content = "Bold" };
    private readonly CheckBox _italic = new() { Content = "Italic" };
    private readonly TextBlock _sample = new()
    {
        Text = "AaBbCcXxYyZz",
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.None,
    };

    private readonly List<string> _families;
    // While the dialog sets its own controls, their change events do nothing.
    private int _quiet;
    private bool _accepted;

    public FontDialogWindow(LayoutFontChoice original, int minSize, int maxSize)
    {
        _original = original;
        Title = "Font";
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DialogChrome.ConfigureLightDialog(this);

        // FontList: FontFamily.Families, which on Windows lists a font's faces beyond Regular /
        // Bold / Italic as families of their own (PP Formula Semi Condensed Medium).
        _families = LayoutFontCollection.ListedFamilies.ToList();
        _fontList.ItemsSource = _families;
        _sizeList.ItemsSource = Sizes.Where(s => s >= minSize && s <= maxSize)
            .Select(s => s.ToString(CultureInfo.InvariantCulture))
            .ToList();

        _fontList.SelectionChanged += (_, _) => OnFamilySelected();
        _fontBox.TextChanged += (_, _) => OnFontTyped();
        _sizeList.SelectionChanged += (_, _) =>
        {
            if (_quiet == 0 && _sizeList.SelectedItem is string size)
            {
                _sizeBox.Text = size;
            }
        };
        _sizeBox.TextChanged += (_, _) => OnSizeTyped();
        _sizeBox.AddHandler(TextInputEvent, OnSizeTextInput, RoutingStrategies.Tunnel);
        _bold.IsCheckedChanged += (_, _) => UpdateSample();
        _italic.IsCheckedChanged += (_, _) => UpdateSample();

        var ok = DialogChrome.Style(new Button { Content = "OK", IsDefault = true });
        var cancel = DialogChrome.Style(new Button { Content = "Cancel", IsCancel = true });
        ok.Click += (_, _) =>
        {
            _accepted = true;
            Close(true);
        };
        cancel.Click += (_, _) => Close(false);
        Closing += (_, _) =>
        {
            // FontDialog_FormClosing: Cancel (or closing the window) restores the original font.
            if (!_accepted)
            {
                SelectedFont = _original;
            }
        };

        var styleGroup = Group("Font Style", new StackPanel { Spacing = 2, Children = { _bold, _italic } });
        styleGroup.Width = 120;
        var sampleGroup = Group("Sample Text", _sample);
        sampleGroup.Height = 84;
        sampleGroup.ClipToBounds = true;

        // Windows: the font list on the left, size box and sizes beside it, Font Style at the
        // right, Sample Text under the sizes.
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            Margin = new Thickness(12),
        };
        Place(grid, new TextBlock { Text = "Font:", Margin = new Thickness(0, 0, 0, 4) }, row: 0, column: 0);
        Place(grid, _fontBox, row: 1, column: 0);
        _fontList.Margin = new Thickness(0, 2, 0, 0);
        Place(grid, _fontList, row: 2, column: 0, rowSpan: 2);
        Place(grid, new TextBlock { Text = "Size:", Margin = new Thickness(16, 0, 0, 4) }, row: 0, column: 1);
        _sizeBox.Margin = new Thickness(16, 0, 0, 0);
        Place(grid, _sizeBox, row: 1, column: 1);
        _sizeList.Margin = new Thickness(16, 2, 0, 0);
        _sizeList.VerticalAlignment = VerticalAlignment.Top;
        Place(grid, _sizeList, row: 2, column: 1);
        styleGroup.Margin = new Thickness(16, 0, 0, 0);
        styleGroup.VerticalAlignment = VerticalAlignment.Top;
        Place(grid, styleGroup, row: 0, column: 2, rowSpan: 3);
        sampleGroup.Margin = new Thickness(16, 8, 0, 0);
        Place(grid, sampleGroup, row: 3, column: 1, columnSpan: 2);

        Content = new DockPanel
        {
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Margin = new Thickness(12, 0, 12, 12),
                    [DockPanel.DockProperty] = Dock.Bottom,
                    Children = { cancel, ok },
                },
                grid,
            },
        };

        SelectedFont = original;
    }

    /// <summary>Raised with the font shown in the sample whenever it changes.</summary>
    public event Action<LayoutFontChoice>? FontChanged;

    /// <summary>The font the sample shows.</summary>
    public LayoutFontChoice SelectedFont
    {
        get => new(Family ?? _original.Family, ParseSize(), _bold.IsChecked == true, _italic.IsChecked == true);
        set
        {
            _quiet++;
            try
            {
                int index = _families.FindIndex(f => string.Equals(f, value.Family, StringComparison.OrdinalIgnoreCase));
                _fontList.SelectedIndex = index;
                _fontBox.Text = value.Family;
                if (index >= 0)
                {
                    _fontList.ScrollIntoView(index);
                }

                _sizeBox.Text = value.Size.ToString("0.##", CultureInfo.InvariantCulture);
                _bold.IsChecked = value.Bold;
                _italic.IsChecked = value.Italic;
            }
            finally
            {
                _quiet--;
            }

            OnFamilySelected();
        }
    }

    private string? Family => _fontList.SelectedItem as string ?? (string.IsNullOrWhiteSpace(_fontBox.Text) ? null : _fontBox.Text);

    /// <summary>lstFont_SelectedFontFamilyChanged: Bold and Italic only as the family has them.</summary>
    private void OnFamilySelected()
    {
        if (_fontList.SelectedItem is string selected && !_fontBox.IsFocused)
        {
            _quiet++;
            _fontBox.Text = selected;
            _quiet--;
        }

        if (Family is { } family)
        {
            bool bold = IsStyleAvailable(family, FontWeight.Bold, FontStyle.Normal);
            bool regular = IsStyleAvailable(family, FontWeight.Normal, FontStyle.Normal);
            bool italics = IsStyleAvailable(family, FontWeight.Normal, FontStyle.Italic)
                || IsStyleAvailable(family, FontWeight.Bold, FontStyle.Italic);
            _quiet++;
            _bold.IsEnabled = true;
            _italic.IsEnabled = true;
            if (!bold && !regular)
            {
                _bold.IsEnabled = false;
                _bold.IsChecked = false;
                if (italics)
                {
                    _italic.IsEnabled = false;
                    _italic.IsChecked = true;
                }
            }
            else if (!bold)
            {
                _bold.IsEnabled = false;
                _bold.IsChecked = false;
            }
            else if (!regular && !italics)
            {
                _bold.IsEnabled = false;
                _bold.IsChecked = true;
            }

            if (!italics)
            {
                _italic.IsEnabled = false;
                _italic.IsChecked = false;
            }

            _quiet--;
        }

        UpdateSample();
    }

    /// <summary>txtFont_TextChanged: typing selects the first font that starts with the text.</summary>
    private void OnFontTyped()
    {
        if (_quiet > 0 || !_fontBox.IsFocused)
        {
            return;
        }

        string text = _fontBox.Text ?? "";
        int index = _families.FindIndex(f => f.StartsWith(text, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _fontList.SelectedIndex = index;
            _fontList.ScrollIntoView(index);
        }
    }

    /// <summary>txtSize_TextChanged: the size in the list when it is there, then the sample.</summary>
    private void OnSizeTyped()
    {
        _quiet++;
        string text = _sizeBox.Text ?? "";
        _sizeList.SelectedItem = (_sizeList.ItemsSource as IEnumerable<string>)?.Contains(text) == true ? text : null;
        _quiet--;
        UpdateSample();
    }

    /// <summary>txtSize_KeyDown: digits and one decimal point.</summary>
    private void OnSizeTextInput(object? sender, TextInputEventArgs e)
    {
        string input = e.Text ?? "";
        bool ok = input.All(c => char.IsDigit(c) || (c == '.' && !(_sizeBox.Text ?? "").Contains('.')));
        if (!ok)
        {
            e.Handled = true;
        }
    }

    private float ParseSize()
    {
        float size = float.TryParse(_sizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : 1;
        return Math.Max(1, size);
    }

    /// <summary>UpdateSampleText: the family at the size, with the styles it has; then FontChanged.</summary>
    private void UpdateSample()
    {
        if (_quiet > 0 || Family is not { } family)
        {
            return;
        }

        bool bold = _bold.IsChecked == true && IsStyleAvailable(family, FontWeight.Bold, FontStyle.Normal);
        bool italic = _italic.IsChecked == true
            && (IsStyleAvailable(family, FontWeight.Normal, FontStyle.Italic) || IsStyleAvailable(family, FontWeight.Bold, FontStyle.Italic));
        float size = ParseSize();
        _sample.FontFamily = new FontFamily(AppFonts.ForLayoutFont(family));
        _sample.FontSize = Math.Min(size, 200);
        _sample.FontWeight = bold ? FontWeight.Bold : FontWeight.Normal;
        _sample.FontStyle = italic ? FontStyle.Italic : FontStyle.Normal;
        FontChanged?.Invoke(new LayoutFontChoice(family, size, bold, italic));
    }

    /// <summary>FontFamily.IsStyleAvailable, from the family's installed faces.</summary>
    internal static bool IsStyleAvailable(string family, FontWeight weight, FontStyle style) =>
        LayoutFontCollection.IsStyleAvailable(family, weight, style);

    private static Border Group(string title, Control content) => new()
    {
        Classes = { "groupBox" },
        Child = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold },
                content,
            },
        },
    };

    private static void Place(Grid grid, Control control, int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetRowSpan(control, rowSpan);
        Grid.SetColumnSpan(control, columnSpan);
        grid.Children.Add(control);
    }
}
