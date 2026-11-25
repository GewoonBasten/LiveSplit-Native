// The common controls of component settings pages: Label, Button, CheckBox, RadioButton,
// ComboBox, NumericUpDown, TextBox, as WinForms behaves (events, auto-checking radio buttons,
// clamped values) and sizes them.
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using LiveSplitNative.ComponentHost.Compat;

namespace System.Windows.Forms;

public class Label : Control
{
    public Label() => base.AutoSize = false;

    public ContentAlignment TextAlign { get; set; } = ContentAlignment.TopLeft;

    public BorderStyle BorderStyle { get; set; }

    public bool AutoEllipsis { get; set; }

    public bool UseMnemonic { get; set; } = true;

    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    public Image Image { get; set; }

    public bool UseCompatibleTextRendering { get; set; }

    protected override Padding DefaultMargin => new(3, 0, 3, 0);

    protected override Size DefaultSize => new(100, 23);

    internal override string HostKind => "Label";

    internal override int HostTextAlign => (int)TextAlign;

    internal override Size GetContentSize()
    {
        Size text = MeasureText(Text);
        int maxWidth = MaximumSize.Width - Padding.Horizontal;
        if (MaximumSize.Width > 0 && text.Width > maxWidth)
        {
            // An AutoSize label with a MaximumSize width wraps its text at word breaks.
            text = MeasureWrapped(Text, Math.Max(1, maxWidth));
        }

        return new Size(text.Width + Padding.Horizontal, text.Height + Padding.Vertical);
    }

    private Size MeasureWrapped(string text, int maxWidth)
    {
        int lineHeight = MeasureText("A").Height;
        int lines = 0;
        int widest = 0;
        foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && MeasureText(candidate).Width > maxWidth)
                {
                    widest = Math.Max(widest, MeasureText(line).Width);
                    lines++;
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }

            widest = Math.Max(widest, MeasureText(line).Width);
            lines++;
        }

        return new Size(Math.Min(widest, maxWidth), lines * lineHeight);
    }
}

public class LinkLabel : Label
{
    public event LinkLabelLinkClickedEventHandler LinkClicked;

    public Color LinkColor { get; set; } = Color.Blue;

    public bool LinkVisited { get; set; }

    internal override void HostClick()
    {
        base.HostClick();
        LinkClicked?.Invoke(this, new LinkLabelLinkClickedEventArgs(null));
    }
}

public class LinkLabelLinkClickedEventArgs : EventArgs
{
    public LinkLabelLinkClickedEventArgs(object link) => Link = link;

    public object Link { get; }
}

public delegate void LinkLabelLinkClickedEventHandler(object sender, LinkLabelLinkClickedEventArgs e);

public abstract class ButtonBase : Control
{
    private FlatStyle _flatStyle = FlatStyle.Standard;

    protected ButtonBase()
    {
    }

    public FlatStyle FlatStyle
    {
        get => _flatStyle;
        set
        {
            _flatStyle = value;
            RaiseHostChanged();
        }
    }

    public FlatButtonAppearance FlatAppearance { get; } = new();

    public bool UseVisualStyleBackColor { get; set; } = true;

    /// <summary>Setting a button's BackColor (outside the designer) turns UseVisualStyleBackColor off.</summary>
    public override Color BackColor
    {
        get => base.BackColor;
        set
        {
            UseVisualStyleBackColor = false;
            base.BackColor = value;
        }
    }

    public ContentAlignment TextAlign { get; set; } = ContentAlignment.MiddleCenter;

    public ContentAlignment ImageAlign { get; set; } = ContentAlignment.MiddleCenter;

    public Image Image { get; set; }

    public bool UseMnemonic { get; set; } = true;

    public bool UseCompatibleTextRendering { get; set; }

    public bool AutoEllipsis { get; set; }

    public TextImageRelation TextImageRelation { get; set; }
}

public class FlatButtonAppearance
{
    public int BorderSize { get; set; } = 1;

    public Color BorderColor { get; set; }

    public Color MouseOverBackColor { get; set; }

    public Color MouseDownBackColor { get; set; }

    public Color CheckedBackColor { get; set; }
}

public enum TextImageRelation
{
    Overlay = 0,
    ImageAboveText = 1,
    TextAboveImage = 2,
    ImageBeforeText = 4,
    TextBeforeImage = 8,
}

