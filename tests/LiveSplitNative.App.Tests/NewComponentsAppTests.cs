using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Adding the components ported 18 Oct the way Basten does on the running timer: Edit Layout,
/// + menu, then the component's settings (a Layout Settings tab, or a double-click). Best
/// Possible Time is, as on Windows, a Run Prediction set to Best Segments.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class NewComponentsAppTests
{
    [AvaloniaFact]
    public async Task Best_Possible_Time_Is_A_Run_Prediction_Set_To_Best_Segments()
    {
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Task edit = window.EditLayoutForTestsAsync();
        Dispatcher.UIThread.RunJobs();
        LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
        AddFromMenu(editor, "Information", "Run Prediction");
        Assert.Equal("Current Pace", editor.WorkingLayout.Components[^1].DisplayName);
        Assert.Contains("Current Pace=-", window.TimerInfoTextsForTests);

        // Layout Settings, the new component's tab: Comparison Best Segments.
        Click(editor, "Layout Settings...");
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
        LayoutSettingsWindow settings = editor.OwnedWindows.OfType<LayoutSettingsWindow>().Single();
        ComponentSettingsEditor tab = settings.GetLogicalDescendants().OfType<ComponentSettingsEditor>()
            .Single(e => e.TabHeader == "Current Pace");
        ComboBox comparison = Labeled<ComboBox>(tab, "Comparison:");
        Assert.Contains("Best Segments", comparison.Items.OfType<string>());
        comparison.SelectedItem = "Best Segments";
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(window.TimerInfoTextsForTests, t => t.StartsWith("Best Possible Time=", StringComparison.Ordinal));

        Click(settings, "OK");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Best Possible Time", editor.WorkingLayout.Components[^1].DisplayName);
        Click(editor, "OK");
        await edit;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Best Possible Time", window.CurrentLayout.Components[^1].DisplayName);
        Assert.Contains(window.TimerInfoTextsForTests, t => t.StartsWith("Best Possible Time=", StringComparison.Ordinal));

        Close(window, dir);
    }

    [AvaloniaTheory]
    [InlineData("Information", "Delta", "Delta", "Background Color")]
    [InlineData("Information", "Comparison Time", "Comparison Time", "Background Color")]
    [InlineData("Information", "Current Comparison", "Current Comparison", "Background Color")]
    [InlineData("Information", "Total Playtime", "Total Playtime", "Background Color")]
    [InlineData("Other", "Counter", "Counter", "Background Color")]
    [InlineData("Media", "Graph", "Graph", "Behind Background Color:")]
    [InlineData("Other", "Hotkey Indicator", "Hotkey Indicator", "Global Hotkeys On Color:")]
    [InlineData("Control", "Manual Game Time", "Manual Game Time", "Game Time Input")]
    [InlineData("Media", "Sound Effects", "Sound Effects", "Drag and Drop Mode:")]
    [InlineData("Other", "therun.gg", "therun.gg", "Upload Key")]
    [InlineData("Control", "AutoSplit Integration", "AutoSplit Integration", "AutoSplit Path:")]
    [InlineData("Control", "Auto Splitting Runtime", "Auto Splitting Runtime", "Script Path:")]
    public async Task Each_New_Component_Adds_And_Opens_Its_Settings(string category, string item, string name, string field)
    {
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Task edit = window.EditLayoutForTestsAsync();
        Dispatcher.UIThread.RunJobs();
        LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
        AddFromMenu(editor, category, item);
        Assert.Equal(name, editor.WorkingLayout.Components[^1].DisplayName);

        // Double-click: the component's own settings window, with its Windows fields.
        Task settingsTask = editor.EditSelectedComponentSettingsForTestsAsync();
        Dispatcher.UIThread.RunJobs();
        ComponentSettingsWindow settings = editor.OwnedWindows.OfType<ComponentSettingsWindow>().Single();
        ComponentSettingsEditor fields = settings.GetLogicalDescendants().OfType<ComponentSettingsEditor>().Single();
        Assert.DoesNotContain(
            fields.GetLogicalDescendants().OfType<TextBlock>(),
            t => (t.Text ?? "").StartsWith("No native settings panel", StringComparison.Ordinal));
        Assert.Contains(fields.GetLogicalDescendants().OfType<TextBlock>(), t => LabelText.Is(t, field));
        Click(settings, "OK");
        await settingsTask;
        Dispatcher.UIThread.RunJobs();
        Click(editor, "OK");
        await edit;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(name, window.CurrentLayout.Components[^1].DisplayName);

        Close(window, dir);
    }

    [AvaloniaFact]
    public async Task Manual_Game_Time_Asks_For_The_Game_Time_Of_Each_Split()
    {
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CurrentLayout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.ManualGameTimePath));
        LiveSplitState state = window.Session.State;

        window.Session.Execute(TimerCommand.StartOrSplit);
        Dispatcher.UIThread.RunJobs();
        ManualGameTimeWindow input = Assert.IsType<ManualGameTimeWindow>(window.ManualGameTimeWindowForTests);
        Assert.Equal("Enter Game Time", input.Title);
        Assert.Equal(TimeSpan.Zero, state.CurrentTime.GameTime);

        await Task.Delay(30);
        input.EnterForTests("1:00");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, state.CurrentSplitIndex);
        Assert.Equal(TimeSpan.FromMinutes(1), state.Run[0].SplitTime.GameTime);

        await Task.Delay(30);
        input.EnterForTests("0:30 + 0:15");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(TimeSpan.FromSeconds(105), state.Run[1].SplitTime.GameTime);

        // Reset closes the window.
        window.Session.Execute(TimerCommand.ResetWithoutSaving);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(window.ManualGameTimeWindowForTests);

        Close(window, dir);
    }

    [AvaloniaFact]
    public async Task World_Record_Adds_And_Says_Unknown_When_Speedrun_Com_Has_No_Such_Game()
    {
        SpeedrunComClient online = TimerSurfaceControl.SpeedrunCom;
        TimerSurfaceControl.SpeedrunCom = new SpeedrunComClient((_, _) => Task.FromResult("{\"data\":[]}"));
        string dir = NewRun();
        try
        {
            var window = new MainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Task edit = window.EditLayoutForTestsAsync();
            Dispatcher.UIThread.RunJobs();
            LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
            AddFromMenu(editor, "Information", "World Record");
            Assert.Equal("World Record", editor.WorkingLayout.Components[^1].DisplayName);

            Task settingsTask = editor.EditSelectedComponentSettingsForTestsAsync();
            Dispatcher.UIThread.RunJobs();
            ComponentSettingsWindow settings = editor.OwnedWindows.OfType<ComponentSettingsWindow>().Single();
            Assert.Contains(settings.GetLogicalDescendants().OfType<TextBlock>(), t => LabelText.Is(t, "Filter Leaderboard By"));
            Click(settings, "OK");
            await settingsTask;
            Click(editor, "OK");
            await edit;

            // The lookup runs in the background; the next frames show what it found.
            for (int i = 0; i < 100 && !window.TimerInfoTextsForTests.Contains("Unknown World Record="); i++)
            {
                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
                window.RefreshTimerForTests();
            }

            Assert.Contains("Unknown World Record=", window.TimerInfoTextsForTests);
            Close(window, dir);
        }
        finally
        {
            TimerSurfaceControl.SpeedrunCom = online;
        }
    }

    /// <summary>
    /// Contextual Slideshow (Media): one information row showing Possible Time Save first, then,
    /// when nothing changes for 3 seconds, Previous Segment; it has no settings.
    /// </summary>
    [AvaloniaFact]
    public async Task Contextual_Slideshow_Adds_And_Takes_Turns()
    {
        Func<TimeSpan> clock = TimerSurfaceControl.SlideshowClock;
        TimeSpan now = TimeSpan.FromHours(1);
        TimerSurfaceControl.SlideshowClock = () => now;
        string dir = NewRun();
        try
        {
            var window = new MainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Task edit = window.EditLayoutForTestsAsync();
            Dispatcher.UIThread.RunJobs();
            LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
            AddFromMenu(editor, "Media", "Contextual Slideshow");
            LayoutComponent added = editor.WorkingLayout.Components[^1];
            Assert.Equal("Contextual Slideshow", added.DisplayName);
            Assert.False(added.HasSettings);
            Click(editor, "OK");
            await edit;
            Dispatcher.UIThread.RunJobs();

            window.RefreshTimerForTests();
            Assert.StartsWith("Possible Time Save=", window.SlideshowShownForTests);
            TimerLayoutSlot slot = window.LayoutSnapshotForTests!.Slots.Single(s => s.Path == StandardLayoutFactory.ContextualSlideshowPath);
            Assert.Equal(31, slot.DrawBounds.Height, 1);

            now += TimeSpan.FromSeconds(3.1);
            window.RefreshTimerForTests();
            Assert.StartsWith("Previous Segment=", window.SlideshowShownForTests);
            now += TimeSpan.FromSeconds(3.1);
            window.RefreshTimerForTests();
            Assert.StartsWith("Current Pace=", window.SlideshowShownForTests);
            Close(window, dir);
        }
        finally
        {
            TimerSurfaceControl.SlideshowClock = clock;
        }
    }

    /// <summary>LayoutEditorDialog's + menu: category submenu, then the component.</summary>
    private static void AddFromMenu(LayoutEditorWindow editor, string category, string item)
    {
        NativeMenuModel menu = editor.BuildAddComponentMenuModel(editor.AddComponent);
        NativeMenuAction group = menu.Items.OfType<NativeMenuAction>().Single(a => a.Header == category);
        NativeMenuAction entry = group.Children!.OfType<NativeMenuAction>().Single(a => a.Header == item);
        Assert.True(entry.IsEnabled);
        entry.Invoke();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Labeled<T>(Control host, string label)
        where T : Control =>
        host.GetLogicalDescendants().OfType<T>()
            .First(c => c.GetLogicalParent() is Panel row && row.Children.OfType<TextBlock>().Any(t => LabelText.Is(t, label)));

    private static void Click(Window window, string content)
    {
        Button button = window.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, content));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static string NewRun()
    {
        string dir = HeadlessUi.NewTempDir();
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Live", CategoryName = "Any%" };
        run.AddSegment("One");
        run.AddSegment("Two");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(20));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(8));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(9));
        XmlRunSerializer.SaveFile(run, splitsPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        return dir;
    }

    private static void Close(MainWindow window, string dir)
    {
        window.Session.State.Run.HasChanged = false;
        window.CurrentLayout.HasChanged = false;
        window.Close();
        HeadlessUi.Delete(dir);
    }
}
