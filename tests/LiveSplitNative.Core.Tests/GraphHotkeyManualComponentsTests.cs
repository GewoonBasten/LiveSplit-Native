using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>Graph, Hotkey Indicator and Manual Game Time (LiveSplit components @ 93a11b85).</summary>
public class GraphHotkeyManualComponentsTests
{
    private static readonly LayoutSettings Colors = LayoutSettings.CreateStandard();

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

    [Fact]
    public void Graph_Deltas_Are_Each_Splits_Delta_And_The_Live_One()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewRun();
        var settings = new GraphComponentSettings();

        GraphData idle = GraphLogic.Calculate(state, settings, Colors);
        Assert.Equal(new TimeSpan?[] { null, null, null }, idle.Deltas);
        Assert.False(idle.IsLiveDeltaActive);
        Assert.Equal(TimeSpan.Zero, idle.FinalSplit);

        model.Start();
        clock.Advance(TimeSpan.FromSeconds(12));
        model.Split();
        clock.Advance(TimeSpan.FromSeconds(6));
        model.Split();
        // A +2, B -2; at 18 s in C (PB 30) there is no live delta yet.
        GraphData running = GraphLogic.Calculate(state, settings, Colors);
        Assert.Equal(new TimeSpan?[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(-2), null }, running.Deltas);
        Assert.Equal((TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(-2)), (running.MaxDelta, running.MinDelta));
        Assert.Equal(TimeSpan.FromSeconds(18), running.FinalSplit);
        Assert.False(running.IsLiveDeltaActive);

        // 33 s: 3 s behind the PB's 30 at the end of C, shown live.
        clock.Advance(TimeSpan.FromSeconds(15));
        GraphData behind = GraphLogic.Calculate(state, settings, Colors);
        Assert.True(behind.IsLiveDeltaActive);
        Assert.Equal(4, behind.Deltas.Count);
        Assert.Equal(TimeSpan.FromSeconds(3), behind.Deltas[^1]);
        Assert.Equal(TimeSpan.FromSeconds(3), behind.MaxDelta);

