using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.Core.Tests;

public class RunEditorDraftTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Livesplit13_PlainText_Loads_PersonalBest_And_Custom()
    {
        // Fixture filename says 1.5; file is Run version="1.3" with time_old plain text.
        Run run = XmlRunSerializer.LoadFile(Fixture("livesplit1.5.lss"));
        Assert.Contains(Run.PersonalBestComparisonName, run.CustomComparisons);
        Assert.Contains("World Record", run.CustomComparisons);
        Assert.Equal(TimeSpan.Parse("00:01:39.3900000"), run[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.Parse("00:01:37"), run[0].Comparisons["World Record"].RealTime);
        Assert.Equal(TimeSpan.Parse("00:01:36.6011740"), run[0].BestSegmentTime.RealTime);
        Assert.True(run[0].SegmentHistory.Count > 0);
        Assert.NotNull(run[0].SegmentHistory.Values.First().RealTime);
    }

    [Fact]
    public void SmallFixture_Draft_RoundTrip_Keeps_Pb_Custom_History_And_GameTime()
    {
        Run original = XmlRunSerializer.LoadFile(Fixture("small-representative.lss"));
        Assert.Equal(TimeSpan.FromSeconds(60), original[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(55), original[0].PersonalBestSplitTime.GameTime);
        Assert.Equal(2, original[0].SegmentHistory.Count);

        RunEditorDraft draft = RunEditorDraft.FromRun(original);
        // Simulate Splits Editor OK with no edits.
        Run committed = draft.ToRun(preserveAttemptHistory: true);

        Assert.Equal(original.CustomComparisons, committed.CustomComparisons);
        Assert.Equal(original[0].PersonalBestSplitTime.RealTime, committed[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(original[0].PersonalBestSplitTime.GameTime, committed[0].PersonalBestSplitTime.GameTime);
        Assert.Equal(original[0].Comparisons["Rival"].RealTime, committed[0].Comparisons["Rival"].RealTime);
        Assert.Equal(original[0].BestSegmentTime.RealTime, committed[0].BestSegmentTime.RealTime);
        Assert.Equal(original[0].SegmentHistory.Count, committed[0].SegmentHistory.Count);
        Assert.Equal(original[0].SegmentHistory[1].RealTime, committed[0].SegmentHistory[1].RealTime);
        Assert.Equal(original.AttemptHistory.Count, committed.AttemptHistory.Count);
    }

    [Fact]
    public void Draft_Edit_RealTime_Does_Not_Zero_GameTime()
    {
        Run original = XmlRunSerializer.LoadFile(Fixture("small-representative.lss"));
        RunEditorDraft draft = RunEditorDraft.FromRun(original);
        draft.Segments[0].SetComparisonTime(
            Run.PersonalBestComparisonName,
            TimingMethod.RealTime,
            TimeSpan.FromSeconds(61));

        Run committed = draft.ToRun(preserveAttemptHistory: true);
        Assert.Equal(TimeSpan.FromSeconds(61), committed[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(TimeSpan.FromSeconds(55), committed[0].PersonalBestSplitTime.GameTime);
    }

    [Fact]
    public void AtomicSave_Writes_TestCopy_Without_Touching_Source()
    {
        string source = Fixture("small-representative.lss");
        byte[] before = File.ReadAllBytes(source);
        string copy = Path.Combine(Path.GetTempPath(), "lsn-editor-test-" + Guid.NewGuid().ToString("N") + ".lss");
        try
        {
            Run run = XmlRunSerializer.LoadFile(source);
            run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(99), TimeSpan.FromSeconds(88));
            XmlRunSerializer.SaveFileAtomic(run, copy);

            Assert.True(File.Exists(copy));
            Assert.Equal(before, File.ReadAllBytes(source));

            Run loaded = XmlRunSerializer.LoadFile(copy);
            Assert.Equal(TimeSpan.FromSeconds(99), loaded[0].PersonalBestSplitTime.RealTime);
            Assert.Equal(TimeSpan.FromSeconds(88), loaded[0].PersonalBestSplitTime.GameTime);
            Assert.Contains("Rival", loaded.CustomComparisons);
        }
        finally
        {
            if (File.Exists(copy))
            {
                File.Delete(copy);
            }
        }
    }

    [Theory]
    [InlineData("livesplit1.6.lss")]
    [InlineData("lso-gametime-small.lss")]
    public void WindowsAndLsoSamples_Load_PersonalBest(string fixtureName)
    {
        string path = Fixture(fixtureName);
        Assert.True(File.Exists(path), path);
        Run run = XmlRunSerializer.LoadFile(path);
        Assert.True(run.Count > 0);
        Assert.Contains(Run.PersonalBestComparisonName, run.CustomComparisons);
        Assert.True(
            run.Segments.Any(s => s.PersonalBestSplitTime.RealTime.HasValue
                || s.PersonalBestSplitTime.GameTime.HasValue),
            "Expected at least one PB split time in " + fixtureName);
    }

    [Fact]
    public void SmsAny_NineDigit_Pb_Loads_And_Draft_RoundTrip_Keeps_Pb()
    {
        // Trimmed from Basten's LastSplitsPath (GCN No ACE (9).lss) — real 9-digit PB,
        // not invented. Custom comps with whole seconds must not be copied into PB.
        Run original = XmlRunSerializer.LoadFile(Fixture("sms-any-pb-9digit.lss"));
        Assert.Equal(2, original.Count);
        Assert.Contains(Run.PersonalBestComparisonName, original.CustomComparisons);
        Assert.Contains("Balanced 116", original.CustomComparisons);
        Assert.Contains("Skibkidi", original.CustomComparisons);
        Assert.Contains("Dogewilders", original.CustomComparisons);

        TimeSpan expectedPb0 = TimeSpanParser.Parse("00:10:19.680000000");
        TimeSpan expectedPb1 = TimeSpanParser.Parse("00:11:33.688999872");
        Assert.Equal(expectedPb0, original[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(expectedPb1, original[1].PersonalBestSplitTime.RealTime);
        Assert.NotEqual(original[0].Comparisons["Balanced 116"].RealTime, original[0].PersonalBestSplitTime.RealTime);
        Assert.NotEqual(original[0].Comparisons["Skibkidi"].RealTime, original[0].PersonalBestSplitTime.RealTime);

        RunEditorDraft draft = RunEditorDraft.FromRun(original);
        Run committed = draft.ToRun(preserveAttemptHistory: true);
        Assert.Equal(expectedPb0, committed[0].PersonalBestSplitTime.RealTime);
        Assert.Equal(expectedPb1, committed[1].PersonalBestSplitTime.RealTime);

        string copy = Path.Combine(Path.GetTempPath(), "lsn-sms-pb-" + Guid.NewGuid().ToString("N") + ".lss");
        try
        {
            XmlRunSerializer.SaveFileAtomic(committed, copy);
            Run reloaded = XmlRunSerializer.LoadFile(copy);
            Assert.Equal(expectedPb0, reloaded[0].PersonalBestSplitTime.RealTime);
            Assert.Equal(expectedPb1, reloaded[1].PersonalBestSplitTime.RealTime);
        }
        finally
        {
            if (File.Exists(copy))
            {
                File.Delete(copy);
            }
        }
    }
}
