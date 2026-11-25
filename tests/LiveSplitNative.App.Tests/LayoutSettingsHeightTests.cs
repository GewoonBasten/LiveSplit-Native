using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Layout Settings, Layout tab (Basten, 13 Oct: content cut off from the Colors group on): when
/// the screen is too short for the whole tab, the window stops at its maximum height and the
/// tab scrolls, so nothing is cut off.
/// </summary>
public class LayoutSettingsHeightTests
{
    [AvaloniaFact]
    public void Layout_Tab_Scrolls_When_The_Screen_Is_Too_Short()
    {
        var dialog = new LayoutSettingsWindow(StandardLayoutFactory.Create()) { ScreenHeightForTests = 500 };
        dialog.Show();
        HeadlessUi.Pump();
        dialog.RefitHeight();
        HeadlessUi.Pump();

        var page = (ScrollViewer)((TabItem)dialog.SettingsTabs.Items[0]!).Content!;
        double pageTop = page.TranslatePoint(new Point(0, 0), dialog.SettingsTabs)!.Value.Y;
        double room = dialog.SettingsTabs.Bounds.Height - pageTop
            - dialog.SettingsTabs.Padding.Bottom - dialog.SettingsTabs.BorderThickness.Bottom;
        Assert.True(dialog.Bounds.Height <= dialog.MaxHeight + 1, $"window {dialog.Bounds.Height} > max {dialog.MaxHeight}");
        Assert.True(page.Bounds.Height <= room + 1, $"page {page.Bounds.Height} taller than its room {room}: cut off");
        Assert.Equal(ScrollBarVisibility.Auto, page.VerticalScrollBarVisibility);
        Assert.True(page.Extent.Height > page.Viewport.Height, $"extent {page.Extent.Height}, viewport {page.Viewport.Height}");
        dialog.Close();
    }

    [AvaloniaFact]
    public void Layout_Tab_Fits_Without_Scrolling_On_A_Tall_Screen()
    {
        var dialog = new LayoutSettingsWindow(StandardLayoutFactory.Create()) { ScreenHeightForTests = 2000 };
        dialog.Show();
        HeadlessUi.Pump();
        dialog.RefitHeight();
        HeadlessUi.Pump();

        var page = (ScrollViewer)((TabItem)dialog.SettingsTabs.Items[0]!).Content!;
        double pageTop = page.TranslatePoint(new Point(0, 0), dialog.SettingsTabs)!.Value.Y;
        double room = dialog.SettingsTabs.Bounds.Height - pageTop
            - dialog.SettingsTabs.Padding.Bottom - dialog.SettingsTabs.BorderThickness.Bottom;
        Assert.True(page.Bounds.Height <= room + 1, $"page {page.Bounds.Height} taller than its room {room}");
        dialog.Close();
    }
}
