namespace LiveSplitNative.Core.UI;

/// <summary>A window frame in screen points with y growing upward (AppKit: origin at the main screen's bottom-left).</summary>
public readonly record struct ScreenFrame(double X, double Y, double Width, double Height)
{
    public double Top => Y + Height;

    public double Right => X + Width;
}

/// <summary>
/// F2 window move and resize (spike S1 mode 3, the one that passed): everything in screen points
/// from where the drag started, so every pointer event gives one whole frame (origin and size
/// together) and the edges not dragged cannot drift. Windows TimerForm moves and resizes the
/// borderless form itself in the same way.
/// </summary>
public readonly record struct ScreenFrameDrag(
    BorderlessResizeEdge? Edge,
    ScreenFrame StartFrame,
    double StartMouseX,
    double StartMouseY)
{
    public static ScreenFrameDrag StartMove(ScreenFrame frame, double mouseX, double mouseY) =>
        new(null, frame, mouseX, mouseY);

    public static ScreenFrameDrag StartResize(BorderlessResizeEdge edge, ScreenFrame frame, double mouseX, double mouseY) =>
        new(edge, frame, mouseX, mouseY);

    public bool IsMove => Edge is null;

    /// <summary>
    /// The frame for this mouse position. A resize keeps the opposite edges exactly where they
    /// started and never goes below the minimum size. The top edge stops at
    /// <paramref name="visibleTop"/> (the screen's top under the menu bar): AppKit does not let a
    /// window's top go higher and would push the whole frame down instead, which moved the
    /// bottom edge 36 pt in the S1 log.
    /// </summary>
    public ScreenFrame FrameFor(double mouseX, double mouseY, double minWidth, double minHeight, double? visibleTop = null)
    {
        double dx = mouseX - StartMouseX;
        double dy = mouseY - StartMouseY;
        ScreenFrame f = StartFrame;
        if (Edge is not { } edge)
        {
            return f with { X = f.X + dx, Y = f.Y + dy };
        }

        bool east = edge is BorderlessResizeEdge.East or BorderlessResizeEdge.NorthEast or BorderlessResizeEdge.SouthEast;
        bool west = edge is BorderlessResizeEdge.West or BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.SouthWest;
        bool north = edge is BorderlessResizeEdge.North or BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.NorthEast;
        bool south = edge is BorderlessResizeEdge.South or BorderlessResizeEdge.SouthWest or BorderlessResizeEdge.SouthEast;

        double x = f.X;
        double y = f.Y;
        double width = f.Width;
        double height = f.Height;
        if (east)
        {
            width = Math.Max(minWidth, f.Width + dx);
        }
        else if (west)
        {
            width = Math.Max(minWidth, f.Width - dx);
            x = f.Right - width;
        }

        // y grows upward: the top edge is Y + Height, the bottom edge is Y.
        if (north)
        {
            height = Math.Max(minHeight, f.Height + dy);
            if (visibleTop is { } top)
            {
                double limit = Math.Max(top, f.Top);
                height = Math.Max(minHeight, Math.Min(height, limit - f.Y));
            }
        }
        else if (south)
        {
            height = Math.Max(minHeight, f.Height - dy);
            y = f.Top - height;
        }

        return new ScreenFrame(x, y, width, height);
    }

    /// <summary>
    /// <paramref name="frame"/> at the size the window size rules gave it: the edges this drag
    /// does not move stay where they are (the top unless the top edge is dragged, the left unless
    /// the left edge is), so only the edge under the mouse gives way.
    /// </summary>
    public ScreenFrame Resized(ScreenFrame frame, double width, double height)
    {
        bool west = Edge is BorderlessResizeEdge.West or BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.SouthWest;
        bool north = Edge is BorderlessResizeEdge.North or BorderlessResizeEdge.NorthWest or BorderlessResizeEdge.NorthEast;
        return new ScreenFrame(
            west ? frame.Right - width : frame.X,
            north ? frame.Y : frame.Top - height,
            width,
            height);
    }
}
