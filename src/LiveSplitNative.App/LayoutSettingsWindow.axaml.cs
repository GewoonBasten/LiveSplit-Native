using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// Avalonia port of LiveSplit.View LayoutSettingsDialog:
/// TabControl with first tab "Layout" (LayoutSettingsControl) then one tab per component.
/// </summary>
public partial class LayoutSettingsWindow : Window
{
    private readonly Layout _layout;
    private readonly LayoutSettings _settings;
    private readonly List<ComponentSettingsEditor> _componentEditors = [];
    private readonly Dictionary<string, ColorSwatchButton> _swatches = new(StringComparer.Ordinal);
    private ColorSwatchButton? _bg1;
    private ColorSwatchButton? _bg2;
    private ComboBox? _backgroundTypeBox;
    private Slider? _imageOpacitySlider, _imageBlurSlider;
    private Button? _backgroundImageButton;
    private Slider? _opacitySlider;
    private NumericUpDown? _componentSpacingBox;
    private TextBlock? _opacityLabel;
    private CheckBox? _alwaysOnTopBox, _allowResizingBox;
    private CheckBox? _showBestSegmentsBox, _antiAliasingBox, _dropShadowsBox, _useRainbowColorBox;

    /// <summary>
    /// Invoked while a color is being picked (and again on Cancel restore). Host may refresh
    /// the live timer surface; dialog does not write layout files.
    /// </summary>
    public Action<string, ArgbColor>? LiveColorPreview { get; set; }

    /// <summary>Push full working layout for live timer preview (SettingsEditSession).</summary>
    public Action<Layout>? LiveLayoutPreview { get; set; }

    private readonly Dictionary<LayoutFontSlot, TextBlock> _fontLabels = new();

    public LayoutSettingsWindow() : this(StandardLayoutFactory.Create())
    {
    }

    /// <summary>Legacy ctor used by older screenshot paths — layout colors only (no component tabs).</summary>
    public LayoutSettingsWindow(LayoutSettings settings)
        : this(WrapSettings(settings))
    {
    }

    /// <summary>Slightly above Component Settings 720 so Layout tab colors fit without a phantom scroll.</summary>
    private const double PreferredMaxDialogHeight = 760;

    public LayoutSettingsWindow(Layout layout)
    {
        InitializeComponent();
        DialogChrome.ConfigureLightDialog(this);
        ApplyScreenCappedMaxHeight();
        _layout = layout.Clone();
        _settings = _layout.Settings;
        BuildTabs();
        Opened += (_, _) => RefitHeight();
        // Only a tab change refits: SelectionChanged bubbles, so every dropdown and accuracy
        // choice inside a tab raised it too, and the refit put that tab's scroll back at the top
        // (Basten 18 Oct: "changing a setting somehow moves the scroll").
        SettingsTabs.SelectionChanged += (_, e) =>
        {
            if (ReferenceEquals(e.Source, SettingsTabs))
            {
                RefitHeight();
            }
        };
    }

    /// <summary>A screen height for the tests (headless has no real screen).</summary>
    internal double? ScreenHeightForTests { get; set; }

    private void ApplyScreenCappedMaxHeight()
    {
        double screen = ScreenHeightForTests ?? MenuChrome.ResolveScreenMaxHeightDipPublic(this);
        MaxHeight = Math.Min(PreferredMaxDialogHeight, Math.Max(360, screen * 0.85));
        SizeToContent = SizeToContent.Manual;
    }

