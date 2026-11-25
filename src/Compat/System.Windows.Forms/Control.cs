// System.Windows.Forms.Control for component settings pages. A control keeps WinForms' state
// (bounds, margins, docking, anchoring, data bindings, events) and lays its children out as
// WinForms does; the app shows the result with native controls through IHostControl.
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using LiveSplitNative.ComponentHost.Compat;

namespace System.Windows.Forms;

public class Control : Component, IWin32Window, IBindableComponent, IHostControl
{
    private readonly SynchronizationContext _context = SynchronizationContext.Current;
    private string _text = "";
    private Rectangle _bounds;
    private Padding _margin;
    private Padding _padding;
    private DockStyle _dock;
    private AnchorStyles _anchor = AnchorStyles.Top | AnchorStyles.Left;
    private bool _autoSize;
    private Size _minimumSize;
    private Size _maximumSize;
    private bool _visible = true;
    private bool _enabled = true;
    private Color _backColor = Color.Empty;
    private Color _foreColor = Color.Empty;
    private Font _font;
    private ControlBindingsCollection _bindings;
    private int _layoutSuspendCount;
    private bool _shown;

    public Control()
    {
        _margin = DefaultMargin;
        _padding = DefaultPadding;
        _bounds = new Rectangle(Point.Empty, DefaultSize);
        Controls = CreateControlsInstance();
    }

    public Control(string text)
        : this() => Text = text;

    public Control(Control parent, string text)
        : this(text) => parent?.Controls.Add(this);

    public event EventHandler Click;
    public event EventHandler TextChanged;
    public event EventHandler VisibleChanged;
    public event EventHandler EnabledChanged;
    public event EventHandler BackColorChanged;
    public event EventHandler ForeColorChanged;
    public event EventHandler FontChanged;
    public event EventHandler SizeChanged;
    public event EventHandler Resize;
    public event EventHandler LocationChanged;
    public event EventHandler GotFocus;
    public event EventHandler LostFocus;
    public event EventHandler Enter;
    public event EventHandler Leave;
    public event EventHandler Validated;
    public event CancelEventHandler Validating;
    public event EventHandler HandleCreated;
    public event EventHandler HandleDestroyed;
    public event EventHandler ParentChanged;
    public event EventHandler DoubleClick;
    public event PaintEventHandler Paint;
    public event ControlEventHandler ControlAdded;
    public event ControlEventHandler ControlRemoved;

    /// <summary>IHostControl.Changed.</summary>
    private event EventHandler HostChanged;

    event EventHandler IHostControl.Changed
    {
        add => HostChanged += value;
        remove => HostChanged -= value;
    }

    public static Font DefaultFont => new(HostText.DialogFont.FamilyName, HostText.DialogFont.Size, HostText.DialogFont.IsPixelUnit ? GraphicsUnit.Pixel : GraphicsUnit.Point);

    public static Color DefaultBackColor => SystemColors.Control;

    public static Color DefaultForeColor => SystemColors.ControlText;

    public static Keys ModifierKeys => Keys.None;

    protected virtual Size DefaultSize => Size.Empty;

    protected virtual Padding DefaultMargin => new(3);

    protected virtual Padding DefaultPadding => Padding.Empty;

    public string Name { get; set; } = "";

    public object Tag { get; set; }

    public IntPtr Handle => IntPtr.Zero;

    public bool IsHandleCreated => _shown;

    public bool Created => _shown;

    public bool IsDisposed { get; private set; }

    public bool Disposing => false;

    public bool InvokeRequired => false;

    public bool HasChildren => Controls.Count > 0;

    public bool CanFocus => Visible && Enabled;

    public bool Focused => false;

    public bool CausesValidation { get; set; } = true;

    public int TabIndex { get; set; }

    public bool TabStop { get; set; } = true;

    public ControlCollection Controls { get; }

    public Control Parent
    {
        get => ParentInternal;
        set
        {
            if (value == ParentInternal)
            {
                return;
            }

            ParentInternal?.Controls.Remove(this);
            value?.Controls.Add(this);
        }
    }

    internal Control ParentInternal { get; set; }

    public ControlBindingsCollection DataBindings => _bindings ??= new ControlBindingsCollection(this);

