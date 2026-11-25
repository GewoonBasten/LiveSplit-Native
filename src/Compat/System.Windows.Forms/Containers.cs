// The containers of System.Windows.Forms: ScrollableControl, ContainerControl (AutoScale),
// UserControl, Panel, GroupBox, TableLayoutPanel (its layout) and Form.
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using LiveSplitNative.ComponentHost.Compat;

namespace System.Windows.Forms;

public class ScrollableControl : Control
{
    public virtual bool AutoScroll { get; set; }

    public Size AutoScrollMinSize { get; set; }

    public Point AutoScrollPosition { get; set; }

    public Size AutoScrollMargin { get; set; }

    public void ScrollControlIntoView(Control activeControl)
    {
    }
}

public class ContainerControl : ScrollableControl
{
    private SizeF _autoScaleDimensions;
    private bool _scaled;

    public SizeF AutoScaleDimensions
    {
        get => _autoScaleDimensions;
        set => _autoScaleDimensions = value;
    }

    public AutoScaleMode AutoScaleMode { get; set; } = AutoScaleMode.Inherit;

    /// <summary>
    /// The dimensions of the font the page is shown in (WinForms: the average width of the
    /// alphabet, and the font height), here the app's dialog font.
    /// </summary>
    public SizeF CurrentAutoScaleDimensions
    {
        get
        {
            if (AutoScaleMode == AutoScaleMode.Dpi)
            {
                return new SizeF(96, 96);
            }

            SizeF alphabet = HostText.Measure("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ", Font.ToHostFont());
            return new SizeF((float)Math.Round(alphabet.Width / 52f), (float)Math.Ceiling(alphabet.Height));
        }
    }

    public SizeF AutoScaleFactor =>
        _autoScaleDimensions.Width <= 0 || _autoScaleDimensions.Height <= 0 || AutoScaleMode is AutoScaleMode.None or AutoScaleMode.Inherit
            ? new SizeF(1, 1)
            : new SizeF(CurrentAutoScaleDimensions.Width / _autoScaleDimensions.Width, CurrentAutoScaleDimensions.Height / _autoScaleDimensions.Height);

    public Control ActiveControl { get; set; }

    public Form ParentForm => FindForm();

    /// <summary>ContainerControl.PerformAutoScale: the page, its children and their margins grow with the font.</summary>
    public void PerformAutoScale()
    {
        if (_scaled)
        {
            return;
        }

        _scaled = true;
        SizeF factor = AutoScaleFactor;
        if (factor.Width == 1 && factor.Height == 1)
        {
            return;
        }

        ScaleCore(factor);
        Location = Point.Empty;
    }

    public bool Validate() => true;

    public bool Validate(bool checkAutoValidate) => true;

    public virtual bool ValidateChildren() => true;

    protected override void Dispose(bool disposing) => base.Dispose(disposing);
}

public class UserControl : ContainerControl
{
    public event EventHandler Load;

    public virtual AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    public BorderStyle BorderStyle { get; set; }

    public virtual AutoValidate AutoValidate { get; set; }

    protected override Size DefaultSize => new(150, 150);

    internal override Size GetContentSize() => AutoSize ? DefaultLayoutPreferredSize(Padding) : Size;

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        OnLoad(EventArgs.Empty);
    }

    protected override void OnLoad(EventArgs e) => Load?.Invoke(this, e);
}

public class Panel : ScrollableControl
{
    public virtual AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    public BorderStyle BorderStyle { get; set; }

    protected override Size DefaultSize => new(200, 100);

    protected override Padding DefaultMargin => new(3);

    internal override Size GetContentSize() => AutoSize ? DefaultLayoutPreferredSize(Padding) : Size;
}

public class GroupBox : Control
{
    public virtual AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    public bool UseCompatibleTextRendering { get; set; }

    protected override Size DefaultSize => new(200, 100);

    /// <summary>WinForms' 3, or the app's spacing (HostText.Spacing).</summary>
    protected override Padding DefaultPadding => new(
        HostText.Spacing.GroupPaddingX,
        HostText.Spacing.GroupPaddingTop,
        HostText.Spacing.GroupPaddingX,
        HostText.Spacing.GroupPaddingBottom);