    /// <summary>
    /// Size to selected tab content up to MaxHeight. ScrollViewer inside the tab scrolls
    /// when the form is taller (Detailed Timer / Subsplits) — OK/Cancel stay docked.
    /// Layout tab uses SizeToContent (compact); component tabs use measured height + scroll.
    /// </summary>
    public void RefitHeight()
    {
        ApplyScreenCappedMaxHeight();
        const double chrome = 140; // title bar + tabs + footer (the page has its own padding)

        if (SettingsTabs.SelectedItem is TabItem { Content: ScrollViewer sv })
        {
            sv.Height = double.NaN;
            sv.ClearValue(MaxHeightProperty);
        }

        // Layout tab: grow to content (PreferFit). Cap + scroll only if it hits MaxHeight.
        if (SettingsTabs.SelectedIndex <= 0)
        {
            // Clear prior Manual Height so SizeToContent can shrink below MaxHeight.
            ClearValue(HeightProperty);
            SizeToContent = SizeToContent.Height;
            if (SettingsTabs.SelectedItem is TabItem { Content: ScrollViewer layoutSv })
            {
                layoutSv.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
            }

            UpdateLayout();
            // The window cannot grow past MaxHeight, so its Bounds never show the overflow (the
            // Colors group onwards was cut off): compare the page's own height with the room
            // the tab control has for it, and scroll the page when it does not fit.
            if (SettingsTabs.SelectedItem is TabItem { Content: ScrollViewer page }
                && page.TranslatePoint(new Point(0, 0), SettingsTabs) is { } pageTop)
            {
                double room = SettingsTabs.Bounds.Height - pageTop.Y
                    - SettingsTabs.Padding.Bottom - SettingsTabs.BorderThickness.Bottom;
                if (page.Bounds.Height > room + 1)
                {
                    SizeToContent = SizeToContent.Manual;
                    Height = Math.Min(MaxHeight, Bounds.Height);
                    page.MaxHeight = Math.Max(120, room);
                    page.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
                }
            }

            return;
        }

        SizeToContent = SizeToContent.Manual;
        UpdateLayout();
        double availW = Bounds.Width > 1 ? Bounds.Width - 24 : Width - 24;
        double contentH = MeasureSelectedTabContentHeight(availW);
        double target = contentH + chrome;
        double cap = MaxHeight > 0 ? MaxHeight : target;
        bool needsScroll = target > cap + 1;
        Height = needsScroll ? cap : Math.Min(cap, Math.Max(MinHeight, target));
        if (SettingsTabs.SelectedItem is TabItem { Content: ScrollViewer compSv })
        {
            if (needsScroll)
            {
                compSv.MaxHeight = Math.Max(120, Height - chrome);
                compSv.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
            }
            else
            {
                compSv.ClearValue(MaxHeightProperty);
                compSv.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
            }
        }

        UpdateLayout();
    }

    private double MeasureSelectedTabContentHeight(double availableWidth)
    {
        if (SettingsTabs.SelectedItem is not TabItem tab)
        {
            return 320;
        }

        Control? inner = tab.Content switch
        {
            ScrollViewer { Content: Control c } => c,
            Control c => c,
            _ => null,
        };

        if (inner is ComponentSettingsEditor editor)
        {
            return editor.MeasureContentHeight(availableWidth);
        }

        if (inner is Control control)
        {
            control.Measure(new Size(Math.Max(200, availableWidth), double.PositiveInfinity));
            return Math.Max(120, control.DesiredSize.Height);
        }

        return 320;
    }

    public Layout? Result { get; private set; }

    /// <summary>Backward-compat for callers that only read LayoutSettings.</summary>
    public LayoutSettings? SettingsResult => Result?.Settings;

    public void SelectTab(int index)
    {
        if (index >= 0 && index < SettingsTabs.ItemCount)
        {
            SettingsTabs.SelectedIndex = index;
        }
    }

    private static Layout WrapSettings(LayoutSettings settings)
    {
        var layout = StandardLayoutFactory.Create();
        layout.Settings.Assign(settings);
        return layout;
    }

