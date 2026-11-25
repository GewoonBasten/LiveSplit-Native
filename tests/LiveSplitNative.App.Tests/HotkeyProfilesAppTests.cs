using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Settings, Hotkey Profiles: New (a copy), Rename (recent splits follow), Remove, and OK keeps them.</summary>
[Collection(MainWindowCollection.Name)]
public class HotkeyProfilesAppTests
{
    [AvaloniaFact]
    public async Task New_Rename_Remove_And_Ok_Keep_The_Profiles()
    {
        var app = new AppSettings();
        app.HotkeyProfile.SplitKey = "Space";
        app.AddToRecentSplits(new RecentSplitsFile("/tmp/a.lss", "Game", "Any%", "RealTime"));
        var window = new SettingsWindow(LayoutSettings.CreateStandard(), TimingMethod.RealTime, app)
        {
            ProfileNameAnswersForTests = new Queue<string?>(["Race", "Default", "Tournament"]),
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Button remove = window.FindControl<Button>("RemoveProfileButton")!;
            ComboBox box = window.FindControl<ComboBox>("HotkeyProfileBox")!;
            Assert.True(box.IsEnabled);
            Assert.False(remove.IsEnabled);

            // New: a copy of the shown profile, shown.
            await window.NewProfileAsync();
            Assert.Equal("Race", window.SelectedHotkeyProfile);
            Assert.Equal("Space", window.HotkeyProfilesForTests["Race"].SplitKey);
            Assert.True(remove.IsEnabled);

            // Rename "Race": "Default" exists (Retry), then "Tournament".
            await window.RenameProfileAsync();
            Assert.Equal(["Default", "Tournament"], window.HotkeyProfilesForTests.Keys);
            Assert.Equal("Tournament", window.SelectedHotkeyProfile);

            // Back to Default: it is shown, and OK keeps it in use.
            box.SelectedItem = "Default";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Default", window.SelectedHotkeyProfile);

            Button ok = window.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
            ok.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["Default", "Tournament"], app.HotkeyProfiles.Keys);
            Assert.Equal("Default", app.CurrentHotkeyProfile);
        }
        finally
        {
            window.Close();
        }

        // Rename the profile the recent splits use: they follow; Remove leaves the last one.
        var second = new SettingsWindow(LayoutSettings.CreateStandard(), TimingMethod.RealTime, app)
        {
            ProfileNameAnswersForTests = new Queue<string?>(["Main"]),
        };
        second.Show();
        Dispatcher.UIThread.RunJobs();
        await second.RenameProfileAsync();
        Assert.Equal("Main", second.SelectedHotkeyProfile);
        second.RemoveProfile();
        Assert.Equal("Tournament", second.SelectedHotkeyProfile);
        Assert.Equal(["Tournament"], second.HotkeyProfilesForTests.Keys);
        Button ok2 = second.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
        ok2.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(["Tournament"], app.HotkeyProfiles.Keys);
        Assert.Equal("Main", app.RecentSplits[0].LastHotkeyProfile);
    }
}
