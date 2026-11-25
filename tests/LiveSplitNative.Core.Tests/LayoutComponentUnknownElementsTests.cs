using System.Text;
using System.Xml;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// A Windows layout's per-component &lt;FontOverrides&gt; are not used here (Basten 18 Oct: every
/// component follows the layout's fonts) but are kept as they were, so saving the layout does not
/// drop them for Windows.
/// </summary>
public class LayoutComponentUnknownElementsTests
{
    private const string Overrides =
        "<FontOverrides><OverrideTimerFont>False</OverrideTimerFont><OverrideTimesFont>False</OverrideTimesFont>"
        + "<OverrideTextFont>True</OverrideTextFont><TextFont>AAEAAAD/////</TextFont></FontOverrides>";

    [Fact]
    public void Other_Component_Elements_Are_Kept_After_The_Settings()
    {
        string xml = "<Layout version=\"1.6\"><Mode>Vertical</Mode><Settings /><Components>"
            + "<Component><Path>LiveSplit.Text.dll</Path><Settings><Text1>World Record</Text1></Settings>" + Overrides + "</Component>"
            + "<Component><Path>LiveSplit.SumOfBest.dll</Path><Settings /></Component>"
            + "</Components></Layout>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        Layout layout = XmlLayoutSerializer.Load(input);
        Assert.Equal([Overrides], layout.Components[0].UnknownOuterXml);
        Assert.Empty(layout.Components[1].UnknownOuterXml);
        Assert.Equal([Overrides], layout.Components[0].Clone().UnknownOuterXml);

        using var output = new MemoryStream();
        XmlLayoutSerializer.Save(layout, output);
        var saved = new XmlDocument();
        saved.LoadXml(Encoding.UTF8.GetString(output.ToArray()).TrimStart('﻿'));
        var first = (XmlElement)saved.SelectSingleNode("/Layout/Components/Component[1]")!;
        Assert.Equal(["Path", "Settings", "FontOverrides"], first.ChildNodes.OfType<XmlElement>().Select(e => e.Name));
        Assert.Equal(Overrides, first["FontOverrides"]!.OuterXml);
        Assert.Null(((XmlElement)saved.SelectSingleNode("/Layout/Components/Component[2]")!)["FontOverrides"]);
    }
}
