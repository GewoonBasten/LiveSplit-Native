using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// macOS: LiveSplit asks for the permission global hotkeys need (Input Monitoring) at start while
/// it is missing, until Don't ask again, and when global hotkeys are turned on.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class HotkeyPermissionTests
{
    [Fact]
    public void Asks_While_The_Permission_Is_Missing_And_At_Start_Until_Dont_Ask_Again()
    {
        var asking = new AppSettings();
        var notAsking = new AppSettings { AskForHotkeyPermission = false };
        Assert.False(MainWindow.ShouldAskForHotkeyPermission(atStart: true, KeyListenPermission.Granted, asking));
        Assert.True(MainWindow.ShouldAskForHotkeyPermission(atStart: true, KeyListenPermission.Denied, asking));
        Assert.True(MainWindow.ShouldAskForHotkeyPermission(atStart: true, KeyListenPermission.NotAsked, asking));
        Assert.False(MainWindow.ShouldAskForHotkeyPermission(atStart: true, KeyListenPermission.Denied, notAsking));

        // Global hotkeys just turned on: asked even after Don't ask again.
        Assert.True(MainWindow.ShouldAskForHotkeyPermission(atStart: false, KeyListenPermission.Denied, notAsking));
        Assert.False(MainWindow.ShouldAskForHotkeyPermission(atStart: false, KeyListenPermission.Granted, notAsking));
    }

    [AvaloniaFact]
    public void The_Question_At_Start_Has_Dont_Ask_Again()
    {
        (Window atStart, Button notNow, Button open, CheckBox? dontAsk) = MainWindow.HotkeyPermissionDialog(atStart: true, globalHotkeysOn: false);
        Assert.NotNull(dontAsk);
        Assert.Equal("Not Now", notNow.Content);
        Assert.Equal("Open System Settings", open.Content);
        Assert.True(open.IsDefault);
        string text = Text(atStart);
        Assert.Contains("Input Monitoring", text, StringComparison.Ordinal);
        Assert.Contains("turn on Global Hotkeys", text, StringComparison.Ordinal);

        (Window turnedOn, _, _, CheckBox? none) = MainWindow.HotkeyPermissionDialog(atStart: false, globalHotkeysOn: true);
        Assert.Null(none);
        Assert.DoesNotContain("turn on Global Hotkeys", Text(turnedOn), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Dont_Ask_Again_Is_Saved_And_Open_Goes_To_MacOs_Or_The_Settings()
    {
        string dir = HeadlessUi.NewTempDir();
        var actions = new List<string>();
        MainWindow? window = null;
        try
        {
            string settingsPath = Path.Combine(dir, "settings.cfg");
            var settings = new AppSettings();
            settings.HotkeyProfile.GlobalHotkeysEnabled = false;
            settings.HotkeyProfile.ToggleGlobalHotkeys = "";
            XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
            AppCli.Parse(["--settings", settingsPath]);
            window = new MainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            MainWindow.HotkeyPermissionActionsForTests = actions;

            // Allowed: no question.
            MainWindow.HotkeyPermissionForTests = KeyListenPermission.Granted;
            await window.AskForHotkeyPermissionAsync(atStart: true);
            Assert.Equal(0, window.HotkeyPermissionQuestionsForTests);

            // Never asked for: Open System Settings brings macOS's own question (which lists LiveSplit).
            MainWindow.HotkeyPermissionForTests = KeyListenPermission.NotAsked;
            MainWindow.HotkeyPermissionAnswerForTests = (Open: true, DontAskAgain: false);
            await window.AskForHotkeyPermissionAsync(atStart: true);
            Assert.Equal(["ask macOS"], actions);
            Assert.True(window.CurrentSettings.AskForHotkeyPermission);

            // Off in System Settings, Not Now with Don't ask again: kept in settings.cfg, not asked at start again.
            MainWindow.HotkeyPermissionForTests = KeyListenPermission.Denied;
            MainWindow.HotkeyPermissionAnswerForTests = (Open: false, DontAskAgain: true);
            await window.AskForHotkeyPermissionAsync(atStart: true);
            Assert.Equal(2, window.HotkeyPermissionQuestionsForTests);
            Assert.False(window.CurrentSettings.AskForHotkeyPermission);
            Assert.False(XmlAppSettingsSerializer.LoadFile(settingsPath).AskForHotkeyPermission);
            await window.AskForHotkeyPermissionAsync(atStart: true);
            Assert.Equal(2, window.HotkeyPermissionQuestionsForTests);

            // Global hotkeys turned on: asked (once a session), and Open System Settings opens the list.
            MainWindow.HotkeyPermissionAnswerForTests = (Open: true, DontAskAgain: false);
            window.CurrentSettings.HotkeyProfile.GlobalHotkeysEnabled = true;
            window.RefreshHotkeyHook();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, window.HotkeyPermissionQuestionsForTests);
            Assert.Equal(["ask macOS", "open settings"], actions);

            window.CurrentSettings.HotkeyProfile.GlobalHotkeysEnabled = false;
            window.RefreshHotkeyHook();
            window.CurrentSettings.HotkeyProfile.GlobalHotkeysEnabled = true;
            window.RefreshHotkeyHook();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, window.HotkeyPermissionQuestionsForTests);
        }
        finally
        {
            MainWindow.HotkeyPermissionForTests = null;
            MainWindow.HotkeyPermissionAnswerForTests = null;
            MainWindow.HotkeyPermissionActionsForTests = null;
            if (window is not null)
            {
                window.CurrentLayout.HasChanged = false;
                window.Session.State.Run.HasChanged = false;
                window.Close();
            }

            HeadlessUi.Delete(dir);
        }
    }

    private static string Text(Window window) =>
        string.Join(" ", window.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
}
