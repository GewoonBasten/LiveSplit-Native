// The rest of System.Windows.Forms that settings pages touch: keys, cursors, context menus,
// tool tips, TextRenderer, MessageBox, color and file dialogs (answered by the app or
// declined), Application.
using System.ComponentModel;
using System.Drawing;
using LiveSplitNative.ComponentHost.Compat;

namespace System.Windows.Forms;

[Flags]
public enum Keys
{
    None = 0,
    KeyCode = 0xFFFF,
    Modifiers = unchecked((int)0xFFFF0000),
    Back = 8,
    Tab = 9,
    Enter = 13,
    Return = 13,
    ShiftKey = 16,
    ControlKey = 17,
    Menu = 18,
    Escape = 27,
    Space = 32,
    Left = 37,
    Up = 38,
    Right = 39,
    Down = 40,
    Delete = 46,
    Shift = 0x10000,
    Control = 0x20000,
    Alt = 0x40000,
}

public enum RightToLeft
{
    No = 0,
    Yes = 1,
    Inherit = 2,
}

public enum ImageLayout
{
    None = 0,
    Tile = 1,
    Center = 2,
    Stretch = 3,
    Zoom = 4,
}

public enum AccessibleRole
{
    Default = -1,
    None = 0,
}

public sealed class Cursor : IDisposable
{
    public void Dispose()
    {
    }
}

public static class Cursors
{
    public static Cursor Default { get; } = new();

    public static Cursor Hand { get; } = new();

    public static Cursor WaitCursor { get; } = new();

    public static Cursor IBeam { get; } = new();
}

public class ToolStripItem : Component
{
    public event EventHandler Click;

    public string Text { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public bool Visible { get; set; } = true;

    public string Name { get; set; } = "";

    public object Tag { get; set; }

    public void PerformClick() => Click?.Invoke(this, EventArgs.Empty);
}

public class ToolStripMenuItem : ToolStripItem
{
    public ToolStripMenuItem()
    {
    }

    public ToolStripMenuItem(string text) => Text = text;

    public bool Checked { get; set; }

    public bool CheckOnClick { get; set; }
}

public class ToolStripSeparator : ToolStripItem
{
}

public class ContextMenuStrip : Component
{
    public List<ToolStripItem> Items { get; } = [];
}

public class ToolTip : Component
{
    public ToolTip()
    {
    }

    public ToolTip(IContainer cont)
    {
    }

    public bool Active { get; set; } = true;

    public int AutoPopDelay { get; set; } = 5000;

    public int InitialDelay { get; set; } = 500;

    public int ReshowDelay { get; set; } = 100;

    public bool ShowAlways { get; set; }

    public string ToolTipTitle { get; set; } = "";

    public ToolTipIcon ToolTipIcon { get; set; }

    public bool IsBalloon { get; set; }

    public void SetToolTip(Control control, string caption)
    {
    }

    public string GetToolTip(Control control) => "";
}

public static class TextRenderer
{
    public static Size MeasureText(string text, Font font) => Measure(null, text, font);

    public static Size MeasureText(string text, Font font, Size proposedSize) => Measure(null, text, font);

    public static Size MeasureText(string text, Font font, Size proposedSize, TextFormatFlags flags) => Measure(null, text, font);

    public static Size MeasureText(IDeviceContext dc, string text, Font font) => Measure(dc, text, font);

    public static Size MeasureText(IDeviceContext dc, string text, Font font, Size proposedSize) => Measure(dc, text, font);

    public static Size MeasureText(IDeviceContext dc, string text, Font font, Size proposedSize, TextFormatFlags flags) => Measure(dc, text, font);

    public static void DrawText(IDeviceContext dc, string text, Font font, Point pt, Color foreColor)
    {
        if (dc is Graphics g)
        {
            using var brush = new SolidBrush(foreColor);
            g.DrawString(text, font, brush, pt);
        }
    }

    public static void DrawText(IDeviceContext dc, string text, Font font, Rectangle bounds, Color foreColor, TextFormatFlags flags)
    {
        if (dc is Graphics g)
        {
            using var brush = new SolidBrush(foreColor);
            g.DrawString(text, font, brush, bounds);
        }
    }

    private static Size Measure(IDeviceContext dc, string text, Font font)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Size.Empty;
        }

