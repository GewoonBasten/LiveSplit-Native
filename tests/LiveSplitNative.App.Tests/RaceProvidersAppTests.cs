using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Manage Racing Services (RaceProviderManagingDialog) and Settings' button for it.</summary>
[Collection(MainWindowCollection.Name)]
public class RaceProvidersAppTests
{
    private static List<RaceProviderSettings> WithUnloaded()
    {
        var document = new System.Xml.XmlDocument();
        document.LoadXml("<RaceProviderPlugins><Plugin name=\"LiveSplit.Other.dll\" enabled=\"False\"><X>1</X></Plugin></RaceProviderPlugins>");
        return RaceProviderSettings.Load(document.DocumentElement);
    }

    private static bool Visible(Control control) => control.IsVisible;

    [AvaloniaFact]
    public void Lists_The_Services_Checked_When_Enabled_And_Shows_The_Selected_One()
    {
        List<RaceProviderSettings> providers = WithUnloaded();
        var dialog = new RaceProvidersWindow(providers);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Equal("Manage Racing Services", dialog.Title);
            Assert.Equal((480, 290), ((int)dialog.Width, (int)dialog.Height));
            List<CheckBox> boxes = dialog.GetLogicalDescendants().OfType<CheckBox>().ToList();
            Assert.Equal([false, true, true], boxes.Select(b => b.IsChecked == true));
            Assert.Equal(
                ["LiveSplit.Other.dll", "racetime.gg", "SRL"],
                dialog.GetLogicalDescendants().OfType<ListBox>().Single().GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

            // Nothing selected: no labels, links or settings.
            TextBlock website = Text(dialog, "Website:");
            TextBlock rules = Text(dialog, "Rules:");
            Assert.False(Visible(website) || Visible(rules));
            Assert.Empty(Links(dialog));

            dialog.Select(1);
            Dispatcher.UIThread.RunJobs();
            Assert.True(Visible(website) && Visible(rules));
            Assert.Equal(["https://racetime.gg", "https://racetime.gg/about/rules"], Links(dialog));
            Assert.Equal(RaceProviderSettings.NoOptionsText, SettingsText(dialog).Text);

            dialog.Select(2);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["https://www.speedrunslive.com", "https://www.speedrunslive.com/rules-faq/rules"], Links(dialog));
            Assert.Equal(Avalonia.Media.TextAlignment.Center, SettingsText(dialog).TextAlignment);

            // A plugin this app does not have: no links, the red "could not be loaded".
            dialog.Select(0);
            Dispatcher.UIThread.RunJobs();
            Assert.False(Visible(website) || Visible(rules));
            Assert.Equal(RaceProviderSettings.NotLoadedText, SettingsText(dialog).Text);

            // providerListBox_ItemCheck: the list's settings change at once.
            dialog.SetChecked(1, false);
            dialog.SetChecked(0, true);
            Assert.Equal([true, false, true], providers.Select(p => p.Enabled));
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public void A_Link_Opens_In_The_Browser()
    {
        Action<string> saved = RacetimeController.OpenBrowser;
        var opened = new List<string>();
        RacetimeController.OpenBrowser = opened.Add;
        var dialog = new RaceProvidersWindow();
        dialog.Show();
        try
        {
            dialog.Select(0);
            Dispatcher.UIThread.RunJobs();
            HyperlinkButton rules = dialog.GetLogicalDescendants().OfType<HyperlinkButton>()
                .Single(l => Equals(l.Content, "https://racetime.gg/about/rules"));
            rules.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(["https://racetime.gg/about/rules"], opened);
        }
        finally
        {
            RacetimeController.OpenBrowser = saved;
            dialog.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("OK", "OK", false)]
    [InlineData("Cancel", "OK", true)]
    [InlineData("OK", "Cancel", true)]
    public async Task Settings_Keeps_The_Changes_When_Both_Dialogs_Are_Confirmed(string dialogButton, string settingsButton, bool racetimeEnabled)
    {
        var appSettings = new AppSettings();
        RacetimeSettings racetime = appSettings.Racetime;
        var settings = new SettingsWindow(LayoutSettings.CreateStandard(), TimingMethod.RealTime, appSettings);
        settings.Show();
        Dispatcher.UIThread.RunJobs();
        Task<object?> closed = WaitClosed(settings);

        HeadlessUi.Click(settings, "Manage Racing Services...");
        await Task.Yield();
        HeadlessUi.Pump();
        RaceProvidersWindow dialog = Assert.IsType<RaceProvidersWindow>(HeadlessUi.Prompt(settings, "Manage Racing Services"));
        dialog.SetChecked(0, false);
        // Settings edits copies until its own OK.
        Assert.True(racetime.Enabled);
        HeadlessUi.Click(dialog, dialogButton);
        await Task.Yield();
        HeadlessUi.Pump();
        Assert.Empty(settings.OwnedWindows);

        HeadlessUi.Click(settings, settingsButton);
        await closed;
        Assert.Equal(racetimeEnabled, appSettings.Racetime.Enabled);
        Assert.Equal(["racetime.gg", "SRL"], appSettings.RaceProviders.Select(p => p.DisplayName));
    }

    private static Task<object?> WaitClosed(Window window)
    {
        var done = new TaskCompletionSource<object?>();
        window.Closed += (_, _) => done.TrySetResult(null);
        return done.Task;
    }

    private static TextBlock Text(Window window, string text) =>
        window.GetLogicalDescendants().OfType<TextBlock>().Single(t => t.Text == text);

    private static List<string> Links(Window window) =>
        [.. window.GetLogicalDescendants().OfType<HyperlinkButton>().Select(l => l.Content as string).OfType<string>().Where(s => s.Length > 0)];

    private static TextBlock SettingsText(Window window) =>
        window.FindControl<Panel>("SettingsPanel")!.Children.OfType<TextBlock>().Single();
}