    public virtual string Text
    {
        get => _text;
        set
        {
            value ??= "";
            if (_text != value)
            {
                _text = value;
                OnTextChanged(EventArgs.Empty);
                RequestLayout();
                RaiseHostChanged();
            }
        }
    }

    public Point Location
    {
        get => _bounds.Location;
        set => SetBounds(value.X, value.Y, _bounds.Width, _bounds.Height);
    }

    public Size Size
    {
        get => _bounds.Size;
        set => SetBounds(_bounds.X, _bounds.Y, value.Width, value.Height);
    }

    public int Left
    {
        get => _bounds.X;
        set => SetBounds(value, _bounds.Y, _bounds.Width, _bounds.Height);
    }

    public int Top
    {
        get => _bounds.Y;
        set => SetBounds(_bounds.X, value, _bounds.Width, _bounds.Height);
    }

    public int Width
    {
        get => _bounds.Width;
        set => SetBounds(_bounds.X, _bounds.Y, value, _bounds.Height);
    }

    public int Height
    {
        get => _bounds.Height;
        set => SetBounds(_bounds.X, _bounds.Y, _bounds.Width, value);
    }

    public int Right => _bounds.Right;

    public int Bottom => _bounds.Bottom;

    public Rectangle Bounds
    {
        get => _bounds;
        set => SetBounds(value.X, value.Y, value.Width, value.Height);
    }

    public Size ClientSize
    {
        get => _bounds.Size;
        set => Size = value;
    }

    public Rectangle ClientRectangle => new(Point.Empty, _bounds.Size);

    public virtual Rectangle DisplayRectangle => new(
        _padding.Left,
        _padding.Top,
        Math.Max(0, _bounds.Width - _padding.Horizontal),
        Math.Max(0, _bounds.Height - _padding.Vertical));

    public Padding Margin
    {
        get => _margin;
        set
        {
            _margin = value;
            RequestLayout();
        }
    }

    public Padding Padding
    {
        get => _padding;
        set
        {
            _padding = value;
            RequestLayout();
        }
    }

    public virtual DockStyle Dock
    {
        get => _dock;
        set
        {
            _dock = value;
            RequestLayout();
        }
    }

    public virtual AnchorStyles Anchor
    {
        get => _anchor;
        set
        {
            _anchor = value;
            RequestLayout();
        }
    }

    public virtual bool AutoSize
    {
        get => _autoSize;
        set
        {
            _autoSize = value;
            RequestLayout();
        }
    }

    public virtual Size MinimumSize
    {
        get => _minimumSize;
        set
        {
            _minimumSize = value;
            RequestLayout();
        }
    }

    public virtual Size MaximumSize
    {
        get => _maximumSize;
        set
        {
            _maximumSize = value;
            RequestLayout();
        }
    }

    /// <summary>Visible when this control and all its parents are (Control.Visible).</summary>
    public bool Visible
    {
        get => _visible && (ParentInternal is null || ParentInternal.Visible);
        set
        {
            if (_visible != value)
            {
                _visible = value;
                OnVisibleChanged(EventArgs.Empty);
                RequestLayout();
                RaiseHostChanged();
            }
        }
    }

