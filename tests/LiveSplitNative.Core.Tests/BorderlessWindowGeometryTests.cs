using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class BorderlessWindowGeometryTests
{
    [Theory]
    [InlineData(5, 50, BorderlessResizeEdge.West)]
    [InlineData(195, 50, BorderlessResizeEdge.East)]
    [InlineData(100, 5, BorderlessResizeEdge.North)]
    [InlineData(100, 195, BorderlessResizeEdge.South)]
    [InlineData(3, 3, BorderlessResizeEdge.NorthWest)]
    [InlineData(197, 3, BorderlessResizeEdge.NorthEast)]
    [InlineData(3, 197, BorderlessResizeEdge.SouthWest)]
    [InlineData(197, 197, BorderlessResizeEdge.SouthEast)]
    public void HitResizeEdge_edges_and_corners(double x, double y, BorderlessResizeEdge expected)
    {
        Assert.Equal(expected, BorderlessWindowGeometry.HitResizeEdge(x, y, 200, 200));
    }

    [Fact]
    public void HitResizeEdge_interior_is_move_not_resize()
    {
        Assert.Null(BorderlessWindowGeometry.HitResizeEdge(100, 100, 200, 200));
        Assert.Null(BorderlessWindowGeometry.HitResizeEdge(11, 11, 200, 200));
    }

    [Fact]
    public void ApplyResize_south_east_grows_size_keeps_origin()
    {
        var (x, y, w, h) = BorderlessWindowGeometry.ApplyResize(
            40, 60, 200, 180, BorderlessResizeEdge.SouthEast, 30, 40, 120, 80);
        Assert.Equal(40, x);
        Assert.Equal(60, y);
        Assert.Equal(230, w);
        Assert.Equal(220, h);
    }

    [Fact]
    public void ApplyResize_north_west_moves_origin_and_clamps_min()
    {
        var (x, y, w, h) = BorderlessWindowGeometry.ApplyResize(
            100, 100, 150, 100, BorderlessResizeEdge.NorthWest, 80, 50, 120, 80);
        // startW-dx = 70 → clamp 120; startH-dy = 50 → clamp 80
        Assert.Equal(120, w);
        Assert.Equal(80, h);
        Assert.Equal(100 + (150 - 120), x);
        Assert.Equal(100 + (100 - 80), y);
    }

    [Fact]
    public void ApplyResize_corners_are_diagonal()
    {
        var se = BorderlessWindowGeometry.ApplyResize(0, 0, 200, 200, BorderlessResizeEdge.SouthEast, 10, 15, 50, 50);
        Assert.Equal((0, 0, 210, 215), se);

        var nw = BorderlessWindowGeometry.ApplyResize(50, 50, 200, 200, BorderlessResizeEdge.NorthWest, -10, -15, 50, 50);
        Assert.Equal((40, 35, 210, 215), nw);
    }

    [Fact]
    public void SanitizeSavedSize_recovers_invalid()
    {
        Assert.Equal((286, 220), BorderlessWindowGeometry.SanitizeSavedSize(0, -5, 286, 220, 25, 44));
        Assert.Equal((286, 220), BorderlessWindowGeometry.SanitizeSavedSize(99999, 5, 286, 220, 25, 44));
        Assert.Equal((300, 400), BorderlessWindowGeometry.SanitizeSavedSize(300, 400, 286, 220, 25, 44));
        Assert.Equal((286, 220), BorderlessWindowGeometry.SanitizeSavedSize(10, 10, 286, 220, 25, 44));
    }

    [Fact]
    public void WindowsMinExtent_is_overall_over_five()
    {
        Assert.Equal(25, BorderlessWindowGeometry.WindowsMinExtent(100));
        Assert.Equal(44, BorderlessWindowGeometry.WindowsMinExtent(220));
        Assert.Equal(25, BorderlessWindowGeometry.WindowsMinExtent(10));
    }

    [Fact]
    public void UniformScaleFactor_matches_windows_paint()
    {
        Assert.Equal(0.5, BorderlessWindowGeometry.UniformScaleFactor(110, 220), 3);
        Assert.Equal(1.0, BorderlessWindowGeometry.UniformScaleFactor(220, 220), 3);
        Assert.Equal(440.0, BorderlessWindowGeometry.LayoutExtentForWindow(220, 0.5), 3);
    }

    [Fact]
    public void WouldCrushContent_detects_b0b8328_scale_path()
    {
        Assert.True(BorderlessWindowGeometry.WouldCrushContent(80, 220));
        Assert.False(BorderlessWindowGeometry.WouldCrushContent(220, 220));
        Assert.False(BorderlessWindowGeometry.WouldCrushContent(300, 220));
    }

    [Fact]
    public void RowsOverlapHost_when_fixed_rows_exceed_host()
    {
        double[] rows = [24, 24, 24, 24]; // 96
        Assert.True(BorderlessWindowGeometry.RowsOverlapHost(60, rows, separatorTotal: 4));
        Assert.False(BorderlessWindowGeometry.RowsOverlapHost(120, rows, separatorTotal: 4));
    }

    [Fact]
    public void FixSizePrimaryExtent_preserves_scale_when_overall_grows()
    {
        // Window 220 tall at OverallSize 220 (scale 1) → add 22px component → Height 242.
        Assert.Equal(242, BorderlessWindowGeometry.FixSizePrimaryExtent(220, 242, 220));
        // Scale 2 preserved: 400 tall at 200 overall → overall 250 → height 500.
        Assert.Equal(500, BorderlessWindowGeometry.FixSizePrimaryExtent(200, 250, 400));
        Assert.Equal(220, BorderlessWindowGeometry.FixSizePrimaryExtent(220, 220, 220));
    }

    [Fact]
    public void RefineOverallSize_never_shrinks_below_estimate()
    {
        Assert.Equal(220, BorderlessWindowGeometry.RefineOverallSize(220, 40));
        Assert.Equal(260, BorderlessWindowGeometry.RefineOverallSize(220, 260));
        Assert.Equal(220, BorderlessWindowGeometry.RefineOverallSize(220, 220));
    }

    [Fact]
    public void ClampRestoredPrimaryExtent_rejects_giant_saved_zoom()
    {
        Assert.Equal(220, BorderlessWindowGeometry.ClampRestoredPrimaryExtent(744, 220));
        Assert.Equal(300, BorderlessWindowGeometry.ClampRestoredPrimaryExtent(300, 220));
        Assert.Equal(330, BorderlessWindowGeometry.ClampRestoredPrimaryExtent(330, 220)); // scale 1.5 ok
    }
}
