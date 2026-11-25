using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Layout Settings → Layout → Fonts (LayoutSettingsControl): each font's name and style, Choose...
/// opens the font dialog, every change previews on the timer, Cancel restores, OK keeps
/// (Basten, 12 Oct 2026: "where can I change the size of the split text?").
/// </summary>
[Collection(MainWindowCollection.Name)]
public class LayoutFontsTests
{
    [AvaloniaFact]
    public async Task Text_Font_Size_Previews_Live_Cancel_Restores_And_Ok_Keeps()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lsn-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string splitsPath = Path.Combine(dir, "run.lss");
        var run = new Run { GameName = "Fonts", CategoryName = "Any%" };
        run.AddSegment("One");
        run.AddSegment("Two");
        XmlRunSerializer.SaveFile(run, splitsPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentSplits(splitsPath, run, TimingMethod.RealTime, AppSettings.DefaultHotkeyProfileName);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        AppCli.Parse(["--settings", settingsPath]);

        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        float before = window.CurrentLayout.Settings.TextFontSizePx;
        double heightBefore = window.Height;
        double overallBefore = window.LayoutSnapshotForTests!.OverallSize;

        Task edit = window.EditLayoutForTestsAsync();
        Dispatcher.UIThread.RunJobs();
        LayoutEditorWindow editor = window.OwnedWindows.OfType<LayoutEditorWindow>().Single();
        Click(editor, "Layout Settings...");
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
        LayoutSettingsWindow layoutSettings = editor.OwnedWindows.OfType<LayoutSettingsWindow>().Single();
        Assert.Contains(layoutSettings.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Segoe UI Regular");
        Assert.Contains(layoutSettings.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Segoe UI Bold");

        // Choose..., a bigger size: the split names grow on the timer at once; Cancel undoes it.
        FontDialogWindow dialog = await OpenFontDialog(layoutSettings, LayoutSettingsWindow.LayoutFontSlot.Text);
        SizeBox(dialog).Text = "22";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(22f, window.CurrentLayout.Settings.TextFontSizePx);
        // SplitComponent.DrawVertical: a split row is Split Height + 25 whatever the font; the
        // Title (1.7 Text font lines) and the information rows' paddings follow the font, and the
        // window grows with the layout, keeping its scale.
        double overallAfter = window.LayoutSnapshotForTests!.OverallSize;
        Assert.True(overallAfter > overallBefore);
        Assert.Equal(overallAfter / overallBefore, window.Height / heightBefore, 2);
        Click(dialog, "Cancel");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(before, window.CurrentLayout.Settings.TextFontSizePx);

        // Again with Italic, then OK in all three dialogs: it stays.
        dialog = await OpenFontDialog(layoutSettings, LayoutSettingsWindow.LayoutFontSlot.Text);
        SizeBox(dialog).Text = "20";
        dialog.GetLogicalDescendants().OfType<CheckBox>().First(c => Equals(c.Content, "Italic")).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Click(dialog, "OK");
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(layoutSettings.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text is "Segoe UI Italic");
        Click(layoutSettings, "OK");
        Dispatcher.UIThread.RunJobs();
        Click(editor, "OK");
        await edit;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(20f, window.CurrentLayout.Settings.TextFontSizePx);
        Assert.True(window.CurrentLayout.Settings.TextFontItalic);

        window.Session.State.Run.HasChanged = false;
        window.CurrentLayout.HasChanged = false;
        window.Close();
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }

    private static async Task<FontDialogWindow> OpenFontDialog(LayoutSettingsWindow layoutSettings, LayoutSettingsWindow.LayoutFontSlot slot)
    {
        _ = layoutSettings.ChooseFontAsync(slot);
        Dispatcher.UIThread.RunJobs();
        await Task.Yield();
        Dispatcher.UIThread.RunJobs();
        return layoutSettings.OwnedWindows.OfType<FontDialogWindow>().Single();
    }

    private static TextBox SizeBox(FontDialogWindow dialog) =>
        dialog.GetLogicalDescendants().OfType<TextBox>().ElementAt(1);

    private static void Click(Window window, string content)
    {
        Button button = window.GetLogicalDescendants().OfType<Button>().First(b => Equals(b.Content, content));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
}
