using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.Core.Tests;

public class XmlUnknownElementTests
{
    [Fact]
    public void RoundTrip_Preserves_Unknown_Root_And_Segment_Elements()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Run version="1.7.0">
              <GameIcon />
              <GameName>X</GameName>
              <CategoryName>Y</CategoryName>
              <LayoutPath />
              <Metadata>
                <Run id="" />
                <Platform usesEmulator="False"></Platform>
                <Region></Region>
                <Variables />
                <CustomVariables />
              </Metadata>
              <Offset>00:00:00</Offset>
              <AttemptCount>0</AttemptCount>
              <AttemptHistory />
              <Segments>
                <Segment>
                  <Name>Only</Name>
                  <Icon />
                  <SplitTimes>
                    <SplitTime name="Personal Best">
                      <RealTime>00:01:00</RealTime>
                    </SplitTime>
                  </SplitTimes>
                  <BestSegmentTime>
                    <RealTime>00:01:00</RealTime>
                  </BestSegmentTime>
                  <SegmentHistory />
                  <FutureProof attr="1">keep-me</FutureProof>
                </Segment>
              </Segments>
              <AutoSplitterSettings />
              <ExperimentalPlugin>payload</ExperimentalPlugin>
            </Run>
            """;

        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        Run run = XmlRunSerializer.Load(input);
        Assert.Contains(run.UnknownRootOuterXml, x => x.Contains("ExperimentalPlugin", StringComparison.Ordinal));
        Assert.Contains(run[0].UnknownOuterXml, x => x.Contains("FutureProof", StringComparison.Ordinal));

        using var output = new MemoryStream();
        XmlRunSerializer.Save(run, output);
        output.Position = 0;
        var doc = new XmlDocument();
        doc.Load(output);
        Assert.NotNull(doc.SelectSingleNode("/Run/ExperimentalPlugin"));
        Assert.Equal("payload", doc.SelectSingleNode("/Run/ExperimentalPlugin")!.InnerText);
        Assert.NotNull(doc.SelectSingleNode("/Run/Segments/Segment/FutureProof"));
        Assert.Equal("1", ((XmlElement)doc.SelectSingleNode("/Run/Segments/Segment/FutureProof")!).GetAttribute("attr"));
    }

    [Fact]
    public void Save_Writes_Version_1_7_0_As_Windows_XMLRunSaver()
    {
        // The file's own version is read (it decides how livesplit-core parses it), but
        // XMLRunSaver always writes 1.7.0, so subsplit names are read back as groups.
        Run run = XmlRunSerializer.LoadFile(Path.Combine(AppContext.BaseDirectory, "fixtures", "livesplit1.6.lss"));
        Assert.Equal("1.6.0", run.Version);
        using var ms = new MemoryStream();
        XmlRunSerializer.Save(run, ms);
        ms.Position = 0;
        var doc = new XmlDocument();
        doc.Load(ms);
        Assert.Equal("1.7.0", doc.DocumentElement!.GetAttribute("version"));
    }
}
