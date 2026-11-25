using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.UI;

/// <summary>GraphComponent.Calculate: the delta of every split, the run's time span and the live delta.</summary>
public sealed record GraphData(
    IReadOnlyList<TimeSpan?> Deltas,
    TimeSpan? FinalSplit,
    TimeSpan MaxDelta,
    TimeSpan MinDelta,
    bool IsLiveDeltaActive);

public enum GraphOpKind
{
    /// <summary>FillRectangle: Points[0] top-left, Points[1] bottom-right.</summary>
    Rectangle,

    /// <summary>A gridline: a 2 wide line from Points[0] to Points[1].</summary>
    Gridline,

    /// <summary>FillPolygon.</summary>
    Polygon,

    /// <summary>DrawLineShadowed: a 1.75 wide round-capped line with three shadow lines below.</summary>
    Line,

    /// <summary>DrawEllipseShadowed: a 5 wide dot at Points[0] with three shadow dots below.</summary>
    Dot,
}

public sealed record GraphOp(GraphOpKind Kind, ArgbColor Color, IReadOnlyList<(float X, float Y)> Points);

/// <summary>
/// What the Graph draws in its area, in drawing order, unflipped (y down from the top of the
/// graph area). With Flip Graph the app mirrors every y (height - y); shadows still go down.
/// </summary>
public sealed record GraphPicture(IReadOnlyList<GraphOp> Ops, bool Flip, ArgbColor ShadowsColor);

/// <summary>
/// LiveSplit.Graph GraphComponent (93a11b85): Calculate, then DrawUnflipped as a list of drawing
/// operations, with upstream's float arithmetic.
/// </summary>
public static class GraphLogic
{
    private const double GraphEdgeValueMs = 200;
    private const float GraphEdgeMin = 5;

    /// <summary>GraphCompositeComponent.ComponentName.</summary>
    public static string Name(string settingsComparison) =>
        "Graph" + (settingsComparison == ComparisonSwitching.CurrentComparisonSetting
            ? ""
            : " (" + StandardComparisonGeneratorsFactory.GetShortComparisonName(settingsComparison) + ")");

    public static GraphData Calculate(LiveSplitState state, GraphComponentSettings settings, LayoutSettings layout)
    {
        string comparison = ComparisonSwitching.ResolveComponentComparison(state, settings.Comparison);
        TimingMethod method = state.CurrentTimingMethod;

        // CalculateFinalSplit.
        TimeSpan? finalSplit = TimeSpan.Zero;
        if (settings.IsLiveGraph)
        {
            if (state.CurrentPhase != TimerPhase.NotRunning)
            {
                finalSplit = state.CurrentTime[method] ?? state.CurrentTime.RealTime;
            }
        }
        else
        {
            foreach (Segment segment in state.Run.Segments)
            {
                if (segment.SplitTime[method] is not null)
                {
                    finalSplit = segment.SplitTime[method];
                }
            }
        }

        // CalculateDeltas.
        var deltas = new List<TimeSpan?>();
        TimeSpan max = TimeSpan.Zero;
        TimeSpan min = TimeSpan.Zero;
        foreach (Segment segment in state.Run.Segments)
        {
            TimeSpan? time = segment.SplitTime[method] - ComparisonTime(segment, comparison, method);
            if (time > max)
            {
                max = time.Value;
            }

            if (time < min)
            {
                min = time.Value;
            }

            deltas.Add(time);
        }

        // CheckLiveSegmentDelta.
        bool live = false;
        if (settings.IsLiveGraph && state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused && state.CurrentSplit is { } current)
        {
            TimeSpan? bestSeg = LiveSplitStateHelper.CheckLiveDelta(state, layout, true, comparison, method);
            TimeSpan? curSplit = ComparisonTime(current, comparison, method);
            TimeSpan? curTime = state.CurrentTime[method];
            if (bestSeg is null && curSplit is not null && curTime - curSplit > min)
            {
                bestSeg = curTime - curSplit;
            }

            if (bestSeg is { } liveDelta)
            {
                if (liveDelta > max)
                {
                    max = liveDelta;
                }

                if (liveDelta < min)
                {
                    min = liveDelta;
                }

                deltas.Add(liveDelta);
                live = true;
            }
        }

        return new GraphData(deltas, finalSplit, max, min, live);
    }

