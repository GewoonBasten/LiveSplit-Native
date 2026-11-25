using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LiveSplitNative.ComponentHost.Compat;

namespace LiveSplitNative.App;

/// <summary>
/// A component DLL's settings page (its WinForms Control) shown with the app's own controls:
/// each WinForms control at the place WinForms' layout gave it, a label as text, check boxes,
/// radio buttons (grouped by parent, as WinForms groups them), buttons (a flat colored button as
/// a color swatch), combo boxes, number boxes, text boxes and group boxes. What the user does goes
/// back to the WinForms control (its events and data bindings update the component's settings);
/// what the page changes in code (enabled, visible, values) is shown at once.
/// </summary>
internal sealed class HostedSettingsView : Canvas
{
    private static readonly List<WeakReference<HostedSettingsView>> Views = [];
    private readonly IHostControl _root;
    private readonly Dictionary<IHostControl, Control> _views = [];
    private readonly Dictionary<IHostControl, IHostControl> _parents = [];
    private bool _syncing;
    private bool _editedPosted;

    /// <param name="fillWidth">
    /// The page's room in the window: a narrower page widens to it, so its cards (groups
    /// anchored left and right) line up with the window's other edges.
    /// </param>
    public HostedSettingsView(IHostControl root, double fillWidth = 0)
    {
        _root = root;
        root.OnShown();
        System.Drawing.Size size = root.Bounds.Size;
        // The page's own margins (the designer leaves room around its controls): the window's
        // page padding replaces them, so the cards line up with the footer's buttons.
        // Measured on what shows (a panel or table holding the controls is not a margin).
        var shown = new List<System.Drawing.Rectangle>();
        void Collect(IHostControl parent, int dx, int dy)
        {
            foreach (IHostControl child in parent.Children.Where(c => c.Visible))
            {
                var at = new System.Drawing.Rectangle(child.Bounds.X + dx, child.Bounds.Y + dy, child.Bounds.Width, child.Bounds.Height);
                if (child.Kind == "Panel" && child.Children.Count > 0)
                {
                    Collect(child, at.X, at.Y);
                }
                else
                {
                    shown.Add(at);
                }
            }
        }

        Collect(root, 0, 0);
        if (shown.Count > 0)
        {
            Inset = new Thickness(
                Math.Max(0, shown.Min(c => c.X)),
                Math.Max(0, shown.Min(c => c.Y)),
                Math.Max(0, size.Width - shown.Max(c => c.Right)),
                0);
        }

        double wanted = fillWidth + Inset.Left + Inset.Right;
        _fillRight = fillWidth > 0 ? Inset.Left + fillWidth : 0;
        if (fillWidth > 0 && wanted > size.Width)
        {
            size = new System.Drawing.Size((int)wanted, size.Height);
        }

        root.PerformLayout(size);
        IndexParents(root);
        FitToPage();
        foreach (IHostControl child in root.Children)
        {
            Children.Add(Build(child, root));
        }

        Watch(root);
        Views.Add(new WeakReference<HostedSettingsView>(this));
    }

    /// <summary>
    /// As big as the page, or as its controls reach when the app's spacing made them reach past
    /// a page of a fixed (designer) size.
    /// </summary>
    private void FitToPage()
    {
        System.Drawing.Rectangle page = _root.Bounds;
        IEnumerable<IHostControl> shown = _root.Children.Where(c => c.Visible);
        int right = shown.Select(c => c.Bounds.Right).DefaultIfEmpty(0).Max();
        int bottom = (int)Math.Ceiling(shown.Select(c => c.Bounds.Bottom + ShiftOf(c, _root) + ExtraHeight(c)).DefaultIfEmpty(0).Max());
        Width = right > page.Width ? right + PageEdge : page.Width;
        Height = bottom > page.Height ? bottom + PageEdge : page.Height;
    }

    /// <summary>Where the window's cards end (page coordinates); 0 = the page's own width.</summary>
    private double _fillRight;

    /// <summary>
    /// A group on the page with nothing beside it on its right: its card reaches the window's
    /// card edge, so it lines up with the footer and the other pages, whatever width the
    /// designer gave it (its controls stay where they are).
    /// </summary>
    private double CardWidth(IHostControl model, System.Drawing.Rectangle bounds)
    {
        if (_fillRight <= 0 || model.Kind != "GroupBox" || IsNested(model)
            || _parents.GetValueOrDefault(model) is not { } parent)
        {
            return bounds.Width;
        }

        // Where the group starts on the page (it may sit in a panel or table).
        double x = bounds.X;
        for (IHostControl? p = parent; p is not null && !ReferenceEquals(p, _root); p = _parents.GetValueOrDefault(p))
        {
            x += p.Bounds.X;
        }

        bool besideRight = parent.Children.Any(c => c.Visible && !ReferenceEquals(c, model)
            && c.Bounds.X >= bounds.Right && c.Bounds.Y < bounds.Bottom && c.Bounds.Bottom > bounds.Y);
        return besideRight || _fillRight - x <= bounds.Width ? bounds.Width : _fillRight - x;
    }