public class Button : ButtonBase, IButtonControl
{
    public DialogResult DialogResult { get; set; }

    public virtual AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    protected override Size DefaultSize => new(75, 23);

    internal override string HostKind => "Button";

    /// <summary>A flat button with a color is a color button (SettingsHelper.ColorButtonClick).</summary>
    internal override Color? HostBackColor => FlatStyle is FlatStyle.Flat or FlatStyle.Popup || !UseVisualStyleBackColor ? BackColor : null;

    public void NotifyDefault(bool value)
    {
    }

    public void PerformClick() => OnClick(EventArgs.Empty);

    internal override Size GetContentSize()
    {
        Size text = MeasureText(Text);
        return new Size(Math.Max(Size.Width, text.Width + 16), Math.Max(Size.Height, text.Height + 8));
    }
}

public interface IButtonControl
{
    DialogResult DialogResult { get; set; }

    void NotifyDefault(bool value);

    void PerformClick();
}

public enum CheckState
{
    Unchecked = 0,
    Checked = 1,
    Indeterminate = 2,
}

public enum Appearance
{
    Normal = 0,
    Button = 1,
}

public class CheckBox : ButtonBase
{
    private CheckState _state;

    public CheckBox() => TextAlign = ContentAlignment.MiddleLeft;

    public event EventHandler CheckedChanged;

    public event EventHandler CheckStateChanged;

    public event EventHandler AppearanceChanged;

    public bool Checked
    {
        get => _state != CheckState.Unchecked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    public CheckState CheckState
    {
        get => _state;
        set
        {
            if (_state != value)
            {
                bool wasChecked = Checked;
                _state = value;
                if (wasChecked != Checked)
                {
                    OnCheckedChanged(EventArgs.Empty);
                }

                CheckStateChanged?.Invoke(this, EventArgs.Empty);
                RaiseHostChanged();
            }
        }
    }

    public bool ThreeState { get; set; }

    public bool AutoCheck { get; set; } = true;

    public Appearance Appearance { get; set; }

    public ContentAlignment CheckAlign { get; set; } = ContentAlignment.MiddleLeft;

    protected override Size DefaultSize => new(104, 24);

    internal override string HostKind => "CheckBox";

    internal override bool HostChecked => Checked;

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    internal override void HostSetChecked(bool value)
    {
        if (Checked != value)
        {
            Checked = value;
        }

        OnClick(EventArgs.Empty);
    }

    internal override void HostClick() => HostSetChecked(!Checked);

    internal override Size GetContentSize()
    {
        Size text = MeasureText(Text);
        return new Size(text.Width + 20 + Padding.Horizontal, Math.Max(Math.Max(text.Height, 16) + 2, HostText.Spacing.CheckHeight) + Padding.Vertical);
    }
}

public class RadioButton : ButtonBase
{
    private bool _checked;

    public RadioButton()
    {
        TextAlign = ContentAlignment.MiddleLeft;
        TabStop = false;
    }

    public event EventHandler CheckedChanged;

    public event EventHandler AppearanceChanged;

    /// <summary>Checking one unchecks the other radio buttons of the same parent (AutoCheck).</summary>
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            if (value && AutoCheck && ParentInternal is { } parent)
            {
                foreach (Control sibling in parent.Controls)
                {
                    if (sibling != this && sibling is RadioButton { AutoCheck: true } radio)
                    {
                        radio.Checked = false;
                    }
                }
            }

            OnCheckedChanged(EventArgs.Empty);
            RaiseHostChanged();
        }
    }

    public new bool TabStop
    {
        get => base.TabStop;
        set => base.TabStop = value;
    }

    public bool AutoCheck { get; set; } = true;

    public Appearance Appearance { get; set; }

    public ContentAlignment CheckAlign { get; set; } = ContentAlignment.MiddleLeft;

    protected override Size DefaultSize => new(104, 24);

    internal override string HostKind => "RadioButton";

    internal override bool HostChecked => Checked;

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    public void PerformClick() => HostSetChecked(true);

    internal override void HostSetChecked(bool value)
    {
        if (value && !Checked)
        {
            Checked = true;
        }

        OnClick(EventArgs.Empty);
    }

    internal override void HostClick() => HostSetChecked(true);

    internal override Size GetContentSize()
    {
        Size text = MeasureText(Text);
        return new Size(text.Width + 20 + Padding.Horizontal, Math.Max(Math.Max(text.Height, 16) + 2, HostText.Spacing.CheckHeight) + Padding.Vertical);
    }
}