    private void BuildTabs()
    {
        SettingsTabs.Items.Clear();
        _componentEditors.Clear();

        // ScrollViewer: SizeToContent grows with content; when MaxHeight caps the window,
        // tall component tabs (Detailed Timer / Subsplits) scroll instead of clipping.
        SettingsTabs.Items.Add(new TabItem
        {
            FontSize = AppFonts.ControlFontSize,
            Header = CompactTabHeader("Layout"),
            Content = WrapScrollable(BuildLayoutTab()),
        });

        foreach (LayoutComponent component in _layout.Components)
        {
            var editor = new ComponentSettingsEditor(component, _layout.Mode);
            editor.SettingsChanged = NotifyLayoutPreview;
            _componentEditors.Add(editor);
            // LayoutSettingsDialog adds a tab only for a component with a settings control
            // (the Separator has none); the editor stays so OK keeps every component in order.
            if (!component.HasSettings)
            {
                continue;
            }

            SettingsTabs.Items.Add(new TabItem
            {
                FontSize = AppFonts.ControlFontSize,
                Header = CompactTabHeader(editor.TabHeader),
                Content = WrapScrollable(editor),
            });
        }
    }

    private void NotifyLayoutPreview()
    {
        FlushLayoutTabIntoSettings();
        for (int i = 0; i < _componentEditors.Count && i < _layout.Components.Count; i++)
        {
            _layout.Components[i] = _componentEditors[i].Apply();
        }

        LiveLayoutPreview?.Invoke(_layout);
    }

    private void FlushLayoutTabIntoSettings()
    {
        if (_alwaysOnTopBox is not null)
        {
            _settings.AlwaysOnTop = _alwaysOnTopBox.IsChecked == true;
        }

        if (_allowResizingBox is not null)
        {
            _settings.AllowResizing = _allowResizingBox.IsChecked == true;
        }

        if (_showBestSegmentsBox is not null)
        {
            _settings.ShowBestSegments = _showBestSegmentsBox.IsChecked == true;
        }

        if (_antiAliasingBox is not null)
        {
            _settings.AntiAliasing = _antiAliasingBox.IsChecked == true;
        }

        if (_dropShadowsBox is not null)
        {
            _settings.DropShadows = _dropShadowsBox.IsChecked == true;
        }

        if (_useRainbowColorBox is not null)
        {
            _settings.UseRainbowColor = _useRainbowColorBox.IsChecked == true;
        }

        if (_opacitySlider is not null)
        {
            _settings.Opacity = (float)_opacitySlider.Value;
        }

        if (_componentSpacingBox?.Value is { } spacing)
        {
            _settings.ComponentSpacing = (float)spacing;
        }

        if (_backgroundTypeBox is not null)
        {
            (_settings.BackgroundType, _settings.BackgroundColorType) = BackgroundTypes();
        }

        if (_imageOpacitySlider is not null)
        {
            _settings.ImageOpacity = (float)Math.Round(_imageOpacitySlider.Value) / 100f;
        }

        if (_imageBlurSlider is not null)
        {
            _settings.ImageBlur = (float)Math.Round(_imageBlurSlider.Value) / 100f;
        }

        if (_bg1 is not null)
        {
            _settings.BackgroundColor = _bg1.ArgbColor;
        }

        if (_bg2 is not null)
        {
            _settings.BackgroundColor2 = _bg2.ArgbColor;
        }

        foreach ((string key, ColorSwatchButton swatch) in _swatches)
        {
            ApplyLiveColor(key, swatch.ArgbColor);
        }
    }

    private static ScrollViewer WrapScrollable(Control content) => new()
    {
        Content = content,
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        Padding = new Thickness(0, 0, 2, 0),
    };


    private static Control CompactTabHeader(string text) =>
        new TextBlock
        {
            Text = text,
            FontSize = AppFonts.ControlFontSize,
            FontFamily = new Avalonia.Media.FontFamily(AppFonts.ChromeFamily),
        };

