// Enums and small types of System.Windows.Forms (values as .NET Framework's).
using System.ComponentModel;

namespace System.Windows.Forms;

[Flags]
public enum AnchorStyles
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8,
}

public enum DockStyle
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 3,
    Right = 4,
    Fill = 5,
}

public enum AutoScaleMode
{
    None = 0,
    Font = 1,
    Dpi = 2,
    Inherit = 3,
}

public enum AutoSizeMode
{
    GrowAndShrink = 0,
    GrowOnly = 1,
}

public enum FlatStyle
{
    Flat = 0,
    Popup = 1,
    Standard = 2,
    System = 3,
}

public enum ComboBoxStyle
{
    Simple = 0,
    DropDown = 1,
    DropDownList = 2,
}

public enum SizeType
{
    AutoSize = 0,
    Absolute = 1,
    Percent = 2,
}

public enum DataSourceUpdateMode
{
    OnValidation = 0,
    OnPropertyChanged = 1,
    Never = 2,
}

public enum DialogResult
{
    None = 0,
    OK = 1,
    Cancel = 2,
    Abort = 3,
    Retry = 4,
    Ignore = 5,
    Yes = 6,
    No = 7,
}

public enum MessageBoxButtons
{
    OK = 0,
    OKCancel = 1,
    AbortRetryIgnore = 2,
    YesNoCancel = 3,
    YesNo = 4,
    RetryCancel = 5,
}

public enum MessageBoxIcon
{
    None = 0,
    Hand = 16,
    Error = 16,
    Stop = 16,
    Question = 32,
    Exclamation = 48,
    Warning = 48,
    Asterisk = 64,
    Information = 64,
}

public enum BorderStyle
{
    None = 0,
    FixedSingle = 1,
    Fixed3D = 2,
}

public enum HorizontalAlignment
{
    Left = 0,
    Right = 1,
    Center = 2,
}

public enum TableLayoutPanelCellBorderStyle
{
    None = 0,
    Single = 1,
}

public enum TableLayoutPanelGrowStyle
{
    FixedSize = 0,
    AddRows = 1,
    AddColumns = 2,
}

[Flags]
public enum TextFormatFlags
{
    Default = 0,
    Top = 0,
    Left = 0,
    HorizontalCenter = 1,
    Right = 2,
    VerticalCenter = 4,
    Bottom = 8,
    WordBreak = 16,
    SingleLine = 32,
    ExpandTabs = 64,
    NoClipping = 256,
    ExternalLeading = 512,
    NoPrefix = 2048,
    Internal = 4096,
    TextBoxControl = 8192,
    PathEllipsis = 16384,
    EndEllipsis = 32768,
    ModifyString = 65536,
    RightToLeft = 131072,
    WordEllipsis = 262144,
    NoFullWidthCharacterBreak = 524288,
    HidePrefix = 1048576,
    PrefixOnly = 2097152,
    PreserveGraphicsClipping = 16777216,
    PreserveGraphicsTranslateTransform = 33554432,
    NoPadding = 268435456,
    LeftAndRightPadding = 536870912,
    GlyphOverhangPadding = 0,
}

public interface IWin32Window
{
    IntPtr Handle { get; }
}

[TypeConverter(typeof(ExpandableObjectConverter))]
public struct Padding : IEquatable<Padding>
{
    public static readonly Padding Empty = new(0);

    public Padding(int all)
    {
        Left = Top = Right = Bottom = all;
    }

    public Padding(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public int All
    {
        get => Left == Top && Top == Right && Right == Bottom ? Left : -1;
        set => Left = Top = Right = Bottom = value;
    }

    public int Left { get; set; }

    public int Top { get; set; }

    public int Right { get; set; }

    public int Bottom { get; set; }

    public int Horizontal => Left + Right;

    public int Vertical => Top + Bottom;

    public Drawing.Size Size => new(Horizontal, Vertical);

    public static bool operator ==(Padding p1, Padding p2) => p1.Equals(p2);

    public static bool operator !=(Padding p1, Padding p2) => !p1.Equals(p2);

    public bool Equals(Padding other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

    public override bool Equals(object obj) => obj is Padding other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    public override string ToString() => $"{{Left={Left},Top={Top},Right={Right},Bottom={Bottom}}}";

    internal Padding Scale(float x, float y) =>
        new((int)Math.Round(Left * x), (int)Math.Round(Top * y), (int)Math.Round(Right * x), (int)Math.Round(Bottom * y));
}

public class PaintEventArgs : EventArgs, IDisposable
{
    public PaintEventArgs(Drawing.Graphics graphics, Drawing.Rectangle clipRect)
    {
        Graphics = graphics;
        ClipRectangle = clipRect;
    }

    public Drawing.Graphics Graphics { get; }

    public Drawing.Rectangle ClipRectangle { get; }

    public void Dispose()
    {
    }
}

public delegate void PaintEventHandler(object sender, PaintEventArgs e);

public class ControlEventArgs : EventArgs
{
    public ControlEventArgs(Control control) => Control = control;

    public Control Control { get; }
}

public delegate void ControlEventHandler(object sender, ControlEventArgs e);

public enum View
{
    LargeIcon = 0,
    Details = 1,
    SmallIcon = 2,
    List = 3,
    Tile = 4,
}

public enum ColumnHeaderStyle
{
    None = 0,
    Nonclickable = 1,
    Clickable = 2,
}

public enum TickStyle
{
    None = 0,
    TopLeft = 1,
    BottomRight = 2,
    Both = 3,
}

public enum Orientation
{
    Horizontal = 0,
    Vertical = 1,
}

public enum ToolTipIcon
{
    None = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}
