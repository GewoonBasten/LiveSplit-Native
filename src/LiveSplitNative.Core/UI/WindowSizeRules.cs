namespace LiveSplitNative.Core.UI;

/// <summary>
/// Windows TimerForm's size rules (93a11b85), in one place:
/// <list type="bullet">
/// <item>The layout is drawn at one uniform scale, primary extent / OverallSize (vertical: the
/// window height). The other axis is simply the space the components get, so a narrower window
/// gives split names less room (they are cut with an ellipsis), not smaller text.</item>
/// <item>MinimumSize: 25 on the secondary axis, max(OverallSize / 5, 25) on the primary one.</item>
/// <item>MaintainMinimumSize: the secondary extent never gets below what the components need
/// (ComponentRenderer.MinimumWidth, the widest component minimum) at the current scale. A window
/// made narrower than that shrinks its primary extent instead, so the whole timer gets smaller
/// and nothing overlaps.</item>
/// </list>
/// </summary>
public static class WindowSizeRules
{
    public const double MinimumFloor = BorderlessWindowGeometry.WindowsMinFloor;

    /// <summary>TimerForm.FixSize's MinimumSize for the window.</summary>
    public static (double Width, double Height) MinimumSize(LayoutMode mode, double overallSize)
    {
        double primary = BorderlessWindowGeometry.WindowsMinExtent(overallSize);
        return mode == LayoutMode.Vertical ? (MinimumFloor, primary) : (primary, MinimumFloor);
    }

    /// <summary>
    /// TimerForm.MaintainMinimumSize. Vertical: when the width is below the components' minimum
    /// width at the current scale (height / OverallSize), the height becomes
    /// height / (minimumWidth / width), rounded as Windows does; horizontal mirrors it with the
    /// minimum height and the width. The result then respects <see cref="MinimumSize"/>, as
    /// WinForms does for any size it is given.
    /// </summary>
    public static (double Width, double Height) MaintainMinimumSize(
        LayoutMode mode,
        double width,
        double height,
        double overallSize,
        double componentsMinimumWidth,
        double componentsMinimumHeight)
    {
        double overall = Math.Max(1, overallSize);
        if (mode == LayoutMode.Vertical)
        {
            double minimumWidth = componentsMinimumWidth * (height / overall);
            if (width > 0 && width < minimumWidth)
            {
                height = (int)((height / (minimumWidth / width)) + 0.5);
            }
        }
        else
        {
            double minimumHeight = componentsMinimumHeight * (width / overall);
            if (height > 0 && height < minimumHeight)
            {
                width = (int)((width / (minimumHeight / height)) + 0.5);
            }
        }

        (double minWidth, double minHeight) = MinimumSize(mode, overall);
        return (Math.Max(width, minWidth), Math.Max(height, minHeight));
    }

    /// <summary>Windows Layout.InvalidSize: no window size saved for that orientation yet.</summary>
    public const int InvalidSize = -1;

    /// <summary>
    /// TimerForm.editor_OrientationSwitched, with <see cref="Layout.Mode"/> already the new
    /// orientation: the orientation being left keeps the window's current size, and the new one
    /// gets Windows' first size when it has none (vertical 300 x OverallSize, horizontal
    /// OverallSize x 45; OverallSize is the new orientation's).
    /// </summary>
    public static void SwitchOrientation(Layout layout, int windowWidth, int windowHeight, double overallSize)
    {
        int overall = (int)(overallSize + 0.5);
        if (layout.Mode == LayoutMode.Vertical)
        {
            layout.HorizontalWidth = windowWidth;
            layout.HorizontalHeight = windowHeight;
            if (layout.VerticalWidth <= 0 || layout.VerticalHeight <= 0)
            {
                layout.VerticalWidth = 300;
                layout.VerticalHeight = overall;
            }
        }
        else
        {
            layout.VerticalWidth = windowWidth;
            layout.VerticalHeight = windowHeight;
            if (layout.HorizontalWidth <= 0 || layout.HorizontalHeight <= 0)
            {
                layout.HorizontalWidth = overall;
                layout.HorizontalHeight = 45;
            }
        }
    }

    /// <summary>
    /// TimerForm.editor_LayoutResized (a component added or removed): the other orientation's
    /// saved size no longer fits the components, so it is dropped and the next switch to that
    /// orientation starts from Windows' first size.
    /// </summary>
    public static void InvalidateOtherOrientation(Layout layout)
    {
        if (layout.Mode == LayoutMode.Horizontal)
        {
            layout.VerticalWidth = InvalidSize;
            layout.VerticalHeight = InvalidSize;
        }
        else
        {
            layout.HorizontalWidth = InvalidSize;
            layout.HorizontalHeight = InvalidSize;
        }
    }

