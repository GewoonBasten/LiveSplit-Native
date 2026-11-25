using Avalonia.Headless.XUnit;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;
using Xunit;
using static LiveSplitNative.App.Tests.HeadlessUi;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Windows TimerForm's recent files: opening splits remembers the ones being left with their
/// timing method and puts the new ones last; a recent file reopens with its timing method;
/// Use Layout opens the run's layout; Close Splits leaves an empty last entry.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class RecentFilesAppTests
{
    [AvaloniaFact]
    public void Opening_Splits_Remembers_Both_Files_And_Their_Timing_Methods()
    {
        string dir = NewTempDir();
        string a = SaveRun(dir, "a.lss", "Game A");
        string b = SaveRun(dir, "b.lss", "Game B");
        MainWindow window = StartWith(dir, a);
        Assert.Equal("Game A", window.Session.State.Run.GameName);

        window.Session.SetTimingMethod(TimingMethod.GameTime);
        Task<bool> open = window.OpenRunFromFileAsync(b);
        Pump();
        Assert.True(open.IsCompletedSuccessfully && open.Result);

        Assert.Equal("Game B", window.Session.State.Run.GameName);
        AppSettings settings = window.CurrentSettings;
        Assert.Equal(new[] { a, b }, settings.RecentSplits.Select(x => x.Path));
        Assert.Equal("GameTime", settings.RecentSplits[0].LastTimingMethod);
        Assert.Equal(b, settings.LastSplitsPath);

        // Back to A: its timing method comes back with it.
        window.Session.SetTimingMethod(TimingMethod.RealTime);
        open = window.OpenRunFromFileAsync(a);
        Pump();
        Assert.True(open.Result);
        Assert.Equal(TimingMethod.GameTime, window.Session.State.CurrentTimingMethod);
        Assert.Equal(new[] { b, a }, settings.RecentSplits.Select(x => x.Path));
        Assert.Equal("RealTime", settings.RecentSplits[0].LastTimingMethod);

        Quit(window);
        Delete(dir);
    }

    [AvaloniaFact]
    public void Splits_With_Use_Layout_Open_That_Layout()
    {
        string dir = NewTempDir();
        string layoutPath = Path.Combine(dir, "own.lsl");
        XmlLayoutSerializer.SaveFile(StandardLayoutFactory.Create(), layoutPath);
        string a = SaveRun(dir, "a.lss", "Game A");
        string b = SaveRun(dir, "b.lss", "Game B", layoutPath);
        MainWindow window = StartWith(dir, a);
        Assert.NotEqual(layoutPath, window.CurrentSettings.LastLayoutPath);

        Task<bool> open = window.OpenRunFromFileAsync(b);
        Pump();

        Assert.True(open.Result);
        Assert.Equal(layoutPath, window.CurrentSettings.LastLayoutPath);
        Quit(window);
        Delete(dir);
    }

    [AvaloniaFact]
    public void Close_Splits_Starts_The_Next_Session_Without_Splits()
    {
        string dir = NewTempDir();
        string a = SaveRun(dir, "a.lss", "Game A");
        MainWindow window = StartWith(dir, a);

        Task close = window.CloseSplitsAsync();
        Pump();

        Assert.True(close.IsCompletedSuccessfully);
        Assert.Equal("", window.CurrentSettings.LastSplitsPath);
        Assert.Equal(new[] { a, "" }, window.CurrentSettings.RecentSplits.Select(x => x.Path));
        Assert.Equal("", window.Session.State.Run.GameName);
        Quit(window);

        AppSettings saved = XmlAppSettingsSerializer.LoadFile(Path.Combine(dir, "settings.cfg"));
        Assert.Equal("", saved.LastSplitsPath);
        Delete(dir);
    }

    private static string SaveRun(string dir, string fileName, string game, string layoutPath = "")
    {
        var run = new Run { GameName = game, CategoryName = "Any%", LayoutPath = layoutPath };
        run.AddSegment("One");
        run.AddSegment("Two");
        string path = Path.Combine(dir, fileName);
        XmlRunSerializer.SaveFile(run, path);
        return path;
    }

    private static MainWindow StartWith(string dir, string splitsPath)
    {
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentSplits(splitsPath, XmlRunSerializer.LoadFile(splitsPath), TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        var window = new MainWindow();
        window.Show();
        Pump();
        return window;
    }

    private static void Quit(MainWindow window)
    {
        window.Session.State.Run.HasChanged = false;
        window.CurrentLayout.HasChanged = false;
        window.Close();
        Pump();
        Assert.False(window.IsVisible);
    }
}