public abstract class ListControl : Control
{
    public event EventHandler SelectedValueChanged;

    public event EventHandler DataSourceChanged;

    public bool FormattingEnabled { get; set; }

    public string DisplayMember { get; set; } = "";

    public string ValueMember { get; set; } = "";

    public string FormatString { get; set; } = "";

    public object DataSource { get; set; }

    public abstract int SelectedIndex { get; set; }

    public object SelectedValue { get; set; }

    public string GetItemText(object item) => item?.ToString() ?? "";

    protected virtual void OnSelectedValueChanged(EventArgs e) => SelectedValueChanged?.Invoke(this, e);
}

public class ComboBox : ListControl
{
    private int _selectedIndex = -1;
    private string _text = "";

    public ComboBox() => Items = new ObjectCollection(this);

    public event EventHandler SelectedIndexChanged;

    public event EventHandler SelectionChangeCommitted;

    public event EventHandler DropDown;

    public event EventHandler DropDownClosed;

    public event EventHandler DropDownStyleChanged;

    public ObjectCollection Items { get; }

    public ComboBoxStyle DropDownStyle { get; set; } = ComboBoxStyle.DropDown;

    public int DropDownWidth { get; set; } = 121;

    public int DropDownHeight { get; set; } = 106;

    public int MaxDropDownItems { get; set; } = 8;

    public bool IntegralHeight { get; set; } = true;

    public bool Sorted { get; set; }

    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    public AutoCompleteMode AutoCompleteMode { get; set; }

    public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

    public AutoCompleteStringCollection AutoCompleteCustomSource { get; set; } = [];

    public override int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < -1 || value >= Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (_selectedIndex != value)
            {
                _selectedIndex = value;
                _text = value >= 0 ? GetItemText(Items[value]) : "";
                OnSelectedIndexChanged(EventArgs.Empty);
                OnSelectedValueChanged(EventArgs.Empty);
                RaiseHostChanged();
            }
        }
    }

    public object SelectedItem
    {
        get => _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : Items.IndexOf(value);
    }

    public override string Text
    {
        get => DropDownStyle == ComboBoxStyle.DropDownList ? GetItemText(SelectedItem) : _text;
        set
        {
            int index = Items.IndexOf(value);
            if (index >= 0)
            {
                SelectedIndex = index;
            }
            else if (DropDownStyle != ComboBoxStyle.DropDownList)
            {
                _text = value ?? "";
                OnTextChanged(EventArgs.Empty);
                RaiseHostChanged();
            }
        }
    }

    protected override Size DefaultSize => new(121, Math.Max(21, HostText.Spacing.ControlHeight));

    internal override int HostMinimumHeight => HostText.Spacing.ControlHeight;

    internal override string HostKind => "ComboBox";

    internal override IReadOnlyList<string> HostItems => Items.Cast<object>().Select(GetItemText).ToList();

    internal override int HostSelectedIndex => _selectedIndex;

    internal override void HostSetSelectedIndex(int index)
    {
        SelectedIndex = index;
        SelectionChangeCommitted?.Invoke(this, EventArgs.Empty);
    }

    internal override Size GetContentSize() => new(Size.Width, Math.Max(Size.Height, MeasureText("A").Height + 6));

    protected virtual void OnSelectedIndexChanged(EventArgs e) => SelectedIndexChanged?.Invoke(this, e);

    public void BeginUpdate()
    {
    }

    public void EndUpdate()
    {
    }

    public int FindStringExact(string s) => Items.Cast<object>().Select(GetItemText).ToList().IndexOf(s);

    internal void ItemsChanged()
    {
        if (_selectedIndex >= Items.Count)
        {
            _selectedIndex = -1;
        }

        RaiseHostChanged();
    }

    public class ObjectCollection : IList
    {
        private readonly List<object> _items = [];
        private readonly ComboBox _owner;

        public ObjectCollection(ComboBox owner) => _owner = owner;

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        bool IList.IsFixedSize => false;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        public virtual object this[int index]
        {
            get => _items[index];
            set
            {
                _items[index] = value;
                _owner.ItemsChanged();
            }
        }

        public int Add(object item)
        {
            _items.Add(item);
            _owner.ItemsChanged();
            return _items.Count - 1;
        }

        public void AddRange(object[] items)
        {
            _items.AddRange(items);
            _owner.ItemsChanged();
        }

        public void Clear()
        {
            _items.Clear();
            _owner._selectedIndex = -1;
            _owner.ItemsChanged();
        }

        public bool Contains(object value) => IndexOf(value) >= 0;

        public int IndexOf(object value) => _items.FindIndex(i => Equals(i, value));

        public void Insert(int index, object item)
        {
            _items.Insert(index, item);
            _owner.ItemsChanged();
        }

        public void Remove(object value)
        {
            _items.Remove(value);
            _owner.ItemsChanged();
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            _owner.ItemsChanged();
        }

        public void CopyTo(object[] destination, int arrayIndex) => _items.CopyTo(destination, arrayIndex);

        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();
    }
}

