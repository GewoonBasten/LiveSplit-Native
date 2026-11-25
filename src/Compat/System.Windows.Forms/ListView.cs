// System.Windows.Forms.ListView for component settings pages: columns and items as WinForms keeps
// them, the items' bounds laid out as Details view lays them out (a header, then one row per
// item), shown by the app as a table (IHostControl.ListView).
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using LiveSplitNative.ComponentHost.Compat;

namespace System.Windows.Forms;

public class ListView : Control
{
    private int _updateCount;

    public ListView()
    {
        Columns = new ColumnHeaderCollection(this);
        Items = new ListViewItemCollection(this);
    }

    public event EventHandler SelectedIndexChanged;

    public event EventHandler ItemActivate;

    public ColumnHeaderCollection Columns { get; }

    public ListViewItemCollection Items { get; }

    public View View { get; set; } = View.LargeIcon;

    public bool FullRowSelect { get; set; }

    public bool GridLines { get; set; }

    public ColumnHeaderStyle HeaderStyle { get; set; } = ColumnHeaderStyle.Clickable;

    public bool MultiSelect { get; set; } = true;

    public bool HideSelection { get; set; } = true;

    public bool CheckBoxes { get; set; }

    public bool Scrollable { get; set; } = true;

    public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;

    protected override Size DefaultSize => new(121, 97);

    /// <summary>The height of one item: the font's line, as Details view sizes its rows.</summary>
    internal int RowHeight => Math.Max(MeasureText("Ag").Height + 4, HostText.Spacing.ListRowHeight);

    /// <summary>The column header's height (none when HeaderStyle is None).</summary>
    internal int HeaderHeight => View == View.Details && HeaderStyle != ColumnHeaderStyle.None ? Math.Max(MeasureText("Ag").Height + 10, HostText.Spacing.ListHeaderHeight) : 0;

    public void BeginUpdate() => _updateCount++;

    public void EndUpdate()
    {
        if (_updateCount > 0 && --_updateCount == 0)
        {
            Changed();
        }
    }

    internal void Changed()
    {
        if (_updateCount == 0)
        {
            RaiseHostChanged();
        }
    }

    protected virtual void OnSelectedIndexChanged(EventArgs e) => SelectedIndexChanged?.Invoke(this, e);

    protected virtual void OnItemActivate(EventArgs e) => ItemActivate?.Invoke(this, e);

    internal override string HostKind => "ListView";

