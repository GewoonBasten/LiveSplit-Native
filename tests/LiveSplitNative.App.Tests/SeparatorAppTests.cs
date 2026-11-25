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

/// <summary>The Separator component draws a line in the layout's Separators color (Basten, 13 Oct: "the separator tool is also missing").</summary>
[Collection(MainWindowCollection.Name)]
public class SeparatorAppTests
{
    [AvaloniaFact]
    public void Separator_Draws_In_The_Separators_Color()
    {
        string dir = HeadlessUi.NewTempDir();
        try
        {
            var run = new Run { GameName = "Separator", CategoryName = "Any%" };
            run.AddSegment("One");
            string splitsPath = Path.Combine(dir, "run.lss");
            XmlRunSerializer.SaveFile(run, splitsPath);
            Layout layout = StandardLayoutFactory.Create();
            layout.Settings.SeparatorsColor = ArgbColor.FromArgb(255, 0, 160, 0);
            layout.Components.Insert(1, StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.SeparatorPath));
            string layoutPath = Path.Combine(dir, "separator.lsl");
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
            Assert.Equal(StandardLayoutFactory.SeparatorPath, window.CurrentLayout.Components[1].Path);
            TimerLayoutSnapshot snapshot = Assert.IsType<TimerLayoutSnapshot>(window.LayoutSnapshotForTests);
            TimerLayoutSlot slot = snapshot.Slots.Single(s => s.ComponentIndex == 1);
            Assert.Equal(2, slot.Bounds.Height, 3);

            using WriteableBitmap frame = Assert.IsType<WriteableBitmap>(window.CaptureRenderedFrame());
            using var buffer = frame.Lock();
            var pixels = new byte[buffer.RowBytes * buffer.Size.Height];
            Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
            int x = (int)Math.Round((slot.Bounds.X + (slot.Bounds.Width / 2)) * snapshot.Scale * frame.Dpi.X / 96);
            int y = (int)Math.Floor((slot.Bounds.Y + 1) * snapshot.Scale * frame.Dpi.Y / 96);
            int offset = (y * buffer.RowBytes) + (x * 4);
            (byte r, byte g, byte b) = buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888
                ? (pixels[offset + 2], pixels[offset + 1], pixels[offset])
                : (pixels[offset], pixels[offset + 1], pixels[offset + 2]);
            Assert.True(r < 30 && g > 120 && b < 30, $"separator drawn {r},{g},{b}");
            window.Close();
        }
        finally
        {
            HeadlessUi.Delete(dir);
        }
    }
}