    /// <summary>WinForms' 3, with the app's gap between group boxes (HostText.Spacing).</summary>
    protected override Padding DefaultMargin => new(3, HostText.Spacing.GroupGap / 2, 3, HostText.Spacing.GroupGap / 2);

    /// <summary>GroupBox.DisplayRectangle: below the caption, inside the padding.</summary>
    public override Rectangle DisplayRectangle => new(
        Padding.Left,
        FontHeight + TopPadding,
        Math.Max(0, Width - Padding.Horizontal),
        Math.Max(0, Height - FontHeight - TopPadding - Padding.Bottom));

    /// <summary>
    /// The room between the caption and the rows. In the app's spacing a group inside a group is
    /// a heading over its rows within its parent's card (no card of its own), so its rows follow
    /// the heading as closely as the card's own rows follow the card's top: none of the room a
    /// card keeps under its heading.
    /// </summary>
    /// <summary>
    /// In the app's spacing a group box is as tall as its rows need (its designer height was
    /// worked out for WinForms' smaller rows and fonts): the app's taller rows neither run out of
    /// its card nor leave room under them.
    /// </summary>
    internal bool FitsContent => HostText.Spacing != HostSpacing.WinForms && !AutoSize;

    /// <summary>The height its rows need: the caption, the room under it, the rows, the padding.</summary>
    internal int ContentHeight =>
        DefaultLayoutPreferredSize(new Padding(Padding.Left, FontHeight + TopPadding, Padding.Right, Padding.Bottom)).Height;

    /// <summary>The size it is laid out at by a table (see <see cref="FitsContent"/>).</summary>
    internal static Size LayoutSize(Control control) =>
        control is GroupBox { FitsContent: true } group ? new Size(control.Size.Width, group.ContentHeight) : control.Size;

    private int TopPadding =>
        HostText.Spacing != HostSpacing.WinForms && IsInGroup ? 0 : Padding.Top;

