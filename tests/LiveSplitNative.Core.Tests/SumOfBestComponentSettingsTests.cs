using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class SumOfBestComponentSettingsTests
{
    [Fact]
    public void SumOfBest_And_BlankSpace_Settings_RoundTrip()
    {
        var sob = new SumOfBestComponentSettings
        {
            Accuracy = TimeAccuracy.Tenths,
            Display2Rows = true,
            OverrideTimeColor = true,
            BackgroundGradient = "Vertical",
        };
        SumOfBestComponentSettings sob2 = SumOfBestComponentSettings.FromInnerXml(sob.ToInnerXml());
        Assert.Equal(TimeAccuracy.Tenths, sob2.Accuracy);
        Assert.True(sob2.Display2Rows);
        Assert.True(sob2.OverrideTimeColor);
        Assert.Equal("Vertical", sob2.BackgroundGradient);

        var blank = new BlankSpaceComponentSettings
        {
            SpaceHeight = 12,
            SpaceWidth = 40,
            BackgroundGradient = "Horizontal",
        };
        BlankSpaceComponentSettings blank2 = BlankSpaceComponentSettings.FromInnerXml(blank.ToInnerXml());
        Assert.Equal(12, blank2.SpaceHeight);
        Assert.Equal(40, blank2.SpaceWidth);
        Assert.Equal("Horizontal", blank2.BackgroundGradient);
    }

    [Fact]
    public void Layout_With_SoB_And_BlankSpace_RoundTrips_Lsl()
    {
        Layout layout = StandardLayoutFactory.Create();
        layout.Components.Insert(2, new LayoutComponent(
            StandardLayoutFactory.BlankSpacePath,
            new BlankSpaceComponentSettings { SpaceHeight = 10 }.ToInnerXml()));
        layout.Components.Insert(layout.Components.Count - 1, new LayoutComponent(
            StandardLayoutFactory.SumOfBestPath,
            new SumOfBestComponentSettings { Accuracy = TimeAccuracy.Hundredths }.ToInnerXml()));

        using var stream = new MemoryStream();
        XmlLayoutSerializer.Save(layout, stream);
        stream.Position = 0;
        Layout again = XmlLayoutSerializer.Load(stream);

        Assert.Contains(again.Components, c => c.Path == StandardLayoutFactory.SumOfBestPath);
        Assert.Contains(again.Components, c => c.Path == StandardLayoutFactory.BlankSpacePath);
        SumOfBestComponentSettings sob = SumOfBestComponentSettings.FromInnerXml(
            again.Components.First(c => c.Path == StandardLayoutFactory.SumOfBestPath).SettingsInnerXml);
        Assert.Equal(TimeAccuracy.Hundredths, sob.Accuracy);
        BlankSpaceComponentSettings blank = BlankSpaceComponentSettings.FromInnerXml(
            again.Components.First(c => c.Path == StandardLayoutFactory.BlankSpacePath).SettingsInnerXml);
        Assert.Equal(10, blank.SpaceHeight);
    }
}
