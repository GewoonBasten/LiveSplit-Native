using System.Runtime.InteropServices;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Splits and Subsplits paint their Background setting as Windows does (Basten, 13 Oct 2026:
/// "can't change background subsplits"): the rows drew a fixed faint stripe whatever was set.
/// SplitsComponent.DrawBackground paints Plain / Vertical / Horizontal over the component;
/// SplitComponent.DrawGeneral paints Alternating row by row, Splits by split and Subsplits by
/// section; Subsplit Color covers a subsplit's row.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class SplitsBackgroundTests
{
    private static readonly ArgbColor Red = ArgbColor.FromArgb(255, 200, 0, 0);
    private static readonly ArgbColor Blue = ArgbColor.FromArgb(255, 0, 0, 200);
    private static readonly ArgbColor Green = ArgbColor.FromArgb(255, 0, 160, 0);

    [AvaloniaFact]
    public void Splits_Alternate_Background_Colors_By_Split()
    {
        var settings = new SplitsComponentSettings
        {
            BackgroundGradient = "Alternating",
            BackgroundColor = Red,
            BackgroundColor2 = Blue,
            ShowThinSeparators = false,
            SeparatorLastSplit = false,
            DisplayIcons = false,
            VisualSplitCount = 6,
            ShowBlankSplits = true,
            LockLastSplit = true,
        };

        // One, Two, Three, two blank rows, then Four locked to the bottom: a blank row takes
        // Background Color (Run.IndexOf(null) is -1), a split its own by its index.
        IReadOnlyList<(byte R, byte G, byte B)> rows = RowColors(
            StandardLayoutFactory.SplitsPath, settings.ToInnerXml(), ["One", "Two", "Three", "Four"], settings.SplitHeight, rowCount: 6);
        AssertColors([Red, Blue, Red, Red, Red, Blue], rows);
    }

    [AvaloniaFact]
    public void Splits_Plain_Background_Fills_The_Component()
    {
        var settings = new SplitsComponentSettings
        {
            BackgroundGradient = "Plain",
            BackgroundColor = Green,
            BackgroundColor2 = Blue,
            ShowThinSeparators = false,
            SeparatorLastSplit = false,
            DisplayIcons = false,
        };
        IReadOnlyList<(byte R, byte G, byte B)> rows = RowColors(
            StandardLayoutFactory.SplitsPath, settings.ToInnerXml(), ["One", "Two", "Three"], settings.SplitHeight);
        AssertColors([Green, Green, Green], rows);
    }

    [AvaloniaFact]
    public void Subsplits_Alternate_By_Section_And_Paint_Subsplit_Color()
    {
        var settings = new SubsplitsComponentSettings
        {
            BackgroundGradient = "Alternating",
            BackgroundColor = Red,
            BackgroundColor2 = Blue,
            Visibility = SubsplitsComponentSettings.SubsplitVisibility.AlwaysShow,
            ShowSectionHeader = false,
            ShowThinSeparators = false,
            SeparatorLastSplit = false,
            DisplayIcons = false,
        };

        // Sections as Windows' SectionList: [-a, -b, One] [Two] [-c, Three].
        string[] names = ["-a", "-b", "One", "Two", "-c", "Three"];
        Assert.Equal([0, 0, 0, 1, 2, 2], SubsplitsLogic.WindowsSections(names.Select(n => new Segment(n)).ToList()));
        AssertColors([Red, Red, Red, Blue, Red, Red],
            RowColors(StandardLayoutFactory.SubsplitsPath, settings.ToInnerXml(), names, settings.SplitHeight));

        // Subsplit Color (Override Layout Settings) on the subsplits' rows only.
        settings.OverrideSubsplitColor = true;
        settings.SubsplitGradient = "Plain";
        settings.SubsplitTopColor = Green;
        AssertColors([Green, Green, Red, Blue, Green, Red],
            RowColors(StandardLayoutFactory.SubsplitsPath, settings.ToInnerXml(), names, settings.SplitHeight));
    }

    private static void AssertColors(ArgbColor[] expected, IReadOnlyList<(byte R, byte G, byte B)> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        // (Windows: Settings.BackgroundGradient Alternating, SplitComponent.DrawGeneral.)
        for (int i = 0; i < expected.Length; i++)
        {
            (byte r, byte g, byte b) = actual[i];
            Assert.True(
                Math.Abs(r - expected[i].R) <= 4 && Math.Abs(g - expected[i].G) <= 4 && Math.Abs(b - expected[i].B) <= 4,
                $"row {i}: expected {expected[i].R},{expected[i].G},{expected[i].B}, drawn {r},{g},{b}");
        }
    }

    /// <summary>The default layout with its splits list replaced, drawn; the color at each row's left edge.</summary>
    private static IReadOnlyList<(byte R, byte G, byte B)> RowColors(string path, string settingsXml, string[] names, float splitHeight, int? rowCount = null)
    {
        string dir = HeadlessUi.NewTempDir();
        try
        {
            var run = new Run { GameName = "Background", CategoryName = "Any%" };
            foreach (string name in names)
            {
                run.AddSegment(name);
            }

            string splitsPath = Path.Combine(dir, "run.lss");
            XmlRunSerializer.SaveFile(run, splitsPath);
            Layout layout = StandardLayoutFactory.Create();
            int list = layout.Components.FindIndex(c => c.Path == StandardLayoutFactory.SplitsPath);
            layout.Components[list] = new LayoutComponent(path, settingsXml);
            string layoutPath = Path.Combine(dir, "background.lsl");
            XmlLayoutSerializer.SaveFile(layout, layoutPath);
            var settings = new AppSettings();
            settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
            settings.AddToRecentLayouts(layoutPath);
            string settingsPath = Path.Combine(dir, "settings.cfg");
            XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
            AppCli.Parse(["--settings", settingsPath]);

            var window = new MainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(path, window.CurrentLayout.Components[list].Path);
            TimerLayoutSnapshot snapshot = Assert.IsType<TimerLayoutSnapshot>(window.LayoutSnapshotForTests);
            TimerLayoutSlot slot = snapshot.Slots.Single(s => s.Path == path);
            using WriteableBitmap frame = Assert.IsType<WriteableBitmap>(window.CaptureRenderedFrame());
            using var buffer = frame.Lock();
            var pixels = new byte[buffer.RowBytes * buffer.Size.Height];
            Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);

            double rowHeight = LayoutMetrics.RowHeightFor(splitHeight);
            var colors = new List<(byte, byte, byte)>();
            for (int i = 0; i < (rowCount ?? names.Length); i++)
            {
                int x = (int)Math.Round((slot.Bounds.X + 2) * snapshot.Scale * frame.Dpi.X / 96);
                int y = (int)Math.Round((slot.Bounds.Y + (i * rowHeight) + (rowHeight / 2)) * snapshot.Scale * frame.Dpi.Y / 96);
                int offset = (y * buffer.RowBytes) + (x * 4);
                bool bgra = buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888;
                colors.Add(bgra
                    ? (pixels[offset + 2], pixels[offset + 1], pixels[offset])
                    : (pixels[offset], pixels[offset + 1], pixels[offset + 2]));
            }

            window.Close();
            return colors;
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }
}