    private bool IsInGroup
    {
        get
        {
            for (Control parent = Parent; parent is not null; parent = parent.Parent)
            {
                if (parent is GroupBox)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal override string HostKind => "GroupBox";

    /// <summary>AutoSize: the children (under the caption, inside the padding), at least the caption.</summary>
    internal override Size GetContentSize()
    {
        if (!AutoSize)
        {
            return FitsContent ? new Size(Width, ContentHeight) : Size;
        }

        Size size = DefaultLayoutPreferredSize(new Padding(Padding.Left, FontHeight + TopPadding, Padding.Right, Padding.Bottom));
        return new Size(Math.Max(size.Width, MeasureText(Text).Width + 14), size.Height);
    }
}

public class Form : ContainerControl
{
    public event EventHandler Load;

    public event FormClosingEventHandler FormClosing;

    public event FormClosedEventHandler FormClosed;

    public DialogResult DialogResult { get; set; }

    public bool TopMost { get; set; }

    public bool ShowInTaskbar { get; set; } = true;

    public bool ShowIcon { get; set; } = true;

    public bool MaximizeBox { get; set; } = true;

    public bool MinimizeBox { get; set; } = true;

    public FormBorderStyle FormBorderStyle { get; set; } = FormBorderStyle.Sizable;

    public FormStartPosition StartPosition { get; set; }

    public FormWindowState WindowState { get; set; }

    public Button AcceptButton { get; set; }

    public Button CancelButton { get; set; }

    public Icon Icon { get; set; }

    public Form Owner { get; set; }

    public double Opacity { get; set; } = 1;

    protected override Size DefaultSize => new(300, 300);

    public new void Show() => Show(null);

    public void Show(IWin32Window owner) => ShowDialog(owner);

    public DialogResult ShowDialog() => ShowDialog(null);

    /// <summary>
    /// A component's own window. Forms the app knows (WinFormsColor's ColorPickerDialog) show the
    /// app's dialog; others are not shown yet and answer Cancel.
    /// </summary>
    public DialogResult ShowDialog(IWin32Window owner)
    {
        Load?.Invoke(this, EventArgs.Empty);
        DialogResult = RunModal(owner);
        FormClosing?.Invoke(this, new FormClosingEventArgs(CloseReason.UserClosing, false));
        FormClosed?.Invoke(this, new FormClosedEventArgs(CloseReason.UserClosing));
        return DialogResult;
    }

    protected internal virtual DialogResult RunModal(IWin32Window owner)
    {
        Console.Error.WriteLine($"[component] {GetType().FullName} (a window of its own) is not shown yet.");
        return DialogResult.Cancel;
    }

    public void Close()
    {
        FormClosing?.Invoke(this, new FormClosingEventArgs(CloseReason.UserClosing, false));
        FormClosed?.Invoke(this, new FormClosedEventArgs(CloseReason.UserClosing));
    }

    public void Activate()
    {
    }
}

public enum FormBorderStyle
{
    None = 0,
    FixedSingle = 1,
    Fixed3D = 2,
    FixedDialog = 3,
    Sizable = 4,
    FixedToolWindow = 5,
    SizableToolWindow = 6,
}

public enum FormStartPosition
{
    Manual = 0,
    CenterScreen = 1,
    WindowsDefaultLocation = 2,
    WindowsDefaultBounds = 3,
    CenterParent = 4,
}

public enum FormWindowState
{
    Normal = 0,
    Minimized = 1,
    Maximized = 2,
}

public enum CloseReason
{
    None = 0,
    WindowsShutDown = 1,
    MdiFormClosing = 2,
    UserClosing = 3,
    TaskManagerClosing = 4,
    FormOwnerClosing = 5,
    ApplicationExitCall = 6,
}

public enum AutoValidate
{
    Inherit = -1,
    Disable = 0,
    EnablePreventFocusChange = 1,
    EnableAllowFocusChange = 2,
}

public class FormClosingEventArgs : CancelEventArgs
{
    public FormClosingEventArgs(CloseReason closeReason, bool cancel)
        : base(cancel) => CloseReason = closeReason;

    public CloseReason CloseReason { get; }
}

public class FormClosedEventArgs : EventArgs
{
    public FormClosedEventArgs(CloseReason closeReason) => CloseReason = closeReason;

    public CloseReason CloseReason { get; }
}

public delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);

public delegate void FormClosedEventHandler(object sender, FormClosedEventArgs e);

public abstract class TableLayoutStyle
{
    public SizeType SizeType { get; set; }

    internal float Size { get; set; }
}

public class ColumnStyle : TableLayoutStyle
{
    public ColumnStyle()
    {
    }

    public ColumnStyle(SizeType sizeType) => SizeType = sizeType;

    public ColumnStyle(SizeType sizeType, float width)
    {
        SizeType = sizeType;
        Width = width;
    }

    public float Width
    {
        get => Size;
        set => Size = value;
    }
}

public class RowStyle : TableLayoutStyle
{
    public RowStyle()
    {
    }

    public RowStyle(SizeType sizeType) => SizeType = sizeType;

    public RowStyle(SizeType sizeType, float height)
    {
        SizeType = sizeType;
        Height = height;
    }

    public float Height
    {
        get => Size;
        set => Size = value;
    }
}

public abstract class TableLayoutStyleCollection : IList
{
    internal readonly List<TableLayoutStyle> Styles = [];

    internal TableLayoutStyleCollection(TableLayoutPanel owner) => Owner = owner;

    internal TableLayoutPanel Owner { get; }

    public int Count => Styles.Count;

    bool IList.IsFixedSize => false;

    bool IList.IsReadOnly => false;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    public TableLayoutStyle this[int index]
    {
        get => Styles[index];
        set
        {
            Styles[index] = value;
            Owner.RequestLayout();
        }
    }

    object IList.this[int index]
    {
        get => Styles[index];
        set => this[index] = (TableLayoutStyle)value;
    }

    public int Add(TableLayoutStyle style)
    {
        Styles.Add(style);
        Owner.RequestLayout();
        return Styles.Count - 1;
    }

    public void Clear()
    {
        Styles.Clear();
        Owner.RequestLayout();
    }

    public void RemoveAt(int index) => Styles.RemoveAt(index);

    int IList.Add(object value) => Add((TableLayoutStyle)value);

    bool IList.Contains(object value) => Styles.Contains(value as TableLayoutStyle);

    int IList.IndexOf(object value) => Styles.IndexOf(value as TableLayoutStyle);

    void IList.Insert(int index, object value) => Styles.Insert(index, (TableLayoutStyle)value);

    void IList.Remove(object value) => Styles.Remove(value as TableLayoutStyle);

    void ICollection.CopyTo(Array array, int index) => ((ICollection)Styles).CopyTo(array, index);

    public IEnumerator GetEnumerator() => Styles.GetEnumerator();
}

public class TableLayoutColumnStyleCollection : TableLayoutStyleCollection
{
    internal TableLayoutColumnStyleCollection(TableLayoutPanel owner)
        : base(owner)
    {
    }

    public new ColumnStyle this[int index]
    {
        get => (ColumnStyle)Styles[index];
        set => base[index] = value;
    }

    public int Add(ColumnStyle columnStyle) => base.Add(columnStyle);

    public void Insert(int index, ColumnStyle columnStyle) => Styles.Insert(index, columnStyle);

    public void Remove(ColumnStyle columnStyle) => Styles.Remove(columnStyle);

    public bool Contains(ColumnStyle columnStyle) => Styles.Contains(columnStyle);

    public int IndexOf(ColumnStyle columnStyle) => Styles.IndexOf(columnStyle);
}

public class TableLayoutRowStyleCollection : TableLayoutStyleCollection
{
    internal TableLayoutRowStyleCollection(TableLayoutPanel owner)
        : base(owner)
    {
    }

    public new RowStyle this[int index]
    {
        get => (RowStyle)Styles[index];
        set => base[index] = value;
    }

    public int Add(RowStyle rowStyle) => base.Add(rowStyle);

    public void Insert(int index, RowStyle rowStyle) => Styles.Insert(index, rowStyle);

    public void Remove(RowStyle rowStyle) => Styles.Remove(rowStyle);

    public bool Contains(RowStyle rowStyle) => Styles.Contains(rowStyle);

    public int IndexOf(RowStyle rowStyle) => Styles.IndexOf(rowStyle);
}

public class TableLayoutControlCollection : Control.ControlCollection
{
    public TableLayoutControlCollection(TableLayoutPanel container)
        : base(container) => Container = container;

    public TableLayoutPanel Container { get; }

    public virtual void Add(Control control, int column, int row)
    {
        Container.SetCellPosition(control, new TableLayoutPanelCellPosition(column, row));
        Add(control);
    }
}

public struct TableLayoutPanelCellPosition : IEquatable<TableLayoutPanelCellPosition>
{
    public TableLayoutPanelCellPosition(int column, int row)
    {
        Column = column;
        Row = row;
    }

    public int Column { get; set; }

    public int Row { get; set; }

    public bool Equals(TableLayoutPanelCellPosition other) => Column == other.Column && Row == other.Row;

    public override bool Equals(object obj) => obj is TableLayoutPanelCellPosition other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Column, Row);
}

public class TableLayoutPanel : Panel
{
    private readonly Dictionary<Control, TableLayoutPanelCellPosition> _positions = [];
    private readonly Dictionary<Control, int> _columnSpans = [];
    private readonly Dictionary<Control, int> _rowSpans = [];
    private int _columnCount;
    private int _rowCount;

