using System.Text;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// The Separator component (LiveSplit.Core SeparatorFactory / SeparatorComponent, 93a11b85):
/// "Other" in the Layout Editor's add menu, an empty path in the .lsl, 2 high or wide.
/// </summary>
public class SeparatorComponentTests
{
    [Fact]
    public void Separator_Is_In_The_Other_Group_Of_The_Add_Menu()
    {
        StandardLayoutFactory.BuiltinComponentInfo info =
            StandardLayoutFactory.BuiltinCatalog.Single(c => c.DisplayName == "Separator");
        Assert.Equal("", info.Path);
        Assert.Equal(StandardLayoutFactory.ComponentCategory.Other, info.Category);
        Assert.Equal("Shows a line to separate components.", info.Description);
        LayoutComponent component = StandardLayoutFactory.CreateBuiltin(info.Path);
        Assert.False(component.HasSettings);
        Assert.Equal(LayoutComponent.SeparatorName, component.DisplayName);
    }

    [Fact]
    public void Windows_Layout_Separators_Load_And_Save()
    {
        // Windows writes a separator as an empty Path with empty Settings. These used to be
        // dropped on load, so saving lost them.
        const string lsl = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Layout version="1.6.1">
              <Mode>Vertical</Mode>
              <Settings />
              <Components>
                <Component><Path>LiveSplit.Title.dll</Path><Settings /></Component>
                <Component><Path></Path><Settings /></Component>
                <Component><Path>LiveSplit.Timer.dll</Path><Settings /></Component>
              </Components>
            </Layout>
            """;
        Layout layout = XmlLayoutSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(lsl)));
        Assert.Equal(["LiveSplit.Title.dll", "", "LiveSplit.Timer.dll"], layout.Components.Select(c => c.Path).ToArray());

        using var saved = new MemoryStream();
        XmlLayoutSerializer.Save(layout, saved);
        saved.Position = 0;
        Layout again = XmlLayoutSerializer.Load(saved);
        Assert.Equal(StandardLayoutFactory.SeparatorPath, again.Components[1].Path);
    }

    [Fact]
    public void Separator_Takes_Two_Units()
    {
        var layout = new Layout { Settings = LayoutSettings.CreateStandard() };
        layout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.SeparatorPath));
        TimerComponentMetric metric = Assert.Single(TimerLayoutMetrics.FromLayout(layout, segmentCount: 1));
        Assert.Equal(2, metric.VerticalSize);
        Assert.Equal(2, metric.HorizontalSize);
    }
}
