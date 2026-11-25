using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Basten, 11 Oct 2026: window position and size are saved exactly like Windows LiveSplit. They
/// live in the layout and reach disk only when the layout is saved; a changed layout is not saved
/// silently on quit but asked about (Yes / No / Cancel).
/// </summary>
[Collection(MainWindowCollection.Name)]
public class LayoutSavingTests
{
    [AvaloniaFact]
    public void Moving_And_Resizing_Writes_Neither_The_Layout_Nor_Settings_Geometry()
    {
        string dir = NewTempDir();
        string layoutPath = Path.Combine(dir, "timer.lsl");
        XmlLayoutSerializer.SaveFile(StandardLayoutFactory.Create(), layoutPath);
        string settingsPath = Path.Combine(dir, "settings.cfg");
        var settings = new AppSettings();
        settings.AddToRecentLayouts(layoutPath);
        XmlAppSettingsSerializer.SaveFile(settings, settingsPath);
        byte[] layoutBefore = File.ReadAllBytes(layoutPath);
        AppCli.Parse(["--settings", settingsPath]);

        var window = new MainWindow();
        window.Show();
        Pump();
        window.Position = new PixelPoint(300, 220);
        window.Width += 40;
        window.Height += 30;
        Pump();
        window.Close();
        Pump();

        Assert.False(window.IsVisible);
        Assert.Equal(layoutBefore, File.ReadAllBytes(layoutPath));
        Assert.DoesNotContain("<Window", File.ReadAllText(settingsPath), StringComparison.Ordinal);
        Delete(dir);
    }

    [AvaloniaFact]
    public void Changed_Layout_Asks_Before_Quitting_Cancel_Stays_And_No_Discards()
    {
        string dir = NewTempDir();
        AppCli.Parse(["--settings", Path.Combine(dir, "settings.cfg")]);
        var window = new MainWindow();
        window.Show();
        Pump();
        window.CurrentLayout.HasChanged = true;

        window.Close();
        Pump();
        Assert.True(window.IsVisible);
        Click(Prompt(window), "Cancel");
        Assert.True(window.IsVisible);

        window.Close();
        Pump();
        Click(Prompt(window), "No");
        Assert.False(window.IsVisible);
        Assert.Empty(Directory.GetFiles(dir, "*.lsl"));
        Delete(dir);
    }

    [AvaloniaFact]
    public void Unchanged_Layout_Quits_Without_Asking()
    {
        string dir = NewTempDir();
        AppCli.Parse(["--settings", Path.Combine(dir, "settings.cfg")]);
        var window = new MainWindow();
        window.Show();
        Pump();

        window.Close();
        Pump();

        Assert.False(window.IsVisible);
        Delete(dir);
    }

    private static Window Prompt(Window owner) => HeadlessUi.Prompt(owner, "Save Layout?");

    private static void Click(Window dialog, string label) => HeadlessUi.Click(dialog, label);

    private static void Pump() => HeadlessUi.Pump();

    private static string NewTempDir() => HeadlessUi.NewTempDir();

    private static void Delete(string dir) => HeadlessUi.Delete(dir);
}
