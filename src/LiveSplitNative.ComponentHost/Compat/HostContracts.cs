using System.Drawing;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.ComponentHost.Compat;

// The contracts between the app and a Windows LiveSplit component loaded unchanged. The component
// and the look-alike LiveSplit.Core / System.Windows.Forms / System.Drawing / UpdateManager it
// binds to live in their own AssemblyLoadContext (CompatComponentLoader); the app only sees these
// interfaces, which use no type of those look-alikes. Colors are System.Drawing.Color from
// System.Drawing.Primitives, which both sides share.

/// <summary>What the look-alike LiveSplit.Core offers the app (one per load context).</summary>
public interface ICompatRuntime
{
    /// <summary>The component factories a component DLL declares ([assembly: ComponentFactory]).</summary>
    IReadOnlyList<IHostedComponentFactory> GetFactories(System.Reflection.Assembly componentAssembly);

    /// <summary>The LiveSplitState a component sees, over the app's timer.</summary>
    IHostedTimer CreateTimer(IHostTimerSource source);
}

/// <summary>The app's timer, as the look-alike LiveSplitState reads it.</summary>
public interface IHostTimerSource
{
    LiveSplitState State { get; }

    TimerModel Model { get; }

    LayoutSettings LayoutSettings { get; }

    LayoutMode LayoutMode { get; }

    /// <summary>The app's settings (settings.cfg), as LiveSplitState.Settings reads them.</summary>
    LiveSplitNative.Core.Options.AppSettings Settings { get; }

    /// <summary>The app's dialogs (color picker, message boxes) for settings pages.</summary>
    IHostDialogs Dialogs { get; }

    /// <summary>
    /// The layout's components as LiveSplitState.Layout.Components shows them, in the layout's
    /// order: those of component DLLs, and the app's Subsplits (as Windows' LiveSplit.Subsplits).
    /// The app's other components are not IComponents and are not among them.
    /// </summary>
    IReadOnlyList<HostLayoutEntry> LayoutComponents { get; }

    /// <summary>The component asked to be redrawn (IInvalidator.Invalidate).</summary>
    void Invalidate();
}

/// <summary>A layout component a component DLL can see: a DLL's component or the app's Subsplits.</summary>
public sealed record HostLayoutEntry(IHostedComponent? Component, IHostSubsplits? Subsplits);

/// <summary>
/// The app's Subsplits component as Windows' LiveSplit.Subsplits SplitsComponent shows itself to
/// other components: its rows (SplitComponents) with their split, Header, CollapsedSplit and the
/// colors of their columns' labels, and its columns (ColumnData). A component that colors a
/// column's label (Best World Segments' gold) has the row drawn so until the next update.
/// </summary>
public interface IHostSubsplits
{
    IReadOnlyList<HostSubsplitsRow> Rows { get; }

    IReadOnlyList<HostSubsplitsColumn> Columns { get; }

    void SetColumnColor(int row, int column, Color color);
}

/// <summary>A row: the run's split it shows (-1 for a blank row), and its columns' colors.</summary>
public sealed record HostSubsplitsRow(int SplitIndex, bool Header, bool CollapsedSplit, IReadOnlyList<Color> ColumnColors);

/// <summary>ColumnData: Type is ColumnType's name (Delta, SplitTime, DeltaorSplitTime, …).</summary>
public sealed record HostSubsplitsColumn(string Name, string Type, string Comparison, string TimingMethod);

public interface IHostDialogs
{
    /// <summary>
    /// SettingsHelper.ColorButtonClick: the picked color, or null when cancelled; <paramref name="live"/>
    /// follows the color while it is being picked. <paramref name="owner"/> is the settings page's control.
    /// </summary>
    Task<Color?> PickColorAsync(Color current, object? owner, Action<Color>? live);

    /// <summary>
    /// The same, modal (ColorDialog.ShowDialog, WinFormsColor's ColorPickerDialog): returns when
    /// the dialog is closed, the app keeping running meanwhile.
    /// </summary>
    Color? PickColorModal(Color current, object? owner, Action<Color>? live);
}

/// <summary>The app's dialogs, for the look-alikes (set by the app before components load).</summary>
public static class HostDialogs
{
    public static IHostDialogs? Current { get; set; }
}

/// <summary>A LiveSplitState over one IHostTimerSource; components are created with it.</summary>
public interface IHostedTimer : IDisposable
{
    /// <summary>Re-reads the run when it changed (called before components update).</summary>
    void Refresh();
}

