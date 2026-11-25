using System.Text;
using System.Xml;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// LiveSplitNative's Component Spacing (Basten 18 Oct): single-line components are 25 + spacing
/// high (6 = Windows' 31); saved in the layout's Settings only when changed, so a layout stays as
/// Windows writes it.
/// </summary>
public class ComponentSpacingTests
{
    private static XmlDocument Save(Layout layout)
    {
        using var stream = new MemoryStream();
        XmlLayoutSerializer.Save(layout, stream);
        var document = new XmlDocument();
        document.LoadXml(Encoding.UTF8.GetString(stream.ToArray()).TrimStart('﻿'));
        return document;
    }

    private static Layout Load(XmlDocument document)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(document.OuterXml));
        return XmlLayoutSerializer.Load(stream);
    }

    [Fact]
    public void The_Default_Is_Windows_Height_And_Is_Not_Written()
    {
        Layout layout = StandardLayoutFactory.Create();
        Assert.Equal(6f, layout.Settings.ComponentSpacing);
        XmlDocument saved = Save(layout);
        Assert.Null(saved.SelectSingleNode("/Layout/Settings/ComponentSpacing"));
        Assert.Equal(6f, Load(saved).Settings.ComponentSpacing);
    }

    [Fact]
    public void A_Changed_Spacing_Is_Saved_And_Read_Back()
    {
        Layout layout = StandardLayoutFactory.Create();
        layout.Settings.ComponentSpacing = 1.5f;
        XmlDocument saved = Save(layout);
        Assert.Equal("1.5", saved.SelectSingleNode("/Layout/Settings/ComponentSpacing")!.InnerText);
        Assert.Equal(1.5f, Load(saved).Settings.ComponentSpacing);
        Assert.Equal(1.5f, layout.Settings.Clone().ComponentSpacing);
    }

    [Theory]
    [InlineData(StandardLayoutFactory.PreviousSegmentPath)]
    [InlineData(StandardLayoutFactory.SumOfBestPath)]
    [InlineData(StandardLayoutFactory.PossibleTimeSavePath)]
    [InlineData(StandardLayoutFactory.TextPath)]
    public void Single_Line_Components_Are_25_Plus_The_Spacing_High(string path)
    {
        Layout layout = StandardLayoutFactory.Create();
        layout.Components.Clear();
        layout.Components.Add(StandardLayoutFactory.CreateBuiltin(path));
        Assert.Equal(31, TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize, 6);

        layout.Settings.ComponentSpacing = 0;
        Assert.Equal(25, TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize, 6);
        layout.Settings.ComponentSpacing = 10.5f;
        Assert.Equal(35.5, TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize, 6);
    }

    [Fact]
    public void Other_Components_Keep_Their_Height()
    {
        Layout layout = StandardLayoutFactory.Create();
        double[] before = TimerLayoutMetrics.FromLayout(layout, 3)
            .Where(m => m.Path != StandardLayoutFactory.PreviousSegmentPath)
            .Select(m => m.VerticalSize).ToArray();
        layout.Settings.ComponentSpacing = 0;
        double[] after = TimerLayoutMetrics.FromLayout(layout, 3)
            .Where(m => m.Path != StandardLayoutFactory.PreviousSegmentPath)
            .Select(m => m.VerticalSize).ToArray();
        Assert.Equal(before, after);
    }
}