        if (dc is Graphics g)
        {
            return g.MeasureText(text, font);
        }

        SizeF size = HostText.Measure(text, font.ToHost());
        return new Size((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
    }
}

public static class MessageBox
{
    /// <summary>The app's answer to a message box (set by the components' runtime).</summary>
    internal static Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> Handler { get; set; } =
        (_, _, buttons, _) => buttons is MessageBoxButtons.OK or MessageBoxButtons.OKCancel ? DialogResult.OK : DialogResult.Cancel;

    public static DialogResult Show(string text) => Show(text, "", MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption) => Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) => Show(text, caption, buttons, MessageBoxIcon.None);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Handler(text ?? "", caption ?? "", buttons, icon);

    public static DialogResult Show(IWin32Window owner, string text) => Show(text);

    public static DialogResult Show(IWin32Window owner, string text, string caption) => Show(text, caption);

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons) => Show(text, caption, buttons);

    public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Show(text, caption, buttons, icon);
}

public abstract class CommonDialog : Component
{
    public object Tag { get; set; }

    public DialogResult ShowDialog() => RunDialog(IntPtr.Zero) ? DialogResult.OK : DialogResult.Cancel;

    public DialogResult ShowDialog(IWin32Window owner) => ShowDialog();

    public abstract void Reset();

    protected abstract bool RunDialog(IntPtr hwndOwner);
}

public class ColorDialog : CommonDialog
{
    public Color Color { get; set; } = Color.Black;

    public bool AllowFullOpen { get; set; } = true;

    public bool AnyColor { get; set; }

    public bool FullOpen { get; set; }

    public int[] CustomColors { get; set; } = [];

    public bool SolidColorOnly { get; set; }

    public bool ShowHelp { get; set; }

    public override void Reset() => Color = Color.Black;

    /// <summary>The app's color picker, modal.</summary>
    protected override bool RunDialog(IntPtr hwndOwner)
    {
        if (HostDialogs.Current?.PickColorModal(Color, null, null) is { } color)
        {
            Color = color;
            return true;
        }

        return false;
    }
}

public abstract class FileDialog : CommonDialog
{
    public string FileName { get; set; } = "";

    public string[] FileNames => string.IsNullOrEmpty(FileName) ? [] : [FileName];

    public string Filter { get; set; } = "";

    public int FilterIndex { get; set; } = 1;

    public string InitialDirectory { get; set; } = "";

    public string Title { get; set; } = "";

    public string DefaultExt { get; set; } = "";

    public bool CheckFileExists { get; set; }

    public bool RestoreDirectory { get; set; }

    public override void Reset() => FileName = "";

    /// <summary>A modal file dialog cannot be shown synchronously here: declined.</summary>
    protected override bool RunDialog(IntPtr hwndOwner) => false;
}

public sealed class OpenFileDialog : FileDialog
{
    public bool Multiselect { get; set; }
}

public sealed class SaveFileDialog : FileDialog
{
    public bool OverwritePrompt { get; set; } = true;
}

public sealed class FolderBrowserDialog : CommonDialog
{
    public string SelectedPath { get; set; } = "";

    public string Description { get; set; } = "";

    public override void Reset() => SelectedPath = "";

    protected override bool RunDialog(IntPtr hwndOwner) => false;
}

public static class Application
{
    public static string ProductVersion => "1.8.37";

    public static string StartupPath => AppContext.BaseDirectory;

    public static void DoEvents()
    {
    }
}

public sealed class Timer : Component
{
    private System.Threading.Timer _timer;
    private readonly SynchronizationContext _context = SynchronizationContext.Current;

    public Timer()
    {
    }

    public Timer(IContainer container)
    {
    }

    public event EventHandler Tick;

    public int Interval { get; set; } = 100;

    public bool Enabled
    {
        get => _timer is not null;
        set
        {
            if (value)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }
    }

    public void Start()
    {
        _timer ??= new System.Threading.Timer(
            _ =>
            {
                if (_context is not null)
                {
                    _context.Post(_ => Tick?.Invoke(this, EventArgs.Empty), null);
                }
                else
                {
                    Tick?.Invoke(this, EventArgs.Empty);
                }
            },
            null,
            Interval,
            Interval);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    protected override void Dispose(bool disposing)
    {
        Stop();
        base.Dispose(disposing);
    }
}
