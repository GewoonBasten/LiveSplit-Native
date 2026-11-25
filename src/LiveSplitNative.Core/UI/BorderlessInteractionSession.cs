namespace LiveSplitNative.Core.UI;

public enum BorderlessPointerAction
{
    None,
    Move,
    Resize,
    ContextMenu,
}

/// <summary>
/// Deterministic interaction state for the borderless timer. All coordinates supplied
/// to one session must use the same unit (DIPs for resize, screen pixels for move).
/// Layout/rendering never participates.
/// </summary>
public readonly record struct BorderlessInteractionSession
{
    private BorderlessInteractionSession(
        BorderlessPointerAction action,
        BorderlessResizeEdge? edge,
        double windowX,
        double windowY,
        double width,
        double height,
        double pointerScreenX,
        double pointerScreenY)
    {
        Action = action;
        Edge = edge;
        WindowX = windowX;
        WindowY = windowY;
        Width = width;
        Height = height;
        PointerScreenX = pointerScreenX;
        PointerScreenY = pointerScreenY;
    }

    public BorderlessPointerAction Action { get; }
    public BorderlessResizeEdge? Edge { get; }
    public double WindowX { get; }
    public double WindowY { get; }
    public double Width { get; }
    public double Height { get; }
    public double PointerScreenX { get; }
    public double PointerScreenY { get; }

    public static BorderlessInteractionSession StartMove(
        double windowX,
        double windowY,
        double pointerScreenX,
        double pointerScreenY) =>
        new(
            BorderlessPointerAction.Move,
            null,
            windowX,
            windowY,
            0,
            0,
            pointerScreenX,
            pointerScreenY);

    public static BorderlessInteractionSession StartResize(
        BorderlessResizeEdge edge,
        double windowX,
        double windowY,
        double width,
        double height,
        double pointerScreenX,
        double pointerScreenY) =>
        new(
            BorderlessPointerAction.Resize,
            edge,
            windowX,
            windowY,
            width,
            height,
            pointerScreenX,
            pointerScreenY);

    public BorderlessWindowRect MoveTo(double pointerScreenX, double pointerScreenY)
    {
        if (Action != BorderlessPointerAction.Move)
        {
            throw new InvalidOperationException("Interaction is not a move.");
        }

        return new BorderlessWindowRect(
            WindowX + pointerScreenX - PointerScreenX,
            WindowY + pointerScreenY - PointerScreenY,
            0,
            0);
    }

    public BorderlessWindowRect ResizeTo(
        double pointerScreenX,
        double pointerScreenY,
        double minWidth,
        double minHeight)
    {
        if (Action != BorderlessPointerAction.Resize || Edge is not { } edge)
        {
            throw new InvalidOperationException("Interaction is not a resize.");
        }

        var (x, y, width, height) = BorderlessWindowGeometry.ApplyResize(
            WindowX,
            WindowY,
            Width,
            Height,
            edge,
            pointerScreenX - PointerScreenX,
            pointerScreenY - PointerScreenY,
            minWidth,
            minHeight);
        return new BorderlessWindowRect(x, y, width, height);
    }

    public static BorderlessPointerAction RoutePointerPress(
        bool isLeftButton,
        bool isRightButton,
        double localX,
        double localY,
        double width,
        double height,
        bool allowMoving,
        bool allowResizing)
    {
        if (isRightButton)
        {
            return BorderlessPointerAction.ContextMenu;
        }

        if (!isLeftButton)
        {
            return BorderlessPointerAction.None;
        }

        if (allowResizing
            && BorderlessWindowGeometry.HitResizeEdge(localX, localY, width, height) is not null)
        {
            return BorderlessPointerAction.Resize;
        }

        return allowMoving ? BorderlessPointerAction.Move : BorderlessPointerAction.None;
    }
}

public readonly record struct BorderlessWindowRect(
    double X,
    double Y,
    double Width,
    double Height);
