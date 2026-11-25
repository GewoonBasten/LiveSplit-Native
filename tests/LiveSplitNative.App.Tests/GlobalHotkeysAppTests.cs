using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Global hotkeys (hook_KeyOrButtonPressed) and the WebSocket server menu item.</summary>
[Collection(MainWindowCollection.Name)]
public class GlobalHotkeysAppTests
{
    [Fact]
    public void Mac_Key_Codes_Become_The_Profiles_Chords()
    {
        Assert.Equal("NumPad1", MacGlobalKeyboardHook.ChordFor(0x53, 0));
        Assert.Equal("Ctrl+Shift+F5", MacGlobalKeyboardHook.ChordFor(0x60, 0x40000 | 0x20000));
        Assert.Equal("Alt+Meta+A", MacGlobalKeyboardHook.ChordFor(0x00, 0x80000 | 0x100000));
        Assert.Equal("D1", MacGlobalKeyboardHook.ChordFor(0x12, 0));
        Assert.Equal("Space", MacGlobalKeyboardHook.ChordFor(0x31, 0));
        Assert.Equal("", MacGlobalKeyboardHook.ChordFor(0x38, 0x20000)); // Shift alone
        Assert.Equal("", MacGlobalKeyboardHook.ChordFor(0x3F, 0)); // fn

        // The same chord the timer window makes for the key.
        Assert.Equal(HotkeyChord.Format(Avalonia.Input.Key.NumPad1, Avalonia.Input.KeyModifiers.Control),
            MacGlobalKeyboardHook.ChordFor(0x53, 0x40000));
    }

    /// <summary>Hide (Basten 2 Nov) is in the timer's menu between Check for Updates... and Exit.</summary>
    [AvaloniaFact]
    public void The_Menu_Has_Hide_Before_Exit()
    {
        string dir = HeadlessUi.NewTempDir();
        try
        {
            MainWindow window = Open(dir, global: false);
            List<string> headers = window.ContextMenuModelForTests().Items.OfType<NativeMenuAction>().Select(i => i.Header).ToList();
            int hide = headers.IndexOf("Hide");
            Assert.True(hide > 0);
            Assert.Equal("Check for Updates...", headers[hide - 1]);
            Assert.Equal("Exit", headers[hide + 1]);
            Close(window);
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    private static MainWindow Open(string dir, bool global)
    {
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Hooked", CategoryName = "Any%" };
        run.AddSegment("One");
        run.AddSegment("Two");
        XmlRunSerializer.SaveFile(run, splitsPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var appSettings = new AppSettings();
        appSettings.HotkeyProfile.SplitKey = "NumPad1";
        appSettings.HotkeyProfile.ToggleGlobalHotkeys = "Ctrl+G";
        appSettings.HotkeyProfile.GlobalHotkeysEnabled = global;
        appSettings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(appSettings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Close(MainWindow window)
    {
        window.Session.State.Run.HasChanged = false;
        window.CurrentLayout.HasChanged = false;
        window.Close();
    }

    [AvaloniaFact]
    public void Keys_From_Other_Apps_Work_Only_With_Global_Hotkeys()
    {
        string dir = HeadlessUi.NewTempDir();
        try
        {
            MainWindow window = Open(dir, global: false);
            LiveSplitState state = window.Session.State;

            // Another app has the keyboard: nothing without global hotkeys.
            window.TimerHasKeyboardForTests = false;
            window.OnGlobalKey("NumPad1");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);

            // Toggle Global Hotkeys works anywhere; then the split key does.
            window.OnGlobalKey("Ctrl+G");
            Assert.True(window.SettingsForTests.HotkeyProfile.GlobalHotkeysEnabled);
            window.OnGlobalKey("NumPad1");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(TimerPhase.Running, state.CurrentPhase);

            // Unbound keys do nothing.
            window.OnGlobalKey("NumPad2");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, state.CurrentSplitIndex);
            Close(window);
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void Control_Menu_Starts_And_Stops_The_Servers()
    {
        string dir = HeadlessUi.NewTempDir();
        try
        {
            MainWindow window = Open(dir, global: false);
            window.SettingsForTests.ServerPort = 0;
            NativeMenuAction control = window.ContextMenuModelForTests().Items.OfType<NativeMenuAction>().First(i => i.Header == "Control");
            NativeMenuAction start = control.Children!.OfType<NativeMenuAction>().First(i => i.Header == "Start WebSocket Server");
            Assert.True(start.IsEnabled);
            Assert.True(control.Children!.OfType<NativeMenuAction>().First(i => i.Header == "Start TCP Server").IsEnabled);
            start.Invoke();
            Assert.Equal(ServerStateType.Websocket, window.ServerForTests!.ServerState);
            Assert.Equal(ServerStateType.Websocket, window.SettingsForTests.ServerState);

            control = window.ContextMenuModelForTests().Items.OfType<NativeMenuAction>().First(i => i.Header == "Control");
            NativeMenuAction stop = control.Children!.OfType<NativeMenuAction>().First(i => i.Header == "Stop WebSocket Server");
            // One server at a time: TCP is off while the WebSocket server runs.
            Assert.False(control.Children!.OfType<NativeMenuAction>().First(i => i.Header == "Start TCP Server").IsEnabled);
            stop.Invoke();
            Assert.Equal(ServerStateType.Off, window.ServerForTests.ServerState);

            control = window.ContextMenuModelForTests().Items.OfType<NativeMenuAction>().First(i => i.Header == "Control");
            control.Children!.OfType<NativeMenuAction>().First(i => i.Header == "Start TCP Server").Invoke();
            Assert.Equal(ServerStateType.TCP, window.ServerForTests.ServerState);
            control = window.ContextMenuModelForTests().Items.OfType<NativeMenuAction>().First(i => i.Header == "Control");
            Assert.False(control.Children!.OfType<NativeMenuAction>().First(i => i.Header == "Start WebSocket Server").IsEnabled);
            control.Children!.OfType<NativeMenuAction>().First(i => i.Header == "Stop TCP Server").Invoke();
            Assert.Equal(ServerStateType.Off, window.ServerForTests.ServerState);
            Close(window);
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }
}
