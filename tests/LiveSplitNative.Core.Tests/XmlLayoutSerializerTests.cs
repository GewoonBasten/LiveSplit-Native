using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class XmlLayoutSerializerTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void StandardLayout_RoundTrip_Preserves_Components_And_Settings()
    {
        Layout original = StandardLayoutFactory.Create();
        original.Settings.AlwaysOnTop = true;
        original.Settings.Opacity = 0.85f;
        original.Settings.AllowResizing = false;
        original.VerticalWidth = 300;
        original.VerticalHeight = 400;

        using var ms = new MemoryStream();
        XmlLayoutSerializer.Save(original, ms);
        ms.Position = 0;
        Layout roundTrip = XmlLayoutSerializer.Load(ms);

        Assert.Equal(original.Mode, roundTrip.Mode);
        Assert.Equal(original.VerticalWidth, roundTrip.VerticalWidth);
        Assert.Equal(original.VerticalHeight, roundTrip.VerticalHeight);
        Assert.Equal(original.Components.Count, roundTrip.Components.Count);
        Assert.Equal(original.Components.Select(c => c.Path), roundTrip.Components.Select(c => c.Path));
        Assert.Equal(original.Settings.AlwaysOnTop, roundTrip.Settings.AlwaysOnTop);
        Assert.Equal(original.Settings.AllowResizing, roundTrip.Settings.AllowResizing);
        Assert.Equal(original.Settings.Opacity, roundTrip.Settings.Opacity, precision: 3);
        Assert.Equal(original.Settings.PersonalBestColor, roundTrip.Settings.PersonalBestColor);
        Assert.Equal(original.Settings.AheadGainingTimeColor, roundTrip.Settings.AheadGainingTimeColor);
        Assert.Equal(original.Settings.NotRunningColor, roundTrip.Settings.NotRunningColor);
    }

    [Fact]
    public void DefaultLayoutFixture_Loads_Title_Splits_Timer_PreviousSegment()
    {
        string path = Fixture("default-layout.lsl");
        Assert.True(File.Exists(path), path);

        Layout layout = XmlLayoutSerializer.LoadFile(path);
        Assert.Equal(LayoutMode.Vertical, layout.Mode);
        Assert.True(layout.Settings.AlwaysOnTop);
        Assert.Equal(4, layout.Components.Count);
        Assert.Equal(StandardLayoutFactory.TitlePath, layout.Components[0].Path);
        Assert.Equal(StandardLayoutFactory.SplitsPath, layout.Components[1].Path);
        Assert.Equal(StandardLayoutFactory.TimerPath, layout.Components[2].Path);
        Assert.Equal(StandardLayoutFactory.PreviousSegmentPath, layout.Components[3].Path);
        Assert.False(string.IsNullOrEmpty(layout.Settings.TimerFontXml));
        Assert.False(string.IsNullOrEmpty(layout.Components[0].SettingsInnerXml));
    }

    [Fact]
    public void DefaultLayoutFixture_RoundTrip_Preserves_FontBlobs_And_ComponentSettings()
    {
        string path = Fixture("default-layout.lsl");
        Layout original = XmlLayoutSerializer.LoadFile(path);
        using var ms = new MemoryStream();
        XmlLayoutSerializer.Save(original, ms);
        ms.Position = 0;
        Layout roundTrip = XmlLayoutSerializer.Load(ms);

        Assert.Equal(original.Settings.TimerFontXml, roundTrip.Settings.TimerFontXml);
        Assert.Equal(original.Settings.TimesFontXml, roundTrip.Settings.TimesFontXml);
        Assert.Equal(original.Settings.TextFontXml, roundTrip.Settings.TextFontXml);
        Assert.Equal(original.Components.Count, roundTrip.Components.Count);
        for (int i = 0; i < original.Components.Count; i++)
        {
            Assert.Equal(original.Components[i].Path, roundTrip.Components[i].Path);
            Assert.Equal(original.Components[i].SettingsInnerXml, roundTrip.Components[i].SettingsInnerXml);
        }
    }
}