    public TableLayoutPanel()
    {
        ColumnStyles = new TableLayoutColumnStyleCollection(this);
        RowStyles = new TableLayoutRowStyleCollection(this);
    }

    public new TableLayoutControlCollection Controls => (TableLayoutControlCollection)base.Controls;

    public TableLayoutColumnStyleCollection ColumnStyles { get; }

    public TableLayoutRowStyleCollection RowStyles { get; }

    public int ColumnCount
    {
        get => _columnCount;
        set
        {
            _columnCount = value;
            RequestLayout();
        }
    }

    public int RowCount
    {
        get => _rowCount;
        set
        {
            _rowCount = value;
            RequestLayout();
        }
    }

    public TableLayoutPanelCellBorderStyle CellBorderStyle { get; set; }

    public TableLayoutPanelGrowStyle GrowStyle { get; set; } = TableLayoutPanelGrowStyle.AddRows;

    protected override ControlCollection CreateControlsInstance() => new TableLayoutControlCollection(this);

    public void SetCellPosition(Control control, TableLayoutPanelCellPosition position)
    {
        _positions[control] = position;
        RequestLayout();
    }

    public TableLayoutPanelCellPosition GetCellPosition(Control control) =>
        _positions.TryGetValue(control, out TableLayoutPanelCellPosition position) ? position : new TableLayoutPanelCellPosition(-1, -1);

    public TableLayoutPanelCellPosition GetPositionFromControl(Control control) => GetCellPosition(control);

