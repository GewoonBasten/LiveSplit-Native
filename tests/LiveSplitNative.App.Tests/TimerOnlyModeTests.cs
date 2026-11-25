using Avalonia;
using Avalonia.Headless.XUnit;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using Xunit;
using static LiveSplitNative.App.Tests.HeadlessUi;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Windows timer-only mode: no splits and a Timer-only layout. It is how a first start looks,
/// Close Splits enters it, resets keep nothing and ask nothing, and opening splits leaves it
/// with the last layout used.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class TimerOnlyModeTests
{
    [AvaloniaFact]
    public void A_First_Start_Is_A_Timer_Only_Window_That_Keeps_Nothing()
    {
        string dir = NewTempDir();
        AppCli.Parse(["--settings", Path.Combine(dir, "settings.cfg")]);
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        var window = new MainWindow();
        window.Show();
        Pump();

        Assert.True(StandardLayoutFactory.IsTimerOnly(window.CurrentLayout));
        Assert.Equal(1, window.Session.State.Run.Count);
        Assert.Equal(new Size(252, 50), new Size(window.Width, window.Height));

        window.Session.Execute(TimerCommand.StartOrSplit);
        clock.Advance(TimeSpan.FromSeconds(5));
        window.Session.Execute(TimerCommand.StartOrSplit);
        Assert.Equal(TimerPhase.Ended, window.Session.State.CurrentPhase);
        Task reset = window.ResetAsync();
        Pump();

        // No "Update Times?" although it was a first PB, and the run is a new blank one.
        Assert.True(reset.IsCompletedSuccessfully);
        Assert.Empty(window.OwnedWindows);
        Assert.Equal(TimerPhase.NotRunning, window.Session.State.CurrentPhase);
        Assert.Empty(window.Session.State.Run.AttemptHistory);
        Assert.Equal(0, window.Session.State.Run.AttemptCount);
        Assert.Null(window.Session.State.Run[0].PersonalBestSplitTime.RealTime);

        // Quitting after a timer-only attempt asks nothing.
        window.Session.Execute(TimerCommand.StartOrSplit);
        window.Close();
        Pump();
        Assert.False(window.IsVisible);
        Delete(dir);
    }

    [AvaloniaFact]
    public void Close_Splits_Goes_Timer_Only_And_Opening_Splits_Brings_The_Layout_Back()
    {
        string dir = NewTempDir();
        string layoutPath = Path.Combine(dir, "full.lsl");
        Layout full = StandardLayoutFactory.Create();
        full.X = 300;
        full.Y = 200;
        XmlLayoutSerializer.SaveFile(full, layoutPath);
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "G", CategoryName = "Any%" };
        run.AddSegment("One");
        run.AddSegment("Two");
        XmlRunSerializer.SaveFile(run, splitsPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentLayouts(layoutPath);
        settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        var window = new MainWindow();
        window.Show();
        Pump();
        Assert.False(StandardLayoutFactory.IsTimerOnly(window.CurrentLayout));
        Assert.Equal(new PixelPoint(300, 200), window.Position);

        Task close = window.CloseSplitsAsync();
        Pump();
        Assert.True(close.IsCompletedSuccessfully);
        Assert.True(StandardLayoutFactory.IsTimerOnly(window.CurrentLayout));
        Assert.Equal(new PixelPoint(300, 200), window.Position);
        Assert.Equal("", window.CurrentSettings.LastLayoutPath);
        Assert.Equal("", window.CurrentSettings.LastSplitsPath);

        Task<bool> open = window.OpenRunFromFileAsync(splitsPath);
        Pump();
        Assert.True(open.IsCompletedSuccessfully && open.Result);
        Assert.False(StandardLayoutFactory.IsTimerOnly(window.CurrentLayout));
        Assert.Equal(layoutPath, window.CurrentSettings.LastLayoutPath);
        Assert.Equal(2, window.Session.State.Run.Count);

        // Out of timer-only mode, resets keep the attempt again.
        window.Session.Execute(TimerCommand.StartOrSplit);
        window.Session.Execute(TimerCommand.Reset);
        Assert.Single(window.Session.State.Run.AttemptHistory);

        window.Session.State.Run.HasChanged = false;
        window.Close();
        Pump();
        Assert.False(window.IsVisible);
        Delete(dir);
    }
}
