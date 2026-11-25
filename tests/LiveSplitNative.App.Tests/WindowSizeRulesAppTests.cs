using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Windows TimerForm.MaintainMinimumSize in the app: a window made narrower than its components
/// need gets shorter instead, so the whole timer is drawn smaller and nothing overlaps; a wide
/// window is left alone.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class WindowSizeRulesAppTests
{
    [AvaloniaFact]
    public void Too_Narrow_A_Window_Gets_Shorter_So_The_Components_Still_Fit()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lsn-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Size Rules", CategoryName = "Any%" };
        run.AddSegment("A segment with a rather long name");
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
        double startWidth = window.Width;
        double startHeight = window.Height;
        (double minimumWidth, _) = window.ComponentsMinimumSize;
        Assert.True(minimumWidth > 100, $"components minimum width {minimumWidth}");
        // The default window is wide enough: nothing changed.
        Assert.True(startWidth >= minimumWidth * window.DrawScale - 0.5, $"start {startWidth}x{startHeight}");

        // Half the width the components need at this scale.
        double narrow = Math.Round(minimumWidth * window.DrawScale / 2);
        window.Width = narrow;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(narrow, window.Width);
        Assert.True(window.Height < startHeight - 1, $"height {window.Height} (was {startHeight})");
        // The components get at least their minimum width at the new scale (Windows rounds the
        // height to whole pixels, so allow one pixel).
        double logicalWidth = window.Width / window.DrawScale;
        Assert.True(logicalWidth >= minimumWidth - 1.5, $"logical width {logicalWidth} < {minimumWidth}");
        // The height is the one MaintainMinimumSize gives: height / (minimumWidth / width).
        Assert.Equal((int)((startHeight / (minimumWidth * (startHeight / window.OverallSize) / narrow)) + 0.5), window.Height, 0);

        // Widening again keeps the smaller height (Windows never grows the window back).
        double shrunk = window.Height;
        window.Width = startWidth;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(shrunk, window.Height);

        window.Close();
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
