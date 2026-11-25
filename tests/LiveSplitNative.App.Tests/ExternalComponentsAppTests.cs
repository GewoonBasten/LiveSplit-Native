using System.Drawing;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.ComponentHost.Compat;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// A real Windows LiveSplit component DLL, unchanged: Reset Chance (TheSoundDefense, MIT,
/// tests/fixtures/components) loaded through the look-alike LiveSplit.Core, System.Windows.Forms,
/// System.Drawing and UpdateManager, in the Add menu, in the layout, drawn, and its WinForms
/// settings page shown and used.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class ExternalComponentsAppTests
{
    private const string ResetChance = "LiveSplit.ResetChance.dll";

    private static string FixtureDirectory()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "tests", "fixtures", "components");
            if (File.Exists(Path.Combine(candidate, ResetChance)))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(ResetChance);
    }

    private static IHostedComponentFactory Factory()
    {
        ExternalComponents.Load(FixtureDirectory());
        Assert.Empty(ExternalComponents.Failures);
        return Assert.Single(ExternalComponents.All, f => f.FileName == ResetChance);
    }

    [AvaloniaFact]
    public void The_Dll_Loads_Unchanged_With_Its_Factory()
    {
        IHostedComponentFactory factory = Factory();
        Assert.Equal("Reset Chance", factory.ComponentName);
        Assert.Equal("Displays the likelihood of the run being reset during this split.", factory.Description);
        Assert.Equal(3, factory.Category);
        Assert.True(ExternalComponents.IsExternal(ResetChance));
    }

    /// <summary>
    /// Two splits, four attempts: three reached the end of split one, one the end of split two.
    /// Running on split one, Reset Chance is 1 - 3/4 = 25%; Success Chance 75%.
    /// </summary>
    [AvaloniaFact]
    public void It_Draws_The_Reset_Chance_Of_The_Current_Split()
    {
        Factory();
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var component = new LayoutComponent(ResetChance);
            window.CurrentLayout.Components.Add(component);
            window.ApplyLayoutForTests(window.CurrentLayout);
            Dispatcher.UIThread.RunJobs();
            HostedLayoutComponent hosted = Assert.IsType<HostedLayoutComponent>(component.RuntimeState);
            Assert.Equal("Reset Chance", component.DisplayName);
            Assert.Equal(31, hosted.VerticalHeight);
            Assert.Contains("<ChanceMode>ResetChance</ChanceMode>", component.SettingsInnerXml);
            Assert.Contains(window.LayoutSnapshotForTests!.Slots, s => s.Path == ResetChance && Math.Abs(s.Bounds.Height - 31) < 8);

            window.Session.Execute(TimerCommand.StartOrSplit);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Reset Chance|25%", Draw(hosted));

            // Its settings, as a layout stores them: Success Chance (the value follows at the next
            // split, as on Windows: ResetChanceComponent keeps it until the split changes).
            component.SettingsInnerXml = component.SettingsInnerXml!.Replace("<ChanceMode>ResetChance</ChanceMode>", "<ChanceMode>SuccessChance</ChanceMode>");
            ExternalComponents.Attach(component);
            Assert.StartsWith("Success Chance|", Draw(hosted));
            window.Session.Execute(TimerCommand.ResetWithoutSaving);
            window.Session.Execute(TimerCommand.StartOrSplit);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Success Chance|75%", Draw(hosted));
        }
        finally
        {
            window.Session.Execute(TimerCommand.ResetWithoutSaving);
            Close(window, dir);
        }
    }

    /// <summary>
    /// A DLL's information row follows the layout's Component Spacing, as the app's own rows do
    /// (25 + spacing; 6 gives Windows' 31) (Basten 28 Oct: "linked to that setting in all cases").
    /// </summary>
    [AvaloniaFact]
    public void Its_Row_Follows_Component_Spacing()
    {
        Factory();
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var component = new LayoutComponent(ResetChance);
            window.CurrentLayout.Components.Add(component);
            window.CurrentLayout.Settings.ComponentSpacing = 14;
            window.ApplyLayoutForTests(window.CurrentLayout);
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            HostedLayoutComponent hosted = Assert.IsType<HostedLayoutComponent>(component.RuntimeState);
            Draw(hosted);
            Assert.Equal(39, hosted.VerticalHeight);
            window.ApplyLayoutForTests(window.CurrentLayout);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(window.LayoutSnapshotForTests!.Slots, s => s.Path == ResetChance && Math.Abs(s.DrawBounds.Height - 39) < 1);
        }
        finally
        {
            window.CurrentLayout.Settings.ComponentSpacing = LayoutSettings.DefaultComponentSpacing;
            Close(window, dir);
        }
    }

    /// <summary>A new component takes the background of the row above it (Basten 28 Oct).</summary>
    [AvaloniaFact]
    public void A_New_Component_Takes_The_Background_Of_The_Row_Above()
    {
        Factory();
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            LayoutComponent above = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.TextPath);
            above.SettingsInnerXml += "<BackgroundColor>FF000000</BackgroundColor><BackgroundColor2>FF000000</BackgroundColor2>";
            var component = new LayoutComponent(ResetChance);
            ExternalComponents.Attach(component);
            LayoutEditorWindow.InheritBackground(component, [above]);
            HostedLayoutComponent hosted = Assert.IsType<HostedLayoutComponent>(component.RuntimeState);
            Assert.Contains("<BackgroundColor>FF000000</BackgroundColor>", hosted.Component.GetSettingsInnerXml());
            Assert.Contains("<BackgroundGradient>Plain</BackgroundGradient>", hosted.Component.GetSettingsInnerXml());

            // A built-in row takes it too; the Splits' alternating rows are passed over.
            LayoutComponent text = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.TextPath);
            LayoutEditorWindow.InheritBackground(text, [above, StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.SplitsPath)]);
            Assert.Contains("<BackgroundColor>FF000000</BackgroundColor>", text.SettingsInnerXml);

            // Contextual Slideshow too (its row's background is LiveSplitNative's own).
            LayoutComponent slideshow = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.ContextualSlideshowPath);
            LayoutEditorWindow.InheritBackground(slideshow, [above]);
            Assert.Equal(ArgbColor.FromArgb(255, 0, 0, 0), ContextualSlideshowComponentSettings.FromInnerXml(slideshow.SettingsInnerXml).BackgroundColor);
        }
        finally
        {
            Close(window, dir);
        }
    }

    [AvaloniaFact]
    public async Task It_Is_In_The_Add_Menu_And_Its_Settings_Page_Changes_Its_Settings()
    {
        Factory();
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Task edit = window.EditLayoutForTestsAsync();
            Dispatcher.UIThread.RunJobs();
            LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
            NativeMenuModel menu = editor.BuildAddComponentMenuModel(editor.AddComponent);
            NativeMenuAction information = menu.Items.OfType<NativeMenuAction>().Single(a => a.Header == "Information");
            NativeMenuAction entry = information.Children!.OfType<NativeMenuAction>().Single(a => a.Header == "Reset Chance");
            entry.Invoke();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Reset Chance", editor.WorkingLayout.Components[^1].DisplayName);

            Task settingsTask = editor.EditSelectedComponentSettingsForTestsAsync();
            Dispatcher.UIThread.RunJobs();
            ComponentSettingsWindow settings = editor.OwnedWindows.OfType<ComponentSettingsWindow>().Single();
            HostedSettingsView page = settings.GetLogicalDescendants().OfType<HostedSettingsView>().Single();

            // The designer's page (AutoScaleDimensions 11 x 24) grown to the app's font, every
            // control inside it.
            Assert.InRange(page.Width, 380, 700);
            Assert.InRange(page.Height, 400, 800);
            foreach (IHostControl control in All(page.Root))
            {
                Assert.True(control.Bounds.Width >= 0 && control.Bounds.Height >= 0, control.Text);
            }

            // What Load set: Reset Chance checked, the gradient Plain (one color button hidden).
            RadioButton resetChance = Radio(page, "Reset Chance");
            Assert.True(resetChance.IsChecked);
            Assert.Contains(page.GetLogicalDescendants().OfType<CheckBox>(), c => (string?)c.Content == "Display 2 Rows");
            Assert.Contains(page.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Background Color");

            // Choosing Runs Ended and Two decimals, and checking Show Trailing Zeroes.
            Radio(page, "Runs Ended").IsChecked = true;
            Radio(page, "Two (1.11%)").IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Assert.False(resetChance.IsChecked);

            Click(settings, "OK");
            await settingsTask;
            Dispatcher.UIThread.RunJobs();
            string xml = editor.WorkingLayout.Components[^1].SettingsInnerXml!;
            Assert.Contains("<ChanceMode>RunsEnded</ChanceMode>", xml);
            Assert.Contains("<Accuracy>TwoDecimal</Accuracy>", xml);

            Click(editor, "OK");
            await edit;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Reset Chance", window.CurrentLayout.Components[^1].DisplayName);

            // Saved as Windows saves it: the DLL's path and its settings.
            string saved = Path.Combine(dir, "layout.lsl");
            XmlLayoutSerializer.SaveFile(window.CurrentLayout, saved);
            string text = File.ReadAllText(saved);
            Assert.Contains("<Path>LiveSplit.ResetChance.dll</Path>", text);
            Assert.Contains("<ChanceMode>RunsEnded</ChanceMode>", text);
        }
        finally
        {
            Close(window, dir);
        }
    }

    [AvaloniaFact]
    public async Task Cancel_Puts_The_Settings_Back()
    {
        Factory();
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var component = new LayoutComponent(ResetChance);
            window.CurrentLayout.Components.Add(component);
            window.ApplyLayoutForTests(window.CurrentLayout);
            Dispatcher.UIThread.RunJobs();
            string before = component.SettingsInnerXml!;

            Task edit = window.EditLayoutForTestsAsync();
            Dispatcher.UIThread.RunJobs();
            LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
            editor.FindControl<ListBox>("ComponentsList")!.SelectedIndex = editor.WorkingLayout.Components.Count - 1;
            Task settingsTask = editor.EditSelectedComponentSettingsForTestsAsync();
            Dispatcher.UIThread.RunJobs();
            ComponentSettingsWindow settings = editor.OwnedWindows.OfType<ComponentSettingsWindow>().Single();
            HostedSettingsView page = settings.GetLogicalDescendants().OfType<HostedSettingsView>().Single();
            Radio(page, "Success Chance").IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Click(settings, "Cancel");
            await settingsTask;
            Dispatcher.UIThread.RunJobs();
            Click(editor, "Cancel");
            await edit;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(before, component.SettingsInnerXml);
            HostedLayoutComponent hosted = Assert.IsType<HostedLayoutComponent>(component.RuntimeState);
            Assert.Contains("<ChanceMode>ResetChance</ChanceMode>", hosted.Component.GetSettingsInnerXml());
        }
        finally
        {
            Close(window, dir);
        }
    }

    /// <summary>
    /// A component DLL fetched into .checks/components (tools/mac/check.command
    /// fetch-components); these have no license file, so they are not fixtures. Null when absent.
    /// </summary>
    private static string? Fetched(string folder, string dll)
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string root = Path.Combine(dir.FullName, ".checks", "components", folder);
            if (Directory.Exists(root))
            {
                return Directory.EnumerateFiles(root, dll, SearchOption.AllDirectories).FirstOrDefault() is { } file
                    ? Path.GetDirectoryName(file)
                    : null;
            }
        }

        return null;
    }

    /// <summary>Pace Placement (andrewhuntsmith) and Alternate Timing Method (Dalet), unchanged.</summary>
    [AvaloniaTheory]
    [InlineData("andrewhuntsmith_LiveSplit.PacePlacement", "LiveSplit.PacePlacement.dll", "Pace Placement", "1st")]
    [InlineData("Dalet_LiveSplit.AlternateTimingMethod", "LiveSplit.AlternateTimingMethod.dll", "Alternate Timing Method", "Game Time")]
    public void Other_Display_Components_Load_Draw_And_Show_Their_Settings(string folder, string dll, string name, string drawn)
    {
        if (Fetched(folder, dll) is not { } directory)
        {
            return;
        }

        ExternalComponents.Load(directory);
        Assert.DoesNotContain(ExternalComponents.Failures, f => f.Path.Contains(dll, StringComparison.Ordinal));
        Assert.Contains(ExternalComponents.All, f => f.FileName == dll && f.ComponentName == name);
        string dir = NewRun();
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var component = new LayoutComponent(dll);
            window.CurrentLayout.Components.Add(component);
            window.ApplyLayoutForTests(window.CurrentLayout);
            Dispatcher.UIThread.RunJobs();
            HostedLayoutComponent hosted = Assert.IsType<HostedLayoutComponent>(component.RuntimeState);
            Assert.Equal(name, component.DisplayName);
            window.Session.Execute(TimerCommand.StartOrSplit);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(drawn, Draw(hosted));

            // Its settings page builds and lays out.
            IHostControl page = Assert.IsAssignableFrom<IHostControl>(hosted.Component.GetSettingsControl(LayoutMode.Vertical));
            var view = new HostedSettingsView(page);
            Assert.True(view.Width > 100 && view.Height > 50);
            Assert.NotEmpty(All(page));
        }
        finally
        {
            window.Session.Execute(TimerCommand.ResetWithoutSaving);
            Close(window, dir);
        }
    }

    /// <summary>
    /// Best World Segments (Zeldocto), unchanged: worlds from subsplits ("-a", "{World 1}b") and
    /// single splits ("c"); its table drawn on the layout and its settings page's world list.
    /// Attempt 1: a 5, b 6, c 7; attempt 2: a 4, b 8, reset. PB is attempt 1.
    /// World 1: best 11 (attempt 1; attempt 2 took 12), PB 11, sum of golds 4 + 6 = 10.
    /// c: best 7, PB 7, sum of golds 7.
    /// </summary>
    [AvaloniaFact]
    public void Best_World_Segments_Shows_The_Worlds_Of_The_Run()
    {
        const string dll = "LiveSplit.BestWorldSegments.dll";
        if (Fetched("Zeldocto_Livesplit.BestWorldSegments", dll) is not { } directory)
        {
            return;
        }

        ExternalComponents.Load(directory);
        Assert.DoesNotContain(ExternalComponents.Failures, f => f.Path.Contains(dll, StringComparison.Ordinal));
        IHostedComponentFactory factory = Assert.Single(ExternalComponents.All, f => f.FileName == dll);
        Assert.Equal("Best World Segments", factory.ComponentName);
        Assert.Equal(2, factory.Category);

        string dir = NewRun(WorldsRun());
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var component = new LayoutComponent(dll);
            window.CurrentLayout.Components.Add(component);
            window.ApplyLayoutForTests(window.CurrentLayout);
            Dispatcher.UIThread.RunJobs();
            HostedLayoutComponent hosted = Assert.IsType<HostedLayoutComponent>(component.RuntimeState);
            Assert.Equal("Best World Segments", component.DisplayName);
            hosted.Component.Update(300, 72, LayoutMode.Vertical);
            // Column labels and two worlds, 24 pixels each.
            Assert.Equal(72, hosted.VerticalHeight);

            // Running (in World 1, whose time so far shows under This Run): the layout
            // (LiveSplitState.Layout) is looked through for Subsplits to color.
            window.Session.Execute(TimerCommand.StartOrSplit);
            Dispatcher.UIThread.RunJobs();
            hosted.Component.Update(300, 72, LayoutMode.Vertical);
            var canvas = new RecordingCanvas();
            hosted.Component.DrawVertical(canvas, 300);
            // (Times are monospaced labels, drawn a character at a time.)
            List<string> texts = canvas.Texts.Where(t => t.Length > 0).Distinct().ToList();
            Assert.Equal(["This Run", "PB", "Best", "World 1"], texts.Take(4).ToList());
            Assert.Contains("c", texts);

            // Its settings page: the world list (Best World, PB, Sum of Golds; SplitTimeFormatter,
            // so with single-digit minutes) and the total.
            IHostControl page = Assert.IsAssignableFrom<IHostControl>(hosted.Component.GetSettingsControl(LayoutMode.Vertical));
            var view = new HostedSettingsView(page);
            IHostControl listView = All(page).Single(c => c.Kind == "ListView");
            HostListView list = Assert.IsType<HostListView>(listView.ListView);
            Assert.Equal(["World", "Best World", "PB", "Sum of Golds"], list.Columns.Select(c => c.Text).ToList());
            Assert.Equal(
                [["World 1", "0:11.00", "0:11.00", "0:10.00"], ["c", "0:07.00", "0:07.00", "0:07.00"], ["Total", "0:18.00", "0:18.00", "0:17.00"]],
                list.Rows.Select(r => r.Cells.ToArray()).ToArray());
            Assert.True(list.Rows[^1].Bold);
            // The list is as tall as its rows (RefreshWorldList, once shown).
            Assert.Equal(list.HeaderHeight + (3 * list.RowHeight) + 4, listView.Bounds.Height);
            var table = Assert.IsType<HostedSettingsView.ListTable>(view.ViewOf(listView));
            Assert.Equal(3, table.Rows.Count);

            // The explanation under the list wraps at its maximum width (430).
            IHostControl note = All(page).Single(c => c.Kind == "Label" && c.Text.StartsWith("Best World is", StringComparison.Ordinal));
            Assert.InRange(note.Bounds.Width, 300, 430);
            Assert.True(note.Bounds.Height > 20);
        }
        finally
        {
            window.Session.Execute(TimerCommand.ResetWithoutSaving);
            Close(window, dir);
        }
    }

    /// <summary>
    /// Worlds from subsplits ("-a", "{World 1}b") and a single split ("c"). Attempt 1: a 5, b 6,
    /// c 7 (the PB); attempt 2: a 4, b 8, reset. Best segments a 4, b 6, c 7.
    /// </summary>
    private static Run WorldsRun()
    {
        var run = new Run { GameName = "Live", CategoryName = "Worlds", AttemptCount = 2 };
        run.AddSegment("-a");
        run.AddSegment("{World 1}b");
        run.AddSegment("c");
        int[][] history = [[5, 6, 7], [4, 8]];
        for (int attempt = 1; attempt <= history.Length; attempt++)
        {
            int[] segments = history[attempt - 1];
            run.AttemptHistory.Add(new Attempt(attempt, attempt == 1 ? new SplitTime(TimeSpan.FromSeconds(18)) : default, null, null, null));
            for (int i = 0; i < segments.Length; i++)
            {
                run[i].SegmentHistory[attempt] = new SplitTime(TimeSpan.FromSeconds(segments[i]));
            }
        }

        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(5));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(11));
        run[2].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(18));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(4));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(6));
        run[2].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(7));

        return run;
    }

    /// <summary>
    /// Best World Segments' gold on our Subsplits (its main use): finishing World 1 faster than
    /// its best world colors the delta of World 1's collapsed row in the gold color (here its own
    /// Override Layout Gold Color, so it cannot be the Subsplits' own best-segment gold), through
    /// LiveSplitState.Layout's mirror of Windows' LiveSplit.Subsplits.
    /// </summary>
    [AvaloniaFact]
    public void Best_World_Segments_Colors_A_New_Best_World_Gold_On_Subsplits()
    {
        const string dll = "LiveSplit.BestWorldSegments.dll";
        if (Fetched("Zeldocto_Livesplit.BestWorldSegments", dll) is not { } directory)
        {
            return;
        }

        ExternalComponents.Load(directory);
        string dir = NewRun(WorldsRun());
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Layout layout = window.CurrentLayout;
            layout.Components.Add(new LayoutComponent(StandardLayoutFactory.SubsplitsPath)
            {
                SettingsInnerXml = new SubsplitsComponentSettings().ToInnerXml(),
            });
            var component = new LayoutComponent(dll);
            layout.Components.Add(component);
            window.ApplyLayoutForTests(layout);
            Dispatcher.UIThread.RunJobs();
            component.SettingsInnerXml = System.Text.RegularExpressions.Regex.Replace(
                component.SettingsInnerXml!.Replace("<OverrideGoldColor>False</OverrideGoldColor>", "<OverrideGoldColor>True</OverrideGoldColor>"),
                "<GoldColor>[0-9A-F]+</GoldColor>",
                "<GoldColor>FF123456</GoldColor>");
            ExternalComponents.Attach(component);
            Assert.Contains("<OverrideGoldColor>True</OverrideGoldColor>", Assert.IsType<HostedLayoutComponent>(component.RuntimeState).Component.GetSettingsInnerXml());

            // a and b in a moment: World 1 in far less than its best (11 s); now on c.
            window.Session.Execute(TimerCommand.StartOrSplit);
            window.Session.Execute(TimerCommand.StartOrSplit);
            window.Session.Execute(TimerCommand.StartOrSplit);
            Dispatcher.UIThread.RunJobs();
            window.RefreshTimerForTests();

            IHostSubsplits subsplits = Assert.Single(window.HostLayoutForTests, e => e.Subsplits is not null).Subsplits!;
            HostSubsplitsRow world = Assert.Single(subsplits.Rows, r => r.CollapsedSplit && r.SplitIndex == 1);
            int delta = subsplits.Columns.ToList().FindIndex(c => c.Type == "Delta");
            int time = subsplits.Columns.ToList().FindIndex(c => c.Type == "SplitTime");
            Assert.True(delta >= 0 && time >= 0);
            Assert.Equal(Color.FromArgb(0x12, 0x34, 0x56).ToArgb(), world.ColumnColors[delta].ToArgb());
            Assert.NotEqual(Color.FromArgb(0x12, 0x34, 0x56).ToArgb(), world.ColumnColors[time].ToArgb());
        }
        finally
        {
            window.Session.Execute(TimerCommand.ResetWithoutSaving);
            Close(window, dir);
        }
    }

    private static IEnumerable<IHostControl> All(IHostControl control) =>
        control.Children.SelectMany(c => (IEnumerable<IHostControl>)[c, .. All(c)]);

    private static RadioButton Radio(HostedSettingsView page, string text) =>
        page.GetLogicalDescendants().OfType<RadioButton>().Single(r => (string?)r.Content == text);

    /// <summary>The component's texts as drawn: "name|value" pairs of what it drew on one line.</summary>
    private static string Draw(HostedLayoutComponent hosted)
    {
        hosted.Component.Update(300, 31, LayoutMode.Vertical);
        var canvas = new RecordingCanvas();
        hosted.Component.DrawVertical(canvas, 300);
        // The label and its shadows are drawn first; the last two texts are name and value.
        List<string> texts = canvas.Texts.Distinct().ToList();
        return string.Join("|", texts);
    }

    private sealed class RecordingCanvas : IHostCanvas
    {
        public List<string> Texts { get; } = [];

        public float DpiY => 96;

        public bool AntiAliasText => false;

        public void Fill(HostBrush brush, RectangleF rect)
        {
        }

        public void FillPolygon(HostBrush brush, IReadOnlyList<PointF> points)
        {
        }

        public void DrawLine(Color color, float width, PointF from, PointF to)
        {
        }

        public void DrawRectangle(Color color, float width, RectangleF rect)
        {
        }

        public void DrawString(string text, HostFont font, HostBrush brush, RectangleF layout, HostStringFormat format) => Texts.Add(text);

        public void DrawStringPath(string text, HostFont font, RectangleF layout, HostStringFormat format, HostBrush? fill, Color outline, float outlineWidth) => Texts.Add(text);

        public SizeF MeasureString(string text, HostFont font, float maxWidth, HostStringFormat format) =>
            new(Math.Min(maxWidth, text.Length * font.SizeInPixels * 0.5f), font.SizeInPixels * 1.33f);

        public Size MeasureText(string text, HostFont font) => new((int)(text.Length * font.SizeInPixels * 0.5f), (int)(font.SizeInPixels * 1.33f));

        public void PushClip(RectangleF rect)
        {
        }

        public void PopClip()
        {
        }
    }

    private static void Click(Window window, string content)
    {
        Avalonia.Controls.Button button = window.GetLogicalDescendants().OfType<Avalonia.Controls.Button>().First(b => Equals(b.Content, content));
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    }

    /// <summary>Two splits; four attempts, three of which finished split one and one split two.</summary>
    private static string NewRun()
    {
        var run = new Run { GameName = "Live", CategoryName = "Any%", AttemptCount = 4 };
        run.AddSegment("One");
        run.AddSegment("Two");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(20));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(8));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(9));
        for (int i = 1; i <= 4; i++)
        {
            run.AttemptHistory.Add(new Attempt(i, i == 4 ? new SplitTime(TimeSpan.FromSeconds(21)) : default, null, null, null));
        }

        run[0].SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(11));
        run[0].SegmentHistory[2] = new SplitTime(TimeSpan.FromSeconds(12));
        run[0].SegmentHistory[4] = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].SegmentHistory[4] = new SplitTime(TimeSpan.FromSeconds(11));
        return NewRun(run);
    }

    /// <summary>The app opens this run (a temporary settings.cfg whose last splits it is).</summary>
    private static string NewRun(Run run)
    {
        string dir = HeadlessUi.NewTempDir();
        string splitsPath = Path.Combine(dir, "run.lss");
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
