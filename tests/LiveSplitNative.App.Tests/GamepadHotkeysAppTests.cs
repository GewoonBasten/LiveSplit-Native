using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Allow Gamepads as Hotkeys: a controller button splits, and sets a hotkey in Settings.</summary>
[Collection(MainWindowCollection.Name)]
public class GamepadHotkeysAppTests
{
    private const string Button = "Buttons0 Wireless Controller 054C-09CC";

    [AvaloniaFact]
    public void A_Controller_Button_Splits_While_The_Profile_Allows_Gamepads()
    {
        string dir = HeadlessUi.NewTempDir();
        var settings = new AppSettings();
        settings.HotkeyProfile.SplitKey = Button;
        settings.HotkeyProfile.GlobalHotkeysEnabled = true;
        settings.HotkeyProfile.AllowGamepadsAsHotkeys = true;
        string path = Path.Combine(dir, "settings.cfg");
        XmlAppSettingsSerializer.SaveFile(settings, path);
        Assert.Equal(Button, XmlAppSettingsSerializer.LoadFile(path).HotkeyProfile.SplitKey);
        AppCli.Parse(["--settings", path]);
        int before = TestEnvironment.Gamepads.Leases;
        var window = new MainWindow();
        window.Show();
        HeadlessUi.Pump();
        try
        {
            Assert.Equal(before + 1, TestEnvironment.Gamepads.Leases);
            TestEnvironment.Gamepads.Press("Buttons1 Wireless Controller 054C-09CC");
            HeadlessUi.Pump();
            Assert.Equal(TimerPhase.NotRunning, window.Session.State.CurrentPhase);
            TestEnvironment.Gamepads.Press(Button);
            HeadlessUi.Pump();
            Assert.Equal(TimerPhase.Running, window.Session.State.CurrentPhase);

            // Not allowed any more: the controllers are let go.
            window.SettingsForTests.HotkeyProfile.AllowGamepadsAsHotkeys = false;
            window.RefreshHotkeyHook();
            Assert.Equal(before, TestEnvironment.Gamepads.Leases);
        }
        finally
        {
            window.Session.State.Run.HasChanged = false;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Controller_Button_Sets_The_Focused_Hotkey_In_Settings()
    {
        var app = new AppSettings();
        app.HotkeyProfile.AllowGamepadsAsHotkeys = true;
        var window = new SettingsWindow(LayoutSettings.CreateStandard(), TimingMethod.RealTime, app);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            int before = TestEnvironment.Gamepads.Leases;
            TextBox split = window.GetLogicalDescendants().OfType<TextBox>().First(b => b.IsReadOnly);
            split.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(before + 1, TestEnvironment.Gamepads.Leases);
            TestEnvironment.Gamepads.Press(Button);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Button, split.Text);
            Assert.Equal(Button, window.HotkeyProfilesForTests[window.SelectedHotkeyProfile].SplitKey);
            // The field gave up the keyboard: no more buttons are taken.
            Assert.Equal(before, TestEnvironment.Gamepads.Leases);

            // Without Allow Gamepads as Hotkeys, a focused field waits for a key only.
            window.FindControl<CheckBox>("AllowGamepadsBox")!.IsChecked = false;
            split.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(before, TestEnvironment.Gamepads.Leases);
        }
        finally
        {
            window.Close();
        }
    }
}