    internal override HostListView HostListView => new(
        Columns.Cast<ColumnHeader>().Select(c => new HostListColumn(c.Text, c.Width, (int)c.TextAlign)).ToList(),
        Items.Cast<ListViewItem>()
            .Select(i => new HostListRow(i.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text).ToList(), i.Font is { } font && font.Bold))
            .ToList(),
        HeaderHeight,
        RowHeight,
        View == View.Details && HeaderStyle != ColumnHeaderStyle.None,
        GridLines);

    public class ColumnHeaderCollection : IList
    {
        private readonly List<ColumnHeader> _items = [];
        private readonly ListView _owner;

        public ColumnHeaderCollection(ListView owner) => _owner = owner;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        bool IList.IsFixedSize => false;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        public virtual ColumnHeader this[int index] => _items[index];

        public virtual ColumnHeader this[string key] => _items.FirstOrDefault(c => c.Name == key);

        object IList.this[int index]
        {
            get => _items[index];
            set => throw new NotSupportedException();
        }

        public virtual ColumnHeader Add(string text) => Add(text, 60);

        public virtual ColumnHeader Add(string text, int width) => Add(text, width, HorizontalAlignment.Left);

        public virtual ColumnHeader Add(string text, int width, HorizontalAlignment textAlign)
        {
            var column = new ColumnHeader { Text = text, Width = width, TextAlign = textAlign };
            Add(column);
            return column;
        }

        public virtual ColumnHeader Add(string key, string text) => Add(key, text, 60);

        public virtual ColumnHeader Add(string key, string text, int width)
        {
            ColumnHeader column = Add(text, width);
            column.Name = key;
            return column;
        }

        public virtual int Add(ColumnHeader value)
        {
            value.ListView = _owner;
            _items.Add(value);
            _owner.Changed();
            return _items.Count - 1;
        }

        public virtual void AddRange(ColumnHeader[] values)
        {
            foreach (ColumnHeader value in values)
            {
                Add(value);
            }
        }

        public virtual void Clear()
        {
            _items.Clear();
            _owner.Changed();
        }

        public bool Contains(ColumnHeader value) => _items.Contains(value);

        public int IndexOf(ColumnHeader value) => _items.IndexOf(value);

        public virtual void Remove(ColumnHeader column)
        {
            _items.Remove(column);
            _owner.Changed();
        }

        public virtual void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            _owner.Changed();
        }

        int IList.Add(object value) => Add((ColumnHeader)value);

        bool IList.Contains(object value) => value is ColumnHeader c && Contains(c);

        int IList.IndexOf(object value) => value is ColumnHeader c ? IndexOf(c) : -1;

        void IList.Insert(int index, object value)
        {
            ((ColumnHeader)value).ListView = _owner;
            _items.Insert(index, (ColumnHeader)value);
            _owner.Changed();
        }

        void IList.Remove(object value) => Remove((ColumnHeader)value);

        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }

    public class ListViewItemCollection : IList
    {
        private readonly List<ListViewItem> _items = [];
        private readonly ListView _owner;

        public ListViewItemCollection(ListView owner) => _owner = owner;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        bool IList.IsFixedSize => false;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        public virtual ListViewItem this[int index]
        {
            get => _items[index];
            set
            {
                _items[index] = value;
                value.ListView = _owner;
                _owner.Changed();
            }
        }

        object IList.this[int index]
        {
            get => this[index];
            set => this[index] = (ListViewItem)value;
        }

        public virtual ListViewItem Add(string text) => Add(new ListViewItem(text));

        public virtual ListViewItem Add(ListViewItem value)
        {
            value.ListView = _owner;
            _items.Add(value);
            _owner.Changed();
            return value;
        }

        public void AddRange(ListViewItem[] items)
        {
            foreach (ListViewItem item in items)
            {
                Add(item);
            }
        }

        public virtual void Clear()
        {
            foreach (ListViewItem item in _items)
            {
                item.ListView = null;
            }

            _items.Clear();
            _owner.Changed();
        }

        public bool Contains(ListViewItem item) => _items.Contains(item);

        public int IndexOf(ListViewItem item) => _items.IndexOf(item);

        public ListViewItem Insert(int index, ListViewItem item)
        {
            item.ListView = _owner;
            _items.Insert(index, item);
            _owner.Changed();
            return item;
        }

        public virtual void Remove(ListViewItem item)
        {
            if (_items.Remove(item))
            {
                item.ListView = null;
                _owner.Changed();
            }
        }

        public virtual void RemoveAt(int index) => Remove(_items[index]);

        int IList.Add(object value)
        {
            Add((ListViewItem)value);
            return _items.Count - 1;
        }

        bool IList.Contains(object value) => value is ListViewItem i && Contains(i);

        int IList.IndexOf(object value) => value is ListViewItem i ? IndexOf(i) : -1;

        void IList.Insert(int index, object value) => Insert(index, (ListViewItem)value);

        void IList.Remove(object value) => Remove((ListViewItem)value);

        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }
}

public class ColumnHeader : Component, ICloneable
{
    public ColumnHeader()
    {
    }

    public ColumnHeader(int imageIndex) => ImageIndex = imageIndex;

    public ColumnHeader(string imageKey) => ImageKey = imageKey;

    public string Name { get; set; } = "";

    public string Text { get; set; } = "ColumnHeader";

    public int Width { get; set; } = 60;

    public HorizontalAlignment TextAlign { get; set; }

    public int ImageIndex { get; set; } = -1;

    public string ImageKey { get; set; } = "";

    public int DisplayIndex { get; set; }

    public object Tag { get; set; }

    public ListView ListView { get; internal set; }

    public int Index => ListView?.Columns.IndexOf(this) ?? -1;

    public object Clone() => new ColumnHeader { Name = Name, Text = Text, Width = Width, TextAlign = TextAlign, ImageIndex = ImageIndex, ImageKey = ImageKey, Tag = Tag };
}

public class ListViewItem : ICloneable
{
    private Font _font;

    public ListViewItem()
    {
        SubItems = new ListViewSubItemCollection(this);
        SubItems.Add(new ListViewSubItem(this, ""));
    }

    public ListViewItem(string text)
        : this() => Text = text;

    public ListViewItem(string text, int imageIndex)
        : this(text) => ImageIndex = imageIndex;

    public ListViewItem(string[] items)
        : this(items, -1)
    {
    }

    public ListViewItem(string[] items, int imageIndex)
    {
        SubItems = new ListViewSubItemCollection(this);
        ImageIndex = imageIndex;
        if (items is null || items.Length == 0)
        {
            SubItems.Add(new ListViewSubItem(this, ""));
        }
        else
        {
            foreach (string item in items)
            {
                SubItems.Add(new ListViewSubItem(this, item ?? ""));
            }
        }
    }

    public ListViewItem(string[] items, int imageIndex, Color foreColor, Color backColor, Font font)
        : this(items, imageIndex)
    {
        ForeColor = foreColor;
        BackColor = backColor;
        Font = font;
    }

    public ListViewSubItemCollection SubItems { get; }

    public string Text
    {
        get => SubItems[0].Text;
        set
        {
            SubItems[0].Text = value ?? "";
            Changed();
        }
    }

