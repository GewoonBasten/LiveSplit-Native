using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LiveSplitNative.Core.Imaging;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Detailed Timer comparison lines, with Basten's layout (Height 50, Segment Timer Size 40, icon
/// 40, Segoe UI 13 / 13 bold, the window 613 x 186 per 50 high). As Windows' DrawGeneral (Basten
/// 18 Oct: back to Windows, no shrinking): the fonts keep their own size, the times end where
/// the segment timer's ActualWidth begins and are cut with "..." when they do not fit; the last
/// line sits on the segment timer's baseline (Basten 17 Oct).
/// </summary>
[Collection(MainWindowCollection.Name)]
public class DetailedTimerAppTests
{
    [AvaloniaFact]
    public void Basten_Layout_Keeps_The_Font_Sizes_And_Cuts_Times_That_Do_Not_Fit()
    {
        var settings = new DetailedTimerComponentSettings
        {
            Height = 50,
            SegmentTimerSizeRatio = 40,
            DisplayIcon = true,
            IconSize = 40,
        };
        string dir = HeadlessUi.NewTempDir();
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        try
        {
            MainWindow window = Open(dir, settings, windowHeight: 186);
            window.Session.Execute(TimerCommand.StartOrSplit);
            clock.Advance(TimeSpan.FromSeconds(25));
            Render();

            TimerSurfaceControl.DetailedTimerText text = Assert.IsType<TimerSurfaceControl.DetailedTimerText>(window.DetailedTimerTextForTests);
            Assert.Equal(10, text.SlotHeight, 3);
            Assert.Equal(13, text.LabelSize, 3);
            Assert.Equal(13, text.TimesSize, 3);
            Assert.True(text.TimesRight <= text.SlotRight + 0.01, $"times end at {text.TimesRight}, slots end at {text.SlotRight}");
            Assert.True(text.FirstTime == "1:00.00" || text.FirstTime.EndsWith("...", StringComparison.Ordinal), text.FirstTime);
            Assert.True(text.SecondTime == "50.00" || text.SecondTime.EndsWith("...", StringComparison.Ordinal), text.SecondTime);

            // Basten 17 Oct: the last time sits on the segment timer's baseline.
            Assert.NotNull(text.SegmentTimerBaseline);
            Assert.Equal(text.SegmentTimerBaseline!.Value, text.LastTimeBaseline!.Value, 2);
            window.Close();
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void Times_Are_Whole_When_The_Slot_Has_Room()
    {
        // Hide Comparison 2: the first line gets the whole segment band (30 high at Height 75).
        var settings = new DetailedTimerComponentSettings
        {
            Height = 75,
            SegmentTimerSizeRatio = 40,
            HideComparison = true,
        };
        string dir = HeadlessUi.NewTempDir();
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        try
        {
            MainWindow window = Open(dir, settings, windowHeight: 75);
            window.Session.Execute(TimerCommand.StartOrSplit);
            clock.Advance(TimeSpan.FromSeconds(5));
            Render();

            TimerSurfaceControl.DetailedTimerText text = Assert.IsType<TimerSurfaceControl.DetailedTimerText>(window.DetailedTimerTextForTests);
            Assert.Equal(30, text.SlotHeight, 3);
            Assert.Equal(13, text.LabelSize, 3);
            Assert.Equal(13, text.TimesSize, 3);
            Assert.Equal("1:00.00", text.FirstTime);
            Assert.True(text.TimesRight <= text.SlotRight + 0.01, $"times end at {text.TimesRight}, slots end at {text.SlotRight}");

            // Basten 17 Oct: the last time sits on the segment timer's baseline.
            Assert.NotNull(text.SegmentTimerBaseline);
            Assert.Equal(text.SegmentTimerBaseline!.Value, text.LastTimeBaseline!.Value, 2);
            window.Close();
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void CutOff_Drops_Characters_And_Adds_Dots_Until_The_Time_Fits()
    {
        // SimpleLabel.CutOff: whole when narrower than the label, else "..." added while
        // characters come off the end, nothing when even "..." does not fit.
        var family = new Avalonia.Media.FontFamily(AppFonts.LayoutTextFamily);
        double Width(string s) => TimerSurfaceControl.MonospacedWidth(s, 13, Avalonia.Media.FontWeight.Bold, family, Avalonia.Media.FontStyle.Normal);
        string Cut(double width) => TimerSurfaceControl.CutOff("1:00.00", width, 13, Avalonia.Media.FontWeight.Bold, family, Avalonia.Media.FontStyle.Normal);

        Assert.Equal("1:00.00", Cut(Width("1:00.00") + 0.5));
        string cut = Cut(Width("1:00.00"));
        Assert.EndsWith("...", cut, StringComparison.Ordinal);
        Assert.StartsWith(cut[..^3], "1:00.00", StringComparison.Ordinal);
        Assert.True(Width(cut) < Width("1:00.00"), cut);
        Assert.Equal("", Cut(1));
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// A layout with only this Detailed Timer, 613 wide; a run of three splits with icons, PB
    /// one minute per split, best segments 50 s.
    /// </summary>
    private static MainWindow Open(string dir, DetailedTimerComponentSettings settings, double windowHeight)
    {
        var run = new Run { GameName = "Super Mario Sunshine", CategoryName = "Any%" };
        string icon = IconBlobDecoder.ToPortableIconXml(IconPng());
        foreach (string name in new[] { "Red Dawgs", "Goopy Inferno", "Shadow Mario" })
        {
            run.AddSegment(name);
        }

        for (int i = 0; i < run.Count; i++)
        {
            run[i].PersonalBestSplitTime = new SplitTime(TimeSpan.FromMinutes(i + 1));
            run[i].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(50));
            run[i].IconXml = icon;
        }

        string splitsPath = Path.Combine(dir, "run.lss");
        XmlRunSerializer.SaveFile(run, splitsPath);
        Layout layout = StandardLayoutFactory.Create();
        layout.Components.Clear();
        layout.Components.Add(new LayoutComponent(StandardLayoutFactory.DetailedTimerPath, settings.ToInnerXml()));
        layout.Mode = LayoutMode.Vertical;
        layout.VerticalWidth = 613;
        layout.VerticalHeight = (int)windowHeight;
        string layoutPath = Path.Combine(dir, "detailed-timer.lsl");
        XmlLayoutSerializer.SaveFile(layout, layoutPath);
        var app = new AppSettings();
        app.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        app.AddToRecentLayouts(layoutPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        XmlAppSettingsSerializer.SaveFile(app, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);

        var window = new MainWindow();
        window.Show();
        Render();
        return window;
    }

    /// <summary>A 32 x 32 gray PNG.</summary>
    private static byte[] IconPng()
    {
        using var bitmap = new WriteableBitmap(new PixelSize(32, 32), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (ILockedFramebuffer buffer = bitmap.Lock())
        {
            var pixels = new byte[buffer.RowBytes * buffer.Size.Height];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 128;
                pixels[i + 1] = 128;
                pixels[i + 2] = 128;
                pixels[i + 3] = 255;
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        }

        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }
}
