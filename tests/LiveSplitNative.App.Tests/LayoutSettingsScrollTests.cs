using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Layout Settings (Basten 18 Oct: "changing a setting somehow moves the scroll in the settings
/// menu"): changing a dropdown or a checkbox in a component tab keeps the tab where it was
/// scrolled to.
/// </summary>
public class LayoutSettingsScrollTests
{
    [AvaloniaFact]
    public void Changing_A_Setting_Keeps_The_Tab_Scrolled_Where_It_Was()
    {
        Layout layout = StandardLayoutFactory.Create();
        int splits = layout.Components.FindIndex(c => c.Path == StandardLayoutFactory.SplitsPath);
        layout.Components[splits] = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.SubsplitsPath);
        var dialog = new LayoutSettingsWindow(layout) { ScreenHeightForTests = 500 };
        dialog.Show();
        HeadlessUi.Pump();
        TabItem tab = dialog.SettingsTabs.Items.OfType<TabItem>()
            .First(t => t.Content is ScrollViewer { Content: ComponentSettingsEditor e } && e.TabHeader == "Subsplits");
        dialog.SettingsTabs.SelectedItem = tab;
        HeadlessUi.Pump();
        dialog.RefitHeight();
        HeadlessUi.Pump();

        var page = (ScrollViewer)tab.Content!;
        Assert.True(page.Extent.Height > page.Viewport.Height + 300, $"extent {page.Extent.Height}, viewport {page.Viewport.Height}");
        page.Offset = new Vector(0, 300);
        HeadlessUi.Pump();
        Assert.Equal(300, page.Offset.Y, 0);

        var editor = (ComponentSettingsEditor)page.Content!;
        ComboBox combo = editor.GetLogicalDescendants().OfType<ComboBox>().First(c => c.IsVisible && c.ItemCount > 1);
        combo.SelectedIndex = combo.SelectedIndex == 0 ? 1 : 0;
        HeadlessUi.Pump();
        Assert.Equal(300, page.Offset.Y, 0);

        CheckBox check = editor.GetLogicalDescendants().OfType<CheckBox>().First();
        check.IsChecked = check.IsChecked != true;
        HeadlessUi.Pump();
        Assert.Equal(300, page.Offset.Y, 0);
        dialog.Close();
    }
}
