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
/// The run's Start Timer value on the real timer: shown when the splits open, counted from
/// when the attempt starts, shown again after a reset and after the app is opened again.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class StartTimerOffsetAppTests
{
    [AvaloniaFact]
    public void Timer_Shows_The_Start_Timer_Value_Through_An_Attempt_And_A_Reopen()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lsn-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Offset", CategoryName = "Any%", Offset = new TimeSpan(0, 0, 5, 40, 440) };
        run.AddSegment("One");
        run.AddSegment("Two");
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
        Assert.Equal("5:40.44", window.TimerText);

        window.Session.Execute(TimerCommand.StartOrSplit);
        clock.Advance(TimeSpan.FromSeconds(3));
        window.Session.Execute(TimerCommand.Pause);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("5:43.44", window.TimerText);

        window.Session.Execute(TimerCommand.ResetWithoutSaving);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("5:40.44", window.TimerText);

        window.Session.State.Run.HasChanged = false;
        window.Close();

        // Opened again: the offset comes back from the file.
        var reopened = new MainWindow();
        reopened.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("5:40.44", reopened.TimerText);
        reopened.Session.State.Run.HasChanged = false;
        reopened.Close();
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