public enum AutoCompleteMode
{
    None = 0,
    Suggest = 1,
    Append = 2,
    SuggestAppend = 3,
}

public enum AutoCompleteSource
{
    FileSystem = 1,
    HistoryList = 2,
    RecentlyUsedList = 4,
    AllUrl = 6,
    AllSystemSources = 7,
    FileSystemDirectories = 32,
    CustomSource = 64,
    None = 128,
    ListItems = 256,
}

public class AutoCompleteStringCollection : List<string>
{
}

public abstract class UpDownBase : ContainerControl
{
    public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;

    public HorizontalAlignment TextAlign { get; set; }

    public bool ReadOnly { get; set; }

    public bool InterceptArrowKeys { get; set; } = true;

    public LeftRightAlignment UpDownAlign { get; set; } = LeftRightAlignment.Right;

    protected override Size DefaultSize => new(120, Math.Max(20, HostText.Spacing.ControlHeight));

    public abstract void UpButton();

    public abstract void DownButton();
}

public enum LeftRightAlignment
{
    Left = 0,
    Right = 1,
}

public class NumericUpDown : UpDownBase, ISupportInitialize
{
    private decimal _value;
    private decimal _minimum;
    private decimal _maximum = 100;
    private bool _initializing;

    public event EventHandler ValueChanged;

    /// <summary>The value, kept between Minimum and Maximum (WinForms throws; a binding to a
    /// setting outside them is clamped here instead).</summary>
    public decimal Value
    {
        get => _value;
        set
        {
            decimal clamped = _initializing ? value : Math.Clamp(value, _minimum, _maximum);
            if (_value != clamped)
            {
                _value = clamped;
                OnValueChanged(EventArgs.Empty);
                RaiseHostChanged();
            }
        }
    }

    public decimal Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < value)
            {
                _maximum = value;
            }

            if (!_initializing && _value < value)
            {
                Value = value;
            }

            RaiseHostChanged();
        }
    }

    public decimal Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > value)
            {
                _minimum = value;
            }

            if (!_initializing && _value > value)
            {
                Value = value;
            }

            RaiseHostChanged();
        }
    }

    public decimal Increment { get; set; } = 1;

    public int DecimalPlaces { get; set; }

    public bool Hexadecimal { get; set; }

    public bool ThousandsSeparator { get; set; }

    public override string Text
    {
        get => _value.ToString("F" + DecimalPlaces, Globalization.CultureInfo.CurrentCulture);
        set
        {
            if (decimal.TryParse(value, Globalization.NumberStyles.Number, Globalization.CultureInfo.CurrentCulture, out decimal parsed))
            {
                Value = parsed;
            }
        }
    }

    internal override string HostKind => "NumericUpDown";

    internal override int HostMinimumHeight => HostText.Spacing.ControlHeight;

    internal override decimal HostValue => _value;

    internal override decimal HostMinimum => _minimum;

    internal override decimal HostMaximum => _maximum;

    internal override int HostDecimalPlaces => DecimalPlaces;

    internal override void HostSetValue(decimal value) => Value = value;

    internal override Size GetContentSize() => new(Size.Width, Math.Max(Size.Height, MeasureText("0").Height + 6));

    public void BeginInit() => _initializing = true;

    public void EndInit()
    {
        _initializing = false;
        Value = _value;
    }

    public override void UpButton() => Value = Math.Min(_maximum, _value + Increment);

    public override void DownButton() => Value = Math.Max(_minimum, _value - Increment);

    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);
}