    /// <summary>
    /// A list or a paragraph across its group (the widest thing in it) widens with the group's
    /// card (see CardWidth), so it fills the card as the app's own lists do.
    /// </summary>
    private double FullRowStretch(IHostControl model, System.Drawing.Rectangle bounds)
    {
        bool paragraph = model.Kind == "Label" && bounds.Height >= 2 * HostText.Measure("A", HostText.DialogFont).Height;
        if (_fillRight <= 0 || !(model.Kind == "ListView" || paragraph))
        {
            return 0;
        }

        // The group it is in, and where it sits in that group.
        double x = bounds.X;
        IHostControl? group = null;
        for (IHostControl? p = _parents.GetValueOrDefault(model); p is not null && !ReferenceEquals(p, _root); p = _parents.GetValueOrDefault(p))
        {
            if (p.Kind == "GroupBox")
            {
                group = p;
                break;
            }

            x += p.Bounds.X;
        }

        if (group is null || IsNested(group))
        {
            return 0;
        }

        double extra = CardWidth(group, group.Bounds) - group.Bounds.Width;
        if (extra <= 0)
        {
            return 0;
        }

        // Only the widest content of the group: what reaches its right side.
        double widest = 0;
        void Right(IHostControl control, double dx)
        {
            foreach (IHostControl child in control.Children.Where(c => c.Visible))
            {
                if (child.Kind == "Panel" && child.Children.Count > 0)
                {
                    Right(child, dx + child.Bounds.X);
                }
                else
                {
                    widest = Math.Max(widest, dx + child.Bounds.Right);
                }
            }
        }

        Right(group, 0);
        // A paragraph's box ends where its last word did: most of the width counts as across.
        return x + bounds.Width >= (paragraph ? widest * 0.8 : widest - 4) ? extra : 0;
    }

    /// <summary>The page's own margins around its controls (left, top, right).</summary>
    public Thickness Inset { get; private set; }

    /// <summary>The room kept past the farthest control of a page that grew (FitToPage).</summary>
    private const double PageEdge = 6;

    /// <summary>
    /// Where a group's first row may start for its heading to fit above the card (the card
    /// starts 6 under the heading, its first row a little lower).
    /// </summary>
    private static double CardContentTop => Math.Ceiling(HostText.Measure("A", HostText.DialogFont).Height) + 18;

    /// <summary>
    /// How far a group's rows move down so its heading fits above the card: a designer page puts
    /// them just under the caption on the frame.
    /// </summary>
    private double OwnShift(IHostControl model)
    {
        if (model.Kind != "GroupBox")
        {
            return 0;
        }

        int top = model.Children.Where(c => c.Visible).Select(c => c.Bounds.Y).DefaultIfEmpty(int.MaxValue).Min();
        double wanted = IsNested(model) ? NestedContentTop : CardContentTop;
        return top == int.MaxValue ? 0 : Math.Max(0, wanted - top);
    }

    /// <summary>A nested group's first row: just under its heading (no frame of its own).</summary>
    private static double NestedContentTop => Math.Ceiling(HostText.Measure("A", HostText.DialogFont).Height) + 5;

