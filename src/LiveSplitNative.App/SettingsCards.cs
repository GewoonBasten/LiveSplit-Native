using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;

namespace LiveSplitNative.App;

/// <summary>
/// The settings look Basten chose (27 Oct, the spacing sketch): groups as white rounded cards
/// with their heading above, rows of one height with a hairline between them, and the fields of
/// a window starting at one place (its label column). See <see cref="SettingsSection"/>,
/// <see cref="SettingsCard"/> and <see cref="SettingsField"/>.
/// </summary>
public static class SettingsLayout
{
    /// <summary>
    /// The width of a window's label column (inherited): every <see cref="SettingsField"/> under
    /// it starts its field there, so fields line up across a window's cards and tabs.
    /// </summary>
    public static readonly AttachedProperty<double> LabelWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("LabelWidth", typeof(SettingsLayout), 180, inherits: true);

    public static double GetLabelWidth(Control control) => control.GetValue(LabelWidthProperty);

    public static void SetLabelWidth(Control control, double value) => control.SetValue(LabelWidthProperty, value);

    /// <summary>The gap between a window's sections and cards.</summary>
    public const double SectionGap = 22;

    /// <summary>How far a dependent option (shown under the one it depends on) is indented.</summary>
    public const double IndentStep = 26;
}

/// <summary>A heading above a card (a group box's caption): the heading, then its card.</summary>
public class SettingsSection : StackPanel
{
    public static readonly StyledProperty<string?> HeaderProperty =
        AvaloniaProperty.Register<SettingsSection, string?>(nameof(Header));

    private readonly TextBlock _header = new() { Classes = { "sectionTitle" } };

    public SettingsSection()
    {
        Spacing = 8;
        Children.Add(_header);
        _header.IsVisible = false;
    }

    public string? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>The heading (tests and translation).</summary>
    public TextBlock HeaderBlock => _header;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty)
        {
            _header.Text = Header;
            _header.IsVisible = !string.IsNullOrEmpty(Header);
        }
    }
}

/// <summary>
/// A card of settings rows: a rounded box, one row per child (at least <see cref="MinRowHeight"/>
/// tall, the child centred in it), a hairline between rows. A child with
/// <see cref="IndentProperty"/> set is indented under the row above (an option that depends on it).
/// </summary>
public class SettingsCard : Control
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Panel.BackgroundProperty.AddOwner<SettingsCard>();

    public static readonly StyledProperty<IBrush?> BorderBrushProperty =
        Border.BorderBrushProperty.AddOwner<SettingsCard>();

    public static readonly StyledProperty<IBrush?> SeparatorBrushProperty =
        AvaloniaProperty.Register<SettingsCard, IBrush?>(nameof(SeparatorBrush));

    public static readonly StyledProperty<double> MinRowHeightProperty =
        AvaloniaProperty.Register<SettingsCard, double>(nameof(MinRowHeight), 36);

    public static readonly StyledProperty<double> CornerRadiusValueProperty =
        AvaloniaProperty.Register<SettingsCard, double>(nameof(CornerRadiusValue), 8);

    /// <summary>The indent level of a row (0, 1, 2…).</summary>
    public static readonly AttachedProperty<int> IndentProperty =
        AvaloniaProperty.RegisterAttached<SettingsCard, Control, int>("Indent");

    /// <summary>A row with its own padding (a table): laid out edge to edge.</summary>
    public static readonly AttachedProperty<bool> FullBleedProperty =
        AvaloniaProperty.RegisterAttached<SettingsCard, Control, bool>("FullBleed");

    /// <summary>Space left and right of a row's content.</summary>
    public const double HorizontalPadding = 14;

    /// <summary>Space above and below a row's content (a 26 px field makes a 40 px row).</summary>
    public const double VerticalPadding = 7;

    static SettingsCard()
    {
        AffectsRender<SettingsCard>(BackgroundProperty, BorderBrushProperty, SeparatorBrushProperty, CornerRadiusValueProperty);
        AffectsMeasure<SettingsCard>(MinRowHeightProperty);
        IndentProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as SettingsCard)?.InvalidateMeasure());
        FullBleedProperty.Changed.AddClassHandler<Control>((c, _) => (c.Parent as SettingsCard)?.InvalidateMeasure());
    }

    public SettingsCard()
    {
        ClipToBounds = true;
        Children.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is { } removed)
            {
                foreach (Control child in removed)
                {
                    LogicalChildren.Remove(child);
                    VisualChildren.Remove(child);
                }
            }

            if (e.NewItems is { } added)
            {
                foreach (Control child in added)
                {
                    LogicalChildren.Add(child);
                    VisualChildren.Add(child);
                }
            }

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                LogicalChildren.Clear();
                VisualChildren.Clear();
            }

            InvalidateMeasure();
        };
    }

    /// <summary>The rows.</summary>
    [Content]
    public Controls Children { get; } = [];

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public IBrush? SeparatorBrush
    {
        get => GetValue(SeparatorBrushProperty);
        set => SetValue(SeparatorBrushProperty, value);
    }

    public double MinRowHeight
    {
        get => GetValue(MinRowHeightProperty);
        set => SetValue(MinRowHeightProperty, value);
    }

    public double CornerRadiusValue
    {
        get => GetValue(CornerRadiusValueProperty);
        set => SetValue(CornerRadiusValueProperty, value);
    }

    public static int GetIndent(Control control) => control.GetValue(IndentProperty);

    public static void SetIndent(Control control, int value) => control.SetValue(IndentProperty, value);

    public static bool GetFullBleed(Control control) => control.GetValue(FullBleedProperty);

    public static void SetFullBleed(Control control, bool value) => control.SetValue(FullBleedProperty, value);

    private readonly List<(Control Child, double Top, double Height)> _rows = [];

    private double Left(Control child) =>
        GetFullBleed(child) ? 0 : HorizontalPadding + (GetIndent(child) * SettingsLayout.IndentStep);

    private double Right(Control child) => GetFullBleed(child) ? 0 : HorizontalPadding;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0;
        double height = 0;
        foreach (Control child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            double inset = Left(child) + Right(child);
            child.Measure(new Size(Math.Max(0, availableSize.Width - inset - 2), double.PositiveInfinity));
            double row = GetFullBleed(child)
                ? child.DesiredSize.Height
                : Math.Max(MinRowHeight, child.DesiredSize.Height + (2 * VerticalPadding));
            height += row + (height > 0 ? 1 : 0);
            width = Math.Max(width, child.DesiredSize.Width + inset + 2);
        }

        return new Size(width, height + 2);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _rows.Clear();
        double y = 1;
        foreach (Control child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }

            if (_rows.Count > 0)
            {
                y += 1; // the hairline
            }

            double row = GetFullBleed(child)
                ? child.DesiredSize.Height
                : Math.Max(MinRowHeight, child.DesiredSize.Height + (2 * VerticalPadding));
            double left = Left(child) + 1;
            double width = Math.Max(0, finalSize.Width - left - Right(child) - 1);
            double height = GetFullBleed(child) ? row : Math.Min(row, child.DesiredSize.Height);
            child.Arrange(new Rect(left, y + ((row - height) / 2), width, height));
            _rows.Add((child, y, row));
            y += row;
        }

        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        double radius = CornerRadiusValue;
        context.DrawRectangle(Background, null, new RoundedRect(bounds, radius));
        if (SeparatorBrush is { } separator)
        {
            for (int i = 1; i < _rows.Count; i++)
            {
                double top = _rows[i].Top - 1;
                double left = GetIndent(_rows[i].Child) > 0 ? Left(_rows[i].Child) + 1 : 1;
                context.FillRectangle(separator, new Rect(left, top, Math.Max(0, bounds.Width - left - 1), 1));
            }
        }

        if (BorderBrush is { } border)
        {
            context.DrawRectangle(null, new Pen(border, 1), new RoundedRect(bounds.Deflate(0.5), radius));
        }
    }
}

