using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Settings → Racing: Race Viewer is kept, Log Out of All Accounts deletes every saved login; and a
/// recent layout that can't be found stays in the recent list (Edit History removes it).
/// </summary>
[Collection(MainWindowCollection.Name)]
public class SettingsAccountsAppTests
{
    private static Button Ok(Window window) =>
        window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));

    [AvaloniaFact]
    public void Race_Viewer_Is_Shown_And_Kept_On_Ok()
    {
        var app = new AppSettings { RaceViewer = "Kadgar" };
        var window = new SettingsWindow(LayoutSettings.CreateStandard(), TimingMethod.RealTime, app);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            ComboBox viewer = window.FindControl<ComboBox>("RaceViewerBox")!;
            Assert.True(viewer.IsEnabled);
            Assert.Equal(2, viewer.SelectedIndex);
            viewer.SelectedIndex = 3;
            Ok(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Speedrun.tv", app.RaceViewer);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Log_Out_Deletes_Every_Saved_Login()
    {
        ICredentialStore racetime = RacetimeController.Credentials;
        var store = new MemoryCredentialStore();
        store.Write(SpeedrunComSubmitter.CredentialName, "key");
        store.Write(RacetimeAuthenticator.AccessTokenName, "access");
        store.Write(RacetimeAuthenticator.RefreshTokenName, "refresh");
        SpeedrunComSharing.Credentials = store;
        RacetimeController.Credentials = store;
        SpeedrunComSharing.Reset();
        Assert.Equal("key", SpeedrunComSharing.Submitter.AccessToken);
        var window = new SettingsWindow(LayoutSettings.CreateStandard(), TimingMethod.RealTime, new AppSettings());
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Button logOut = window.FindControl<Button>("LogOutButton")!;
            Assert.True(logOut.IsEnabled);
            logOut.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(logOut.IsEnabled);
            Assert.Null(store.Read(SpeedrunComSubmitter.CredentialName));
            Assert.Null(store.Read(RacetimeAuthenticator.AccessTokenName));
            Assert.Null(store.Read(RacetimeAuthenticator.RefreshTokenName));
            Assert.Null(SpeedrunComSharing.Submitter.AccessToken);

            // Nothing saved: the button is off from the start.
            var again = new SettingsWindow(LayoutSettings.CreateStandard(), TimingMethod.RealTime, new AppSettings());
            Assert.False(again.FindControl<Button>("LogOutButton")!.IsEnabled);
        }
        finally
        {
            window.Close();
            RacetimeController.Credentials = racetime;
            SpeedrunComSharing.Credentials = new MemoryCredentialStore();
            SpeedrunComSharing.Reset();
        }
    }

    [Fact]
    public void Race_Viewer_Round_Trips_Through_Settings_Cfg()
    {
        string path = Path.Combine(HeadlessUi.NewTempDir(), "settings.cfg");
        XmlAppSettingsSerializer.SaveFile(new AppSettings { RaceViewer = "MultiTwitch" }, path);
        Assert.Equal("MultiTwitch", XmlAppSettingsSerializer.LoadFile(path).RaceViewer);
        File.WriteAllText(path, File.ReadAllText(path).Replace("<RaceViewer>MultiTwitch</RaceViewer>", "<RaceViewer>Nope</RaceViewer>", StringComparison.Ordinal));
        Assert.Equal("SpeedRunsLive", XmlAppSettingsSerializer.LoadFile(path).RaceViewer);
    }

    [AvaloniaFact]
    public async Task A_Missing_Recent_Layout_Stays_In_The_Recent_List()
    {
        string dir = HeadlessUi.NewTempDir();
        string missing = Path.Combine(dir, "gone.lsl");
        var settings = new AppSettings();
        settings.AddToRecentLayouts(missing);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        var window = new MainWindow();
        window.Show();
        HeadlessUi.Pump();
        try
        {
            Assert.Contains(missing, window.SettingsForTests.RecentLayouts);
            Task<bool> open = window.OpenLayoutFromPathForTestsAsync(missing);
            HeadlessUi.Pump();
            foreach (Window dialog in window.OwnedWindows.ToList())
            {
                dialog.Close();
            }

            Assert.False(await open);
            Assert.Contains(missing, window.SettingsForTests.RecentLayouts);
        }
        finally
        {
            foreach (Window dialog in window.OwnedWindows.ToList())
            {
                dialog.Close();
            }

            window.Close();
        }
    }
}
