using System.Text;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// Opening splits as Windows LiveSplit does (livesplit-core's parser + StandardFormatsRunFactory).
/// The corpus tests in LiveSplitNative.Upstream.Tests compare whole files against the real thing;
/// these pin the rules that need no native library.
/// </summary>
public class LssParserTests
{
    private static Run Load(string version, string segments, string extra = "") =>
        XmlRunSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes($"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Run version="{version}">
              <GameIcon />
              <GameName> Game </GameName>
              <CategoryName>Any%</CategoryName>
              <Offset>00:00:00</Offset>
              <AttemptCount>2</AttemptCount>
              {extra}
              <Segments>{segments}</Segments>
            </Run>
            """)));

    private static string Segment(string name, string inner = "") => $"<Segment><Name>{name}</Name>{inner}</Segment>";

    [Fact]
    public void Legacy_Subsplits_Get_A_Space_After_The_Group_Name()
    {
        Run run = Load("1.7.0", Segment("-1 | Windmill") + Segment("{Bianco Hills}2 | Peter") + Segment("{Lone}3"));
        Assert.Equal(["-1 | Windmill", "{Bianco Hills} 2 | Peter", "{Lone}3"], run.Segments.Select(s => s.Name));
    }

    [Fact]
    public void Empty_Group_Name_Is_Dropped_And_A_Last_Dash_Stays()
    {
        Run run = Load("1.7.0", Segment("-a") + Segment("{}  b") + Segment("-c"));
        Assert.Equal(["-a", "b", "-c"], run.Segments.Select(s => s.Name));
    }

    [Fact]
    public void Native_Segment_Groups_Become_Subsplit_Names_Clamped_To_The_Segments()
    {
        Run run = Load("1.8.1", Segment("1") + Segment("2") + Segment("3"));
        Assert.Equal(["1", "2", "3"], run.Segments.Select(s => s.Name));

        string groups = """<SegmentGroups><SegmentGroup start="1" end="9"><Name>Late</Name></SegmentGroup><SegmentGroup start="0" end="2"><Name>Early</Name></SegmentGroup></SegmentGroups>""";
        Run grouped = XmlRunSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(
            $"""<Run version="1.8.1"><GameIcon /><GameName /><CategoryName /><Offset>00:00:00</Offset><AttemptCount>0</AttemptCount><Segments>{Segment("1") + Segment("2") + Segment("3")}</Segments>{groups}</Run>""")));
        // Sorted by start; the later group starts where the first ends and ends at the last segment.
        Assert.Equal(["-1", "{Early} 2", "{Late} 3"], grouped.Segments.Select(s => s.Name));
        Assert.DoesNotContain(grouped.UnknownRootOuterXml, x => x.Contains("SegmentGroups", StringComparison.Ordinal));
    }

    [Fact]
    public void Text_Is_Trimmed_And_Generators_And_Race_Comparisons_Are_Handled()
    {
        Run run = Load("1.7.0", Segment(
            "  A  ",
            """<SplitTimes><SplitTime name="Personal Best"><RealTime>00:01:00</RealTime></SplitTime><SplitTime name="Best Segments"><RealTime>00:00:50</RealTime></SplitTime><SplitTime name="Rival"><RealTime>00:00:55</RealTime></SplitTime></SplitTimes>"""));
        Assert.Equal(("Game", "A"), (run.GameName, run[0].Name));
        Assert.Equal(["Personal Best", "Rival"], run.CustomComparisons);
        Assert.False(run[0].Comparisons.ContainsKey("Best Segments"));

        Assert.Throws<InvalidDataException>(() => Load("1.7.0", Segment(
            "A", """<SplitTimes><SplitTime name="[Race] x"><RealTime>00:01:00</RealTime></SplitTime></SplitTimes>""")));
    }

    [Fact]
    public void Files_Windows_Cannot_Open_Throw()
    {
        // A required element missing, no segments, a bad time, a bad bool.
        Assert.Throws<InvalidDataException>(() => XmlRunSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(
            "<Run><GameName /><CategoryName /><Offset>00:00:00</Offset><AttemptCount>0</AttemptCount><Segments><Segment><Name>a</Name></Segment></Segments></Run>"))));
        Assert.Throws<InvalidDataException>(() => Load("1.7.0", ""));
        Assert.Throws<InvalidDataException>(() => Load("1.7.0", Segment("a", "<BestSegmentTime><RealTime>00:61:00</RealTime></BestSegmentTime>")));
        Assert.Throws<InvalidDataException>(() => Load("1.7.0", Segment("a"), """<Metadata><Run id="" /><Platform usesEmulator="yes" /></Metadata>"""));
    }

    [Fact]
    public void Attempts_From_Run_History_And_An_End_Before_The_Start_Is_Dropped()
    {
        Run old = Load("1.4", Segment("a"), """<RunHistory><Time id="1">00:01:00</Time><Time id="2"></Time></RunHistory>""");
        Assert.Equal(
            new (int, TimeSpan?)[] { (1, TimeSpan.FromMinutes(1)), (2, null) },
            old.AttemptHistory.Select(a => (a.Index, a.Time.RealTime)));

        Run run = Load("1.7.0", Segment("a"), """
            <AttemptHistory>
              <Attempt id="1" started="10/09/2026 14:31:30" isStartedSynced="True" ended="10/09/2026 14:31:00" isEndedSynced="False" />
              <Attempt id="2" started="10/09/2026 14:31:30" isStartedSynced="False" ended="10/09/2026 14:32:00" isEndedSynced="False" />
            </AttemptHistory>
            """);
        Assert.Null(run.AttemptHistory[0].Ended);
        Assert.Equal(new DateTime(2026, 10, 9, 14, 31, 30, DateTimeKind.Utc), run.AttemptHistory[0].Started!.Value.Time);
        Assert.True(run.AttemptHistory[0].Started!.Value.SyncedWithAtomicClock);
        Assert.Equal(TimeSpan.FromSeconds(30), run.AttemptHistory[1].Duration);
    }

    [Fact]
    public void Segment_History_Keeps_The_First_Time_Of_An_Id()
    {
        Run run = Load("1.7.0", Segment("a", """<SegmentHistory><Time id="3"><RealTime>00:00:01</RealTime></Time><Time id="3"><RealTime>00:00:02</RealTime></Time></SegmentHistory>"""));
        Assert.Equal(TimeSpan.FromSeconds(1), run[0].SegmentHistory[3].RealTime);
    }

    [Theory]
    [InlineData("00:10:19.673999967", 6196739999)]
    [InlineData("1:00", 600000000)]
    [InlineData("75", 750000000)]
    [InlineData("100:00:00", 3600000000000)]
    [InlineData("-00:00:01.5", -15000000)]
    [InlineData("1.23:34:56.789", 1712967890000)]
    [InlineData("-1.23:34:56.789", -1712967890000)]
    [InlineData("00:00:00.1234567891", 1234567)]
    public void Times_Parse_As_LiveSplit_Core(string text, long ticks) =>
        Assert.Equal(ticks, LssTime.Parse(text).Ticks);

    [Theory]
    [InlineData("1:2:3:4")]
    [InlineData("00:60")]
    [InlineData("00: 01")]
    [InlineData("")]
    [InlineData("1.-23:34:56")]
    public void Bad_Times_Are_Rejected(string text) =>
        Assert.Throws<InvalidDataException>(() => LssTime.Parse(text));
}