    /// <summary>Enabled when this control and all its parents are (Control.Enabled).</summary>
    public bool Enabled
    {
        get => _enabled && (ParentInternal is null || ParentInternal.Enabled);
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                OnEnabledChanged(EventArgs.Empty);
                RaiseHostChangedTree();
            }
        }
    }

    public virtual Color BackColor
    {
        get => _backColor.IsEmpty ? ParentInternal?.BackColor ?? DefaultBackColor : _backColor;
        set
        {
            if (_backColor != value)
            {
                _backColor = value;
                OnBackColorChanged(EventArgs.Empty);
                RaiseHostChanged();
            }
        }
    }

    public virtual Color ForeColor
    {
        get => _foreColor.IsEmpty ? ParentInternal?.ForeColor ?? DefaultForeColor : _foreColor;
        set
        {
            _foreColor = value;
            ForeColorChanged?.Invoke(this, EventArgs.Empty);
            RaiseHostChanged();
        }
    }

    public virtual Font Font
    {
        get => _font ?? ParentInternal?.Font ?? DefaultFont;
        set
        {
            _font = value;
            FontChanged?.Invoke(this, EventArgs.Empty);
            RequestLayout();
        }
    }

    public virtual Cursor Cursor { get; set; }

    public virtual ContextMenuStrip ContextMenuStrip { get; set; }

    public virtual RightToLeft RightToLeft { get; set; }

    public bool UseWaitCursor { get; set; }

    public AccessibleRole AccessibleRole { get; set; }

    public string AccessibleName { get; set; }

    public string AccessibleDescription { get; set; }

    public BindingContext BindingContext { get; set; } = new();

    public virtual ImageLayout BackgroundImageLayout { get; set; }

    public virtual Image BackgroundImage { get; set; }

    protected int FontHeight => (int)Math.Ceiling(HostText.Measure("A", Font.ToHostFont()).Height);

    public void SetBounds(int x, int y, int width, int height)
    {
        var bounds = new Rectangle(x, y, Math.Max(0, width), Math.Max(HostMinimumHeight, Math.Max(0, height)));
        if (bounds == _bounds)
        {
            return;
        }

        bool resized = bounds.Size != _bounds.Size;
        bool moved = bounds.Location != _bounds.Location;
        Size old = _bounds.Size;
        _bounds = bounds;
        if (resized)
        {
            AnchorChildren(old);
        }
        if (moved)
        {
            LocationChanged?.Invoke(this, EventArgs.Empty);
        }

        if (resized)
        {
            OnSizeChanged(EventArgs.Empty);
            OnResize(EventArgs.Empty);
            if (_shown)
            {
                PerformLayout();
            }
        }

        RaiseHostChanged();
    }

    public void SuspendLayout() => _layoutSuspendCount++;

    public void ResumeLayout() => ResumeLayout(true);

    public void ResumeLayout(bool performLayout)
    {
        if (_layoutSuspendCount > 0)
        {
            _layoutSuspendCount--;
        }

        if (performLayout && _layoutSuspendCount == 0 && _shown)
        {
            PerformLayout();
        }
    }

    public void PerformLayout()
    {
        if (_layoutSuspendCount > 0)
        {
            return;
        }

        if (ParentInternal is null && _shown && HostText.Spacing.AlignLabelColumns)
        {
            TableLayoutPanel.AlignLabelColumns(this);
        }

        if (ParentInternal is null && AutoSize && _shown)
        {
            // An AutoSize page in its dialog is as big as it wants to be.
            Size preferred = GetPreferredSize(Size.Empty);
            if (preferred != _bounds.Size)
            {
                SetBounds(_bounds.X, _bounds.Y, preferred.Width, preferred.Height);
                return;
            }
        }

        OnLayout(new LayoutEventArgs(this, ""));
    }

    public void PerformLayout(Control affectedControl, string affectedProperty) => PerformLayout();

    protected virtual void OnLayout(LayoutEventArgs levent)
    {
        LayoutChildren();
        foreach (Control child in Controls)
        {
            child.PerformLayout();
        }

        Layout?.Invoke(this, levent);
    }

    public event LayoutEventHandler Layout;

    /// <summary>
    /// WinForms anchoring, across: when a control gets wider or narrower, a child anchored to
    /// the right keeps its distance to the right edge (anchored left and right it grows, anchored
    /// to neither it stays centred). Heights are not anchored here yet. A table places its
    /// children itself.
    /// </summary>
    private void AnchorChildren(Size old)
    {
        // Only once shown: a designer's InitializeComponent sizes the page after adding its
        // controls at their final places.
        int dx = _bounds.Width - old.Width;
        if (dx == 0 || !_shown || this is TableLayoutPanel)
        {
            return;
        }

        foreach (Control child in Controls)
        {
            if (child.Dock != DockStyle.None)
            {
                continue;
            }

            bool left = (child.Anchor & AnchorStyles.Left) != 0;
            bool right = (child.Anchor & AnchorStyles.Right) != 0;
            Rectangle b = child._bounds;
            if (right && left)
            {
                child.SetBounds(b.X, b.Y, b.Width + dx, b.Height);
            }
            else if (right)
            {
                child.SetBounds(b.X + dx, b.Y, b.Width, b.Height);
            }
            else if (!left)
            {
                child.SetBounds(b.X + (dx / 2), b.Y, b.Width, b.Height);
            }
        }
    }

    /// <summary>Places the children in the display rectangle (DefaultLayout: docking, then the rest).</summary>
    internal virtual void LayoutChildren()
    {
        Rectangle space = DisplayRectangle;
        // DefaultLayout docks the last child first.
        for (int i = Controls.Count - 1; i >= 0; i--)
        {
            Control child = Controls[i];
            if (!child._visible || child.Dock == DockStyle.None)
            {
                continue;
            }

            Size size = child.AutoSize ? child.GetPreferredSize(space.Size) : child.Size;
            switch (child.Dock)
            {
                case DockStyle.Top:
                    child.SetBounds(space.X, space.Y, space.Width, size.Height);
                    space = new Rectangle(space.X, space.Y + size.Height, space.Width, Math.Max(0, space.Height - size.Height));
                    break;
                case DockStyle.Bottom:
                    child.SetBounds(space.X, space.Bottom - size.Height, space.Width, size.Height);
                    space = new Rectangle(space.X, space.Y, space.Width, Math.Max(0, space.Height - size.Height));
                    break;
                case DockStyle.Left:
                    child.SetBounds(space.X, space.Y, size.Width, space.Height);
                    space = new Rectangle(space.X + size.Width, space.Y, Math.Max(0, space.Width - size.Width), space.Height);
                    break;
                case DockStyle.Right:
                    child.SetBounds(space.Right - size.Width, space.Y, size.Width, space.Height);
                    space = new Rectangle(space.X, space.Y, Math.Max(0, space.Width - size.Width), space.Height);
                    break;
                case DockStyle.Fill:
                    child.SetBounds(space.X, space.Y, space.Width, space.Height);
                    break;
            }
        }

        foreach (Control child in Controls)
        {
            if (child.Dock == DockStyle.None && child.AutoSize)
            {
                Size preferred = child.GetPreferredSize(Size.Empty);
                child.SetBounds(child.Left, child.Top, preferred.Width, preferred.Height);
            }
        }
    }

    public Size PreferredSize => GetPreferredSize(Size.Empty);

    /// <summary>The size the control wants (AutoSize), at least MinimumSize.</summary>
    public virtual Size GetPreferredSize(Size proposedSize) => ApplyMinimum(GetContentSize());

    /// <summary>The control's own preferred size before MinimumSize (by kind).</summary>
    internal virtual Size GetContentSize() => _bounds.Size;

    /// <summary>
    /// DefaultLayout's preferred size of a container: its docked children stacked, its other
    /// children's far edges (with their margins), and the space <paramref name="around"/> the
    /// display rectangle. (AutoSizeMode is taken as GrowAndShrink: the size follows the content.)
    /// </summary>
    internal Size DefaultLayoutPreferredSize(Padding around)
    {
        int dockedWidth = 0;
        int dockedHeight = 0;
        int fillWidth = 0;
        int fillHeight = 0;
        int right = 0;
        int bottom = 0;
        foreach (Control child in Controls)
        {
            if (!child._visible)
            {
                continue;
            }

            Size size = child.AutoSize || child.Dock == DockStyle.Fill ? child.GetPreferredSize(Size.Empty) : child.Size;
            switch (child.Dock)
            {
                case DockStyle.None:
                    right = Math.Max(right, child.Left + size.Width + child.Margin.Right);
                    bottom = Math.Max(bottom, child.Top + size.Height + child.Margin.Bottom);
                    break;
                case DockStyle.Top:
                case DockStyle.Bottom:
                    dockedHeight += size.Height;
                    fillWidth = Math.Max(fillWidth, size.Width);
                    break;
                case DockStyle.Left:
                case DockStyle.Right:
                    dockedWidth += size.Width;
                    fillHeight = Math.Max(fillHeight, size.Height);
                    break;
                case DockStyle.Fill:
                    fillWidth = Math.Max(fillWidth, size.Width);
                    fillHeight = Math.Max(fillHeight, size.Height);
                    break;
            }
        }

        return new Size(
            Math.Max(dockedWidth + fillWidth + around.Horizontal, right + around.Right),
            Math.Max(dockedHeight + fillHeight + around.Vertical, bottom + around.Bottom));
    }

    internal Size ApplyMinimum(Size size) =>
        new(Math.Max(size.Width, _minimumSize.Width), Math.Max(size.Height, _minimumSize.Height));

    internal Size MeasureText(string text)
    {
        SizeF size = HostText.Measure(string.IsNullOrEmpty(text) ? " " : text, Font.ToHostFont());
        return new Size((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
    }

    public void Invalidate()
    {
    }

    public void Invalidate(bool invalidateChildren)
    {
    }

    public void Invalidate(Rectangle rc)
    {
    }

    public void Refresh()
    {
    }

    public void Update()
    {
    }

    public bool Focus() => false;

    public void Select()
    {
    }

    public void BringToFront()
    {
    }

    public void SendToBack()
    {
    }

    public void Show() => Visible = true;

    public void Hide() => Visible = false;

    public void CreateControl()
    {
    }

    public Form FindForm()
    {
        Control control = this;
        while (control is not null and not Form)
        {
            control = control.ParentInternal;
        }

        return control as Form;
    }

    public IAsyncResult BeginInvoke(Delegate method) => BeginInvoke(method, null);

    public IAsyncResult BeginInvoke(Delegate method, params object[] args)
    {
        var result = new InvokeResult();
        void Run()
        {
            try
            {
                result.Value = method.DynamicInvoke(args);
            }
            finally
            {
                result.Complete();
            }
        }

        if (_context is not null)
        {
            _context.Post(_ => Run(), null);
        }
        else
        {
            Run();
        }

        return result;
    }

    public object EndInvoke(IAsyncResult asyncResult)
    {
        var result = (InvokeResult)asyncResult;
        result.AsyncWaitHandle.WaitOne();
        return result.Value;
    }

    public object Invoke(Delegate method) => method.DynamicInvoke();

    public object Invoke(Delegate method, params object[] args) => method.DynamicInvoke(args);

    protected virtual ControlCollection CreateControlsInstance() => new(this);

    protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);

    protected virtual void OnVisibleChanged(EventArgs e) => VisibleChanged?.Invoke(this, e);

    protected virtual void OnEnabledChanged(EventArgs e) => EnabledChanged?.Invoke(this, e);

    protected virtual void OnBackColorChanged(EventArgs e) => BackColorChanged?.Invoke(this, e);

    protected virtual void OnSizeChanged(EventArgs e) => SizeChanged?.Invoke(this, e);

    protected virtual void OnResize(EventArgs e) => Resize?.Invoke(this, e);

    protected virtual void OnClick(EventArgs e) => Click?.Invoke(this, e);

    protected virtual void OnPaint(PaintEventArgs e) => Paint?.Invoke(this, e);

    protected virtual void OnControlAdded(ControlEventArgs e) => ControlAdded?.Invoke(this, e);

    protected virtual void OnControlRemoved(ControlEventArgs e) => ControlRemoved?.Invoke(this, e);

    protected virtual void OnParentChanged(EventArgs e) => ParentChanged?.Invoke(this, e);

    protected virtual void OnHandleCreated(EventArgs e) => HandleCreated?.Invoke(this, e);

    protected virtual void OnLoad(EventArgs e)
    {
    }

    /// <summary>The control is shown for the first time: bindings read their values, then Load.</summary>
    protected virtual void OnCreateControl()
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            IsDisposed = true;
            foreach (Control child in Controls.Cast<Control>().ToList())
            {
                child.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    internal void RequestLayout()
    {
        if (_shown)
        {
            (ParentInternal ?? this).PerformLayout();
        }
    }

    internal void RaiseHostChanged() => HostChanged?.Invoke(this, EventArgs.Empty);

    private void RaiseHostChangedTree()
    {
        RaiseHostChanged();
        foreach (Control child in Controls)
        {
            child.RaiseHostChangedTree();
        }
    }

    /// <summary>
    /// The control (and its children) is shown: bindings read their settings, then Load. Showing
    /// again in a new window does it again, as WinForms does when a page gets a new parent form
    /// (new handle and binding context).
    /// </summary>
    internal void ShowTree(bool again = false)
    {
        if (_shown && !again)
        {
            return;
        }

        foreach (Control child in Controls)
        {
            child.ShowTree(again);
        }

        _bindings?.ReadAll();
        _shown = true;
        OnHandleCreated(EventArgs.Empty);
        OnCreateControl();
    }

    internal bool IsShown => _shown;

    /// <summary>The control's own Visible flag (not its parents').</summary>
    internal bool OwnVisible => _visible;

    /// <summary>ContainerControl.PerformAutoScale: every bound and margin of the tree times the factor.</summary>
    internal virtual void ScaleTree(SizeF factor)
    {
        foreach (Control child in Controls)
        {
            child.ScaleCore(factor);
        }
    }

    internal void ScaleCore(SizeF factor)
    {
        _bounds = new Rectangle(
            (int)Math.Round(_bounds.X * factor.Width),
            (int)Math.Round(_bounds.Y * factor.Height),
            (int)Math.Round(_bounds.Width * factor.Width),
            Math.Max(HostMinimumHeight, (int)Math.Round(_bounds.Height * factor.Height)));
        _margin = _margin.Scale(factor.Width, factor.Height);
        _padding = _padding.Scale(factor.Width, factor.Height);
        _minimumSize = new Size((int)Math.Round(_minimumSize.Width * factor.Width), (int)Math.Round(_minimumSize.Height * factor.Height));
        _maximumSize = new Size((int)Math.Round(_maximumSize.Width * factor.Width), (int)Math.Round(_maximumSize.Height * factor.Height));
        ScaleTree(factor);
    }

    // IHostControl: what the app shows.
    internal virtual string HostKind => "Panel";

    string IHostControl.Kind => HostKind;

    string IHostControl.Text => Text;

    Rectangle IHostControl.Bounds => _bounds;

    bool IHostControl.Visible => _visible;

    bool IHostControl.Enabled => Enabled;

    bool IHostControl.Checked => HostChecked;

    Color? IHostControl.BackColor => HostBackColor;

    IReadOnlyList<string> IHostControl.Items => HostItems;

    int IHostControl.SelectedIndex => HostSelectedIndex;

    decimal IHostControl.Value => HostValue;

    decimal IHostControl.Minimum => HostMinimum;

    decimal IHostControl.Maximum => HostMaximum;

    int IHostControl.DecimalPlaces => HostDecimalPlaces;

    int IHostControl.TextAlign => HostTextAlign;

    IReadOnlyList<IHostControl> IHostControl.Children => Controls.Cast<IHostControl>().ToList();

    HostListView IHostControl.ListView => HostListView;

    internal virtual HostListView HostListView => null;

    /// <summary>The least height the app's control needs (an input control's, HostText.Spacing).</summary>
    internal virtual int HostMinimumHeight => 0;

    internal virtual bool HostChecked => false;

    internal virtual Color? HostBackColor => null;

    internal virtual IReadOnlyList<string> HostItems => [];

    internal virtual int HostSelectedIndex => -1;

    internal virtual decimal HostValue => 0;

    internal virtual decimal HostMinimum => 0;

    internal virtual decimal HostMaximum => 0;

    internal virtual int HostDecimalPlaces => 0;

    internal virtual int HostTextAlign => (int)ContentAlignment.TopLeft;

    void IHostControl.PerformLayout(Size size)
    {
        if (size != _bounds.Size)
        {
            Size old = _bounds.Size;
            _bounds = new Rectangle(_bounds.Location, size);
            AnchorChildren(old);
        }

        PerformLayout();
    }

    void IHostControl.OnShown()
    {
        if (this is ContainerControl container)
        {
            container.PerformAutoScale();
        }

        ShowTree(again: true);
        // A list Load fills (a comparison combo box whose SelectedItem is bound) shows its bound
        // value: the bindings read once more after Load, as WinForms' do when the page is shown.
        ReadBindingsTree();
        // The page becomes visible in its dialog: as when WinForms shows a form, each visible
        // control raises VisibleChanged, a parent before its children (after Load).
        RaiseShownVisibleChanged();
        PerformLayout();
    }

    private void ReadBindingsTree()
    {
        foreach (Control child in Controls)
        {
            child.ReadBindingsTree();
        }

        _bindings?.ReadAll();
    }

    private void RaiseShownVisibleChanged()
    {
        if (!Visible)
        {
            return;
        }

        OnVisibleChanged(EventArgs.Empty);
        foreach (Control child in Controls.Cast<Control>().ToList())
        {
            child.RaiseShownVisibleChanged();
        }
    }

    void IHostControl.SetChecked(bool value) => HostSetChecked(value);

    void IHostControl.Click() => HostClick();

    void IHostControl.SetSelectedIndex(int index) => HostSetSelectedIndex(index);

    void IHostControl.SetValue(decimal value) => HostSetValue(value);

    void IHostControl.SetText(string text) => Text = text;

    internal virtual void HostSetChecked(bool value)
    {
    }

    internal virtual void HostClick() => OnClick(EventArgs.Empty);

    internal virtual void HostSetSelectedIndex(int index)
    {
    }

    internal virtual void HostSetValue(decimal value)
    {
    }

    public class ControlCollection : IList, ICloneable
    {
        private readonly List<Control> _items = [];

        public ControlCollection(Control owner) => Owner = owner;

        public Control Owner { get; }

        public int Count => _items.Count;

        public virtual Control this[int index] => _items[index];

        public virtual Control this[string key] => _items.FirstOrDefault(c => string.Equals(c.Name, key, StringComparison.OrdinalIgnoreCase));

        bool IList.IsFixedSize => false;

        bool IList.IsReadOnly => false;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        object IList.this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException();
        }

        public virtual void Add(Control value)
        {
            if (value is null || _items.Contains(value))
            {
                return;
            }

            value.ParentInternal?.Controls.Remove(value);
            _items.Add(value);
            value.ParentInternal = Owner;
            value.OnParentChanged(EventArgs.Empty);
            Owner.OnControlAdded(new ControlEventArgs(value));
            if (Owner.IsShown)
            {
                value.ShowTree();
                Owner.PerformLayout();
            }

            Owner.RaiseHostChanged();
        }

        public virtual void AddRange(Control[] controls)
        {
            foreach (Control control in controls)
            {
                Add(control);
            }
        }

        public virtual void Remove(Control value)
        {
            if (value is not null && _items.Remove(value))
            {
                value.ParentInternal = null;
                value.OnParentChanged(EventArgs.Empty);
                Owner.OnControlRemoved(new ControlEventArgs(value));
                Owner.RequestLayout();
                Owner.RaiseHostChanged();
            }
        }

        public void RemoveAt(int index) => Remove(_items[index]);

        public virtual void Clear()
        {
            foreach (Control control in _items.ToList())
            {
                Remove(control);
            }
        }

        public bool Contains(Control control) => _items.Contains(control);

        public int IndexOf(Control control) => _items.IndexOf(control);

        public int GetChildIndex(Control child) => _items.IndexOf(child);

        public void SetChildIndex(Control child, int newIndex)
        {
            if (_items.Remove(child))
            {
                _items.Insert(Math.Clamp(newIndex, 0, _items.Count), child);
                Owner.RequestLayout();
                Owner.RaiseHostChanged();
            }
        }

        public Control[] Find(string key, bool searchAllChildren)
        {
            var found = new List<Control>();
            foreach (Control control in _items)
            {
                if (string.Equals(control.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(control);
                }

                if (searchAllChildren)
                {
                    found.AddRange(control.Controls.Find(key, true));
                }
            }

            return [.. found];
        }

        public IEnumerator GetEnumerator() => _items.ToList().GetEnumerator();

        int IList.Add(object value)
        {
            Add((Control)value);
            return _items.Count - 1;
        }

        bool IList.Contains(object value) => value is Control control && Contains(control);

        int IList.IndexOf(object value) => value is Control control ? IndexOf(control) : -1;

        void IList.Insert(int index, object value) => throw new NotSupportedException();

        void IList.Remove(object value) => Remove(value as Control);

        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        object ICloneable.Clone() => new List<Control>(_items);
    }

    private sealed class InvokeResult : IAsyncResult
    {
        private readonly ManualResetEvent _done = new(false);

        public object Value { get; set; }

        public object AsyncState => null;

        public WaitHandle AsyncWaitHandle => _done;

        public bool CompletedSynchronously => false;

        public bool IsCompleted { get; private set; }

        public void Complete()
        {
            IsCompleted = true;
            _done.Set();
        }
    }
}

public class LayoutEventArgs : EventArgs
{
    public LayoutEventArgs(Control affectedControl, string affectedProperty)
    {
        AffectedControl = affectedControl;
        AffectedProperty = affectedProperty;
    }

    public Control AffectedControl { get; }

    public string AffectedProperty { get; }
}

public delegate void LayoutEventHandler(object sender, LayoutEventArgs e);

internal static class FontExtensions
{
    public static HostFont ToHostFont(this Font font) => font.ToHost();
}