public abstract class TextBoxBase : Control
{
    public bool ReadOnly { get; set; }

    public bool Multiline { get; set; }

    public virtual int MaxLength { get; set; } = 32767;

    public BorderStyle BorderStyle { get; set; } = BorderStyle.Fixed3D;

    public bool WordWrap { get; set; } = true;

    public string[] Lines
    {
        get => Text.Split('\n');
        set => Text = string.Join(Environment.NewLine, value ?? []);
    }

    public int SelectionStart { get; set; }

    public int SelectionLength { get; set; }

    public void SelectAll()
    {
    }

    public void AppendText(string text) => Text += text;

    public void Clear() => Text = "";

    protected override Size DefaultSize => new(100, Math.Max(20, HostText.Spacing.ControlHeight));

    internal override int HostMinimumHeight => Multiline ? 0 : HostText.Spacing.ControlHeight;

    internal override string HostKind => "TextBox";

    internal override Size GetContentSize() => new(Size.Width, Math.Max(Size.Height, MeasureText("A").Height + 6));
}

public class TextBox : TextBoxBase
{
    public HorizontalAlignment TextAlign { get; set; }

    public char PasswordChar { get; set; }

    public bool UseSystemPasswordChar { get; set; }

    public ScrollBars ScrollBars { get; set; }

    public CharacterCasing CharacterCasing { get; set; }

    public AutoCompleteMode AutoCompleteMode { get; set; }

    public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

    public AutoCompleteStringCollection AutoCompleteCustomSource { get; set; } = [];
}

public enum ScrollBars
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    Both = 3,
}

public enum CharacterCasing
{
    Normal = 0,
    Upper = 1,
    Lower = 2,
}

/// <summary>
/// TrackBar: a slider between Minimum and Maximum (shown as the app's slider). Value is kept in
/// range (WinForms throws); the user's moves raise Scroll and ValueChanged.
/// </summary>
public class TrackBar : Control, ISupportInitialize
{
    private int _value;
    private int _minimum;
    private int _maximum = 10;
    private bool _initializing;

    public event EventHandler ValueChanged;

    public event EventHandler Scroll;

    public int Value
    {
        get => _value;
        set
        {
            int clamped = _initializing ? value : Math.Clamp(value, _minimum, _maximum);
            if (_value != clamped)
            {
                _value = clamped;
                OnValueChanged(EventArgs.Empty);
                RaiseHostChanged();
            }
        }
    }

    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < value)
            {
                _maximum = value;
            }

            if (!_initializing && _value < value)
            {
                Value = value;
            }

            RaiseHostChanged();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > value)
            {
                _minimum = value;
            }

            if (!_initializing && _value > value)
            {
                Value = value;
            }

            RaiseHostChanged();
        }
    }

    public int SmallChange { get; set; } = 1;

    public int LargeChange { get; set; } = 5;

    public int TickFrequency { get; set; } = 1;

    public TickStyle TickStyle { get; set; } = TickStyle.BottomRight;

    public Orientation Orientation { get; set; }

    public bool AutoSize { get; set; } = true;

    public void SetRange(int minValue, int maxValue)
    {
        Minimum = minValue;
        Maximum = maxValue;
    }

    protected override Size DefaultSize => new(104, 45);

    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    protected virtual void OnScroll(EventArgs e) => Scroll?.Invoke(this, e);

    internal override string HostKind => "TrackBar";

    internal override int HostMinimumHeight => HostText.Spacing.ControlHeight;

    internal override decimal HostValue => _value;

    internal override decimal HostMinimum => _minimum;

    internal override decimal HostMaximum => _maximum;

    internal override void HostSetValue(decimal value)
    {
        int before = _value;
        Value = (int)Math.Round(value);
        if (_value != before)
        {
            OnScroll(EventArgs.Empty);
        }
    }

    public void BeginInit() => _initializing = true;

    public void EndInit()
    {
        _initializing = false;
        Value = _value;
    }
}
