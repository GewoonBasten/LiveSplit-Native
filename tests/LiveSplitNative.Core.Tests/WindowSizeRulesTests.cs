using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>Windows TimerForm FixSize / MaintainMinimumSize.</summary>
public class WindowSizeRulesTests
{
    [Fact]
    public void Minimum_Size_Is_25_And_A_Fifth_Of_The_Layout()
    {
        Assert.Equal((25, 104), WindowSizeRules.MinimumSize(LayoutMode.Vertical, 519));
        Assert.Equal((104, 25), WindowSizeRules.MinimumSize(LayoutMode.Horizontal, 519));
        Assert.Equal((25, 25), WindowSizeRules.MinimumSize(LayoutMode.Vertical, 60));
    }

    [Fact]
    public void Wide_Enough_Windows_Keep_Their_Size()
    {
        // 519 tall at scale 1 needs 180 wide; 286 is fine.
        Assert.Equal((286, 519), WindowSizeRules.MaintainMinimumSize(LayoutMode.Vertical, 286, 519, 519, 180, 25));
        Assert.Equal((180, 519), WindowSizeRules.MaintainMinimumSize(LayoutMode.Vertical, 180, 519, 519, 180, 25));
    }

    [Fact]
    public void Too_Narrow_Shrinks_The_Whole_Timer_So_The_Content_Still_Fits()
    {
        // 120 wide where the components need 180 at scale 1: height 519 * 120 / 180 = 346.
        (double w, double h) = WindowSizeRules.MaintainMinimumSize(LayoutMode.Vertical, 120, 519, 519, 180, 25);
        Assert.Equal(120, w);
        Assert.Equal(346, h);
        // The scale is now 346 / 519, so the components get 120 / (346 / 519) = 180 logical width.
        Assert.True(w / (h / 519) >= 179.5);

        // Already scaled down (height 260): the minimum is 180 * 260 / 519 = 90.2, so 80 wide shrinks.
        Assert.Equal((80, 231), WindowSizeRules.MaintainMinimumSize(LayoutMode.Vertical, 80, 260, 519, 180, 25));
    }

    [Fact]
    public void The_Minimum_Size_Still_Wins_At_The_Extreme()
    {
        (double w, double h) = WindowSizeRules.MaintainMinimumSize(LayoutMode.Vertical, 10, 519, 519, 180, 25);
        Assert.Equal(25, w);
        Assert.Equal(104, h);
    }

    [Fact]
    public void Horizontal_Mirrors_It_With_The_Height()
    {
        // Horizontal: width / OverallSize is the scale; too short a window narrows it.
        Assert.Equal((300, 30), WindowSizeRules.MaintainMinimumSize(LayoutMode.Horizontal, 600, 30, 600, 300, 60));
    }

    [Fact]
    public void A_Rule_Size_Keeps_The_Edges_That_Are_Not_Dragged()
    {
        var rect = new BorderlessWindowRect(100, 200, 120, 519);
        // East / south drags: the top-left corner stays.
        Assert.Equal(new BorderlessWindowRect(100, 200, 120, 346), WindowSizeRules.Resize(rect, BorderlessResizeEdge.East, 120, 346));
        Assert.Equal(new BorderlessWindowRect(100, 200, 120, 346), WindowSizeRules.Resize(rect, null, 120, 346));
        // A west drag keeps the right edge, a north drag the bottom edge.
        Assert.Equal(new BorderlessWindowRect(120, 200, 100, 519), WindowSizeRules.Resize(rect, BorderlessResizeEdge.West, 100, 519));
        Assert.Equal(new BorderlessWindowRect(100, 373, 120, 346), WindowSizeRules.Resize(rect, BorderlessResizeEdge.North, 120, 346));
        Assert.Equal(new BorderlessWindowRect(120, 373, 100, 346), WindowSizeRules.Resize(rect, BorderlessResizeEdge.NorthWest, 100, 346));
    }

    [Fact]
    public void A_Frame_Drag_Gives_Way_Only_At_The_Edge_Under_The_Mouse()
    {
        // AppKit frames: y grows upward, the top edge is Y + Height.
        var start = new ScreenFrame(100, 300, 286, 519);
        var east = ScreenFrameDrag.StartResize(BorderlessResizeEdge.East, start, 386, 500);
        ScreenFrame e = east.Resized(new ScreenFrame(100, 300, 120, 519), 120, 346);
        Assert.Equal(start.Top, e.Top);
        Assert.Equal(start.X, e.X);
        Assert.Equal(346, e.Height);

        var north = ScreenFrameDrag.StartResize(BorderlessResizeEdge.North, start, 200, 819);
        ScreenFrame n = north.Resized(new ScreenFrame(100, 300, 286, 700), 286, 600);
        Assert.Equal(300, n.Y);
        Assert.Equal(900, n.Top);

        var west = ScreenFrameDrag.StartResize(BorderlessResizeEdge.SouthWest, start, 100, 300);
        ScreenFrame w = west.Resized(new ScreenFrame(266, 280, 120, 539), 120, 360);
        Assert.Equal(386, w.Right);
        Assert.Equal(819, w.Top);
    }
}
