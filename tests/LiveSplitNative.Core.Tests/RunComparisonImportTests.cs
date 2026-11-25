using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.Tests;

public class RunComparisonImportTests
{
    [Fact]
    public void ClearHistory_And_ClearTimes()
    {
        Run run = Run.CreateStandard();
        run.ClearSegments();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[0].SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(11));
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(TimeSpan.FromSeconds(20)), null, null, null));
        run.AttemptCount = 3;
        run.EnsureComparison("Rival");
        run[0].Comparisons["Rival"] = new SplitTime(TimeSpan.FromSeconds(12));

        run.ClearHistory();
        Assert.Empty(run.AttemptHistory);
        Assert.Empty(run[0].SegmentHistory);
        Assert.Equal(3, run.AttemptCount);
        Assert.True(run[0].Comparisons.ContainsKey("Rival"));

        run.ClearTimes();
        Assert.Equal(0, run.AttemptCount);
        Assert.Equal([Run.PersonalBestComparisonName], run.CustomComparisons);
        Assert.False(run[0].Comparisons.ContainsKey("Rival"));
        Assert.Null(run[0].BestSegmentTime.RealTime);
    }

    [Fact]
    public void TryImportComparison_Matches_By_Name()
    {
        Run target = Run.CreateStandard();
        target.ClearSegments();
        target.AddSegment("Intro");
        target.AddSegment("Boss");
        target.AddSegment("Ending");

        Run source = Run.CreateStandard();
        source.ClearSegments();
        source.AddSegment("intro");
        source.AddSegment("Boss");
        source.AddSegment("Ending");
        source[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(5));
        source[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(40));
        source[2].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(90));

        Assert.True(target.TryImportComparison(source, "From File", out string? error));
        Assert.Null(error);
        Assert.Contains("From File", target.CustomComparisons);
        Assert.Equal(TimeSpan.FromSeconds(5), target[0].Comparisons["From File"].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(40), target[1].Comparisons["From File"].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(90), target[2].Comparisons["From File"].RealTime);

        Assert.False(target.TryImportComparison(source, "From File", out error));
        Assert.Contains("already exists", error);
        Assert.False(target.TryImportComparison(source, "[Race] x", out error));
        Assert.Contains("[Race]", error);
    }
}
