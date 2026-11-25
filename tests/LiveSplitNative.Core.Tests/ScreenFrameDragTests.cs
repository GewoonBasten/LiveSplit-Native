using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>F2: move and resize in screen points (y up), one whole frame per pointer event.</summary>
public class ScreenFrameDragTests
{
    private static readonly ScreenFrame Start = new(547, 96, 697, 840);

    [Theory]
    [InlineData(BorderlessResizeEdge.North)]
    [InlineData(BorderlessResizeEdge.South)]
    [InlineData(BorderlessResizeEdge.East)]
    [InlineData(BorderlessResizeEdge.West)]
    [InlineData(BorderlessResizeEdge.NorthWest)]
    [InlineData(BorderlessResizeEdge.NorthEast)]
    [InlineData(BorderlessResizeEdge.SouthWest)]
    [InlineData(BorderlessResizeEdge.SouthEast)]
    public void The_Edges_Not_Dragged_Never_Move(BorderlessResizeEdge edge)
    {
        var drag = ScreenFrameDrag.StartResize(edge, Start, 1000, 500);
        var rng = new Random((int)edge);
        for (int i = 0; i < 500; i++)
        {
            ScreenFrame f = drag.FrameFor(1000 + (rng.NextDouble() * 1600) - 800, 500 + (rng.NextDouble() * 1200) - 600, 25, 25);
            string name = edge.ToString();
            if (!name.Contains("West", StringComparison.Ordinal))
            {
                Assert.Equal(Start.X, f.X);
            }

            if (!name.Contains("East", StringComparison.Ordinal))
            {
                Assert.Equal(Start.Right, f.Right, 9);
            }

            if (!name.Contains("North", StringComparison.Ordinal))
            {
                Assert.Equal(Start.Top, f.Top, 9);
            }

            if (!name.Contains("South", StringComparison.Ordinal))
            {
                Assert.Equal(Start.Y, f.Y);
            }

            Assert.True(f.Width >= 25 && f.Height >= 25);
        }
    }

    [Fact]
    public void Dragging_Up_Grows_The_Top_And_Down_Grows_The_Bottom()
    {
        ScreenFrame north = ScreenFrameDrag.StartResize(BorderlessResizeEdge.North, Start, 900, 936).FrameFor(900, 956, 25, 25);
        Assert.Equal(new ScreenFrame(547, 96, 697, 860), north);
        ScreenFrame south = ScreenFrameDrag.StartResize(BorderlessResizeEdge.South, Start, 900, 96).FrameFor(900, 86, 25, 25);
        Assert.Equal(new ScreenFrame(547, 86, 697, 850), south);
        ScreenFrame west = ScreenFrameDrag.StartResize(BorderlessResizeEdge.West, Start, 547, 500).FrameFor(500, 500, 25, 25);
        Assert.Equal(new ScreenFrame(500, 96, 744, 840), west);
    }

    [Fact]
    public void The_Top_Stops_Under_The_Menu_Bar_So_The_Bottom_Stays()
    {
        // The S1 NE drag: mouse to the top of a 1080 pt screen whose visible top is 1050.
        var drag = ScreenFrameDrag.StartResize(BorderlessResizeEdge.NorthEast, Start, 1238.68, 930.35);
        ScreenFrame f = drag.FrameFor(1112.93, 1080, 25, 25, visibleTop: 1050);
        Assert.Equal(96, f.Y);
        Assert.Equal(1050, f.Top);
        Assert.Equal(Start.X, f.X);

        // Without the limit the top would be 1080 and AppKit would push the frame down.
        Assert.Equal(1080 - 930.35 + Start.Top, drag.FrameFor(1112.93, 1080, 25, 25).Top, 6);

        // A window that already reaches above the limit keeps its height; it is never shrunk.
        var high = ScreenFrameDrag.StartResize(BorderlessResizeEdge.North, new ScreenFrame(0, 500, 300, 560), 10, 1060);
        Assert.Equal(1060, high.FrameFor(10, 1070, 25, 25, visibleTop: 1050).Top);
    }

    [Fact]
    public void Min_Size_Holds_And_West_Keeps_The_Right_Edge()
    {
        var drag = ScreenFrameDrag.StartResize(BorderlessResizeEdge.SouthWest, Start, 547, 96);
        ScreenFrame f = drag.FrameFor(5000, 5000, 120, 60);
        Assert.Equal(120, f.Width);
        Assert.Equal(60, f.Height);
        Assert.Equal(Start.Right, f.Right);
        Assert.Equal(Start.Top, f.Top);
    }

    [Fact]
    public void A_Move_Follows_The_Mouse_Exactly()
    {
        var drag = ScreenFrameDrag.StartMove(Start, 700.25, 400.5);
        Assert.True(drag.IsMove);
        Assert.Equal(new ScreenFrame(557.5, 86.25, 697, 840), drag.FrameFor(710.75, 390.75, 25, 25));
    }
}