public interface IHostedComponentFactory
{
    /// <summary>The DLL's file name, which a layout stores as the component's Path.</summary>
    string FileName { get; }

    string ComponentName { get; }

    string Description { get; }

    /// <summary>ComponentCategory as a number (Timer 1, List 2, Information 3, Media 4, Control 5, Other 6).</summary>
    int Category { get; }

    IHostedComponent Create(IHostedTimer timer);
}

/// <summary>An IComponent, seen through types the app knows.</summary>
public interface IHostedComponent : IDisposable
{
    string ComponentName { get; }

    float HorizontalWidth { get; }

    float MinimumHeight { get; }

    float VerticalHeight { get; }

    float MinimumWidth { get; }

    float PaddingTop { get; }

    float PaddingBottom { get; }

    float PaddingLeft { get; }

    float PaddingRight { get; }

    /// <summary>IComponent.ContextMenuControls (null when none).</summary>
    IReadOnlyDictionary<string, Action>? ContextMenuControls { get; }

    void Update(float width, float height, LayoutMode mode);

    void DrawVertical(IHostCanvas canvas, float width);

    void DrawHorizontal(IHostCanvas canvas, float height);

    /// <summary>The inner XML of IComponent.GetSettings's &lt;Settings&gt; (what a layout stores).</summary>
    string GetSettingsInnerXml();

    /// <summary>IComponent.SetSettings with &lt;Settings&gt;inner XML&lt;/Settings&gt;.</summary>
    void SetSettingsInnerXml(string? innerXml);

    /// <summary>IComponent.GetSettingsControl, or null when it has none.</summary>
    IHostControl? GetSettingsControl(LayoutMode mode);
}

/// <summary>Where a component draws: the app's renderer, in the component's own coordinates.</summary>
public interface IHostCanvas
{
    float DpiY { get; }

    /// <summary>Graphics.TextRenderingHint is AntiAlias (outlined text is drawn only then).</summary>
    bool AntiAliasText { get; }

    void Fill(HostBrush brush, RectangleF rect);

    void FillPolygon(HostBrush brush, IReadOnlyList<PointF> points);

    void DrawLine(Color color, float width, PointF from, PointF to);

    void DrawRectangle(Color color, float width, RectangleF rect);

    void DrawString(string text, HostFont font, HostBrush brush, RectangleF layout, HostStringFormat format);

    /// <summary>Text as an outline (GraphicsPath.AddString + DrawPath / FillPath).</summary>
    void DrawStringPath(string text, HostFont font, RectangleF layout, HostStringFormat format, HostBrush? fill, Color outline, float outlineWidth);

    /// <summary>Graphics.MeasureString: the size of the text's layout box.</summary>
    SizeF MeasureString(string text, HostFont font, float maxWidth, HostStringFormat format);

    /// <summary>TextRenderer.MeasureText with NoPadding: the drawn width of the text in pixels.</summary>
    Size MeasureText(string text, HostFont font);

    void PushClip(RectangleF rect);

    void PopClip();
}

/// <summary>A font as System.Drawing.Font describes it.</summary>
public readonly record struct HostFont(string FamilyName, float Size, bool IsPixelUnit, bool Bold, bool Italic, bool Underline, bool Strikeout)
{
    /// <summary>The size in pixels (points are 96/72 pixels).</summary>
    public float SizeInPixels => IsPixelUnit ? Size : Size * 96f / 72f;
}

/// <summary>StringAlignment: 0 Near, 1 Center, 2 Far.</summary>
public readonly record struct HostStringFormat(int Alignment, int LineAlignment, bool NoWrap, bool EllipsisCharacter);

/// <summary>A SolidBrush, or a LinearGradientBrush from Start (Color1) to End (Color2).</summary>
public readonly record struct HostBrush(Color Color1, Color Color2, PointF Start, PointF End, bool IsGradient)
{
    public static HostBrush Solid(Color color) => new(color, color, default, default, false);
}

/// <summary>
/// A WinForms control of a settings page, laid out the way WinForms does (bounds in pixels in its
/// parent). The app shows each as a native control and passes the user's input back.
/// </summary>
public interface IHostControl
{
    /// <summary>Label, CheckBox, RadioButton, Button, ComboBox, NumericUpDown, TrackBar, TextBox, GroupBox, ListView, Panel.</summary>
    string Kind { get; }

