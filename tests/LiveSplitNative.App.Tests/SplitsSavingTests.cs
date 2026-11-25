using Avalonia.Headless.XUnit;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using Xunit;
using static LiveSplitNative.App.Tests.HeadlessUi;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Windows TimerForm's splits prompts: "Save Splits?" before quitting with changed splits,
/// "Save as Personal Best?" when saving mid-attempt (the file gets the attempt as a reset would
/// record it, the timer keeps running), and "Update Times?" on reset with Warn On Reset on.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class SplitsSavingTests
{
    [AvaloniaFact]
    public void Changed_Splits_Ask_Before_Quitting_Cancel_Stays_And_Yes_Saves_The_Attempt()
    {
        using var app = TimerApp.Start();
        app.StartAndSplitOnce(TimeSpan.FromSeconds(2));

        app.Window.Close();
        Pump();
        Assert.True(app.Window.IsVisible);
        Click(Prompt(app.Window, "Save Splits?"), "Cancel");
        Assert.True(app.Window.IsVisible);
        Assert.Equal(TimerPhase.Running, app.State.CurrentPhase);
        Assert.Equal(0, XmlRunSerializer.LoadFile(app.SplitsPath).AttemptCount);

        app.Window.Close();
        Pump();
        Click(Prompt(app.Window, "Save Splits?"), "Yes");

        Assert.False(app.Window.IsVisible);
        Run saved = XmlRunSerializer.LoadFile(app.SplitsPath);
        Assert.Equal(1, saved.AttemptCount);
        Assert.Single(saved.AttemptHistory);
        Assert.Equal(TimeSpan.FromSeconds(2), saved[0].BestSegmentTime.RealTime);
    }

    [AvaloniaFact]
    public void Changed_Splits_No_Quits_Without_Writing_The_File()
    {
        using var app = TimerApp.Start();
        byte[] before = File.ReadAllBytes(app.SplitsPath);
        app.StartAndSplitOnce(TimeSpan.FromSeconds(2));

        app.Window.Close();
        Pump();
        Click(Prompt(app.Window, "Save Splits?"), "No");

        Assert.False(app.Window.IsVisible);
        Assert.Equal(before, File.ReadAllBytes(app.SplitsPath));
    }

    [AvaloniaFact]
    public void Saving_Mid_Attempt_Asks_About_Pb_And_No_Keeps_The_Timer_Running()
    {
        using var app = TimerApp.Start();
        app.StartAndSplitOnce(TimeSpan.FromSeconds(2));

        Task<bool> save = app.Window.SaveSplitsAsync(saveAs: false, promptPersonalBest: true);
        Pump();
        Click(Prompt(app.Window, "Save as Personal Best?"), "No");

        Assert.True(save.IsCompletedSuccessfully && save.Result);
        Assert.Equal(TimerPhase.Running, app.State.CurrentPhase);
        Assert.Equal(1, app.State.CurrentSplitIndex);
        Assert.False(app.State.Run.HasChanged);
        Run saved = XmlRunSerializer.LoadFile(app.SplitsPath);
        Assert.Single(saved.AttemptHistory);
        Assert.Equal(TimeSpan.FromSeconds(2), saved[0].BestSegmentTime.RealTime);
        Assert.Null(app.State.Run[0].BestSegmentTime.RealTime);
        app.QuitQuietly();
    }

    [AvaloniaFact]
    public void Saving_Mid_Attempt_Cancel_Saves_Nothing()
    {
        using var app = TimerApp.Start();
        byte[] before = File.ReadAllBytes(app.SplitsPath);
        app.StartAndSplitOnce(TimeSpan.FromSeconds(2));

        Task<bool> save = app.Window.SaveSplitsAsync(saveAs: false, promptPersonalBest: true);
        Pump();
        Click(Prompt(app.Window, "Save as Personal Best?"), "Cancel");

        Assert.True(save.IsCompletedSuccessfully);
        Assert.False(save.Result);
        Assert.Equal(before, File.ReadAllBytes(app.SplitsPath));
        Assert.True(app.State.Run.HasChanged);
        app.QuitQuietly();
    }

    [AvaloniaFact]
    public void Warn_On_Reset_Asks_After_A_Gold_Cancel_Keeps_Running_And_No_Drops_The_Times()
    {
        using var app = TimerApp.Start();
        app.StartAndSplitOnce(TimeSpan.FromSeconds(2));

        Task reset = app.Window.ResetAsync();
        Pump();
        Click(Prompt(app.Window, "Update Times?"), "Cancel");
        Assert.True(reset.IsCompletedSuccessfully);
        Assert.Equal(TimerPhase.Running, app.State.CurrentPhase);

        reset = app.Window.ResetAsync();
        Pump();
        Click(Prompt(app.Window, "Update Times?"), "No");
        Assert.True(reset.IsCompletedSuccessfully);
        Assert.Equal(TimerPhase.NotRunning, app.State.CurrentPhase);
        Assert.Null(app.State.Run[0].BestSegmentTime.RealTime);
        Assert.Empty(app.State.Run.AttemptHistory);

        app.StartAndSplitOnce(TimeSpan.FromSeconds(3));
        reset = app.Window.ResetAsync();
        Pump();
        Click(Prompt(app.Window, "Update Times?"), "Yes");
        Assert.Equal(TimerPhase.NotRunning, app.State.CurrentPhase);
        Assert.Equal(TimeSpan.FromSeconds(3), app.State.Run[0].BestSegmentTime.RealTime);
        Assert.Single(app.State.Run.AttemptHistory);
        app.QuitQuietly();
    }

    [AvaloniaFact]
    public void Reset_Without_A_Gold_Or_With_Warn_On_Reset_Off_Does_Not_Ask()
    {
        using var app = TimerApp.Start(warnOnReset: false);
        app.StartAndSplitOnce(TimeSpan.FromSeconds(2));

        Task reset = app.Window.ResetAsync();
        Pump();

        Assert.True(reset.IsCompletedSuccessfully);
        Assert.Empty(app.Window.OwnedWindows);
        Assert.Equal(TimerPhase.NotRunning, app.State.CurrentPhase);
        Assert.Equal(TimeSpan.FromSeconds(2), app.State.Run[0].BestSegmentTime.RealTime);

        // Warn On Reset on, but nothing beaten: started and reset at once.
        app.Settings.WarnOnReset = true;
        app.Window.Session.Execute(TimerCommand.StartOrSplit);
        reset = app.Window.ResetAsync();
        Pump();
        Assert.Empty(app.Window.OwnedWindows);
        Assert.Equal(TimerPhase.NotRunning, app.State.CurrentPhase);
        app.QuitQuietly();
    }

    /// <summary>The app on a temp settings.cfg with a saved three-segment run and a manual clock.</summary>
    private sealed class TimerApp : IDisposable
    {
        private readonly ManualTimeSourceScope _clock;

        private TimerApp(string dir, string splitsPath, ManualTimeSourceScope clock, MainWindow window)
        {
            Dir = dir;
            SplitsPath = splitsPath;
            _clock = clock;
            Window = window;
        }

        public string Dir { get; }

        public string SplitsPath { get; }

        public MainWindow Window { get; }

        public LiveSplitState State => Window.Session.State;

        public AppSettings Settings => Window.CurrentSettings;

        public static TimerApp Start(bool warnOnReset = true)
        {
            string dir = NewTempDir();
            string splitsPath = Path.Combine(dir, "run.lss");
            var run = new Run { GameName = "Test Game", CategoryName = "Any%" };
            run.AddSegment("One");
            run.AddSegment("Two");
            run.AddSegment("Three");
            XmlRunSerializer.SaveFile(run, splitsPath);

            string settingsPath = Path.Combine(dir, "settings.cfg");
            var settings = new AppSettings { WarnOnReset = warnOnReset };
            settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
            XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
            AppCli.Parse(["--settings", settingsPath]);

            ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
            var window = new MainWindow();
            window.Show();
            Pump();
            Assert.Equal(3, window.Session.State.Run.Count);
            Assert.False(window.Session.State.Run.HasChanged);
            return new TimerApp(dir, splitsPath, clock, window);
        }

        public void StartAndSplitOnce(TimeSpan segment)
        {
            Window.Session.Execute(TimerCommand.StartOrSplit);
            _clock.Advance(segment);
            Window.Session.Execute(TimerCommand.StartOrSplit);
            Pump();
            Assert.Equal(1, State.CurrentSplitIndex);
        }

        /// <summary>Closes without the "Save Splits?" question, for tests about something else.</summary>
        public void QuitQuietly()
        {
            State.Run.HasChanged = false;
            Window.Close();
            Pump();
            Assert.False(Window.IsVisible);
        }

        public void Dispose()
        {
            _clock.Dispose();
            Delete(Dir);
        }
    }
}