    /// <summary>
    /// The largest window size, as Windows' maximum tracking size: the virtual screen (all
    /// screens together). WinForms never gives a form more than that, whatever size is set.
    /// </summary>
    public static (double Width, double Height) MaximumSize(IReadOnlyList<BorderlessWindowRect> screens)
    {
        if (screens.Count == 0)
        {
            return (double.PositiveInfinity, double.PositiveInfinity);
        }

        double left = screens.Min(s => s.X);
        double top = screens.Min(s => s.Y);
        double right = screens.Max(s => s.X + s.Width);
        double bottom = screens.Max(s => s.Y + s.Height);
        return (Math.Max(MinimumFloor, right - left), Math.Max(MinimumFloor, bottom - top));
    }

    /// <summary>
    /// TimerForm.SetLayout: a window with less than 32 x 32 (or its whole size, if smaller)
    /// on every screen is moved into the working area of the screen nearest to it. Returns the
    /// top-left corner (screen coordinates, y down).
    /// </summary>
    public static (double X, double Y) KeepVisible(
        BorderlessWindowRect bounds,
        IReadOnlyList<BorderlessWindowRect> screens,
        IReadOnlyList<BorderlessWindowRect> workingAreas)
    {
        const double minimumVisibleSize = 32;
        double minVisibleWidth = Math.Min(minimumVisibleSize, bounds.Width);
        double minVisibleHeight = Math.Min(minimumVisibleSize, bounds.Height);
        if (screens.Count == 0 || screens.Any(s =>
            {
                (double w, double h) = Intersection(s, bounds);
                return w >= minVisibleWidth && h >= minVisibleHeight;
            }))
        {
            return (bounds.X, bounds.Y);
        }

        BorderlessWindowRect area = workingAreas[ScreenIndexFor(bounds, screens)];
        return (
            Math.Max(area.X, Math.Min(bounds.X, area.X + area.Width - bounds.Width)),
            Math.Max(area.Y, Math.Min(bounds.Y, area.Y + area.Height - bounds.Height)));
    }

    /// <summary>
    /// macOS addition after an orientation switch (Basten, 12 Oct 2026: back in vertical the
    /// timer was below the screen): the window keeps its size but moves, as little as needed, to
    /// lie inside the working area of the screen it is mostly on. A window larger than that area
    /// starts at its left / top edge. Windows only applies <see cref="KeepVisible"/>.
    /// </summary>
    public static (double X, double Y) FitInWorkingArea(
        BorderlessWindowRect bounds,
        IReadOnlyList<BorderlessWindowRect> screens,
        IReadOnlyList<BorderlessWindowRect> workingAreas)
    {
        if (screens.Count == 0)
        {
            return (bounds.X, bounds.Y);
        }

        BorderlessWindowRect area = workingAreas[ScreenIndexFor(bounds, screens)];
        double x = bounds.Width >= area.Width
            ? area.X
            : Math.Clamp(bounds.X, area.X, area.X + area.Width - bounds.Width);
        double y = bounds.Height >= area.Height
            ? area.Y
            : Math.Clamp(bounds.Y, area.Y, area.Y + area.Height - bounds.Height);
        return (x, y);
    }

    /// <summary>Screen.FromRectangle: the screen with the largest overlap, else the nearest one.</summary>
    private static int ScreenIndexFor(BorderlessWindowRect bounds, IReadOnlyList<BorderlessWindowRect> screens)
    {
        int best = 0;
        double bestArea = -1;
        double bestDistance = double.PositiveInfinity;
        double cx = bounds.X + (bounds.Width / 2);
        double cy = bounds.Y + (bounds.Height / 2);
        for (int i = 0; i < screens.Count; i++)
        {
            (double w, double h) = Intersection(screens[i], bounds);
            double area = w * h;
            BorderlessWindowRect s = screens[i];
            double dx = Math.Max(0, Math.Max(s.X - cx, cx - (s.X + s.Width)));
            double dy = Math.Max(0, Math.Max(s.Y - cy, cy - (s.Y + s.Height)));
            double distance = (dx * dx) + (dy * dy);
            if (area > bestArea || (area == bestArea && area <= 0 && distance < bestDistance))
            {
                best = i;
                bestArea = area;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static (double Width, double Height) Intersection(BorderlessWindowRect a, BorderlessWindowRect b)
    {
        double w = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
        double h = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
        return (Math.Max(0, w), Math.Max(0, h));
    }

    /// <summary>
    /// <paramref name="rect"/> (screen coordinates, y down) at the size the rules gave it during a
    /// resize from <paramref name="edge"/>: the edges that are not being dragged stay where they
    /// are, so the window gives way only at the edge under the mouse. With no edge (a size change
    /// that does not come from a drag) the top-left corner stays, as WinForms keeps Location.
    /// </summary>
    public static BorderlessWindowRect Resize(
        BorderlessWindowRect rect,
        BorderlessResizeEdge? edge,
        double width,
        double height)
    {
        bool west = edge is BorderlessResizeEdge.West or BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.SouthWest;
        bool north = edge is BorderlessResizeEdge.North or BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.NorthEast;
        return new BorderlessWindowRect(
            west ? rect.X + rect.Width - width : rect.X,
            north ? rect.Y + rect.Height - height : rect.Y,
            width,
            height);
    }
}
