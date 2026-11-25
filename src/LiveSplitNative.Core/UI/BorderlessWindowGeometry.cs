namespace LiveSplitNative.Core.UI;

/// <summary>
/// Pure geometry for borderless timer move/resize (Windows TimerForm RESIZE_HANDLE_SIZE = 10).
/// Avalonia BeginResizeDrag is unreliable on macOS SystemDecorations=None — callers apply
/// <see cref="ApplyResize"/> from pointer deltas to real Width/Height/Position.
/// </summary>
public enum BorderlessResizeEdge
{
    North,
    South,
    East,
    West,
    NorthWest,
    NorthEast,
    SouthWest,
    SouthEast,
}

public static class BorderlessWindowGeometry
{
    public const double ResizeHandleDip = 10;
    /// <summary>Windows TimerForm MinimumSize floor (both axes).</summary>
    public const int WindowsMinFloor = 25;
    public const int DefaultMinWidth = WindowsMinFloor;
    public const int DefaultMinHeightFloor = WindowsMinFloor;
    public const int MaxReasonableSize = 10000;

    /// <summary>Windows FixSize: min extent = max((int)(OverallSize / 5 + 0.5), 25).</summary>
    public static double WindowsMinExtent(double overallSize) =>
        Math.Max(WindowsMinFloor, (int)((overallSize / 5.0) + 0.5));

    /// <summary>Windows PaintForm: scaleFactor = windowExtent / OverallSize.</summary>
    public static double UniformScaleFactor(double windowExtent, double overallSize) =>
        overallSize < 1 || windowExtent < 1 ? 1 : windowExtent / overallSize;

    /// <summary>Windows transformedWidth/Height = windowExtent / scaleFactor.</summary>
    public static double LayoutExtentForWindow(double windowExtent, double scale) =>
        scale < 1e-6 ? windowExtent : windowExtent / scale;

    /// <summary>
    /// Windows TimerForm.FixSize: when OverallSize changes, scale the window's primary
    /// extent by the same ratio so paint scale (Height/OverallSize) stays constant —
    /// e.g. Add Component grows the form instead of exploding zoom.
    /// </summary>
    public static double FixSizePrimaryExtent(double oldOverall, double newOverall, double currentPrimary) =>
        oldOverall < 1 || newOverall < 1 || Math.Abs(oldOverall - newOverall) < 0.5
            ? currentPrimary
            : Math.Max(WindowsMinFloor, Math.Round(newOverall / oldOverall * currentPrimary));

    /// <summary>
    /// OverallSize = configured metrics. Measure may only <b>raise</b> it (timer bleed);
    /// never shrink from a partial/LTC-tainted measure (that made scale = Height/tiny ≫ 1).
    /// </summary>
    public static double RefineOverallSize(double estimated, double measured) =>
        measured > estimated + 0.5 ? measured : estimated;

    /// <summary>
    /// Reject restore sizes that imply absurd zoom from prior bugs (saved Height ≫ OverallSize).
    /// Windows initial fit is ≈ OverallSize; intentional stretch remains available by resizing.
    /// </summary>
    public const double MaxRestoredScale = 1.5;

    public static int ClampRestoredPrimaryExtent(int savedPrimary, double overallSize)
    {
        if (overallSize < 1 || savedPrimary < 1)
        {
            return savedPrimary;
        }

        double scale = UniformScaleFactor(savedPrimary, overallSize);
        if (scale <= MaxRestoredScale)
        {
            return savedPrimary;
        }

        return Math.Max(WindowsMinFloor, (int)Math.Round(overallSize));
    }

    public static BorderlessResizeEdge? HitResizeEdge(
        double localX,
        double localY,
        double width,
        double height,
        double handleDip = ResizeHandleDip)
    {
        if (width < 1 || height < 1)
        {
            return null;
        }

        double h = handleDip;
        bool left = localX <= h;
        bool right = localX >= width - h;
        bool top = localY <= h;
        bool bottom = localY >= height - h;

        if (top && left) return BorderlessResizeEdge.NorthWest;
        if (top && right) return BorderlessResizeEdge.NorthEast;
        if (bottom && left) return BorderlessResizeEdge.SouthWest;
        if (bottom && right) return BorderlessResizeEdge.SouthEast;
        if (top) return BorderlessResizeEdge.North;
        if (bottom) return BorderlessResizeEdge.South;
        if (left) return BorderlessResizeEdge.West;
        if (right) return BorderlessResizeEdge.East;
        return null;
    }

    /// <summary>
    /// Apply screen-space pointer delta to a start rect. Clamps to min size; when the west/north
    /// edge is clamped, position stays consistent with the clamped size.
    /// </summary>
    public static (double X, double Y, double Width, double Height) ApplyResize(
        double startX,
        double startY,
        double startWidth,
        double startHeight,
        BorderlessResizeEdge edge,
        double deltaX,
        double deltaY,
        double minWidth,
        double minHeight)
    {
        double x = startX;
        double y = startY;
        double w = startWidth;
        double h = startHeight;

        bool east = edge is BorderlessResizeEdge.East or BorderlessResizeEdge.NorthEast or BorderlessResizeEdge.SouthEast;
        bool west = edge is BorderlessResizeEdge.West or BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.SouthWest;
        bool south = edge is BorderlessResizeEdge.South or BorderlessResizeEdge.SouthWest or BorderlessResizeEdge.SouthEast;
        bool north = edge is BorderlessResizeEdge.North or BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.NorthEast;

        if (east)
        {
            w = Math.Max(minWidth, startWidth + deltaX);
        }
        else if (west)
        {
            w = Math.Max(minWidth, startWidth - deltaX);
            x = startX + (startWidth - w);
        }

        if (south)
        {
            h = Math.Max(minHeight, startHeight + deltaY);
        }
        else if (north)
        {
            h = Math.Max(minHeight, startHeight - deltaY);
            y = startY + (startHeight - h);
        }

        return (x, y, w, h);
    }

    /// <summary>
    /// Recover zero/negative/absurd persisted sizes to a safe fallback at or above mins.
    /// </summary>
    public static (int Width, int Height) SanitizeSavedSize(
        int? savedWidth,
        int? savedHeight,
        int fallbackWidth,
        int fallbackHeight,
        int minWidth = DefaultMinWidth,
        int minHeight = DefaultMinHeightFloor)
    {
        int width = savedWidth is > 0 and <= MaxReasonableSize ? savedWidth.Value : fallbackWidth;
        int height = savedHeight is > 0 and <= MaxReasonableSize ? savedHeight.Value : fallbackHeight;
        if (width < minWidth) width = Math.Max(minWidth, fallbackWidth);
        if (height < minHeight) height = Math.Max(minHeight, fallbackHeight);
        return (width, height);
    }

    /// <summary>
    /// True when a short window would need scale &lt; 1. With Windows uniform ScaleTransform this is
    /// expected (not a crush). Kept for tests / detecting the old per-control Height×scale path.
    /// </summary>
    public static bool WouldCrushContent(double availableExtent, double naturalContentExtent) =>
        naturalContentExtent > 0.5 && availableExtent + 0.5 < naturalContentExtent;

    /// <summary>
    /// Unscaled row stack vs unscaled host — overlap only if natural layout is wrong (not after
    /// uniform root scale, which shrinks the whole composition together).
    /// </summary>
    public static bool RowsOverlapHost(double hostHeight, IEnumerable<double> rowHeights, double separatorTotal = 0)
    {
        double needed = separatorTotal + rowHeights.Sum();
        return hostHeight + 0.5 < needed;
    }
}
