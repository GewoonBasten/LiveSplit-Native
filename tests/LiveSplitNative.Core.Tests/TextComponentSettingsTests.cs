using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class TextComponentSettingsTests
{
    [Fact]
    public void TextSettings_RoundTrip()
    {
        var settings = new TextComponentSettings
        {
            Text1 = "Platform",
            Text2 = "PC",
            CustomVariable = true,
            Display2Rows = true,
            OverrideTextColor = true,
            BackgroundGradient = "Vertical",
        };
        TextComponentSettings again = TextComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.Equal("Platform", again.Text1);
        Assert.Equal("PC", again.Text2);
        Assert.True(again.CustomVariable);
        Assert.True(again.Display2Rows);
        Assert.True(again.OverrideTextColor);
        Assert.Equal("Vertical", again.BackgroundGradient);
    }

    [Fact]
    public void CreateBuiltin_Text_Has_Defaults_And_Catalog_Marks_Native()
    {
        LayoutComponent text = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.TextPath);
        TextComponentSettings settings = TextComponentSettings.FromInnerXml(text.SettingsInnerXml);
        Assert.Equal("Text", settings.Text1);
        Assert.False(settings.CustomVariable);

        StandardLayoutFactory.BuiltinComponentInfo info =
            StandardLayoutFactory.BuiltinCatalog.Single(c => c.Path == StandardLayoutFactory.TextPath);
        Assert.True(info.NativeRendered);
    }

    [Fact]
    public void Layout_With_Text_RoundTrips_Lsl()
    {
        Layout layout = StandardLayoutFactory.Create();
        layout.Components.Add(new LayoutComponent(
            StandardLayoutFactory.TextPath,
            new TextComponentSettings { Text1 = "A", Text2 = "B" }.ToInnerXml()));

        using var stream = new MemoryStream();
        XmlLayoutSerializer.Save(layout, stream);
        stream.Position = 0;
        Layout again = XmlLayoutSerializer.Load(stream);

        LayoutComponent text = again.Components.Single(c => c.Path == StandardLayoutFactory.TextPath);
        TextComponentSettings settings = TextComponentSettings.FromInnerXml(text.SettingsInnerXml);
        Assert.Equal("A", settings.Text1);
        Assert.Equal("B", settings.Text2);
    }

    [Fact]
    public void CustomVariableValue_Reads_Permanent_Then_Category()
    {
        var meta = new RunMetadata();
        meta.PermanentCustomVariables["Region"] = "EU";
        meta.VariableValueNames["Difficulty"] = "Normal";
        Assert.Equal("EU", meta.CustomVariableValue("Region"));
        Assert.Equal("Normal", meta.CustomVariableValue("Difficulty"));
        Assert.Null(meta.CustomVariableValue("Missing"));
    }
}
