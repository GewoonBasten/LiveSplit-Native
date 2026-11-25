using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;
using static LiveSplitNative.App.Tests.HeadlessUi;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// The context menu opens when the right button comes up (Windows WM_CONTEXTMENU), never while
/// it is down: on macOS the NSMenu's tracking loop would swallow that mouse-up and Avalonia would
/// keep the button pressed, so no later left press arrived (moving and resizing stopped working
/// after Edit Layout / Layout Settings were opened from the menu). The real-window lifecycle is
/// checked by --interaction-selftest; this covers the input order headless.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class ContextMenuInputTests
{
    [AvaloniaFact]
    public void Right_Press_Waits_For_The_Release_And_Left_Presses_Still_Arrive()
    {
        string dir = NewTempDir();
        AppCli.Parse(["--settings", Path.Combine(dir, "settings.cfg")]);
        var window = new MainWindow();
        window.Show();
        Pump();
        var center = new Point(window.ClientSize.Width / 2, window.ClientSize.Height / 2);

        window.MouseDown(center, MouseButton.Right);
        Pump();
        Assert.Equal(0, window.ContextMenusShown);
        window.MouseUp(center, MouseButton.Right);
        Pump();
        Assert.Equal(1, window.ContextMenusShown);

        // Close the (headless, Avalonia) menu, then a left press is a press again.
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Pump();
        int presses = window.PointerPresses;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Pump();
        Assert.Equal(presses + 1, window.PointerPresses);

        window.Close();
        Pump();
        Delete(dir);
    }
}