        // Not a live graph: the time span ends at the last split.
        settings.IsLiveGraph = false;
        GraphData fixedGraph = GraphLogic.Calculate(state, settings, Colors);
        Assert.False(fixedGraph.IsLiveDeltaActive);
        Assert.Equal(TimeSpan.FromSeconds(18), fixedGraph.FinalSplit);
    }

    [Fact]
    public void Graph_Draws_Behind_Above_Ahead_Then_Fills_Lines_And_Dots()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewRun();
        var settings = new GraphComponentSettings { ShowBestSegments = true };
        model.Start();
        clock.Advance(TimeSpan.FromSeconds(12));
        model.Split();
        clock.Advance(TimeSpan.FromSeconds(6));
        model.Split();

        GraphData data = GraphLogic.Calculate(state, settings, Colors);
        GraphPicture picture = GraphLogic.Draw(state, settings, data, 180, 120);
        GraphOp behind = picture.Ops[0];
        GraphOp ahead = picture.Ops[1];
        Assert.Equal((GraphOpKind.Rectangle, settings.BehindGraphColor), (behind.Kind, behind.Color));
        Assert.Equal((GraphOpKind.Rectangle, settings.AheadGraphColor), (ahead.Kind, ahead.Color));
        // Max +2 and min -2: the middle is halfway down, the bands meet there.
        float middle = behind.Points[1].Y;
        Assert.Equal(60, middle, 3);
        Assert.Equal(60, ahead.Points[0].Y, 3);
        Assert.Equal(120, ahead.Points[1].Y, 3);
        Assert.Contains(picture.Ops, op => op.Kind == GraphOpKind.Gridline);
        Assert.Contains(picture.Ops, op => op.Kind == GraphOpKind.Polygon);

        // Two splits: two lines (from the start, A, B) and the dots of A and B (not the start).
        List<GraphOp> lines = picture.Ops.Where(op => op.Kind == GraphOpKind.Line).ToList();
        List<GraphOp> dots = picture.Ops.Where(op => op.Kind == GraphOpKind.Dot).ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(2, dots.Count);
        // B (6 s against a best of 9) is a gold; A (12 s against 8) is not.
        Assert.Equal(settings.GraphGoldColor, dots[0].Color);
        Assert.Equal(settings.GraphColor, dots[1].Color);
        // B ends at the right edge (its split is the last time), A at 12 / 18 of the width.
        Assert.Equal(180, dots[0].Points[0].X, 3);
        Assert.Equal(120, dots[1].Points[0].X, 3);
        // Ahead (B, -2) is below the middle, behind (A, +2) above.
        Assert.True(dots[0].Points[0].Y > middle);
        Assert.True(dots[1].Points[0].Y < middle);
        Assert.False(picture.Flip);
    }

    [Fact]
    public void Graph_Settings_Are_Windows_Settings()
    {
        Assert.Equal(
            "<Version>1.5</Version><Height>120</Height><Width>180</Width><BehindGraphColor>FF732828</BehindGraphColor>"
            + "<AheadGraphColor>FF287334</AheadGraphColor><GridlinesColor>50000000</GridlinesColor>"
            + "<PartialFillColorBehind>19FFFFFF</PartialFillColorBehind><CompleteFillColorBehind>32FFFFFF</CompleteFillColorBehind>"
            + "<PartialFillColorAhead>19FFFFFF</PartialFillColorAhead><CompleteFillColorAhead>32FFFFFF</CompleteFillColorAhead>"
            + "<GraphColor>FFFFFFFF</GraphColor><ShadowsColor>38000000</ShadowsColor><GraphLinesColor>FFFFFFFF</GraphLinesColor>"
            + "<LiveGraph>True</LiveGraph><FlipGraph>False</FlipGraph><Comparison>Current Comparison</Comparison>"
            + "<ShowBestSegments>False</ShowBestSegments><GraphGoldColor>FFD8AF1F</GraphGoldColor>",
            new GraphComponentSettings().ToInnerXml());

        // Before 1.2 one partial and one complete fill color served both sides.
        GraphComponentSettings old = GraphComponentSettings.FromInnerXml(
            "<Version>1.1</Version><Height>90</Height><Width>200</Width><PartialFillColor>11223344</PartialFillColor>"
            + "<CompleteFillColor>55667788</CompleteFillColor><LiveGraph>False</LiveGraph>");
        Assert.Equal(90f, old.GraphHeight);
        Assert.Equal(old.PartialFillColorAhead, old.PartialFillColorBehind);
        Assert.Equal(ArgbColor.FromArgb(0x55, 0x66, 0x77, 0x88), old.CompleteFillColorAhead);
        Assert.False(old.IsLiveGraph);
        Assert.Equal(ArgbColor.FromArgb(255, 255, 215, 0), old.GraphGoldColor);

        Assert.Equal("Graph", GraphLogic.Name("Current Comparison"));
        Assert.Equal("Graph (Best)", GraphLogic.Name("Best Segments"));
    }

    [Fact]
    public void Graph_And_Hotkey_Indicator_Sizes()
    {
        Layout layout = StandardLayoutFactory.Create();
        layout.Components.Clear();
        layout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.GraphPath));
        layout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.HotkeyIndicatorPath));
        layout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.ManualGameTimePath));
        IReadOnlyList<TimerComponentMetric> metrics = TimerLayoutMetrics.FromLayout(layout, 3);
        Assert.Equal((122d, 182d), (metrics[0].VerticalSize, metrics[0].HorizontalSize));
        Assert.Equal((3d, 3d), (metrics[1].VerticalSize, metrics[1].HorizontalSize));
        Assert.Equal((0d, 0d), (metrics[2].VerticalSize, metrics[2].HorizontalSize));

        Assert.Equal(
            "<HotkeysOnColor>FF29CC54</HotkeysOnColor><HotkeysOffColor>FFCC3729</HotkeysOffColor>"
            + "<IndicatorHeight>3</IndicatorHeight><IndicatorWidth>3</IndicatorWidth>",
            new HotkeyIndicatorComponentSettings().ToInnerXml());
        Assert.Equal(
            "<Version>1.4</Version><UseSegmentTimes>True</UseSegmentTimes>",
            new ManualGameTimeComponentSettings().ToInnerXml());
    }

    [Fact]
    public void Manual_Game_Time_Sets_The_Game_Time_Of_Each_Split()
    {
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1));
        (LiveSplitState state, TimerModel model) = NewRun();
        var segments = new ManualGameTimeComponentSettings { UseSegmentTimes = true };
        model.Start();
        ManualGameTimeLogic.Start(state);
        clock.Advance(TimeSpan.FromSeconds(20));
        Assert.True(state.IsGameTimePaused);
        Assert.Equal(TimeSpan.Zero, state.CurrentTime.GameTime);

        Assert.True(ManualGameTimeLogic.Enter(state, segments, "1:00"));
        model.Split();
        Assert.Equal(TimeSpan.FromMinutes(1), state.Run[0].SplitTime.GameTime);

        // Segment times add up; "+" adds the parts.
        Assert.True(ManualGameTimeLogic.Enter(state, segments, "0:30 + 15"));
        model.Split();
        Assert.Equal(TimeSpan.FromSeconds(105), state.Run[1].SplitTime.GameTime);

        // Something that is not a time changes nothing.
        Assert.False(ManualGameTimeLogic.Enter(state, segments, "soon"));
        Assert.Equal(TimeSpan.FromSeconds(105), state.CurrentTime.GameTime);

        // Undo: back to the previous split's game time.
        model.UndoSplit();
        ManualGameTimeLogic.UndoSplit(state);
        Assert.Equal(TimeSpan.FromMinutes(1), state.CurrentTime.GameTime);

        // Split times are taken as they are.
        var splits = new ManualGameTimeComponentSettings { UseSegmentTimes = false };
        Assert.True(ManualGameTimeLogic.Enter(state, splits, "2:00"));
        model.Split();
        Assert.Equal(TimeSpan.FromMinutes(2), state.Run[1].SplitTime.GameTime);
    }

    [Fact]
    public void New_Components_Are_In_The_Add_Menu()
    {
        (string Path, StandardLayoutFactory.ComponentCategory Category, string Name)[] expected =
        [
            (StandardLayoutFactory.GraphPath, StandardLayoutFactory.ComponentCategory.Media, "Graph"),
            (StandardLayoutFactory.ManualGameTimePath, StandardLayoutFactory.ComponentCategory.Control, "Manual Game Time"),
            (StandardLayoutFactory.HotkeyIndicatorPath, StandardLayoutFactory.ComponentCategory.Other, "Hotkey Indicator"),
        ];
        foreach ((string path, StandardLayoutFactory.ComponentCategory category, string name) in expected)
        {
            StandardLayoutFactory.BuiltinComponentInfo info = Assert.Single(StandardLayoutFactory.BuiltinCatalog, c => c.Path == path);
            Assert.Equal((category, name), (info.Category, info.DisplayName));
            Assert.Equal(name, StandardLayoutFactory.CreateBuiltin(path).DisplayName);
        }
    }
}
