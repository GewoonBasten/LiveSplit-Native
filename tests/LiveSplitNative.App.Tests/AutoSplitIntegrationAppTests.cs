using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>AutoSplit Integration: the autosplitter's commands drive the timer; its settings page.</summary>
[Collection(MainWindowCollection.Name)]
public class AutoSplitIntegrationAppTests
{
    [AvaloniaFact]
    public async Task Autosplitter_Commands_Drive_The_Timer()
    {
        string dir = HeadlessUi.NewTempDir();
        try
        {
            string splitsPath = Path.Combine(dir, "run.lss");
            var run = new Run { GameName = "Live", CategoryName = "Any%" };
            run.AddSegment("One");
            run.AddSegment("Two");
            XmlRunSerializer.SaveFile(run, splitsPath);
            string settingsPath = Path.Combine(dir, "settings.cfg");
            var appSettings = new AppSettings();
            appSettings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
            XmlAppSettingsSerializer.SaveFile(appSettings, settingsPath);
            AppCli.Parse(["--settings", settingsPath]);
            var window = new MainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Layout layout = window.CurrentLayout.Clone();
            layout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.AutoSplitIntegrationPath));
            window.ApplyLayoutForTests(layout);
            LayoutComponent component = window.CurrentLayout.Components[^1];
            AutoSplitIntegrationRuntime runtime = Assert.IsType<AutoSplitIntegrationRuntime>(component.RuntimeState);
            Assert.True(runtime.HasStarted);
            Assert.False(runtime.IsRunning);
            LiveSplitState state = window.Session.State;

            // Pilgrim's split starts a stopped timer, then splits.
            runtime.CommandReceived!("split");
            Assert.Equal(TimerPhase.Running, state.CurrentPhase);
            Assert.False(runtime.ConsumeIgnore(AutoSplitCommands.Start));
            await Task.Delay(30);
            runtime.CommandReceived!("split");
            Assert.Equal(1, state.CurrentSplitIndex);
            runtime.CommandReceived!("pause");
            Assert.Equal(TimerPhase.Paused, state.CurrentPhase);
            runtime.CommandReceived!("pause");
            runtime.CommandReceived!("reset");
            Assert.Equal(TimerPhase.NotRunning, state.CurrentPhase);
            runtime.CommandReceived!("start");
            Assert.Equal(TimerPhase.Running, state.CurrentPhase);

            window.Session.State.Run.HasChanged = false;
            window.CurrentLayout.HasChanged = false;
            window.Close();
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void Settings_Page_Has_The_Paths_Buttons_And_Pausing()
    {
        var editor = new ComponentSettingsEditor(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.AutoSplitIntegrationPath));
        List<string> texts = editor.GetLogicalDescendants().OfType<TextBlock>().Select(LabelText.Of).ToList();
        Assert.Contains("AutoSplit Path", texts);
        Assert.Contains("Settings Path", texts);
        Assert.Contains("AutoSplit version: N/A", texts);
        List<Button> buttons = editor.GetLogicalDescendants().OfType<Button>().ToList();
        Assert.False(buttons.Single(b => Equals(b.Content, "Start AutoSplit")).IsEnabled);
        Assert.False(buttons.Single(b => Equals(b.Content, "Kill AutoSplit")).IsEnabled);
        Assert.Equal(2, buttons.Count(b => Equals(b.Content, "Browse...")));

        List<TextBox> boxes = editor.GetLogicalDescendants().OfType<TextBox>().ToList();
        boxes[0].Text = "/Applications/Pilgrim Autosplitter.app";
        boxes[1].Text = "/Users/basten/sms.json";
        editor.GetLogicalDescendants().OfType<CheckBox>().Single().IsChecked = true;
        var settings = AutoSplitIntegrationSettings.FromInnerXml(editor.Apply().SettingsInnerXml);
        Assert.Equal("/Applications/Pilgrim Autosplitter.app", settings.AutoSplitPath);
        Assert.Equal("/Users/basten/sms.json", settings.SettingsPath);
        Assert.True(settings.GameTimePausing);
    }
}
