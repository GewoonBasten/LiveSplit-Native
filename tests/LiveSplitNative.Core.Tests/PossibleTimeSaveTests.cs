using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class PossibleTimeSaveTests
{
    [Fact]
    public void PossibleTimeSave_Settings_RoundTrip()
    {
        var settings = new PossibleTimeSaveComponentSettings
        {
            Accuracy = TimeAccuracy.Tenths,
            TotalTimeSave = true,
            DropDecimals = true,
            Comparison = "Rival",
        };
        PossibleTimeSaveComponentSettings again = PossibleTimeSaveComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.Equal(TimeAccuracy.Tenths, again.Accuracy);
        Assert.True(again.TotalTimeSave);
        Assert.True(again.DropDecimals);
        Assert.Equal("Rival", again.Comparison);
    }

    [Fact]
    public void GetPossibleTimeSave_Uses_Comparison_Minus_Golds()
    {
        Run run = Run.CreateStandard();
        run.ClearSegments();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(30));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(70));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(25));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(35));

        var state = new LiveSplitState(run);
        TimeSpan? save0 = PossibleTimeSaveCalculator.GetPossibleTimeSave(state, 0, Run.PersonalBestComparisonName);
        // 30 - 0 - 25 = 5s
        Assert.Equal(TimeSpan.FromSeconds(5), save0);

        TimeSpan? save1 = PossibleTimeSaveCalculator.GetPossibleTimeSave(state, 1, Run.PersonalBestComparisonName);
        // 70 - 30 - 35 = 5s
        Assert.Equal(TimeSpan.FromSeconds(5), save1);
    }

    [Fact]
    public void TotalPossibleTimeSave_Sums_Remaining()
    {
        Run run = Run.CreateStandard();
        run.ClearSegments();
        run.AddSegment("A");
        run.AddSegment("B");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(30));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(70));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(25));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(35));
        var state = new LiveSplitState(run) { CurrentSplitIndex = 0, CurrentPhase = TimerPhase.NotRunning };

        TimeSpan? total = PossibleTimeSaveCalculator.GetDisplayedValue(state, Run.PersonalBestComparisonName, totalTimeSave: true);
        Assert.Equal(TimeSpan.FromSeconds(10), total);
    }

    [Fact]
    public void CreateBuiltin_PossibleTimeSave_Has_Default_Settings()
    {
        LayoutComponent c = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.PossibleTimeSavePath);
        PossibleTimeSaveComponentSettings s = PossibleTimeSaveComponentSettings.FromInnerXml(c.SettingsInnerXml);
        Assert.Equal(TimeAccuracy.Hundredths, s.Accuracy);
        Assert.False(s.TotalTimeSave);
    }
}