    /// <summary>GraphComponent.DrawUnflipped for an area of width x height.</summary>
    public static GraphPicture Draw(
        LiveSplitState state,
        GraphComponentSettings settings,
        GraphData data,
        float width,
        float height)
    {
        var ops = new List<GraphOp>();
        TimeSpan totalDelta = data.MinDelta - data.MaxDelta;

        // CalculateMiddleAndGraphEdge.
        float graphEdge = 0;
        float graphHeight = height / 2.0f;
        float middle = graphHeight;
        if (totalDelta != TimeSpan.Zero)
        {
            graphEdge = (float)(GraphEdgeValueMs / (-totalDelta.TotalMilliseconds + (GraphEdgeValueMs * 2))
                * ((graphHeight * 2) - (GraphEdgeMin * 2)));
            graphEdge += GraphEdgeMin;
            middle = (float)((-(data.MaxDelta.TotalMilliseconds / totalDelta.TotalMilliseconds) * (graphHeight - graphEdge) * 2) + graphEdge);
        }

        // DrawGreenAndRedGraphPortions.
        ops.Add(Rect(settings.BehindGraphColor, 0, 0, width, middle));
        ops.Add(Rect(settings.AheadGraphColor, 0, middle, width, (graphHeight * 2) - middle));

        // CalculateGridlines.
        double gridValueX = -1;
        if (state.CurrentPhase != TimerPhase.NotRunning && data.FinalSplit > TimeSpan.Zero)
        {
            double finalMs = data.FinalSplit.Value.TotalMilliseconds;
            gridValueX = 1000;
            while (finalMs / gridValueX > width / 20)
            {
                gridValueX *= 6;
            }

            gridValueX = gridValueX / finalMs * width;
        }

        double gridValueY = -1;
        if (state.CurrentPhase != TimerPhase.NotRunning && totalDelta < TimeSpan.Zero)
        {
            gridValueY = 1000;
            while ((-totalDelta.TotalMilliseconds) / gridValueY > (graphHeight - graphEdge) * 2 / 20)
            {
                gridValueY *= 6;
            }

            gridValueY = gridValueY / (-totalDelta.TotalMilliseconds) * (graphHeight - graphEdge) * 2;
        }

        // DrawGridlines.
        if (gridValueX > 0)
        {
            for (double x = gridValueX; x < width; x += gridValueX)
            {
                ops.Add(new GraphOp(GraphOpKind.Gridline, settings.GridlinesColor, [((float)x, 0), ((float)x, graphHeight * 2)]));
            }
        }

        for (float y = middle - 1; y > 0; y -= (float)gridValueY)
        {
            ops.Add(new GraphOp(GraphOpKind.Gridline, settings.GridlinesColor, [(0, y), (width, y)]));
            if (gridValueY < 0)
            {
                break;
            }
        }

        for (float y = middle; y < graphHeight * 2; y += (float)gridValueY)
        {
            ops.Add(new GraphOp(GraphOpKind.Gridline, settings.GridlinesColor, [(0, y), (width, y)]));
            if (gridValueY < 0)
            {
                break;
            }
        }

        new GraphDrawing(state, settings, data, width, totalDelta, graphEdge, graphHeight, middle, ops).DrawGraph();
        return new GraphPicture(ops, settings.FlipGraph, settings.ShadowsColor);
    }

    private static GraphOp Rect(ArgbColor color, float x, float y, float w, float h) =>
        new(GraphOpKind.Rectangle, color, [(x, y), (x + w, y + h)]);

    private static TimeSpan? ComparisonTime(Segment segment, string comparison, TimingMethod method) =>
        segment.Comparisons.TryGetValue(comparison, out SplitTime time) ? time[method] : null;