    private Control BuildLayoutTab()
    {
        var root = new StackPanel { Spacing = SettingsLayout.SectionGap, Margin = ComponentSettingsEditor.PagePadding };

        root.Children.Add(BuildBackgroundGroup());

        _opacitySlider = new Slider
        {
            Minimum = 0.15,
            Maximum = 1,
            Value = Math.Clamp(_settings.Opacity, 0.15f, 1f),
            Width = 200,
        };
        _opacityLabel = new TextBlock
        {
            Text = $"{(int)(_opacitySlider.Value * 100)}%",
            Width = 40,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _opacitySlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && _opacityLabel is not null && _opacitySlider is not null)
            {
                _opacityLabel.Text = $"{(int)(_opacitySlider.Value * 100)}%";
                NotifyLayoutPreview();
            }
        };
        _backgroundTypeBox.SelectionChanged += (_, _) => NotifyLayoutPreview();
        var window = new SettingsCard();
        root.Children.Add(window);
        window.Children.Add(Row(
            "Opacity:",
            new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { _opacitySlider, _opacityLabel },
            }));

        _alwaysOnTopBox = PreviewCheck("Always on Top", _settings.AlwaysOnTop);
        _allowResizingBox = PreviewCheck("Allow Resizing", _settings.AllowResizing);
        window.Children.Add(_alwaysOnTopBox);
        window.Children.Add(_allowResizingBox);

        // LiveSplitNative's Component Spacing (Basten 18 Oct): the space added to a 25 high
        // single-line component, 0-25 in steps of 0.1 like Split Height; 6 is Windows' height.
        _componentSpacingBox = ComponentSettingsEditor.CreateSettingsNumericUpDown(
            Math.Round((decimal)_settings.ComponentSpacing, 1), 0, 25, 0.1m);
        _componentSpacingBox.FormatString = "0.0";
        _componentSpacingBox.ValueChanged += (_, _) =>
        {
            if (_componentSpacingBox.Value is not null)
            {
                NotifyLayoutPreview();
            }
        };
        window.Children.Add(Row("Component Spacing:", _componentSpacingBox));

        var display = Group("Display",
            _showBestSegmentsBox = PreviewCheck("Show Best Segments", _settings.ShowBestSegments),
            _antiAliasingBox = PreviewCheckAntiAliasing(_settings.AntiAliasing),
            _dropShadowsBox = PreviewCheck("Drop Shadows", _settings.DropShadows),
            _useRainbowColorBox = PreviewCheck("Rainbow Color", _settings.UseRainbowColor));
        root.Children.Add(display);

        // LayoutSettingsControl's Fonts group: each layout font's name and style, and Choose...
        root.Children.Add(Group("Fonts",
            FontRow("Timer:", LayoutFontSlot.Timer),
            FontRow("Times:", LayoutFontSlot.Times),
            FontRow("Text:", LayoutFontSlot.Text)));

        // LayoutSettingsControl's colors, in its two columns: a row of the card per pair.
        var colorsPanel = new SettingsCard();
        AddColorRow(colorsPanel, "Best Segment:", "BestSegmentColor", _settings.BestSegmentColor,
            "Not Running:", "NotRunningColor", _settings.NotRunningColor);
        AddColorRow(colorsPanel, "Ahead (Gaining Time):", "AheadGainingTimeColor", _settings.AheadGainingTimeColor,
            "Personal Best:", "PersonalBestColor", _settings.PersonalBestColor);
        AddColorRow(colorsPanel, "Ahead (Losing Time):", "AheadLosingTimeColor", _settings.AheadLosingTimeColor,
            "Paused:", "PausedColor", _settings.PausedColor);
        AddColorRow(colorsPanel, "Behind (Gaining Time):", "BehindGainingTimeColor", _settings.BehindGainingTimeColor,
            "Thin Separators:", "ThinSeparatorsColor", _settings.ThinSeparatorsColor);
        AddColorRow(colorsPanel, "Behind (Losing Time):", "BehindLosingTimeColor", _settings.BehindLosingTimeColor,
            "Separators:", "SeparatorsColor", _settings.SeparatorsColor);
        AddColorRow(colorsPanel, "Text:", "TextColor", _settings.TextColor,
            "Shadows:", "ShadowsColor", _settings.ShadowsColor);
        AddColorRow(colorsPanel, "Text Outlines:", "TextOutlineColor", _settings.TextOutlineColor, null, null, default);
        root.Children.Add(new SettingsSection { Header = "Colors", Children = { colorsPanel } });

        return root;
    }

    internal enum LayoutFontSlot
    {
        Timer,
        Times,
        Text,
    }

    /// <summary>The font of a slot as the font dialog edits it.</summary>
    internal LayoutFontChoice GetFont(LayoutFontSlot slot) => slot switch
    {
        LayoutFontSlot.Timer => new(_settings.TimerFontFamily, _settings.TimerFontSizePx, _settings.TimerFontBold, _settings.TimerFontItalic),
        LayoutFontSlot.Times => new(_settings.TimesFontFamily, _settings.TimesFontSizePx, _settings.TimesFontBold, _settings.TimesFontItalic),
        _ => new(_settings.TextFontFamily, _settings.TextFontSizePx, _settings.TextFontBold, _settings.TextFontItalic),
    };

    private void SetFont(LayoutFontSlot slot, LayoutFontChoice font)
    {
        switch (slot)
        {
            case LayoutFontSlot.Timer:
                (_settings.TimerFontFamily, _settings.TimerFontSizePx, _settings.TimerFontBold, _settings.TimerFontItalic) =
                    (font.Family, font.Size, font.Bold, font.Italic);
                break;
            case LayoutFontSlot.Times:
                (_settings.TimesFontFamily, _settings.TimesFontSizePx, _settings.TimesFontBold, _settings.TimesFontItalic) =
                    (font.Family, font.Size, font.Bold, font.Italic);
                break;
            default:
                (_settings.TextFontFamily, _settings.TextFontSizePx, _settings.TextFontBold, _settings.TextFontItalic) =
                    (font.Family, font.Size, font.Bold, font.Italic);
                break;
        }
    }

    private Control FontRow(string label, LayoutFontSlot slot)
    {
        var name = new TextBlock
        {
            Text = GetFont(slot).Describe(),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            [Grid.ColumnProperty] = 0,
        };
        _fontLabels[slot] = name;
        var choose = DialogChrome.Style(new Button
        {
            Content = "Choose...",
            [Grid.ColumnProperty] = 1,
        });
        choose.Click += async (_, _) => await ChooseFontAsync(slot);
        return new SettingsField
        {
            Label = label,
            Field = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children = { name, choose },
            },
        };
    }

    /// <summary>
    /// btnTimer_Click / btnTimes_Click / btnTextFont_Click: the font dialog with sizes 11-26, and
    /// for the timer 7-20 showing its size / 50 x 18 (closer to the other fonts, finer steps);
    /// every change previews on the timer, Cancel restores the font.
    /// </summary>
    internal async Task ChooseFontAsync(LayoutFontSlot slot)
    {
        LayoutFontChoice current = GetFont(slot);
        bool timer = slot == LayoutFontSlot.Timer;
        LayoutFontChoice shown = timer ? current with { Size = current.Size / 50f * 18f } : current;
        var dialog = new FontDialogWindow(shown, timer ? 7 : 11, timer ? 20 : 26);
        dialog.FontChanged += font =>
        {
            SetFont(slot, timer ? font with { Size = font.Size / 18f * 50f } : font);
            NotifyLayoutPreview();
        };
        await OwnedDialog.ShowAsync<bool?>(this, dialog);
        _fontLabels[slot].Text = GetFont(slot).Describe();
    }

    private void AddColorRow(
        SettingsCard host,
        string leftLabel,
        string leftKey,
        ArgbColor leftColor,
        string? rightLabel,
        string? rightKey,
        ArgbColor rightColor)
    {
        var leftSwatch = new ColorSwatchButton { ArgbColor = leftColor };
        _swatches[leftKey] = leftSwatch;
        WireLivePreview(leftSwatch, leftKey, c => ApplyLiveColor(leftKey, c));
        // Two label + swatch pairs; the left label in the window's label column.
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{SettingsLayout.GetLabelWidth(this)},24,32,*,24"),
        };
        row.Children.Add(new TextBlock
        {
            Text = leftLabel.TrimEnd(':'),
            VerticalAlignment = VerticalAlignment.Center,
            [Grid.ColumnProperty] = 0,
        });
        row.Children.Add(new Border
        {
            Child = leftSwatch,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            [Grid.ColumnProperty] = 1,
        });

        if (rightLabel is not null && rightKey is not null)
        {
            var rightSwatch = new ColorSwatchButton { ArgbColor = rightColor };
            _swatches[rightKey] = rightSwatch;
            WireLivePreview(rightSwatch, rightKey, c => ApplyLiveColor(rightKey, c));
            row.Children.Add(new TextBlock
            {
                Text = rightLabel.TrimEnd(':'),
                VerticalAlignment = VerticalAlignment.Center,
                [Grid.ColumnProperty] = 3,
            });
            row.Children.Add(new Border
            {
                Child = rightSwatch,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                [Grid.ColumnProperty] = 4,
            });
        }

        host.Children.Add(row);
    }

    /// <summary>A labelled row in the window's label column.</summary>
    private static SettingsField Row(string label, Control control) => new() { Label = label, Field = control };

    /// <summary>A group box: its rows in a card under its heading.</summary>
    private static SettingsSection Group(string title, params Control[] rows)
    {
        var card = new SettingsCard();
        foreach (Control row in rows)
        {
            card.Children.Add(row);
        }

        return new SettingsSection { Header = title, Children = { card } };
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        FlushLayoutTabIntoSettings();
        _layout.Components.Clear();
        foreach (ComponentSettingsEditor editor in _componentEditors)
        {
            _layout.Components.Add(editor.Apply());
        }

        _layout.HasChanged = true;
        Result = _layout;
        Close(Result);
    }

    private CheckBox PreviewCheck(string content, bool value)
    {
        var box = new CheckBox
        {
            Content = content,
            IsChecked = value,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        box.IsCheckedChanged += (_, _) => NotifyLayoutPreview();
        return box;
    }

    private CheckBox PreviewCheckAntiAliasing(bool value)
    {
        var box = new CheckBox
        {
            // TextBlock + NoWrap: Fluent wraps at the hyphen ("Anti-") and clips "aliasing".
            Content = new TextBlock
            {
                Text = "Anti-aliasing",
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.None,
            },
            IsChecked = value,
            HorizontalAlignment = HorizontalAlignment.Left,
            MinWidth = 140,
        };
        box.IsCheckedChanged += (_, _) => NotifyLayoutPreview();
        return box;
    }

    private void WireLivePreview(ColorSwatchButton swatch, string key, Action<ArgbColor> apply)
    {
        swatch.LivePreview = c =>
        {
            apply(c);
            LiveColorPreview?.Invoke(key, c);
            NotifyLayoutPreview();
        };
    }

    private void ApplyLiveColor(string key, ArgbColor c)
    {
        switch (key)
        {
            case "BestSegmentColor": _settings.BestSegmentColor = c; break;
            case "AheadGainingTimeColor": _settings.AheadGainingTimeColor = c; break;
            case "AheadLosingTimeColor": _settings.AheadLosingTimeColor = c; break;
            case "BehindGainingTimeColor": _settings.BehindGainingTimeColor = c; break;
            case "BehindLosingTimeColor": _settings.BehindLosingTimeColor = c; break;
            case "TextColor": _settings.TextColor = c; break;
            case "TextOutlineColor": _settings.TextOutlineColor = c; break;
            case "NotRunningColor": _settings.NotRunningColor = c; break;
            case "PersonalBestColor": _settings.PersonalBestColor = c; break;
            case "PausedColor": _settings.PausedColor = c; break;
            case "ThinSeparatorsColor": _settings.ThinSeparatorsColor = c; break;
            case "SeparatorsColor": _settings.SeparatorsColor = c; break;
            case "ShadowsColor": _settings.ShadowsColor = c; break;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
