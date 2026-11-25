using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Open Splits / Layout From URL... and Edit History (both menus).</summary>
[Collection(MainWindowCollection.Name)]
public class OpenFromUrlAndHistoryAppTests
{
    private static string NewRun(out string splitsPath)
    {
        string dir = HeadlessUi.NewTempDir();
        splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Local", CategoryName = "Any%" };
        run.AddSegment("One");
        XmlRunSerializer.SaveFile(run, splitsPath);
        var settings = new AppSettings();
        settings.AddToRecentSplits(Path.Combine(dir, "old.lss"), run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        settings.AddToRecentLayouts(Path.Combine(dir, "a.lsl"));
        settings.AddToRecentLayouts(Path.Combine(dir, "b.lsl"));
        string settingsPath = Path.Combine(dir, "settings.cfg");
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        return dir;
    }

    [AvaloniaFact]
    public async Task From_Url_Opens_Splits_And_Layouts_As_Unsaved()
    {
        var web = new Run { GameName = "From The Web", CategoryName = "100%" };
        web.AddSegment("A");
        web.AddSegment("B");
        using var lss = new MemoryStream();
        XmlRunSerializer.Save(web, lss);
        Layout webLayout = StandardLayoutFactory.Create();
        webLayout.Components.RemoveAll(c => c.Path != StandardLayoutFactory.TimerPath);
        using var lsl = new MemoryStream();
        XmlLayoutSerializer.Save(webLayout, lsl);
        var asked = new List<Uri>();
        Func<Uri, CancellationToken, Task<byte[]>> download = MainWindow.DownloadFromUrl;
        MainWindow.DownloadFromUrl = (uri, _) =>
        {
            asked.Add(uri);
            return uri.AbsolutePath.EndsWith(".lss", StringComparison.Ordinal) ? Task.FromResult(lss.ToArray())
                : uri.AbsolutePath.EndsWith(".lsl", StringComparison.Ordinal) ? Task.FromResult(lsl.ToArray())
                : uri.Host == "bad" ? Task.FromResult(Encoding.UTF8.GetBytes("<html/>"))
                : throw new HttpRequestException("404");
        };
        string dir = NewRun(out _);
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            DialogChrome.PromptAnswersForTests = new Queue<string?>(["https://example.com/run.lss", "https://example.com/layout.lsl"]);
            Assert.True(await window.OpenSplitsFromUrlAsync());
            Assert.Equal("From The Web", window.Session.State.Run.GameName);
            Assert.True(window.Session.State.Run.HasChanged);
            Assert.True(await window.OpenLayoutFromUrlAsync());
            Assert.Equal([StandardLayoutFactory.TimerPath], window.CurrentLayout.Components.Select(c => c.Path));
            Assert.True(window.CurrentLayout.HasChanged);

            // Not a splits file, then cancelled: nothing changes.
            DialogChrome.PromptAnswersForTests = new Queue<string?>(["https://bad/x", null]);
            Task<bool> bad = window.OpenSplitsFromUrlAsync();
            Dispatcher.UIThread.RunJobs();
            CloseErrorDialogs(window);
            Assert.False(await bad);
            Assert.False(await window.OpenSplitsFromUrlAsync());
            Assert.Equal("From The Web", window.Session.State.Run.GameName);
            Assert.Equal(3, asked.Count);
        }
        finally
        {
            DialogChrome.PromptAnswersForTests = null;
            MainWindow.DownloadFromUrl = download;
            window.Session.State.Run.HasChanged = false;
            window.CurrentLayout.HasChanged = false;
            window.Close();
            HeadlessUi.Delete(dir);
        }
    }

    [AvaloniaFact]
    public void Edit_History_Lists_Newest_First_And_Removes_The_Selected()
    {
        var dialog = new EditHistoryWindow(["/a.lsl", "", "/b.lsl", "/c.lsl"]);
        Assert.Equal(["/c.lsl", "/b.lsl", "/a.lsl"], dialog.Listed);
        dialog.SelectForTests("/b.lsl");
        dialog.RemoveSelected();
        Assert.Equal(["/c.lsl", "/a.lsl"], dialog.Listed);
        // The empty entry (the default layout) is kept, in its place.
        Assert.Equal(["/a.lsl", "", "/c.lsl"], dialog.History);
    }

    private static void CloseErrorDialogs(MainWindow window)
    {
        foreach (Avalonia.Controls.Window owned in window.OwnedWindows.ToList())
        {
            owned.Close();
        }

        Dispatcher.UIThread.RunJobs();
    }
}
