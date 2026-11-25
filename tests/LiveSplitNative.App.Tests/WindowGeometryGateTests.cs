using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// F1 gate: a split, pause, reset or any other timer command changes no window property. The
/// window only changes when the layout or the run's segment count does.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class WindowGeometryGateTests
{
    [AvaloniaFact]
    public void Timer_Commands_Change_No_Window_Property()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lsn-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        // Three segments, so the steps below split, pause, skip and undo inside a running attempt
        // (without splits the app would start in timer-only mode).
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Gate", CategoryName = "Any%" };
        run.AddSegment("One");
        run.AddSegment("Two");
        run.AddSegment("Three");
        XmlRunSerializer.SaveFile(run, splitsPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));

        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        WindowProperties before = WindowProperties.Of(window);
        // NaN sizes would compare equal to themselves and hide any change.
        Assert.True(double.IsFinite(before.Width) && before.Width > 0, $"width {before.Width}");
        Assert.True(double.IsFinite(before.Height) && before.Height > 0, $"height {before.Height}");
        Assert.True(before.ClientSize.Width > 0 && before.ClientSize.Height > 0, $"client {before.ClientSize}");

        (string Label, Action Act)[] steps =
        [
            ("start", () => window.Session.Execute(TimerCommand.StartOrSplit)),
            ("advance 3 s", () => clock.Advance(TimeSpan.FromSeconds(3))),
            ("split", () => window.Session.Execute(TimerCommand.StartOrSplit)),
            ("pause", () => window.Session.Execute(TimerCommand.Pause)),
            ("resume", () => window.Session.Execute(TimerCommand.StartOrSplit)),
            ("undo all pauses", () => window.Session.Execute(TimerCommand.UndoAllPauses)),
            ("next comparison", () => window.Session.Execute(TimerCommand.SwitchComparisonNext)),
            ("previous comparison", () => window.Session.Execute(TimerCommand.SwitchComparisonPrevious)),
            ("skip split", () => window.Session.Execute(TimerCommand.SkipSplit)),
            ("undo split", () => window.Session.Execute(TimerCommand.UndoSplit)),
            ("advance 2 s", () => clock.Advance(TimeSpan.FromSeconds(2))),
            ("split", () => window.Session.Execute(TimerCommand.StartOrSplit)),
            ("split to end", () =>
            {
                for (int i = 0; i < 10 && window.Session.State.CurrentPhase == TimerPhase.Running; i++)
                {
                    clock.Advance(TimeSpan.FromSeconds(1));
                    window.Session.Execute(TimerCommand.StartOrSplit);
                }
            }),
            ("reset", () => window.Session.Execute(TimerCommand.Reset)),
            ("start again", () => window.Session.Execute(TimerCommand.StartOrSplit)),
            ("reset without saving", () => window.Session.Execute(TimerCommand.ResetWithoutSaving)),
            ("game time", () => window.Session.SetTimingMethod(TimingMethod.GameTime)),
        ];

        foreach ((string label, Action act) in steps)
        {
            act();
            Dispatcher.UIThread.RunJobs();
            WindowProperties after = WindowProperties.Of(window);
            Assert.True(before == after, $"after {label}:\nbefore {before}\nafter  {after}");
        }

        // Sanity: the commands really ran.
        Assert.True(window.Session.State.Run.AttemptCount >= 2);
        // The attempts changed the splits; quit without the "Save Splits?" question.
        window.Session.State.Run.HasChanged = false;
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

    private readonly record struct WindowProperties(
        double Width,
        double Height,
        PixelPoint Position,
        double MinWidth,
        double MinHeight,
        double MaxWidth,
        double MaxHeight,
        Size ClientSize,
        bool Topmost,
        bool CanResize,
        WindowState WindowState,
        SizeToContent SizeToContent)
    {
        public static WindowProperties Of(Window w) => new(
            w.Width, w.Height, w.Position, w.MinWidth, w.MinHeight, w.MaxWidth, w.MaxHeight,
            w.ClientSize, w.Topmost, w.CanResize, w.WindowState, w.SizeToContent);
    }
}