    public string Name { get; set; } = "";

    public object Tag { get; set; }

    public int ImageIndex { get; set; } = -1;

    public string ImageKey { get; set; } = "";

    public Color ForeColor { get; set; } = Color.Empty;

    public Color BackColor { get; set; } = Color.Empty;

    public Font Font
    {
        get => _font ?? ListView?.Font ?? Control.DefaultFont;
        set
        {
            _font = value;
            Changed();
        }
    }

    public bool Selected { get; set; }

    public bool Checked { get; set; }

    public bool Focused { get; set; }

    public bool UseItemStyleForSubItems { get; set; } = true;

    public string ToolTipText { get; set; } = "";

    public ListView ListView { get; internal set; }

    public int Index => ListView?.Items.IndexOf(this) ?? -1;

    /// <summary>The row in Details view: under the header, one row per item, as wide as the columns.</summary>
    public Rectangle Bounds
    {
        get
        {
            if (ListView is not { } list || Index < 0)
            {
                return Rectangle.Empty;
            }

            int width = list.Columns.Cast<ColumnHeader>().Sum(c => c.Width);
            return new Rectangle(0, list.HeaderHeight + (Index * list.RowHeight), width, list.RowHeight);
        }
    }

    public Rectangle GetBounds(ItemBoundsPortion portion) => Bounds;

    public void EnsureVisible()
    {
    }

    public void Remove() => ListView?.Items.Remove(this);

    public virtual object Clone()
    {
        var clone = new ListViewItem(SubItems.Cast<ListViewSubItem>().Select(s => s.Text).ToArray(), ImageIndex)
        {
            Name = Name,
            Tag = Tag,
            ForeColor = ForeColor,
            BackColor = BackColor,
            Checked = Checked,
            UseItemStyleForSubItems = UseItemStyleForSubItems,
            ToolTipText = ToolTipText,
        };
        if (_font is not null)
        {
            clone.Font = _font;
        }

        return clone;
    }

    public override string ToString() => "ListViewItem: {" + Text + "}";

    internal void Changed() => ListView?.Changed();

    public class ListViewSubItem
    {
        private string _text = "";

        public ListViewSubItem()
        {
        }

        public ListViewSubItem(ListViewItem owner, string text)
        {
            Owner = owner;
            _text = text ?? "";
        }

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? "";
                Owner?.Changed();
            }
        }

        public string Name { get; set; } = "";

        public object Tag { get; set; }

        public Color ForeColor { get; set; } = Color.Empty;

        public Color BackColor { get; set; } = Color.Empty;

        public Font Font { get; set; }

        internal ListViewItem Owner { get; set; }

        public override string ToString() => "ListViewSubItem: {" + Text + "}";
    }

    public class ListViewSubItemCollection : IList
    {
        private readonly List<ListViewSubItem> _items = [];
        private readonly ListViewItem _owner;

        public ListViewSubItemCollection(ListViewItem owner) => _owner = owner;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        bool IList.IsFixedSize => false;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        public ListViewSubItem this[int index]
        {
            get => _items[index];
            set
            {
                value.Owner = _owner;
                _items[index] = value;
                _owner.Changed();
            }
        }

        object IList.this[int index]
        {
            get => this[index];
            set => this[index] = (ListViewSubItem)value;
        }

        public ListViewSubItem Add(ListViewSubItem item)
        {
            item.Owner = _owner;
            _items.Add(item);
            _owner.Changed();
            return item;
        }

        public ListViewSubItem Add(string text) => Add(new ListViewSubItem(_owner, text));

        public void AddRange(string[] items)
        {
            foreach (string item in items)
            {
                Add(item);
            }
        }

        public void Clear()
        {
            _items.Clear();
            _owner.Changed();
        }

        public bool Contains(ListViewSubItem item) => _items.Contains(item);

        public int IndexOf(ListViewSubItem item) => _items.IndexOf(item);

        public void Insert(int index, ListViewSubItem item)
        {
            item.Owner = _owner;
            _items.Insert(index, item);
            _owner.Changed();
        }

        public void Remove(ListViewSubItem item)
        {
            _items.Remove(item);
            _owner.Changed();
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            _owner.Changed();
        }

        int IList.Add(object value)
        {
            Add((ListViewSubItem)value);
            return _items.Count - 1;
        }

        bool IList.Contains(object value) => value is ListViewSubItem s && Contains(s);

        int IList.IndexOf(object value) => value is ListViewSubItem s ? IndexOf(s) : -1;

        void IList.Insert(int index, object value) => Insert(index, (ListViewSubItem)value);

        void IList.Remove(object value) => Remove((ListViewSubItem)value);

        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }
}

public enum ItemBoundsPortion
{
    Entire = 0,
    Icon = 1,
    Label = 2,
    ItemOnly = 3,
}