    public void SetColumn(Control control, int column) => SetCellPosition(control, new TableLayoutPanelCellPosition(column, GetCellPosition(control).Row));

    public void SetRow(Control control, int row) => SetCellPosition(control, new TableLayoutPanelCellPosition(GetCellPosition(control).Column, row));

    public int GetColumn(Control control) => GetCellPosition(control).Column;

    public int GetRow(Control control) => GetCellPosition(control).Row;

    public void SetColumnSpan(Control control, int value)
    {
        _columnSpans[control] = Math.Max(1, value);
        RequestLayout();
    }

    public int GetColumnSpan(Control control) => _columnSpans.TryGetValue(control, out int span) ? span : 1;

    public void SetRowSpan(Control control, int value)
    {
        _rowSpans[control] = Math.Max(1, value);
        RequestLayout();
    }

    public int GetRowSpan(Control control) => _rowSpans.TryGetValue(control, out int span) ? span : 1;

    public Control GetControlFromPosition(int column, int row) =>
        base.Controls.Cast<Control>().FirstOrDefault(c => GetCellPosition(c).Equals(new TableLayoutPanelCellPosition(column, row)));

    public int[] GetColumnWidths() => Measure().Columns;

    /// <summary>The page's shared label column (HostText.Spacing.AlignLabelColumns), 0 for none.</summary>
    internal int SharedLabelColumn { get; set; }

    /// <summary>
    /// A two-column table of labels and their controls (labels in an AutoSize first column).
    /// </summary>
    private bool IsLabelTable =>
        Math.Max(1, ColumnCount) == 2
        && (ColumnStyles.Styles.Count == 0 || ColumnStyles.Styles[0].SizeType == SizeType.AutoSize)
        && base.Controls.Cast<Control>().Any(c => c is Label && c.OwnVisible && GetColumnSpan(c) == 1);

    /// <summary>
    /// LiveSplit Native's spacing: the label tables of a page (one per group, say) share the
    /// widest one's label column, so the controls of every group line up.
    /// </summary>
    internal static void AlignLabelColumns(Control page)
    {
        var tables = new List<TableLayoutPanel>();
        void Find(Control control)
        {
            if (control is TableLayoutPanel { IsLabelTable: true } table)
            {
                tables.Add(table);
            }

            foreach (Control child in control.Controls)
            {
                Find(child);
            }
        }

        Find(page);
        foreach (TableLayoutPanel table in tables)
        {
            table.SharedLabelColumn = 0;
        }

        int width = tables.Select(t => t.Measure(preferred: true).Columns[0]).DefaultIfEmpty(0).Max();
        if (tables.Count > 0)
        {
            width = Math.Max(width, HostText.Spacing.LabelColumn);
        }
        foreach (TableLayoutPanel table in tables)
        {
            table.SharedLabelColumn = width;
        }
    }

    public int[] GetRowHeights() => Measure().Rows;

    internal override void ScaleTree(SizeF factor)
    {
        foreach (TableLayoutStyle style in ColumnStyles.Styles.Where(s => s.SizeType == SizeType.Absolute))
        {
            style.Size *= factor.Width;
        }

        foreach (TableLayoutStyle style in RowStyles.Styles.Where(s => s.SizeType == SizeType.Absolute))
        {
            style.Size *= factor.Height;
        }

        base.ScaleTree(factor);
    }

    internal override Size GetContentSize()
    {
        (int[] columns, int[] rows, _) = Measure(preferred: true);
        return new Size(columns.Sum() + Padding.Horizontal, rows.Sum() + Padding.Vertical);
    }