    /// <summary>
    /// A group inside a group: a part of its parent's card (its heading, then its rows), not a
    /// card of its own, as nested groups are on the app's own pages.
    /// </summary>
    private bool IsNested(IHostControl model)
    {
        for (IHostControl? p = _parents.GetValueOrDefault(model); p is not null && !ReferenceEquals(p, _root); p = _parents.GetValueOrDefault(p))
        {
            if (p.Kind == "GroupBox")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every control's parent, before anything is measured (IsNested needs it).</summary>
    private void IndexParents(IHostControl model)
    {
        foreach (IHostControl child in model.Children)
        {
            _parents[child] = model;
            IndexParents(child);
        }
    }

    /// <summary>How much taller a control shows than WinForms made it: its groups' shifts.</summary>
    private double ExtraHeight(IHostControl model)
    {
        if (model.Children.Count == 0)
        {
            return 0;
        }

        // The bottom margin stays: the lowest row moves down as far as the rows grew. A
        // container its rows already overrun (a designer table of fixed height whose rows are
        // the app's taller ones) grows to hold them.
        List<IHostControl> shown = model.Children.Where(c => c.Visible).ToList();
        if (shown.Count == 0)
        {
            return OwnShift(model);
        }

        double before = shown.Max(c => c.Bounds.Bottom);
        double after = shown.Max(c => c.Bounds.Bottom + ShiftOf(c, model) + ExtraHeight(c));
        double margin = Math.Max(0, model.Bounds.Height - before);
        return OwnShift(model) + Math.Max(0, after + margin - model.Bounds.Height);
    }

    /// <summary>
    /// How far a control moves down: as far as the controls above it in its parent grew (and
    /// moved), so nothing overlaps a group that made room for its heading.
    /// </summary>
    private double ShiftOf(IHostControl model, IHostControl parent)
    {
        double shift = 0;
        foreach (IHostControl above in parent.Children)
        {
            if (ReferenceEquals(above, model) || !above.Visible || above.Bounds.Bottom > model.Bounds.Y)
            {
                continue;
            }

            double grown = ExtraHeight(above);
            if (grown > 0)
            {
                shift = Math.Max(shift, ShiftOf(above, parent) + grown);
            }
        }

        return shift;
    }

    /// <summary>Raised (once per batch) after the page changed something: the settings may have.</summary>
    public event Action? Edited;

    /// <summary>The window showing the page a control belongs to (for its dialogs).</summary>
    public static Window? WindowOf(IHostControl? control)
    {
        foreach (WeakReference<HostedSettingsView> reference in Views.ToList())
        {
            if (!reference.TryGetTarget(out HostedSettingsView? view))
            {
                Views.Remove(reference);
                continue;
            }

            if (control is null || view.Contains(control))
            {
                if (TopLevel.GetTopLevel(view) is Window window)
                {
                    return window;
                }
            }
        }

        return null;
    }

    /// <summary>The app control showing a WinForms control (tests).</summary>
    internal Control? ViewOf(IHostControl control) => _views.GetValueOrDefault(control);

    internal IHostControl Root => _root;

    private bool Contains(IHostControl control) => ReferenceEquals(control, _root) || _views.ContainsKey(control);

    private readonly HashSet<IHostControl> _watched = [];

    private void Watch(IHostControl control)
    {
        if (_watched.Add(control))
        {
            control.Changed += (_, _) => OnModelChanged(control);
        }
    }

    private Control Build(IHostControl model, IHostControl parent)
    {
        Control view = model.Kind switch
        {
            "Label" => new Cell(new TextBlock()),
            "CheckBox" => new Cell(NewCheckBox(model)),
            "RadioButton" => new Cell(NewRadioButton(model, parent)),
            "Button" => NewButton(model),
            "ComboBox" => NewComboBox(model),
            "NumericUpDown" => NewNumericUpDown(model),
            "TrackBar" => NewTrackBar(model),
            "TextBox" => NewTextBox(model),
            "GroupBox" => NewGroupBox(model),
            "ListView" => new ListTable(),
            _ => new Canvas(),
        };
        _views[model] = view;
        _parents[model] = parent;
        if (view is Canvas canvas)
        {
            AddChildren(model, canvas);
        }
        else if (view is GroupView group)
        {
            AddChildren(model, group.Content);
        }

        Sync(model, view);
        Watch(model);
        return view;
    }

    private void AddChildren(IHostControl model, Canvas canvas)
    {
        foreach (IHostControl child in model.Children)
        {
            canvas.Children.Add(Build(child, model));
        }
    }

    private Control NewCheckBox(IHostControl model)
    {
        var box = new CheckBox { MinHeight = 0, Padding = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        box.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing && box.IsChecked != model.Checked)
            {
                Safe(() => model.SetChecked(box.IsChecked == true));
            }
        };
        return box;
    }

    private Control NewRadioButton(IHostControl model, IHostControl parent)
    {
        var radio = new RadioButton
        {
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(4, 0, 0, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            // WinForms groups radio buttons by their parent.
            GroupName = "winforms-" + RuntimeHelpers.GetHashCode(parent),
        };
        radio.IsCheckedChanged += (_, _) =>
        {
            if (!_syncing && radio.IsChecked == true && !model.Checked)
            {
                Safe(() => model.SetChecked(true));
            }
        };
        return radio;
    }

    private Control NewButton(IHostControl model)
    {
        var button = new Button
        {
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        if (model.BackColor is null)
        {
            button.Classes.Add("dialog");
        }
        else
        {
            button.Classes.Add("colorSwatch");
            button.CornerRadius = new CornerRadius(0);
            button.BorderThickness = new Thickness(1);
            button.BorderBrush = Brushes.Black;
        }

        button.Click += (_, _) => Safe(model.Click);
        return button;
    }

    private Control NewComboBox(IHostControl model)
    {
        var combo = new ComboBox { MinHeight = 0, Classes = { "settingsCombo" }, VerticalContentAlignment = VerticalAlignment.Center };
        combo.SelectionChanged += (_, _) =>
        {
            if (!_syncing && combo.SelectedIndex != model.SelectedIndex)
            {
                Safe(() => model.SetSelectedIndex(combo.SelectedIndex));
            }
        };
        return combo;
    }

    private Control NewNumericUpDown(IHostControl model)
    {
        NumericUpDown box = ComponentSettingsEditor.CreateSettingsNumericUpDown(model.Value, model.Minimum, model.Maximum);
        // The WinForms box's own size.
        box.MinHeight = 0;
        box.MinWidth = 0;
        box.MaxWidth = double.PositiveInfinity;
        box.ValueChanged += (_, _) =>
        {
            if (!_syncing && box.Value is { } value && value != model.Value)
            {
                Safe(() => model.SetValue(value));
            }
        };
        return box;
    }

    /// <summary>A TrackBar as the app's slider (whole steps; its value as a tooltip, as Sound's sliders).</summary>
    private Control NewTrackBar(IHostControl model)
    {
        var slider = new Slider
        {
            Minimum = (double)model.Minimum,
            Maximum = (double)model.Maximum,
            Value = (double)model.Value,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        SliderChrome.Compact(slider);
        ToolTip.SetTip(slider, model.Value.ToString("0", System.Globalization.CultureInfo.CurrentCulture));
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty)
            {
                return;
            }

            ToolTip.SetTip(slider, slider.Value.ToString("0", System.Globalization.CultureInfo.CurrentCulture));
            decimal value = (decimal)Math.Round(slider.Value);
            if (!_syncing && value != model.Value)
            {
                Safe(() => model.SetValue(value));
            }
        };
        return slider;
    }

    private Control NewTextBox(IHostControl model)
    {
        var box = new TextBox { MinHeight = 0, VerticalContentAlignment = VerticalAlignment.Center };
        box.TextChanged += (_, _) =>
        {
            if (!_syncing && box.Text != model.Text)
            {
                Safe(() => model.SetText(box.Text ?? ""));
            }
        };
        return box;
    }

    /// <summary>A group box: the frame from half the caption's height down, the caption on it.</summary>
    private Control NewGroupBox(IHostControl model)
    {
        var frame = new Border
        {
            BorderBrush = AppThemeController.GetDialogBrush("LsDialogBorderLightBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Background = AppThemeController.GetDialogBrush("LsDialogGroupBoxBackground"),
        };
        var caption = new TextBlock
        {
            Background = AppThemeController.GetDialogBrush("LsDialogWindowBackground"),
            Padding = new Thickness(2, 0),
        };
        var canvas = new Canvas();
        var group = new GroupView(frame, caption, canvas);
        return group;
    }

    /// <summary>
    /// The rows of a group's controls, top to bottom (in the group): the vertical extents of its
    /// controls (through a table it holds), merged where they overlap.
    /// </summary>
    private List<(double Top, double Bottom, bool List, double Left)> RowBands(IHostControl group)
    {
        var spans = new List<(double Top, double Bottom, bool List, double Left)>();
        void Collect(IHostControl control, double offset, int depth, double left)
        {
            foreach (IHostControl child in control.Children.Where(c => c.Visible))
            {
                // A panel filling the group (its table): its controls are the rows.
                if (depth == 0 && child.Kind == "Panel" && child.Children.Count > 0)
                {
                    Collect(child, offset + child.Bounds.Y + ShiftOf(child, control), depth + 1, left + child.Bounds.X);
                    continue;
                }

                double y = offset + child.Bounds.Y + ShiftOf(child, control);
                spans.Add((y, y + child.Bounds.Height + ExtraHeight(child), child.Kind == "ListView", left + child.Bounds.X));
            }
        }

        Collect(group, 0, 0, 0);
        spans.Sort();
        var bands = new List<(double Top, double Bottom, bool List, double Left)>();
        foreach ((double top, double bottom, bool list, double left) in spans)
        {
            if (bands.Count > 0 && top < bands[^1].Bottom + 1)
            {
                bands[^1] = (bands[^1].Top, Math.Max(bands[^1].Bottom, bottom), bands[^1].List || list, Math.Min(bands[^1].Left, left));
            }
            else
            {
                bands.Add((top, bottom, list, left));
            }
        }

        return bands;
    }

    /// <summary>A combo box's width besides its text: padding and the drop-down arrow.</summary>
    private const double ComboChrome = 56;

    /// <summary>What a component's page does on input; its exceptions are logged, not the app's end.</summary>
    private static void Safe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[component] settings page: " + ex);
        }
    }

    private void OnModelChanged(IHostControl model)
    {
        if (ReferenceEquals(model, _root))
        {
            IndexParents(_root);
            FitToPage();
            RebuildChildren(model, this);
        }
        else if (_views.TryGetValue(model, out Control? view))
        {
            if (view is Canvas canvas && model.Children.Count != canvas.Children.Count)
            {
                RebuildChildren(model, canvas);
            }

            if (view is GroupView group && model.Children.Count != group.Content.Children.Count)
            {
                RebuildChildren(model, group.Content);
            }

            Sync(model, view);
        }

        // A group that grew or shrank moves what is below it.
        foreach ((IHostControl other, Control otherView) in _views)
        {
            if (!ReferenceEquals(other, model))
            {
                IHostControl? parent = _parents.GetValueOrDefault(other);
                SetTop(otherView, other.Bounds.Y + (parent is null ? 0 : ShiftOf(other, parent)));
                otherView.Height = Math.Max(0, other.Bounds.Height + ExtraHeight(other));
            }
        }

        if (!_editedPosted)
        {
            _editedPosted = true;
            Dispatcher.UIThread.Post(() =>
            {
                _editedPosted = false;
                Edited?.Invoke();
            });
        }
    }

    private void RebuildChildren(IHostControl model, Canvas canvas)
    {
        if (canvas.Children.Count == model.Children.Count
            && model.Children.All(c => _views.TryGetValue(c, out Control? v) && canvas.Children.Contains(v)))
        {
            return;
        }

        canvas.Children.Clear();
        AddChildren(model, canvas);
    }

    /// <summary>The app control shows the WinForms control's state now.</summary>
    private void Sync(IHostControl model, Control view)
    {
        _syncing = true;
        try
        {
            System.Drawing.Rectangle bounds = model.Bounds;
            SetLeft(view, bounds.X);
            IHostControl? owner = _parents.GetValueOrDefault(model);
            SetTop(view, bounds.Y + (owner is null ? 0 : ShiftOf(model, owner)));
            view.Width = Math.Max(0, CardWidth(model, bounds) + FullRowStretch(model, bounds));
            view.Height = Math.Max(0, bounds.Height + ExtraHeight(model));
            view.IsVisible = model.Visible;
            view.IsEnabled = model.Enabled;
            switch (view)
            {
                case Cell { Child: TextBlock label } cell:
                    // The settings look: a label without its trailing colon.
                    label.Text = model.Text.EndsWith(':') ? model.Text[..^1].TrimEnd() : model.Text;
                    label.Opacity = model.Enabled ? 1 : 0.45;
                    // A label WinForms gave more than one line (AutoSize with a MaximumSize width)
                    // wraps in its box.
                    cell.Wrap = bounds.Height >= 2 * HostText.Measure("A", HostText.DialogFont).Height;
                    // A paragraph widened with its card (fewer lines) sits in the middle of its row.
                    cell.Alignment = cell.Wrap && FullRowStretch(model, bounds) > 0 ? 0x010 : model.TextAlign;
                    break;
                case Cell { Child: CheckBox box }:
                    box.Content = model.Text;
                    box.IsChecked = model.Checked;
                    break;
                case Cell { Child: RadioButton radio }:
                    radio.Content = model.Text;
                    radio.IsChecked = model.Checked;
                    break;
                case Button button:
                    if (model.BackColor is { } color)
                    {
                        button.Background = new SolidColorBrush(Color.FromArgb(color.A == 0 ? (byte)40 : color.A, color.R, color.G, color.B));
                    }
                    else
                    {
                        button.Content = model.Text;
                    }

                    break;
                case ComboBox combo:
                    if (!combo.Items.Cast<object>().Select(i => i?.ToString()).SequenceEqual(model.Items))
                    {
                        combo.ItemsSource = model.Items.ToList();
                    }

                    combo.SelectedIndex = model.SelectedIndex;
                    // The app's font is wider than the one the page was made for: a combo box
                    // whose items would be cut grows to show them, as far as its parent allows.
                    double needed = model.Items.Select(i => (double)HostText.Measure(i, HostText.DialogFont).Width).DefaultIfEmpty(0).Max() + ComboChrome;
                    if (needed > bounds.Width && _parents.TryGetValue(model, out IHostControl? parent))
                    {
                        combo.Width = Math.Max(bounds.Width, Math.Min(needed, parent.Bounds.Width - bounds.X - 3));
                    }

                    break;
                case NumericUpDown number:
                    number.Minimum = model.Minimum;
                    number.Maximum = model.Maximum;
                    number.FormatString = "F" + model.DecimalPlaces;
                    number.Value = model.Value;
                    break;
                case Slider slider:
                    slider.Minimum = (double)model.Minimum;
                    slider.Maximum = (double)model.Maximum;
                    slider.Value = (double)model.Value;
                    break;
                case TextBox text:
                    if (text.Text != model.Text)
                    {
                        text.Text = model.Text;
                    }

                    break;
                case GroupView group:
                    group.Caption.Text = model.Text;
                    // The settings look: a card under its heading. A designer page's rows sit
                    // just under the caption: they move down (OwnShift) so the heading fits
                    // above the card, and what is below follows. A group inside a group is a
                    // part of that card: its heading over its rows, no frame or lines of its own.
                    group.ContentShift = OwnShift(model);
                    group.IsNested = IsNested(model);
                    group.CaptionX = model.Children.Where(c => c.Visible).Select(c => (double)c.Bounds.X).DefaultIfEmpty(4).Min();
                    group.IsCard = true;
                    group.SetRows(group.IsNested
                        ? []
                        : RowBands(model).Select(b => (b.Top + group.ContentShift, b.Bottom + group.ContentShift, b.List, b.Left)).ToList());

                    break;
                case ListTable table when model.ListView is { } list:
                    table.Show(list);
                    break;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// A check box or radio button in its WinForms bounds: centered on the line, as WinForms
    /// draws its glyph and text in the middle (the app's control may be taller than the box).
    /// </summary>
    internal sealed class Cell : Panel
    {
        public Cell(Control child)
        {
            Child = child;
            ClipToBounds = false;
            Children.Add(child);
        }

        public Control Child { get; }

        /// <summary>
        /// ContentAlignment of a label's text (TopLeft 0x001 … BottomRight 0x400); check boxes and
        /// radio buttons are MiddleLeft.
        /// </summary>
        private int _alignment = 0x010;

        public int Alignment
        {
            get => _alignment;
            set
            {
                _alignment = value;
                InvalidateArrange();
            }
        }

        private bool _wrap;

        /// <summary>The text wraps at the box's width (a label of more than one line).</summary>
        public bool Wrap
        {
            get => _wrap;
            set
            {
                _wrap = value;
                if (Child is TextBlock text)
                {
                    text.TextWrapping = value ? TextWrapping.Wrap : TextWrapping.NoWrap;
                }

                InvalidateMeasure();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Child.Measure(Wrap && !double.IsNaN(Width) ? new Size(Width, double.PositiveInfinity) : Size.Infinity);
            return new Size(0, 0);
        }

        // The child at its own size, placed by the alignment in the box (not squeezed, so
        // neither clipped nor wrapped or cut short: the app's font is a little wider than the
        // one the page was designed with, so a right-aligned label may reach left of its box).
        protected override Size ArrangeOverride(Size finalSize)
        {
            double width = Wrap ? finalSize.Width : Math.Max(finalSize.Width, Child.DesiredSize.Width);
            // A wrapped text at its own height, so its alignment places it in the box.
            double height = Wrap ? Child.DesiredSize.Height : Math.Max(finalSize.Height, Child.DesiredSize.Height);
            double x = (Alignment & 0x444) != 0 ? finalSize.Width - width
                : (Alignment & 0x222) != 0 ? (finalSize.Width - width) / 2
                : 0;
            double y = (Alignment & 0x700) != 0 ? finalSize.Height - height
                : (Alignment & 0x070) != 0 ? (finalSize.Height - height) / 2
                : 0;
            if (Child is TextBlock text)
            {
                text.TextAlignment = (Alignment & 0x444) != 0 ? TextAlignment.Right : (Alignment & 0x222) != 0 ? TextAlignment.Center : TextAlignment.Left;
            }

            Child.Arrange(new Rect(x, y, width, height));
            return finalSize;
        }
    }

    /// <summary>
    /// A WinForms ListView in Details view, as a table in the app's style: the column headers
    /// (plain, as WinForms shows them), then one row per item, with grid lines when the
    /// ListView has them; cells at WinForms' column widths and row heights, aligned as their
    /// columns are.
    /// </summary>
    internal sealed class ListTable : Border
    {
        private HostListView? _shown;

        public ListTable()
        {
            // The rows are clipped to the rounded shape and the frame is drawn over them, so the
            // header's color does not cover the frame's rounded corners.
            CornerRadius = new CornerRadius(6);
            ClipToBounds = true;
            Bind(BackgroundProperty, this.GetResourceObservable("LsDialogGroupBoxNestedBackground"));
            _frame.Bind(BorderBrushProperty, _frame.GetResourceObservable("LsDialogBorderBrush"));
        }

        private readonly Border _frame = new()
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            IsHitTestVisible = false,
        };

        /// <summary>The rows' texts as shown (tests).</summary>
        public IReadOnlyList<IReadOnlyList<string>> Rows => _shown?.Rows.Select(r => r.Cells).ToList() ?? [];

        public void Show(HostListView list)
        {
            if (_shown is { } shown
                && shown.HeaderHeight == list.HeaderHeight
                && shown.RowHeight == list.RowHeight
                && shown.ShowHeader == list.ShowHeader
                && shown.GridLines == list.GridLines
                && shown.Columns.SequenceEqual(list.Columns)
                && shown.Rows.Count == list.Rows.Count
                && shown.Rows.Zip(list.Rows).All(p => p.First.Bold == p.Second.Bold && p.First.Cells.SequenceEqual(p.Second.Cells)))
            {
                return;
            }

            _shown = list;
            // The columns keep their proportions and fill the list's width (it may be wider than
            // WinForms made it: FullRowStretch).
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            foreach (HostListColumn column in list.Columns)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(Math.Max(1, column.Width), GridUnitType.Star));
            }

            int row = 0;
            if (list.ShowHeader)
            {
                grid.RowDefinitions.Add(new RowDefinition(list.HeaderHeight, GridUnitType.Pixel));
                for (int c = 0; c < list.Columns.Count; c++)
                {
                    var header = new Border
                    {
                        BorderThickness = new Thickness(0, 0, c < list.Columns.Count - 1 ? 1 : 0, 1),
                        Child = TextCell(list.Columns[c].Text, list.Columns[c].Alignment, FontWeight.Normal, "LsDialogHeaderForeground"),
                    };
                    header.Bind(BackgroundProperty, header.GetResourceObservable("LsDialogHeaderBackground"));
                    header.Bind(BorderBrushProperty, header.GetResourceObservable("LsDialogHeaderBorderBrush"));
                    Grid.SetColumn(header, c);
                    grid.Children.Add(header);
                }

                row++;
            }

            for (int index = 0; index < list.Rows.Count; index++)
            {
                HostListRow item = list.Rows[index];
                bool last = index == list.Rows.Count - 1;
                // The last row reaches the list's frame (WinForms leaves a few pixels below it)
                // and has no line of its own under it: the frame is its bottom edge.
                grid.RowDefinitions.Add(last
                    ? new RowDefinition(1, GridUnitType.Star) { MinHeight = list.RowHeight }
                    : new RowDefinition(list.RowHeight, GridUnitType.Pixel));
                for (int c = 0; c < list.Columns.Count; c++)
                {
                    string text = c < item.Cells.Count ? item.Cells[c] : "";
                    var cell = new Border
                    {
                        BorderThickness = list.GridLines ? new Thickness(0, 0, c < list.Columns.Count - 1 ? 1 : 0, last ? 0 : 1) : new Thickness(0),
                        Child = TextCell(text, list.Columns[c].Alignment, item.Bold ? FontWeight.Bold : FontWeight.Normal, "LsDialogForeground"),
                    };
                    cell.Bind(BorderBrushProperty, cell.GetResourceObservable("LsDialogListSeparatorBrush"));
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, c);
                    grid.Children.Add(cell);
                }

                row++;
            }

            // The frame's 1 px inside the rounded shape, the rows within it.
            grid.Margin = new Thickness(1);
            if (_frame.Parent is Panel previous)
            {
                previous.Children.Remove(_frame);
            }

            Child = new Panel { Children = { grid, _frame } };
        }

        private static TextBlock TextCell(string text, int alignment, FontWeight weight, string foreground)
        {
            var block = new TextBlock
            {
                Text = text,
                FontWeight = weight,
                Padding = new Thickness(8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = alignment switch
                {
                    1 => TextAlignment.Right,
                    2 => TextAlignment.Center,
                    _ => TextAlignment.Left,
                },
            };
            block.Bind(TextBlock.ForegroundProperty, block.GetResourceObservable(foreground));
            return block;
        }
    }

    /// <summary>Hairlines across a card at the given heights (between its rows).</summary>
    private sealed class RowLines : Control
    {
        public static readonly StyledProperty<IBrush?> BrushProperty =
            AvaloniaProperty.Register<RowLines, IBrush?>(nameof(Brush));

        static RowLines() => AffectsRender<RowLines>(BrushProperty);

        public IBrush? Brush
        {
            get => GetValue(BrushProperty);
            set => SetValue(BrushProperty, value);
        }

        public List<double> Ys { get; set; } = [];

        public override void Render(DrawingContext context)
        {
            if (Brush is null)
            {
                return;
            }

            foreach (double y in Ys)
            {
                context.FillRectangle(Brush, new Rect(0, y, Bounds.Width, 1));
            }
        }
    }

    /// <summary>
    /// A WinForms GroupBox: frame, caption and the children's canvas over both. As a card (the
    /// settings look): the caption as a heading above a rounded card; otherwise WinForms' frame
    /// with the caption on its top line.
    /// </summary>
    private sealed class GroupView : Panel
    {
        private bool _isCard;

        public GroupView(Border frame, TextBlock caption, Canvas content)
        {
            Frame = frame;
            Caption = caption;
            Content = content;
            caption.HorizontalAlignment = HorizontalAlignment.Left;
            caption.VerticalAlignment = VerticalAlignment.Top;
            caption.Margin = new Thickness(6, 0, 0, 0);
            Children.Add(frame);
            Children.Add(caption);
            Children.Add(content);
        }

        public Border Frame { get; }

        public TextBlock Caption { get; }

        public Canvas Content { get; }

        private readonly RowLines _lines = new();

        /// <summary>
        /// The card around its rows: hairlines halfway between them (none next to a list, which
        /// has its own frame), and the card's edges as far from the first and last row as half
        /// the room between rows, so every row sits in the middle of its band.
        /// </summary>
        public void SetRows(List<(double Top, double Bottom, bool List, double Left)> bands)
        {
            var ys = new List<double>();
            var gaps = new List<double>();
            for (int i = 1; i < bands.Count; i++)
            {
                gaps.Add(bands[i].Top - bands[i - 1].Bottom);
                if (!bands[i - 1].List && !bands[i].List)
                {
                    ys.Add(Math.Round((bands[i - 1].Bottom + bands[i].Top) / 2));
                }
            }

            gaps.Sort();
            double pad = gaps.Count > 0 ? Math.Clamp(Math.Round(gaps[gaps.Count / 2] / 2), 6, 12) : 10;
            // A card holding a list (a framed table): the card's edge as far from it above and
            // below as at the sides, so the table sits evenly in the card.
            if (bands.Any(b => b.List))
            {
                pad = Math.Clamp(bands.Min(b => b.Left), 8, 24);
            }
            _frameTop = bands.Count > 0 ? bands[0].Top - pad : double.NaN;
            _frameBottom = bands.Count > 0 ? bands[^1].Bottom + pad : double.NaN;
            _lines.Ys = ys;
            _lines.InvalidateVisual();
            InvalidateArrange();
        }

        private double _frameTop = double.NaN;
        private double _frameBottom = double.NaN;

        /// <summary>How far the rows move down inside the group (see OwnShift).</summary>
        public double ContentShift { get; set; }

        /// <summary>Where a nested group's heading starts: over its rows' left edge.</summary>
        public double CaptionX { get; set; } = 4;

        /// <summary>A group inside a card: its heading and rows, no frame (set before IsCard).</summary>
        public bool IsNested
        {
            get => _isNested;
            set
            {
                _isNested = value;
                Frame.IsVisible = !value;
                InvalidateArrange();
            }
        }

        private bool _isNested;

        public bool IsCard
        {
            get => _isCard;
            set
            {
                if (_isCard == value)
                {
                    return;
                }

                _isCard = value;
                if (value)
                {
                    Frame.Bind(Border.BackgroundProperty, Frame.GetResourceObservable("LsDialogCardBackground"));
                    Frame.Bind(Border.BorderBrushProperty, Frame.GetResourceObservable("LsDialogCardBorderBrush"));
                    Frame.CornerRadius = new CornerRadius(8);
                    Caption.Background = null;
                    Caption.Padding = new Thickness(0);
                    Caption.Margin = new Thickness(4, 0, 0, 0);
                    Caption.FontWeight = FontWeight.SemiBold;
                    _lines.Bind(RowLines.BrushProperty, _lines.GetResourceObservable("LsDialogRowSeparatorBrush"));
                    Children.Insert(1, _lines);
                }

                InvalidateArrange();
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Caption.Measure(finalSize);
            double top = IsCard
                ? Math.Round(Caption.DesiredSize.Height) + 6
                : Math.Round(Caption.DesiredSize.Height / 2);
            if (IsNested)
            {
                _lines.Arrange(new Rect(1, 0, Math.Max(0, finalSize.Width - 2), finalSize.Height));
                Caption.Arrange(new Rect(new Point(CaptionX, 0), Caption.DesiredSize));
                Content.Arrange(new Rect(0, ContentShift, finalSize.Width, Math.Max(0, finalSize.Height - ContentShift)));
                return finalSize;
            }

            double bottom = finalSize.Height;
            double captionY = 0;
            if (IsCard && !double.IsNaN(_frameTop))
            {
                // The card hugs its rows (SetRows); the heading stays 6 above it.
                top = Math.Max(top, _frameTop);
                captionY = top - Math.Round(Caption.DesiredSize.Height) - 6;
                bottom = Math.Min(finalSize.Height, Math.Max(top, _frameBottom));
            }

            Frame.Arrange(new Rect(0, top, finalSize.Width, Math.Max(0, bottom - top)));
            _lines.Arrange(new Rect(1, 0, Math.Max(0, finalSize.Width - 2), finalSize.Height));
            Caption.Arrange(new Rect(new Point(IsCard ? 4 : 6, captionY), Caption.DesiredSize));
            Content.Arrange(new Rect(0, ContentShift, finalSize.Width, Math.Max(0, finalSize.Height - ContentShift)));
            return finalSize;
        }
    }
}