    /// <summary>GraphComponent.DrawGraph and its helpers.</summary>
    private sealed class GraphDrawing(
        LiveSplitState state,
        GraphComponentSettings settings,
        GraphData data,
        float width,
        TimeSpan totalDelta,
        float graphEdge,
        float graphHeight,
        float middle,
        List<GraphOp> ops)
    {
        private readonly IReadOnlyList<TimeSpan?> _deltas = data.Deltas;
        private readonly TimingMethod _method = state.CurrentTimingMethod;
        private ArgbColor _brush = settings.GraphColor;

        public void DrawGraph()
        {
            var circles = new List<(float X, float Y)>();
            if (_deltas.Count == 0)
            {
                return;
            }

            float heightOne = graphHeight;
            if (totalDelta != TimeSpan.Zero)
            {
                heightOne = (float)(((-data.MaxDelta.TotalMilliseconds) / totalDelta.TotalMilliseconds
                    * (graphHeight - graphEdge) * 2) + graphEdge);
            }

            float heightTwo = 0;
            float widthOne = 0;
            float widthTwo = 0;
            int y = 0;
            var points = new List<(float X, float Y)> { (0, middle) };
            circles.Add((widthOne, heightOne));
            while (y < _deltas.Count)
            {
                while (_deltas[y] is null && y < _deltas.Count - 1)
                {
                    y++;
                }

                if (_deltas[y] is not null)
                {
                    RightSide(ref heightTwo, ref widthTwo, y);
                    FillBeneathGraph(heightOne, heightTwo, widthOne, widthTwo, y, points);
                    circles.Add((widthTwo, heightTwo));
                    LeftSide(ref heightOne, ref widthOne, y);
                }
                else
                {
                    FinalPolygon(points);
                }

                y++;
            }

            CirclesAndLines(circles);
        }

        private void CirclesAndLines(List<(float X, float Y)> circles)
        {
            int i = _deltas.Count - 1;
            circles.Reverse();
            (float X, float Y) previous = circles[0];
            circles.RemoveAt(0);
            foreach ((float X, float Y) circle in circles)
            {
                while (_deltas[i] is null)
                {
                    i--;
                }

                ArgbColor color = settings.GraphColor;
                bool finalDelta = previous.X == width && data.IsLiveDeltaActive;
                if (!finalDelta && settings.ShowBestSegments && LiveSplitStateHelper.CheckBestSegment(state, i, _method))
                {
                    color = settings.GraphGoldColor;
                }

                ops.Add(new GraphOp(GraphOpKind.Line, color, [previous, circle]));
                if (!finalDelta)
                {
                    ops.Add(new GraphOp(GraphOpKind.Dot, color, [previous]));
                }

                previous = circle;
                i--;
            }
        }

        private void LeftSide(ref float heightOne, ref float widthOne, int y)
        {
            heightOne = totalDelta == TimeSpan.Zero
                ? graphHeight
                : ((float)((_deltas[y]!.Value.TotalMilliseconds - data.MaxDelta.TotalMilliseconds) / totalDelta.TotalMilliseconds)
                    * (graphHeight - graphEdge) * 2) + graphEdge;
            if (y != _deltas.Count - 1 && state.Run[y].SplitTime[_method] is { } split)
            {
                widthOne = (float)(split.TotalMilliseconds / data.FinalSplit!.Value.TotalMilliseconds * width);
            }
        }

        private void RightSide(ref float heightTwo, ref float widthTwo, int y)
        {
            if (y == _deltas.Count - 1 && data.IsLiveDeltaActive)
            {
                widthTwo = width;
            }
            else if (state.Run[y].SplitTime[_method] is { } split)
            {
                widthTwo = (float)(split.TotalMilliseconds / data.FinalSplit!.Value.TotalMilliseconds * width);
            }

            heightTwo = totalDelta != TimeSpan.Zero
                ? (float)(((_deltas[y]!.Value.TotalMilliseconds - data.MaxDelta.TotalMilliseconds) / totalDelta.TotalMilliseconds
                    * (graphHeight - graphEdge) * 2) + graphEdge)
                : graphHeight;
        }

        private void FillBeneathGraph(
            float heightOne,
            float heightTwo,
            float widthOne,
            float widthTwo,
            int y,
            List<(float X, float Y)> points)
        {
            bool liveSegment = y == _deltas.Count - 1 && data.IsLiveDeltaActive;
            if ((heightTwo - middle) / (heightOne - middle) > 0)
            {
                // AddFillOneSide.
                if (liveSegment)
                {
                    _brush = heightTwo > middle ? settings.PartialFillColorAhead : settings.PartialFillColorBehind;
                    Fill([(widthOne, middle), (widthOne, heightOne), (widthTwo, heightTwo), (widthTwo, middle)]);
                }
                else
                {
                    points.Add((widthTwo, heightTwo));
                }
            }
            else
            {
                float ratio = (heightOne - middle) / (heightOne - heightTwo);
                if (float.IsNaN(ratio))
                {
                    ratio = 0.0f;
                }

                float cross = widthOne + ((widthTwo - widthOne) * ratio);

                // AddFillFirstHalf.
                if (liveSegment)
                {
                    _brush = heightOne > middle ? settings.PartialFillColorAhead : settings.PartialFillColorBehind;
                    if (totalDelta != TimeSpan.Zero)
                    {
                        Fill([(widthOne, middle), (widthOne, heightOne), (cross, middle)]);
                    }
                }
                else
                {
                    points.Add((cross, middle));
                    _brush = heightOne > middle ? settings.CompleteFillColorAhead : settings.CompleteFillColorBehind;
                    Fill([.. points]);
                }

                // AddFillSecondHalf.
                if (liveSegment)
                {
                    _brush = heightTwo > middle ? settings.PartialFillColorAhead : settings.PartialFillColorBehind;
                    if (totalDelta != TimeSpan.Zero)
                    {
                        Fill([(cross, middle), (widthTwo, heightTwo), (widthTwo, middle)]);
                    }
                }
                else
                {
                    _brush = heightTwo > middle ? settings.CompleteFillColorAhead : settings.CompleteFillColorBehind;
                    points.Clear();
                    points.Add((cross, middle));
                    points.Add((widthTwo, heightTwo));
                }
            }

            if (y == _deltas.Count - 1)
            {
                FinalPolygon(points);
            }
        }

        private void FinalPolygon(List<(float X, float Y)> points)
        {
            points.Add((points[^1].X, middle));
            if (points.Count > 1)
            {
                _brush = points[^2].Y > middle ? settings.CompleteFillColorAhead : settings.CompleteFillColorBehind;
                Fill([.. points]);
            }
        }

        private void Fill(IReadOnlyList<(float X, float Y)> polygon) =>
            ops.Add(new GraphOp(GraphOpKind.Polygon, _brush, polygon));
    }
}