/// <summary>
/// A labelled field in a card: the label in the window's label column
/// (<see cref="SettingsLayout.LabelWidthProperty"/>), an optional grey hint after it (a unit such
/// as "(Hz)"), then the field.
/// </summary>
public class SettingsField : Grid
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<SettingsField, string?>(nameof(Label));

    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<SettingsField, string?>(nameof(Hint));

    public static readonly StyledProperty<Control?> FieldProperty =
        AvaloniaProperty.Register<SettingsField, Control?>(nameof(Field));

    private readonly TextBlock _label = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Classes = { "fieldLabel" } };

    public SettingsField()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        Children.Add(_label);
        UpdateLabel();
        UpdateColumn();
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    [Content]
    public Control? Field
    {
        get => GetValue(FieldProperty);
        set => SetValue(FieldProperty, value);
    }

    /// <summary>The label (tests and translation).</summary>
    public TextBlock LabelBlock => _label;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == HintProperty)
        {
            UpdateLabel();
        }
        else if (change.Property == FieldProperty)
        {
            if (change.OldValue is Control old)
            {
                Children.Remove(old);
            }

            if (change.NewValue is Control field)
            {
                SetColumn(field, 1);
                field.VerticalAlignment = VerticalAlignment.Center;
                if (field.HorizontalAlignment == HorizontalAlignment.Stretch && field is not (TextBox or Panel))
                {
                    field.HorizontalAlignment = HorizontalAlignment.Left;
                }

                Children.Add(field);
            }
        }
        else if (change.Property == SettingsLayout.LabelWidthProperty)
        {
            UpdateColumn();
        }
        else if (change.Property == SettingsCard.IndentProperty)
        {
            UpdateColumn();
        }
    }

    private void UpdateColumn()
    {
        // An indented field's label is that much narrower, so its field still lines up.
        double width = SettingsLayout.GetLabelWidth(this) - (SettingsCard.GetIndent(this) * SettingsLayout.IndentStep);
        _label.Width = Math.Max(0, width);
    }

    /// <summary>
    /// The label as shown: Label is the Windows text (it is what translation looks up), shown
    /// without its trailing colon and with a trailing "(…)" (a unit) in grey, as the hint.
    /// </summary>
    internal static (string Text, string? Hint) Split(string? label)
    {
        string text = (label ?? "").TrimEnd();
        if (text.EndsWith(':') || text.EndsWith('：'))
        {
            text = text[..^1].TrimEnd();
        }

        if (text.Length > 0 && (text[^1] == ')' || text[^1] == '）'))
        {
            int open = Math.Max(text.LastIndexOf('('), text.LastIndexOf('（'));
            if (open > 0)
            {
                return (text[..open].TrimEnd(), text[open..]);
            }
        }

        return (text, null);
    }

    private void UpdateLabel()
    {
        _label.Inlines ??= [];
        _label.Inlines.Clear();
        (string text, string? unit) = Split(Label);
        _label.Inlines.Add(new Run(text));
        string? shownHint = Hint ?? unit;
        if (!string.IsNullOrEmpty(shownHint))
        {
            var hint = new Run(" " + shownHint);
            hint.Classes.Add("hint");
            _label.Inlines.Add(hint);
        }
    }
}