    /// <summary>TableLayout: column widths and row heights from the styles, then each child in its cells.</summary>
    internal override void LayoutChildren()
    {
        (int[] columns, int[] rows, Dictionary<Control, Rectangle> cells) = Measure();
        Rectangle display = DisplayRectangle;
        foreach ((Control child, Rectangle cell) in cells)
        {
            Rectangle area = new(display.X + cell.X, display.Y + cell.Y, cell.Width, cell.Height);
            Rectangle inner = new(
                area.X + child.Margin.Left,
                area.Y + child.Margin.Top,
                Math.Max(0, area.Width - child.Margin.Horizontal),
                Math.Max(0, area.Height - child.Margin.Vertical));
            Size size = child.AutoSize ? child.GetPreferredSize(inner.Size) : GroupBox.LayoutSize(child);
            AnchorStyles anchor = child.Dock switch
            {
                DockStyle.Fill => AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                DockStyle.Top => AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                DockStyle.Bottom => AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                DockStyle.Left => AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
                DockStyle.Right => AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom,
                _ => child.Anchor,
            };
            (int x, int width) = Place(inner.X, inner.Width, size.Width, (anchor & AnchorStyles.Left) != 0, (anchor & AnchorStyles.Right) != 0);
            // With the app's taller rows a control on its row's top (WinForms' default anchor)
            // is centred in it instead, as the app's own rows centre theirs.
            bool top = (anchor & AnchorStyles.Top) != 0;
            bool bottom = (anchor & AnchorStyles.Bottom) != 0;
            if (HostText.Spacing.RowHeight > 0 && top && !bottom)
            {
                top = false;
            }

            (int y, int height) = Place(inner.Y, inner.Height, size.Height, top, bottom);
            child.SetBounds(x, y, width, height);
        }
    }

    private static (int Position, int Length) Place(int start, int available, int wanted, bool near, bool far)
    {
        if (near && far)
        {
            return (start, available);
        }

        int length = Math.Min(wanted, available);
        if (near)
        {
            return (start, length);
        }

        return far ? (start + available - length, length) : (start + ((available - length) / 2), length);
    }

    private (int[] Columns, int[] Rows, Dictionary<Control, Rectangle> Cells) Measure(bool preferred = false)
    {
        // Cells: given positions first, then the rest in order into free cells.
        var placed = new Dictionary<Control, (int Column, int Row, int Columns, int Rows)>();
        int columnCount = Math.Max(1, _columnCount);
        var taken = new HashSet<(int, int)>();
        List<Control> children = base.Controls.Cast<Control>().Where(c => c.OwnVisible).ToList();
        foreach (Control child in children)
        {
            TableLayoutPanelCellPosition position = GetCellPosition(child);
            if (position.Column >= 0 && position.Row >= 0)
            {
                int spanColumns = GetColumnSpan(child);
                int spanRows = GetRowSpan(child);
                placed[child] = (position.Column, position.Row, spanColumns, spanRows);
                for (int c = 0; c < spanColumns; c++)
                {
                    for (int r = 0; r < spanRows; r++)
                    {
                        taken.Add((position.Column + c, position.Row + r));
                    }
                }
            }
        }

        // The rest in order, each at the next place where all the cells it spans are free (a
        // control spanning more columns than are left in the row goes to the next row).
        int next = 0;
        foreach (Control child in children.Where(c => !placed.ContainsKey(c)))
        {
            int spanColumns = Math.Min(GetColumnSpan(child), columnCount);
            int spanRows = GetRowSpan(child);
            while (next % columnCount + spanColumns > columnCount
                || Enumerable.Range(0, spanColumns).Any(c => Enumerable.Range(0, spanRows).Any(r => taken.Contains(((next % columnCount) + c, (next / columnCount) + r)))))
            {
                next++;
            }

            int column = next % columnCount;
            int row = next / columnCount;
            placed[child] = (column, row, spanColumns, spanRows);
            for (int c = 0; c < spanColumns; c++)
            {
                for (int r = 0; r < spanRows; r++)
                {
                    taken.Add((column + c, row + r));
                }
            }

            next += spanColumns;
        }

        int columns = Math.Max(columnCount, placed.Values.Select(p => p.Column + p.Columns).DefaultIfEmpty(0).Max());
        int rows = Math.Max(Math.Max(1, _rowCount), placed.Values.Select(p => p.Row + p.Rows).DefaultIfEmpty(0).Max());
        Rectangle display = DisplayRectangle;
        int[] widths = Sizes(
            columns,
            ColumnStyles.Styles,
            preferred ? 0 : display.Width,
            column => placed.Where(p => p.Value.Column == column && p.Value.Columns == 1).Select(p => Preferred(p.Key).Width + p.Key.Margin.Horizontal)
                .Append(column == 0 ? SharedLabelColumn : 0),
            placed.Where(p => p.Value.Columns > 1).Select(p => (p.Value.Column, p.Value.Columns, Preferred(p.Key).Width + p.Key.Margin.Horizontal)));
        int[] heights = Sizes(
            rows,
            RowStyles.Styles,
            preferred ? 0 : display.Height,
            // LiveSplit Native's spacing: a row at least as tall as the app's own rows.
            row => placed.Where(p => p.Value.Row == row && p.Value.Rows == 1).Select(p => Preferred(p.Key).Height + p.Key.Margin.Vertical + (IsContainer(p.Key) ? 0 : HostText.Spacing.RowPadding))
                .Append(placed.Any(p => p.Value.Row == row) ? HostText.Spacing.RowHeight : 0),
            placed.Where(p => p.Value.Rows > 1).Select(p => (p.Value.Row, p.Value.Rows, Preferred(p.Key).Height + p.Key.Margin.Vertical)));
        var cells = new Dictionary<Control, Rectangle>();
        foreach ((Control child, (int column, int row, int spanColumns, int spanRows)) in placed)
        {
            int x = widths.Take(column).Sum();
            int y = heights.Take(row).Sum();
            int width = widths.Skip(column).Take(spanColumns).Sum();
            int height = heights.Skip(row).Take(spanRows).Sum();
            cells[child] = new Rectangle(x, y, width, height);
        }

        return (widths, heights, cells);
    }

