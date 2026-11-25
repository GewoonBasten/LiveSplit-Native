using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>SplitComponent.UpdateColumn / UpdateAll colors and texts (LiveSplit.Splits, 93a11b85).</summary>
public class SplitColumnLogicTests
{
    private static readonly ArgbColor Before = ArgbColor.FromArgb(255, 10, 20, 30);
    private static readonly ArgbColor Current = ArgbColor.FromArgb(255, 40, 50, 60);
    private static readonly ArgbColor After = ArgbColor.FromArgb(255, 70, 80, 90);
    private static readonly ArgbColor Deltas = ArgbColor.FromArgb(255, 1, 2, 3);

    private static (LiveSplitState State, TimerModel Model, ManualTimeSourceScope Clock) Running()
    {
        ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        var run = new Run();
        foreach (string name in new[] { "A", "B", "C" })
        {
            run.AddSegment(name);
        }

        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(20));
        run[2].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(30));
        var state = new LiveSplitState(run);
        var model = new TimerModel { CurrentState = state };
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(12));
        model.Split();
        clock.Advance(TimeSpan.FromSeconds(11));
        return (state, model, clock);
    }

    private static SplitsComponentSettings Colored() => new()
    {
        OverrideTextColor = true,
        BeforeNamesColor = Before,
        CurrentNamesColor = Current,
        AfterNamesColor = After,
        OverrideTimesColor = true,
        BeforeTimesColor = Before,
        CurrentTimesColor = Current,
        AfterTimesColor = After,
        OverrideDeltasColor = true,
        DeltasColor = Deltas,
    };

    [Fact]
    public void Name_And_Time_Colors_Follow_Before_Current_After()
    {
        (LiveSplitState state, _, ManualTimeSourceScope clock) = Running();
        using (clock)
        {
            var layout = new LayoutSettings();
            SplitsComponentSettings settings = Colored();
            Assert.Equal(Before, SplitColumnLogic.NameColor(state, 0, settings, layout));
            Assert.Equal(Current, SplitColumnLogic.NameColor(state, 1, settings, layout));
            Assert.Equal(After, SplitColumnLogic.NameColor(state, 2, settings, layout));

            var time = new ColumnData("Time", ColumnType.SplitTime, "Current Comparison", "Current Timing Method");
            GeneralTimeFormatterPair f = Formatters(settings);
            Assert.Equal(Before, SplitColumnLogic.Column(state, 0, time, settings, layout, f.Time, f.Delta).Color);
            Assert.Equal(Current, SplitColumnLogic.Column(state, 1, time, settings, layout, f.Time, f.Delta).Color);
            Assert.Equal(After, SplitColumnLogic.Column(state, 2, time, settings, layout, f.Time, f.Delta).Color);
            Assert.Equal("0:12", SplitColumnLogic.Column(state, 0, time, settings, layout, f.Time, f.Delta).Text);
            Assert.Equal("0:30", SplitColumnLogic.Column(state, 2, time, settings, layout, f.Time, f.Delta).Text);

            // Without the override everything is the layout's text color.
            settings.OverrideTextColor = false;
            settings.OverrideTimesColor = false;
            Assert.Equal(layout.TextColor, SplitColumnLogic.NameColor(state, 0, settings, layout));
            Assert.Equal(layout.TextColor, SplitColumnLogic.Column(state, 2, time, settings, layout, f.Time, f.Delta).Color);
        }
    }

    [Fact]
    public void Live_Delta_Uses_The_Deltas_Color_And_A_Plus_Sign()
    {
        (LiveSplitState state, _, ManualTimeSourceScope clock) = Running();
        using (clock)
        {
            var layout = new LayoutSettings();
            SplitsComponentSettings settings = Colored();
            var delta = new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method");
            GeneralTimeFormatterPair f = Formatters(settings);
            // 23 s into split B, whose PB split is 20 s: +3.0 live, behind, in the deltas color.
            SplitCellText live = SplitColumnLogic.Column(state, 1, delta, settings, layout, f.Time, f.Delta);
            Assert.Equal("+3.0", live.Text);
            Assert.Equal(Deltas, live.Color);
            // A finished split shows its delta (+2.0) colored by ahead / behind, not the override.
            SplitCellText done = SplitColumnLogic.Column(state, 0, delta, settings, layout, f.Time, f.Delta);
            Assert.Equal("+2.0", done.Text);
            Assert.NotEqual(Deltas, done.Color);
            // Upcoming splits show no delta.
            Assert.Equal("", SplitColumnLogic.Column(state, 2, delta, settings, layout, f.Time, f.Delta).Text);
        }
    }

    private readonly record struct GeneralTimeFormatterPair(
        LiveSplitNative.Core.Formatting.GeneralTimeFormatter Time,
        LiveSplitNative.Core.Formatting.GeneralTimeFormatter Delta);

    private static GeneralTimeFormatterPair Formatters(SplitsComponentSettings settings) =>
        new(SplitColumnLogic.TimeFormatter(settings.SplitTimesAccuracy),
            SplitColumnLogic.DeltaFormatter(settings.DeltasAccuracy, settings.DropDecimals));
}
