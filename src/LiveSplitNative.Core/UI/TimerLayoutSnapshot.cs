namespace LiveSplitNative.Core.UI;

/// <summary>
/// Immutable timer-surface geometry. Component metrics are expressed in Windows
/// LiveSplit's unscaled layout space; the window contributes exactly one uniform
/// paint scale on the layout's primary axis.
/// </summary>
public sealed record TimerLayoutSnapshot
{
    public required LayoutMode Mode { get; init; }
    public required double WindowWidth { get; init; }
    public required double WindowHeight { get; init; }
    public required double LogicalWidth { get; init; }
    public required double LogicalHeight { get; init; }
    public required double OverallSize { get; init; }
    public required double Scale { get; init; }
    public double ScaleX => Scale;
    public double ScaleY => Scale;
    public required IReadOnlyList<TimerLayoutSlot> Slots { get; init; }

    public static TimerLayoutSnapshot Create(
        LayoutMode mode,
        double windowWidth,
        double windowHeight,
        IReadOnlyList<TimerComponentMetric> metrics)
    {
        double safeWidth = Math.Max(1, windowWidth);
        double safeHeight = Math.Max(1, windowHeight);
        double overall = CalculateOverallSize(mode, metrics);
        double primary = mode == LayoutMode.Vertical ? safeHeight : safeWidth;
        double scale = primary / overall;
        double logicalWidth = mode == LayoutMode.Vertical ? safeWidth / scale : overall;
        double logicalHeight = mode == LayoutMode.Vertical ? overall : safeHeight / scale;

        var slots = new TimerLayoutSlot[metrics.Count];
        double offset = 0;
        for (int i = 0; i < metrics.Count; i++)
        {
            TimerComponentMetric metric = metrics[i];
            double length = EffectiveLength(mode, metrics, i);
            // DrawVerticalComponent: the component draws from its place's start over its whole
            // size and is shown from half the padding it shares with the component before to
            // half the padding it shares with the one after (so the places still tile).
            double size = mode == LayoutMode.Vertical ? metric.VerticalSize : metric.HorizontalSize;
            double before = SharedPaddingBefore(mode, metrics, i) / 2;
            double after = SharedPaddingAfter(mode, metrics, i) / 2;
            double shown = Math.Max(0, size - before - after);
            TimerLayoutRect bounds = mode == LayoutMode.Vertical
                ? new TimerLayoutRect(0, offset + before, logicalWidth, shown)
                : new TimerLayoutRect(offset + before, 0, shown, logicalHeight);
            TimerLayoutRect draw = mode == LayoutMode.Vertical
                ? new TimerLayoutRect(0, offset, logicalWidth, Math.Max(0, size))
                : new TimerLayoutRect(offset, 0, Math.Max(0, size), logicalHeight);
            slots[i] = new TimerLayoutSlot(metric.Path, i, bounds, draw);
            offset += length;
        }

        return new TimerLayoutSnapshot
        {
            Mode = mode,
            WindowWidth = safeWidth,
            WindowHeight = safeHeight,
            LogicalWidth = logicalWidth,
            LogicalHeight = logicalHeight,
            OverallSize = overall,
            Scale = scale,
            Slots = Array.AsReadOnly(slots),
        };
    }

    public static double CalculateOverallSize(
        LayoutMode mode,
        IReadOnlyList<TimerComponentMetric> metrics)
    {
        double total = 0;
        for (int i = 0; i < metrics.Count; i++)
        {
            total += EffectiveLength(mode, metrics, i);
        }

        return Math.Max(1, total);
    }

    public static bool HasOverlap(IReadOnlyList<TimerLayoutSlot> slots)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            for (int j = i + 1; j < slots.Count; j++)
            {
                if (slots[i].Bounds.Intersects(slots[j].Bounds))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double EffectiveLength(
        LayoutMode mode,
        IReadOnlyList<TimerComponentMetric> metrics,
        int index)
    {
        TimerComponentMetric current = metrics[index];
        double raw = mode == LayoutMode.Vertical ? current.VerticalSize : current.HorizontalSize;

        // LiveSplit ComponentRenderer subtracts twice the half-padding shared by
        // adjacent components, so padding is represented exactly once.
        return Math.Max(0, raw - SharedPaddingAfter(mode, metrics, index));
    }

    /// <summary>GetPaddingBelow / ToRight: the padding shared with the next component that has a size.</summary>
    private static double SharedPaddingAfter(LayoutMode mode, IReadOnlyList<TimerComponentMetric> metrics, int index)
    {
        TimerComponentMetric current = metrics[index];
        double trailing = mode == LayoutMode.Vertical ? current.PaddingBottom : current.PaddingRight;
        for (int i = index + 1; i < metrics.Count; i++)
        {
            TimerComponentMetric next = metrics[i];
            if ((mode == LayoutMode.Vertical ? next.VerticalSize : next.HorizontalSize) > 0)
            {
                return Math.Min(trailing, mode == LayoutMode.Vertical ? next.PaddingTop : next.PaddingLeft);
            }
        }

        return 0;
    }

    /// <summary>GetPaddingAbove / ToLeft: the padding shared with the component before that has a size.</summary>
    private static double SharedPaddingBefore(LayoutMode mode, IReadOnlyList<TimerComponentMetric> metrics, int index)
    {
        TimerComponentMetric current = metrics[index];
        double leading = mode == LayoutMode.Vertical ? current.PaddingTop : current.PaddingLeft;
        for (int i = index - 1; i >= 0; i--)
        {
            TimerComponentMetric previous = metrics[i];
            if ((mode == LayoutMode.Vertical ? previous.VerticalSize : previous.HorizontalSize) > 0)
            {
                return Math.Min(leading, mode == LayoutMode.Vertical ? previous.PaddingBottom : previous.PaddingRight);
            }
        }

        return 0;
    }
}

public readonly record struct TimerComponentMetric(
    string Path,
    double VerticalSize,
    double HorizontalSize,
    double PaddingTop = 0,
    double PaddingBottom = 0,
    double PaddingLeft = 0,
    double PaddingRight = 0);

/// <summary>
/// A component's place: <see cref="Bounds"/> is what is shown of it (the paddings it shares with
/// its neighbours split between them), <see cref="DrawBounds"/> the whole size it draws in.
/// </summary>
public readonly record struct TimerLayoutSlot(
    string Path,
    int ComponentIndex,
    TimerLayoutRect Bounds,
    TimerLayoutRect? Draw = null)
{
    public TimerLayoutRect DrawBounds => Draw ?? Bounds;
}

public readonly record struct TimerLayoutRect(
    double X,
    double Y,
    double Width,
    double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public bool Intersects(TimerLayoutRect other) =>
        X < other.Right
        && Right > other.X
        && Y < other.Bottom
        && Bottom > other.Y;
}