    /// <summary>A group box or a panel of controls: its own rows keep their room, not it.</summary>
    private static bool IsContainer(Control control) =>
        control is GroupBox || (control.HostKind == "Panel" && control.Controls.Count > 0);

    private static Size Preferred(Control control) =>
        control.AutoSize || control.Dock == DockStyle.Fill ? control.GetPreferredSize(Size.Empty) : GroupBox.LayoutSize(control);

    /// <summary>
    /// Column widths (row heights): absolute ones as given, AutoSize ones as their content, a
    /// control spanning several widening the last AutoSize one it spans (or a Percent one when
    /// measuring the preferred size); then Percent ones share what is left of the space, or,
    /// with none, the last one takes it.
    /// </summary>
    private static int[] Sizes(int count, List<TableLayoutStyle> styles, int available, Func<int, IEnumerable<int>> contents, IEnumerable<(int Start, int Count, int Size)> spans)
    {
        var sizes = new int[count];
        float percentTotal = 0;
        SizeType TypeOf(int i) => (i < styles.Count ? styles[i] : null)?.SizeType ?? SizeType.AutoSize;
        for (int i = 0; i < count; i++)
        {
            TableLayoutStyle style = i < styles.Count ? styles[i] : null;
            switch (TypeOf(i))
            {
                case SizeType.Absolute:
                    sizes[i] = (int)Math.Round(style.Size);
                    break;
                case SizeType.Percent:
                    percentTotal += style.Size;
                    // Measuring the preferred size: as big as its content.
                    sizes[i] = available > 0 ? 0 : contents(i).DefaultIfEmpty(0).Max();
                    break;
                default:
                    sizes[i] = contents(i).DefaultIfEmpty(0).Max();
                    break;
            }
        }

        foreach ((int start, int spanned, int size) in spans)
        {
            int end = Math.Min(count, start + spanned);
            int deficit = size - sizes.Skip(start).Take(end - start).Sum();
            if (deficit <= 0)
            {
                continue;
            }

            int target = -1;
            for (int i = end - 1; i >= start && target < 0; i--)
            {
                if (TypeOf(i) == SizeType.AutoSize || (available == 0 && TypeOf(i) == SizeType.Percent))
                {
                    target = i;
                }
            }

            if (target >= 0 && !(available > 0 && Enumerable.Range(start, end - start).Any(i => TypeOf(i) == SizeType.Percent)))
            {
                sizes[target] += deficit;
            }
        }

        int remaining = Math.Max(0, available - sizes.Sum());
        if (percentTotal == 0 && available > 0 && count > 0)
        {
            // TableLayout: without percent rows (columns) the last one takes the space left.
            sizes[count - 1] += remaining;
        }
        else if (percentTotal > 0 && available > 0)
        {
            int given = 0;
            int last = -1;
            for (int i = 0; i < count; i++)
            {
                if (TypeOf(i) == SizeType.Percent)
                {
                    sizes[i] = (int)(remaining * styles[i].Size / percentTotal);
                    given += sizes[i];
                    last = i;
                }
            }

            if (last >= 0)
            {
                sizes[last] += remaining - given;
            }
        }

        return sizes;
    }
}
