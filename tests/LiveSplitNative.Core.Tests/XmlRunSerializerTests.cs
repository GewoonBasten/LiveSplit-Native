using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.Core.Tests;

public class XmlRunSerializerTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Theory]
    [InlineData("small-representative.lss")]
    [InlineData("livesplit1.6.lss")]
    [InlineData("livesplit1.5.lss")]
    public void RoundTrip_Preserves_Core_Fields(string fixtureName)
    {
        string path = Fixture(fixtureName);
        Assert.True(File.Exists(path), path);

        Run original = XmlRunSerializer.LoadFile(path);
        using var ms = new MemoryStream();
        XmlRunSerializer.Save(original, ms);
        ms.Position = 0;
        Run roundTrip = XmlRunSerializer.Load(ms);

        Assert.Equal(original.GameName, roundTrip.GameName);
        Assert.Equal(original.CategoryName, roundTrip.CategoryName);
        Assert.Equal(original.LayoutPath, roundTrip.LayoutPath);
        Assert.Equal(original.Offset, roundTrip.Offset);
        Assert.Equal(original.AttemptCount, roundTrip.AttemptCount);
        Assert.Equal(original.Count, roundTrip.Count);
        Assert.Equal(original.AttemptHistory.Count, roundTrip.AttemptHistory.Count);
        Assert.Equal(original.Metadata.RunId, roundTrip.Metadata.RunId);
        Assert.Equal(original.Metadata.PlatformName, roundTrip.Metadata.PlatformName);
        Assert.Equal(original.Metadata.UsesEmulator, roundTrip.Metadata.UsesEmulator);
        Assert.Equal(original.Metadata.RegionName, roundTrip.Metadata.RegionName);
        Assert.Equal(original.Metadata.VariableValueNames, roundTrip.Metadata.VariableValueNames);
        Assert.Equal(original.Metadata.PermanentCustomVariables, roundTrip.Metadata.PermanentCustomVariables);
        Assert.Equal(original.CustomComparisons, roundTrip.CustomComparisons);
        // Guard against null↔null false greens on plain-text (version < 1.4.1) fixtures.
        if (fixtureName is "livesplit1.5.lss" or "small-representative.lss" or "livesplit1.6.lss")
        {
            Assert.Contains(
                original.Segments,
                s => s.PersonalBestSplitTime.RealTime.HasValue || s.PersonalBestSplitTime.GameTime.HasValue);
        }

        for (int i = 0; i < original.Count; i++)
        {
            Segment a = original[i];
            Segment b = roundTrip[i];
            Assert.Equal(a.Name, b.Name);
            Assert.Equal(a.BestSegmentTime.RealTime, b.BestSegmentTime.RealTime);
            Assert.Equal(a.BestSegmentTime.GameTime, b.BestSegmentTime.GameTime);
            Assert.Equal(a.SegmentHistory.Count, b.SegmentHistory.Count);
            foreach (string comparison in original.CustomComparisons)
            {
                a.Comparisons.TryGetValue(comparison, out SplitTime at);
                b.Comparisons.TryGetValue(comparison, out SplitTime bt);
                Assert.Equal(at.RealTime, bt.RealTime);
                Assert.Equal(at.GameTime, bt.GameTime);
            }

            foreach ((int id, SplitTime time) in a.SegmentHistory)
            {
                Assert.True(b.SegmentHistory.ContainsKey(id));
                Assert.Equal(time.RealTime, b.SegmentHistory[id].RealTime);
                Assert.Equal(time.GameTime, b.SegmentHistory[id].GameTime);
            }
        }

        for (int i = 0; i < original.AttemptHistory.Count; i++)
        {
            Attempt a = original.AttemptHistory[i];
            Attempt b = roundTrip.AttemptHistory[i];
            Assert.Equal(a.Index, b.Index);
            Assert.Equal(a.Time.RealTime, b.Time.RealTime);
            Assert.Equal(a.Time.GameTime, b.Time.GameTime);
            Assert.Equal(a.PauseTime, b.PauseTime);
            Assert.Equal(a.Started?.SyncedWithAtomicClock, b.Started?.SyncedWithAtomicClock);
            Assert.Equal(a.Ended?.SyncedWithAtomicClock, b.Ended?.SyncedWithAtomicClock);
        }
    }

    [Fact]
    public void Speedrun_Com_Variables_From_LiveSplit_One_Are_Read()
    {
        // Basten's splits (written by livesplit-core): <SpeedrunComVariables>, not <Variables>.
        Run run = XmlRunSerializer.LoadFile(Fixture("sms-any-gcn-no-ace-9-full.lss"));
        Assert.Equal("GCN", run.Metadata.VariableValueNames["Game Version"]);
        Assert.Equal("No ACE", run.Metadata.VariableValueNames["Arbitrary Code Execution"]);
        Assert.Equal(("Wii", "EUR / PAL", false), (run.Metadata.PlatformName, run.Metadata.RegionName, run.Metadata.UsesEmulator));
    }

    [Fact]
    public void SmallFixture_Has_Expected_Shape()
    {
        Run run = XmlRunSerializer.LoadFile(Fixture("small-representative.lss"));
        Assert.Equal("Test Game", run.GameName);
        Assert.Equal("Any%", run.CategoryName);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), run.Offset);
        Assert.Equal(3, run.AttemptCount);
        Assert.Equal(2, run.Count);
        Assert.Equal(2, run.AttemptHistory.Count);
        Assert.Contains("Rival", run.CustomComparisons);
        Assert.Equal("NES", run.Metadata.PlatformName);
        Assert.True(run.Metadata.UsesEmulator);
        Assert.Equal("Normal", run.Metadata.VariableValueNames["Difficulty"]);
        Assert.Equal("yes", run.Metadata.PermanentCustomVariables["PermFlag"]);
        Assert.Equal(TimeSpan.FromSeconds(60), run[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(50), run[0].BestSegmentTime.RealTime);
        Assert.Equal(2, run[0].SegmentHistory.Count);
    }

    [Fact]
    public void RoundTrip_Does_Not_Drop_Xml_Nodes()
    {
        Run run = XmlRunSerializer.LoadFile(Fixture("small-representative.lss"));
        using var ms = new MemoryStream();
        XmlRunSerializer.Save(run, ms);
        ms.Position = 0;
        var doc = new XmlDocument();
        doc.Load(ms);
        XmlElement root = doc.DocumentElement!;
        Assert.Equal("1.7.0", root.GetAttribute("version"));
        Assert.NotNull(root["GameIcon"]);
        Assert.NotNull(root["Metadata"]);
        Assert.NotNull(root["AttemptHistory"]);
        Assert.NotNull(root["Segments"]);
        Assert.NotNull(root["AutoSplitterSettings"]);
        Assert.Equal(2, root["Segments"]!.SelectNodes("Segment")!.Count);
        // 2 segments × 2 comparisons (Personal Best + Rival)
        Assert.Equal(4, root["Segments"]!.SelectNodes("Segment/SplitTimes/SplitTime")!.Count);
    }
}