    string Text { get; }

    Rectangle Bounds { get; }

    bool Visible { get; }

    bool Enabled { get; }

    bool Checked { get; }

    /// <summary>A flat button's BackColor (a color button).</summary>
    Color? BackColor { get; }

    IReadOnlyList<string> Items { get; }

    int SelectedIndex { get; }

    decimal Value { get; }

    decimal Minimum { get; }

    decimal Maximum { get; }

    int DecimalPlaces { get; }

    /// <summary>A label's TextAlign (System.Drawing.ContentAlignment's value; TopLeft is 1).</summary>
    int TextAlign { get; }

    IReadOnlyList<IHostControl> Children { get; }

    /// <summary>A ListView's columns and rows (null for other controls).</summary>
    HostListView? ListView { get; }

    /// <summary>Raised when anything the app shows changed (text, state, bounds, children).</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Lays the control and its children out for this size (WinForms Size), after AutoScale.
    /// The app calls it once with the page's own size and whenever the width it is given changes.
    /// </summary>
    void PerformLayout(Size size);

    /// <summary>UserControl.Load: raised once before the page is first shown.</summary>
    void OnShown();

    void SetChecked(bool value);

    void Click();

    void SetSelectedIndex(int index);

    void SetValue(decimal value);

    void SetText(string text);
}

/// <summary>
/// A ListView in Details view as WinForms lays it out: the header (when shown) of HeaderHeight
/// pixels, then one row of RowHeight pixels per item; columns of their widths.
/// </summary>
public sealed record HostListView(
    IReadOnlyList<HostListColumn> Columns,
    IReadOnlyList<HostListRow> Rows,
    int HeaderHeight,
    int RowHeight,
    bool ShowHeader,
    bool GridLines);

/// <summary>A column header: Alignment is HorizontalAlignment (0 Left, 1 Right, 2 Center).</summary>
public readonly record struct HostListColumn(string Text, int Width, int Alignment);

/// <summary>An item: its texts (the item and its subitems) and whether its font is bold.</summary>
public sealed record HostListRow(IReadOnlyList<string> Cells, bool Bold);

/// <summary>The app's text measuring, for WinForms' AutoSize and TextRenderer (set at startup).</summary>
public static class HostText
{
    /// <summary>The size of one line of text in pixels, as GDI draws it in a control.</summary>
    public static Func<string, HostFont, SizeF> Measure { get; set; } =
        (text, font) => new SizeF(text.Length * font.SizeInPixels * 0.55f, font.SizeInPixels * 1.2f);

    /// <summary>
    /// A face's vertical metrics in design units, as GDI+ FontFamily gives them (GetCellAscent,
    /// GetCellDescent, GetEmHeight): family, bold, italic. Arial's until the app sets its own.
    /// </summary>
    public static Func<string, bool, bool, (int Ascent, int Descent, int EmHeight)> CellMetrics { get; set; } =
        (_, _, _) => (1854, 434, 2048);

    /// <summary>The font WinForms controls use (Control.DefaultFont): the app's dialog font.</summary>
    public static HostFont DialogFont { get; set; } = new("Microsoft Sans Serif", 8.25f, false, false, false, false, false);

    /// <summary>How settings pages are spaced (WinForms' own until the app sets its own).</summary>
    public static HostSpacing Spacing { get; set; } = HostSpacing.WinForms;
}

/// <summary>
/// The spacing of component settings pages: the height of an input control (combo box, number
/// box, text box: WinForms makes it the font's) and of a check box's or radio button's line, a
/// group box's padding around its content and the gap between group boxes, whether the
/// label columns of a page's groups line up, the least height of a table's row (its controls
/// centred in it), the least width of a label column, the room a table row keeps around its
/// controls, and the least heights of a list's rows and header (0: WinForms' own).
/// </summary>
public sealed record HostSpacing(int ControlHeight, int CheckHeight, int GroupPaddingX, int GroupPaddingTop, int GroupPaddingBottom, int GroupGap, bool AlignLabelColumns, int RowHeight = 0, int LabelColumn = 0, int RowPadding = 0, int ListRowHeight = 0, int ListHeaderHeight = 0)
{
    /// <summary>WinForms' own: nothing added.</summary>
    public static HostSpacing WinForms { get; } = new(0, 0, 3, 3, 3, 6, false);
}
