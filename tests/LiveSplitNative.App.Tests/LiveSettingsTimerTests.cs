using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// The whole chain the user goes through: Edit Layout, Layout Settings, a change in a tab, the
/// running timer window. The change must show on the timer while the dialog is open, Cancel must
/// take it back, OK must keep it, and the window geometry in the layout must stay the window's.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class LiveSettingsTimerTests
{
    [AvaloniaFact]
    public async Task Layout_Settings_Changes_Reach_The_Timer_Live_And_Cancel_Takes_Them_Back()
    {
        string dir = NewRun(out string settingsPath);
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        double startHeight = window.Height;
        float startTimerHeight = TimerSettings(window).TimerHeight;

        // Round 1: change the Timer height and Drop Shadows, then Cancel both dialogs.
        (Task editTask, LayoutEditorWindow editor, LayoutSettingsWindow settings) = await OpenLayoutSettings(window);
        bool shadowsBefore = window.CurrentLayout.Settings.DropShadows;
        NumberIn(settings, "Timer", "Height:").Value += 30;
        CheckIn(settings, "Drop Shadows").IsChecked = !shadowsBefore;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(startTimerHeight + 30, TimerSettings(window).TimerHeight);
        Assert.Equal(!shadowsBefore, window.CurrentLayout.Settings.DropShadows);
        Assert.True(window.Height > startHeight + 1, $"window {window.Height} should grow from {startHeight}");
        Assert.Equal((int)Math.Round(window.Height), window.CurrentLayout.VerticalHeight);

        settings.Close(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(startTimerHeight, TimerSettings(window).TimerHeight);
        Assert.Equal(shadowsBefore, window.CurrentLayout.Settings.DropShadows);
        editor.Close(null);
        await editTask;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(startTimerHeight, TimerSettings(window).TimerHeight);
        Assert.Equal((int)Math.Round(window.Height), window.CurrentLayout.VerticalHeight);

        // Round 2: change it again and press OK in both dialogs: it stays, geometry included.
        (editTask, editor, settings) = await OpenLayoutSettings(window);
        NumberIn(settings, "Timer", "Height:").Value += 30;
        Dispatcher.UIThread.RunJobs();
        Click(settings, "OK");
        Dispatcher.UIThread.RunJobs();
        Click(editor, "OK");
        await editTask;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(startTimerHeight + 30, TimerSettings(window).TimerHeight);
        Assert.Equal((int)Math.Round(window.Height), window.CurrentLayout.VerticalHeight);
        Assert.Equal((int)Math.Round(window.Width), window.CurrentLayout.VerticalWidth);

        window.Session.State.Run.HasChanged = false;
        window.CurrentLayout.HasChanged = false;
        window.Close();
        TryDelete(dir);
    }

    [AvaloniaFact]
    public async Task Show_Gradient_Shades_The_Timer_Digits_Not_The_Background()
    {
        string dir = NewRun(out _);
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.True(TimerSettings(window).ShowGradient);
        Assert.True(window.TimerDigitsGradient);

        (Task editTask, LayoutEditorWindow editor, LayoutSettingsWindow settings) = await OpenLayoutSettings(window);
        ComponentSettingsEditor timerTab = settings.GetLogicalDescendants().OfType<ComponentSettingsEditor>()
            .First(e => e.TabHeader == "Timer");
        CheckBox gradient = timerTab.GetLogicalDescendants().OfType<CheckBox>().First(c => Equals(c.Content, "Show Gradient"));
        gradient.IsChecked = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(TimerSettings(window).ShowGradient);
        Assert.False(window.TimerDigitsGradient);
        // The background stays as the Background setting says (transparent by default).
        Assert.Equal("Plain", TimerSettings(window).BackgroundGradient);

        gradient.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(window.TimerDigitsGradient);

        settings.Close(null);
        Dispatcher.UIThread.RunJobs();
        editor.Close(null);
        await editTask;
        window.Session.State.Run.HasChanged = false;
        window.CurrentLayout.HasChanged = false;
        window.Close();
        TryDelete(dir);
    }

    private static TimerComponentSettings TimerSettings(MainWindow window) =>
        TimerComponentSettings.FromInnerXml(
            window.CurrentLayout.Components.First(c => c.Path == StandardLayoutFactory.TimerPath).SettingsInnerXml);

    private static async Task<(Task Edit, LayoutEditorWindow Editor, LayoutSettingsWindow Settings)> OpenLayoutSettings(MainWindow window)
    {
        Task edit = window.EditLayoutForTestsAsync();
        Dispatcher.UIThread.RunJobs();
        LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
        Click(editor, "Layout Settings...");
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
        LayoutSettingsWindow settings = editor.OwnedWindows.OfType<LayoutSettingsWindow>().Single();
        return (edit, editor, settings);
    }

    private static void Click(Window window, string content)
    {
        Button button = window.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, content));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static NumericUpDown NumberIn(LayoutSettingsWindow settings, string tab, string label)
    {
        ComponentSettingsEditor editor = settings.GetLogicalDescendants().OfType<ComponentSettingsEditor>()
            .First(e => e.TabHeader == tab);
        return editor.GetLogicalDescendants().OfType<NumericUpDown>()
            .First(n => n.GetLogicalParent() is Panel row && row.Children.OfType<TextBlock>().Any(t => LabelText.Is(t, label)));
    }

    private static CheckBox CheckIn(LayoutSettingsWindow settings, string content) =>
        settings.GetLogicalDescendants().OfType<CheckBox>().First(c => Equals(c.Content, content));

    private static string NewRun(out string settingsPath)
    {
        string dir = Path.Combine(Path.GetTempPath(), "lsn-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Live", CategoryName = "Any%" };
        run.AddSegment("One");
        run.AddSegment("Two");
        XmlRunSerializer.SaveFile(run, splitsPath);
        settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        return dir;
    }

    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: it is only a temp folder.
        }
    }
}
