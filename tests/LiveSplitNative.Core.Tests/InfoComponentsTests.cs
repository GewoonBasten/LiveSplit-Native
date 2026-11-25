using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// The Update methods of Run Prediction, Delta, Comparison Time, Current Comparison and Total
/// Playtime (LiveSplit components @ 93a11b85), on a three-split run: PB splits 10 / 20 / 30 s,
/// best segments 8 / 9 / 9 s (Best Segments 8 / 17 / 26 s).
/// </summary>
public class InfoComponentsTests
{
    private static (LiveSplitState State, TimerModel Model) NewRun()
    {
        var run = new Run();
        foreach (string name in new[] { "A", "B", "C" })
        {
            run.AddSegment(name);
        }

        int[] pb = [10, 20, 30];
        int[] best = [8, 9, 9];
        for (int i = 0; i < 3; i++)
        {
            run[i].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(pb[i]));
            run[i].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(best[i]));
        }

        run.RegenerateComparisons(new ComparisonGeneratorSettings());
        var state = new LiveSplitState(run);
        return (state, new TimerModel { CurrentState = state });
    }

    private static readonly LayoutSettings Colors = LayoutSettings.CreateStandard();

    [Fact]
    public void Run_Prediction_Is_The_Last_Delta_Or_The_Live_One_Plus_The_Final_Time()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewRun();
        var pace = new RunPredictionComponentSettings();
        var bestPossible = new RunPredictionComponentSettings { Comparison = BestSegmentsComparisonGenerator.ComparisonName };

        // Before the run: Current Pace shows nothing, Best Possible Time the sum of best.
        InfoComponentView idle = InfoComponentLogic.RunPrediction(state, pace, Colors);
        Assert.Equal(("Current Pace", "-"), (idle.Name, idle.Value));
        Assert.Equal(["Cur. Pace", "Pace"], idle.AlternateNames);
        InfoComponentView bpt = InfoComponentLogic.RunPrediction(state, bestPossible, Colors);
        Assert.Equal(("Best Possible Time", "0:26"), (bpt.Name, bpt.Value));
        Assert.Equal(["Best Poss. Time", "Best Time", "BPT"], bpt.AlternateNames);

        // A split at 11 s: +1 on the PB (0:31), +3 on the best segments (0:29).
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(11));
        model.Split();
        Assert.Equal("0:31", InfoComponentLogic.RunPrediction(state, pace, Colors).Value);
        Assert.Equal("0:29", InfoComponentLogic.RunPrediction(state, bestPossible, Colors).Value);

        // 25 s into B (PB 20): the live delta +5 is bigger, so 0:35.
        clock.Advance(TimeSpan.FromSeconds(14));
        Assert.Equal("0:35", InfoComponentLogic.RunPrediction(state, pace, Colors).Value);

        // Ended: the run's own final time.
        model.Split();
        clock.Advance(TimeSpan.FromSeconds(8));
        model.Split();
        Assert.Equal(TimerPhase.Ended, state.CurrentPhase);
        Assert.Equal("0:33", InfoComponentLogic.RunPrediction(state, pace, Colors).Value);
        Assert.Equal("0:33", InfoComponentLogic.RunPrediction(state, bestPossible, Colors).Value);
    }

    [Theory]
    [InlineData("Current Comparison", "Current Pace")]
    [InlineData("Personal Best", "Current Pace")]
    [InlineData("Best Segments", "Best Possible Time")]
    [InlineData("Worst Segments", "Worst Possible Time")]
    [InlineData("Average Segments", "Predicted Time")]
    [InlineData("Latest Run", "Current Pace (Latest)")]
    [InlineData("My Rival", "Current Pace (My Rival)")]
    public void Run_Prediction_Is_Named_After_Its_Comparison(string comparison, string name)
    {
        Assert.Equal(name, InfoComponentLogic.RunPredictionName(comparison));
        var component = new LayoutComponent(
            StandardLayoutFactory.RunPredictionPath,
            new RunPredictionComponentSettings { Comparison = comparison }.ToInnerXml());
        Assert.Equal(name, component.DisplayName);
    }

    [Fact]
    public void Delta_Shows_The_Last_Or_Live_Delta_In_The_Split_Colors()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewRun();
        var settings = new DeltaComponentSettings();

        InfoComponentView idle = InfoComponentLogic.Delta(state, settings, Colors);
        Assert.Equal(("Personal Best", "-"), (idle.Name, idle.Value));
        Assert.Equal(["PB"], idle.AlternateNames);
        Assert.Equal(Colors.TextColor, idle.ValueColor);

        model.Start();
        clock.Advance(TimeSpan.FromSeconds(11));
        model.Split();
        InfoComponentView behind = InfoComponentLogic.Delta(state, settings, Colors);
        Assert.Equal("+1.0", behind.Value);
        Assert.Equal(Colors.BehindLosingTimeColor, behind.ValueColor);

        // 25 s into B: the live delta.
        clock.Advance(TimeSpan.FromSeconds(14));
        Assert.Equal("+5.0", InfoComponentLogic.Delta(state, settings, Colors).Value);

        // Custom labels, one for behind and one for ahead.
        settings.OverrideText = true;
        settings.DifferentialText = true;
        settings.CustomText = "Behind";
        settings.CustomTextAhead = "Ahead";
        InfoComponentView custom = InfoComponentLogic.Delta(state, settings, Colors);
        Assert.Equal("Behind", custom.Name);
        Assert.Empty(custom.AlternateNames);

        // Ended at 33 s against the PB's 30.
        model.Split();
        clock.Advance(TimeSpan.FromSeconds(8));
        model.Split();
        Assert.Equal("+3.0", InfoComponentLogic.Delta(state, new DeltaComponentSettings(), Colors).Value);

        Assert.Equal("Delta", InfoComponentLogic.DeltaName("Current Comparison"));
        Assert.Equal("Delta (Best)", InfoComponentLogic.DeltaName("Best Segments"));
    }

    [Fact]
    public void Delta_Ahead_Uses_The_Ahead_Label_And_Color()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewRun();
        var settings = new DeltaComponentSettings
        {
            OverrideText = true,
            DifferentialText = true,
            CustomText = "Behind",
            CustomTextAhead = "Ahead",
        };
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(9));
        model.Split();
        InfoComponentView view = InfoComponentLogic.Delta(state, settings, Colors);
        Assert.Equal(("Ahead", "−1.0"), (view.Name, view.Value));
        Assert.Equal(Colors.AheadGainingTimeColor, view.ValueColor);
    }

    [Fact]
    public void Comparison_Time_Shows_The_Final_Split_Or_Segment_Time()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewRun();
        var final = new ComparisonTimeComponentSettings();
        var split = new ComparisonTimeComponentSettings { Type = ComparisonTimeType.SplitTime };
        var segment = new ComparisonTimeComponentSettings { Type = ComparisonTimeType.SegmentTime };

        InfoComponentView idle = InfoComponentLogic.ComparisonTime(state, final, Colors);
        Assert.Equal(("Personal Best", "0:30"), (idle.Name, idle.Value));
        Assert.Equal(["PB"], idle.AlternateNames);
        Assert.Equal("-", InfoComponentLogic.ComparisonTime(state, split, Colors).Value);
        Assert.Equal("-", InfoComponentLogic.ComparisonTime(state, segment, Colors).Value);

        model.Start();
        clock.Advance(TimeSpan.FromSeconds(11));
        model.Split();
        Assert.Equal("0:20", InfoComponentLogic.ComparisonTime(state, split, Colors).Value);
        Assert.Equal("0:10", InfoComponentLogic.ComparisonTime(state, segment, Colors).Value);

        model.Split();
        model.Split();
        Assert.Equal("0:30", InfoComponentLogic.ComparisonTime(state, split, Colors).Value);
        Assert.Equal("0:10", InfoComponentLogic.ComparisonTime(state, segment, Colors).Value);

        // A chosen timing method is in the name; this run has no game time.
        var gameTime = new ComparisonTimeComponentSettings { TimingMethod = "Game Time" };
        InfoComponentView view = InfoComponentLogic.ComparisonTime(state, gameTime, Colors);
        Assert.Equal(("Personal Best (Game Time)", "-"), (view.Name, view.Value));
    }

    [Fact]
    public void Comparison_Time_Is_Named_After_Its_Overrides()
    {
        Assert.Equal("Comparison Time", InfoComponentLogic.ComparisonTimeName(new ComparisonTimeComponentSettings()));
        Assert.Equal(
            "Comparison Time (Best Segments)",
            InfoComponentLogic.ComparisonTimeName(new ComparisonTimeComponentSettings { Comparison = "Best Segments" }));
        Assert.Equal(
            "Comparison Time (Real Time)",
            InfoComponentLogic.ComparisonTimeName(new ComparisonTimeComponentSettings { TimingMethod = "Real Time" }));
        Assert.Equal(
            "Comparison Time (Best Segments, Game Time)",
            InfoComponentLogic.ComparisonTimeName(new ComparisonTimeComponentSettings
            {
                Comparison = "Best Segments",
                TimingMethod = "Game Time",
            }));
    }

    [Fact]
    public void Current_Comparison_Shows_The_Timers_Comparison_In_The_Text_Font()
    {
        (LiveSplitState state, _) = NewRun();
        state.CurrentComparison = BestSegmentsComparisonGenerator.ComparisonName;
        var settings = new CurrentComparisonComponentSettings
        {
            OverrideTimeColor = true,
            TimeColor = ArgbColor.FromArgb(255, 1, 2, 3),
        };
        InfoComponentView view = InfoComponentLogic.CurrentComparison(state, settings, Colors);
        Assert.Equal(("Comparing Against", "Best Segments"), (view.Name, view.Value));
        Assert.Equal(["Comparison", "Comp."], view.AlternateNames);
        Assert.True(view.ValueInTextFont);
        Assert.Equal(Colors.TextColor, view.NameColor);
        Assert.Equal(ArgbColor.FromArgb(255, 1, 2, 3), view.ValueColor);
    }

    [Fact]
    public void Total_Playtime_Adds_Every_Attempt_And_The_Current_One()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewRun();
        Run run = state.Run;
        var started = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        // 1.6+: start to end (2 days 1:30) less 10 minutes of pauses.
        run.AttemptHistory.Add(new Attempt(
            1,
            default,
            new AtomicDateTime(started, false),
            new AtomicDateTime(started + new TimeSpan(2, 1, 30, 0), false),
            TimeSpan.FromMinutes(10)));
        // Before 1.6, finished: the run's time.
        run.AttemptHistory.Add(new Attempt(2, new SplitTime(TimeSpan.FromMinutes(5)), null, null, null));
        // Before 1.6, reset: the segments it has in the history.
        run.AttemptHistory.Add(new Attempt(3, default, null, null, null));
        run[0].SegmentHistory[3] = new SplitTime(TimeSpan.FromMinutes(2));
        run[1].SegmentHistory[3] = new SplitTime(TimeSpan.FromMinutes(3));

        TimeSpan history = InfoComponentLogic.AttemptHistoryPlaytime(run);
        Assert.Equal(new TimeSpan(2, 1, 30, 0), history);
        var settings = new TotalPlaytimeComponentSettings();
        InfoComponentView idle = InfoComponentLogic.TotalPlaytime(state, settings, Colors, history);
        Assert.Equal(("Total Playtime", "2d 1:30:00"), (idle.Name, idle.Value));
        Assert.Empty(idle.AlternateNames);
        settings.ShowTotalHours = true;
        Assert.Equal("49:30:00", InfoComponentLogic.TotalPlaytime(state, settings, Colors, history).Value);

        model.Start();
        clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal("49:31:01", InfoComponentLogic.TotalPlaytime(state, settings, Colors, history).Value);
    }

    [Fact]
    public void Days_Formatter_Matches_Total_Playtimes()
    {
        Assert.Equal("0", DaysTimeFormatter.Format(null));
        Assert.Equal("0:59", DaysTimeFormatter.Format(TimeSpan.FromSeconds(59.9)));
        Assert.Equal("12:34", DaysTimeFormatter.Format(new TimeSpan(0, 12, 34)));
        Assert.Equal("1:00:00", DaysTimeFormatter.Format(TimeSpan.FromHours(1)));
        Assert.Equal("3d 4:05:06", DaysTimeFormatter.Format(new TimeSpan(3, 4, 5, 6)));
    }

    [Fact]
    public void New_Components_Are_In_The_Add_Menu_With_Windows_Defaults()
    {
        (string Path, StandardLayoutFactory.ComponentCategory Category, string Name)[] expected =
        [
            (StandardLayoutFactory.RunPredictionPath, StandardLayoutFactory.ComponentCategory.Information, "Run Prediction"),
            (StandardLayoutFactory.DeltaPath, StandardLayoutFactory.ComponentCategory.Information, "Delta"),
            (StandardLayoutFactory.ComparisonTimePath, StandardLayoutFactory.ComponentCategory.Information, "Comparison Time"),
            (StandardLayoutFactory.CurrentComparisonPath, StandardLayoutFactory.ComponentCategory.Information, "Current Comparison"),
            (StandardLayoutFactory.TotalPlaytimePath, StandardLayoutFactory.ComponentCategory.Information, "Total Playtime"),
            (StandardLayoutFactory.CounterPath, StandardLayoutFactory.ComponentCategory.Other, "Counter"),
        ];
        foreach ((string path, StandardLayoutFactory.ComponentCategory category, string name) in expected)
        {
            StandardLayoutFactory.BuiltinComponentInfo info = Assert.Single(StandardLayoutFactory.BuiltinCatalog, c => c.Path == path);
            Assert.Equal((category, name), (info.Category, info.DisplayName));
            Assert.NotNull(StandardLayoutFactory.DefaultSettingsFor(path));
        }

        // The Layout Editor list shows ComponentName.
        Assert.Equal("Current Pace", StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.RunPredictionPath).DisplayName);
        Assert.Equal("Delta", StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.DeltaPath).DisplayName);
        Assert.Equal("Comparison Time", StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.ComparisonTimePath).DisplayName);
        Assert.Equal("Counter", StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.CounterPath).DisplayName);
    }

    [Fact]
    public void Settings_Are_Written_As_Windows_Writes_Them()
    {
        Assert.Equal(
            "<Version>1.4</Version><TextColor>FFFFFFFF</TextColor><OverrideTextColor>False</OverrideTextColor>"
            + "<TimeColor>FFFFFFFF</TimeColor><OverrideTimeColor>False</OverrideTimeColor><Accuracy>Seconds</Accuracy>"
            + "<BackgroundColor>00000000</BackgroundColor><BackgroundColor2>00000000</BackgroundColor2>"
            + "<BackgroundGradient>Plain</BackgroundGradient><Comparison>Current Comparison</Comparison>"
            + "<Display2Rows>False</Display2Rows>",
            new RunPredictionComponentSettings().ToInnerXml());
        Assert.Equal(
            "<Version>1.4</Version><TextColor>FFFFFFFF</TextColor><OverrideTextColor>False</OverrideTextColor>"
            + "<Accuracy>Tenths</Accuracy><BackgroundColor>00000000</BackgroundColor>"
            + "<BackgroundColor2>00000000</BackgroundColor2><BackgroundGradient>Plain</BackgroundGradient>"
            + "<Comparison>Current Comparison</Comparison><Display2Rows>False</Display2Rows>"
            + "<DropDecimals>True</DropDecimals><OverrideText>False</OverrideText>"
            + "<DifferentialText>False</DifferentialText><CustomText></CustomText><CustomTextAhead></CustomTextAhead>",
            new DeltaComponentSettings().ToInnerXml());
        Assert.Equal(
            "<Version>1.0</Version><GlobalHotkeysEnabled>False</GlobalHotkeysEnabled>"
            + "<OverrideTextColor>False</OverrideTextColor><CounterTextColor>FFFFFFFF</CounterTextColor>"
            + "<CounterValueColor>FFFFFFFF</CounterValueColor><BackgroundColor>00000000</BackgroundColor>"
            + "<BackgroundColor2>00000000</BackgroundColor2><BackgroundGradient>Plain</BackgroundGradient>"
            + "<CounterText>Counter:</CounterText><InitialValue>0</InitialValue><Increment>1</Increment>"
            + "<IncrementKey>Add</IncrementKey><DecrementKey>Subtract</DecrementKey><ResetKey>NumPad0</ResetKey>",
            new CounterComponentSettings().ToInnerXml());
        Assert.Contains("<Version>1.6</Version>", new TotalPlaytimeComponentSettings().ToInnerXml());
        Assert.EndsWith("<ShowTotalHours>False</ShowTotalHours>", new TotalPlaytimeComponentSettings().ToInnerXml());
        Assert.EndsWith(
            "<Comparison>Current Comparison</Comparison><TimingMethod>Current Timing Method</TimingMethod>"
            + "<Display2Rows>False</Display2Rows><Type>FinalTime</Type>",
            new ComparisonTimeComponentSettings().ToInnerXml());
    }

    [Fact]
    public void Windows_Settings_Are_Read()
    {
        DeltaComponentSettings delta = DeltaComponentSettings.FromInnerXml(
            "<Version>1.4</Version><TextColor>FFFF0000</TextColor><OverrideTextColor>True</OverrideTextColor>"
            + "<Accuracy>Hundredths</Accuracy><BackgroundColor>FF101010</BackgroundColor>"
            + "<BackgroundColor2>FF202020</BackgroundColor2><BackgroundGradient>Vertical</BackgroundGradient>"
            + "<Comparison>Best Segments</Comparison><Display2Rows>True</Display2Rows>"
            + "<DropDecimals>False</DropDecimals><OverrideText>True</OverrideText>"
            + "<DifferentialText>True</DifferentialText><CustomText>Behind</CustomText><CustomTextAhead>Ahead</CustomTextAhead>");
        Assert.Equal(TimeAccuracy.Hundredths, delta.Accuracy);
        Assert.Equal(("Best Segments", "Vertical", true, false), (delta.Comparison, delta.BackgroundGradient, delta.Display2Rows, delta.DropDecimals));
        Assert.Equal(("Behind", "Ahead"), (delta.CustomText, delta.CustomTextAhead));
        Assert.Equal(ArgbColor.FromArgb(255, 255, 0, 0), delta.TextColor);

        ComparisonTimeComponentSettings time = ComparisonTimeComponentSettings.FromInnerXml(
            "<Version>1.4</Version><Comparison>Personal Best</Comparison><TimingMethod>Game Time</TimingMethod><Type>SegmentTime</Type>");
        Assert.Equal(("Personal Best", "Game Time", ComparisonTimeType.SegmentTime), (time.Comparison, time.TimingMethod, time.Type));

        CounterComponentSettings counter = CounterComponentSettings.FromInnerXml(
            "<Version>1.0</Version><CounterText>Deaths:</CounterText><InitialValue>-3</InitialValue><Increment>2</Increment>"
            + "<IncrementKey>D1, Control</IncrementKey><DecrementKey /><ResetKey>NumPad0</ResetKey>");
        Assert.Equal(("Deaths:", -3, 2), (counter.CounterText, counter.InitialValue, counter.Increment));
        Assert.Equal(("D1, Control", "", "NumPad0"), (counter.IncrementKey, counter.DecrementKey, counter.ResetKey));
    }

    [Theory]
    [InlineData(StandardLayoutFactory.RunPredictionPath)]
    [InlineData(StandardLayoutFactory.DeltaPath)]
    [InlineData(StandardLayoutFactory.ComparisonTimePath)]
    [InlineData(StandardLayoutFactory.CurrentComparisonPath)]
    [InlineData(StandardLayoutFactory.TotalPlaytimePath)]
    public void Info_Components_Are_One_Info_Row_High(string path)
    {
        Layout layout = StandardLayoutFactory.Create();
        layout.Components.Clear();
        layout.Components.Add(StandardLayoutFactory.CreateBuiltin(path));
        Assert.Equal(31, TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize, 6);
        layout.Settings.ComponentSpacing = 0;
        Assert.Equal(25, TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize, 6);
    }

    [Fact]
    public void Counter_Is_One_And_A_Fifth_Text_Lines_High()
    {
        Layout layout = StandardLayoutFactory.Create();
        layout.Components.Clear();
        layout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.CounterPath));
        layout.Settings.TextFontSizePx = 16;
        Assert.Equal(1.2 * 16 * 2724.0 / 2048.0, TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize, 6);
    }
}
