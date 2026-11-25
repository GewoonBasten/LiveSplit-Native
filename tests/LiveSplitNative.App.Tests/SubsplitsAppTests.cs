using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// The Subsplits port in the real window (Basten, 13 Oct: "it sees the group segment also as a
/// separate split", "subsplits are not minimized when timer is not running"): the rows follow
/// LiveSplit.Subsplits' sections with the header row, and the mouse wheel scrolls them.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class SubsplitsAppTests
{
    // Sections (SectionList): [0-2] Bianco Hills, [3-4] Ricco Harbor, [5], [6].
    private static readonly string[] Names =
    [
        "-Windmill", "-Hillside Cave", "{Bianco Hills}Shadow Mario",
        "-Red Dawgs", "{Ricco Harbor}Shadow Mario",
        "Gelato Beach",
        "Corona Mountain",
    ];

    private static readonly ArgbColor Green = ArgbColor.FromArgb(255, 0, 160, 0);

    [AvaloniaFact]
    public void Not_Running_Shows_The_First_Section_Under_Its_Header_And_Collapses_The_Rest()
    {
        var settings = new SubsplitsComponentSettings
        {
            VisualSplitCount = 7,
            DisplayIcons = false,
            HeaderGradient = "Plain",
            HeaderTopColor = Green,
        };
        string dir = HeadlessUi.NewTempDir();
        try
        {
            MainWindow window = Open(dir, settings);
            Assert.Equal(
                "[Bianco Hills - 3:00.0] |   Windmill  1:00 |   Hillside Cave  2:00 |   Shadow Mario  3:00 | Ricco Harbor  5:00 | Gelato Beach  6:00 | Corona Mountain  7:00 (separator 0)",
                window.SubsplitsRowsForTests);

            // DrawHeader: the header row is painted with the Header Background Color.
            (byte r, byte g, byte b) = RowColor(window, row: 0, settings.SplitHeight);
            Assert.True(Math.Abs(r - Green.R) <= 4 && Math.Abs(g - Green.G) <= 4 && Math.Abs(b - Green.B) <= 4, $"header row drawn {r},{g},{b}");
            (r, g, b) = RowColor(window, row: 1, settings.SplitHeight);
            Assert.False(Math.Abs(g - Green.G) <= 4 && r < 10, $"split row drawn {r},{g},{b}");
            window.Close();
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void Mouse_Wheel_Scrolls_The_List_And_A_Split_Brings_It_Back()
    {
        var settings = new SubsplitsComponentSettings { VisualSplitCount = 7, DisplayIcons = false };
        string dir = HeadlessUi.NewTempDir();
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        TimeSpan wheelTime = TimeSpan.FromHours(1);
        Func<TimeSpan> wheelClock = MainWindow.WheelClock;
        try
        {
            MainWindow window = Open(dir, settings);
            window.Session.Execute(TimerCommand.StartOrSplit);
            clock.Advance(TimeSpan.FromSeconds(10));
            var center = new Point(window.ClientSize.Width / 2, window.ClientSize.Height / 2);
            MainWindow.WheelClock = () => wheelTime;

            // One wheel step down per event: the list moves to Ricco Harbor's section, and the
            // split scrolled to is outlined (^) while the running split stays current (*).
            window.MouseWheel(center, new Vector(0, -1));
            window.MouseWheel(center, new Vector(0, -1));
            window.MouseWheel(center, new Vector(0, -1));
            HeadlessUi.Pump();
            string scrolled = window.SubsplitsRowsForTests;
            Assert.Contains("[Ricco Harbor", scrolled);
            Assert.Contains("Red Dawgs^", scrolled);
            Assert.Contains("Bianco Hills*", scrolled);

            // A trackpad's small steps in the same scroll add up to MainWindow.TrackpadStep
            // before the list moves on.
            window.MouseWheel(center, new Vector(0, 0.1));
            window.MouseWheel(center, new Vector(0, 0.1));
            HeadlessUi.Pump();
            Assert.Equal(scrolled, window.SubsplitsRowsForTests);
            window.MouseWheel(center, new Vector(0, 0.1));
            HeadlessUi.Pump();
            Assert.Contains("Shadow Mario^", window.SubsplitsRowsForTests);
            string up = window.SubsplitsRowsForTests;

            // After a pause, the smallest movement of the wheel is one split at once.
            wheelTime += TimeSpan.FromSeconds(1);
            window.MouseWheel(center, new Vector(0, -0.1));
            HeadlessUi.Pump();
            Assert.NotEqual(up, window.SubsplitsRowsForTests);
            Assert.Equal(scrolled, window.SubsplitsRowsForTests);

            // Splitting brings the list back to the current split.
            window.Session.Execute(TimerCommand.StartOrSplit);
            HeadlessUi.Pump();
            Assert.DoesNotContain("^", window.SubsplitsRowsForTests);
            Assert.Contains("Hillside Cave*", window.SubsplitsRowsForTests);
            window.Close();
        }
        finally
        {
            MainWindow.WheelClock = wheelClock;
            HeadlessUi.Delete(dir);
        }
    }

    /// <summary>The default layout with a Subsplits list, on a run with sections (PB one minute per split).</summary>
    private static MainWindow Open(string dir, SubsplitsComponentSettings settings)
    {
        var run = new Run { GameName = "Super Mario Sunshine", CategoryName = "Any%" };
        foreach (string name in Names)
        {
            run.AddSegment(name);
        }

        for (int i = 0; i < run.Count; i++)
        {
            run[i].PersonalBestSplitTime = new SplitTime(TimeSpan.FromMinutes(i + 1));
        }

        string splitsPath = Path.Combine(dir, "run.lss");
        XmlRunSerializer.SaveFile(run, splitsPath);
        Layout layout = StandardLayoutFactory.Create();
        int list = layout.Components.FindIndex(c => c.Path == StandardLayoutFactory.SplitsPath);
        layout.Components[list] = new LayoutComponent(StandardLayoutFactory.SubsplitsPath, settings.ToInnerXml());
        string layoutPath = Path.Combine(dir, "subsplits.lsl");
        XmlLayoutSerializer.SaveFile(layout, layoutPath);
        var app = new AppSettings();
        app.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        app.AddToRecentLayouts(layoutPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        XmlAppSettingsSerializer.SaveFile(app, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);

        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>The color drawn at the left edge of a Subsplits row.</summary>
    private static (byte R, byte G, byte B) RowColor(MainWindow window, int row, float splitHeight)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        TimerLayoutSnapshot snapshot = Assert.IsType<TimerLayoutSnapshot>(window.LayoutSnapshotForTests);
        TimerLayoutSlot slot = snapshot.Slots.Single(s => s.Path == StandardLayoutFactory.SubsplitsPath);
        using WriteableBitmap frame = Assert.IsType<WriteableBitmap>(window.CaptureRenderedFrame());
        using var buffer = frame.Lock();
        var pixels = new byte[buffer.RowBytes * buffer.Size.Height];
        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
        double rowHeight = LayoutMetrics.RowHeightFor(splitHeight);
        int x = (int)Math.Round((slot.Bounds.X + 2) * snapshot.Scale * frame.Dpi.X / 96);
        int y = (int)Math.Round((slot.Bounds.Y + (row * rowHeight) + (rowHeight / 2)) * snapshot.Scale * frame.Dpi.Y / 96);
        int offset = (y * buffer.RowBytes) + (x * 4);
        return buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
            ? (pixels[offset + 2], pixels[offset + 1], pixels[offset])
            : (pixels[offset], pixels[offset + 1], pixels[offset + 2]);
    }
}
