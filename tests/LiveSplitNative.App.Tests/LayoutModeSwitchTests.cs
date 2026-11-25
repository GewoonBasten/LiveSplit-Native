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
using Xunit.Abstractions;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Vertical -> Horizontal -> Vertical through Edit Layout (live preview, then OK): the window must
/// come back with a sensible size and a drawn timer every time (Basten, 12 Oct: switching back to
/// vertical lost the timer and the app crashed in rendering).
/// </summary>
[Collection(MainWindowCollection.Name)]
public class LayoutModeSwitchTests(ITestOutputHelper output)
{
    [AvaloniaFact]
    public async Task Switching_Mode_Back_And_Forth_Keeps_A_Valid_Window()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lsn-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Modes", CategoryName = "Any%" };
        run.AddSegment("One");
        run.AddSegment("Two");
        run.AddSegment("Three");
        XmlRunSerializer.SaveFile(run, splitsPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);

        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Log(window, "start");
        (double startWidth, double startHeight) = (window.Width, window.Height);
        Assert.True(window.CurrentLayout.HorizontalWidth <= 0, "the default layout has no horizontal size yet");

        bool first = true;
        foreach (bool horizontal in new[] { true, false, true, false })
        {
            Task edit = window.EditLayoutForTestsAsync();
            Dispatcher.UIThread.RunJobs();
            LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
            RadioButton radio = editor.GetLogicalDescendants().OfType<RadioButton>()
                .First(r => Equals(r.Content, horizontal ? "Horizontal" : "Vertical"));
            radio.IsChecked = true;
            Dispatcher.UIThread.RunJobs();
            Log(window, (horizontal ? "horizontal" : "vertical") + " preview");
            AssertValid(window, horizontal);
            editor.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "OK"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await edit;
            Dispatcher.UIThread.RunJobs();
            Log(window, (horizontal ? "horizontal" : "vertical") + " OK");
            AssertValid(window, horizontal);
            if (horizontal && first)
            {
                // editor_OrientationSwitched: no horizontal size yet, so OverallSize x 45.
                Assert.Equal(45, window.Height);
                first = false;
            }

            if (!horizontal)
            {
                // The vertical size the window had before is back, exactly.
                Assert.Equal((startWidth, startHeight), (window.Width, window.Height));
            }
        }

        // editor_LayoutResized: adding a component drops the horizontal size, so the next switch
        // starts from OverallSize x 45 again; Cancel brings the dropped size back.
        int savedHorizontalWidth = window.CurrentLayout.HorizontalWidth;
        Assert.True(savedHorizontalWidth > 0);
        Task cancelled = window.EditLayoutForTestsAsync();
        Dispatcher.UIThread.RunJobs();
        LayoutEditorWindow addEditor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
        addEditor.LayoutResized?.Invoke();
        Assert.Equal(-1, window.CurrentLayout.HorizontalWidth);
        addEditor.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, "Cancel"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await cancelled;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(savedHorizontalWidth, window.CurrentLayout.HorizontalWidth);

        window.Session.State.Run.HasChanged = false;
        window.CurrentLayout.HasChanged = false;
        window.Close();
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }

    private void Log(MainWindow window, string label) =>
        output.WriteLine(
            $"{label}: mode={window.CurrentLayout.Mode} window={window.Width:0.#}x{window.Height:0.#} " +
            $"client={window.ClientSize.Width:0.#}x{window.ClientSize.Height:0.#} scale={window.DrawScale:0.###} " +
            $"overall={window.OverallSize:0.#} layoutV={window.CurrentLayout.VerticalWidth}x{window.CurrentLayout.VerticalHeight} " +
            $"layoutH={window.CurrentLayout.HorizontalWidth}x{window.CurrentLayout.HorizontalHeight}");

    private static void AssertValid(MainWindow window, bool horizontal)
    {
        Assert.Equal(horizontal ? LayoutMode.Horizontal : LayoutMode.Vertical, window.CurrentLayout.Mode);
        Assert.True(double.IsFinite(window.Width) && window.Width >= 25 && window.Width <= 4000, $"width {window.Width}");
        Assert.True(double.IsFinite(window.Height) && window.Height >= 25 && window.Height <= 4000, $"height {window.Height}");
        Assert.True(window.DrawScale > 0.05 && window.DrawScale < 20, $"scale {window.DrawScale}");
        // Vertical: taller than wide for the default layout; horizontal: wider than tall.
        Assert.True(horizontal ? window.Width > window.Height : window.Height > window.Width,
            $"{(horizontal ? "horizontal" : "vertical")} window {window.Width}x{window.Height}");
    }
}
