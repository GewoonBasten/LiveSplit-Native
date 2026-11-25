using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class TimerLayoutSnapshotTests
{
    [Fact]
    public void Default_layout_uses_authoritative_windows_geometry()
    {
        Layout layout = StandardLayoutFactory.Create();

        Assert.Equal(286, layout.VerticalWidth);
        Assert.Equal(519, layout.VerticalHeight);

        TimerLayoutSnapshot snapshot = TimerLayoutSnapshot.Create(
            layout.Mode,
            layout.VerticalWidth,
            layout.VerticalHeight,
            TimerLayoutMetrics.FromLayout(layout, segmentCount: 43));

        Assert.Equal(layout.VerticalHeight / snapshot.OverallSize, snapshot.Scale, 6);
        Assert.Equal(layout.VerticalWidth / snapshot.Scale, snapshot.LogicalWidth, 6);
        Assert.Equal(snapshot.OverallSize, snapshot.LogicalHeight, 6);
        Assert.Equal(layout.Components.Count, snapshot.Slots.Count);
        Assert.False(TimerLayoutSnapshot.HasOverlap(snapshot.Slots));

        TimerLayoutSnapshot shortRun = TimerLayoutSnapshot.Create(
            layout.Mode,
            layout.VerticalWidth,
            layout.VerticalHeight,
            TimerLayoutMetrics.FromLayout(layout, segmentCount: 3));
        Assert.Equal(snapshot.OverallSize, shortRun.OverallSize);
    }

    [Theory]
    [InlineData(120, 80)]
    [InlineData(690, 157)]
    [InlineData(1200, 900)]
    [InlineData(80, 1400)]
    public void Vertical_extreme_sizes_have_one_finite_uniform_scale(double width, double height)
    {
        Layout layout = StandardLayoutFactory.Create();
        TimerLayoutSnapshot snapshot = TimerLayoutSnapshot.Create(
            LayoutMode.Vertical,
            width,
            height,
            TimerLayoutMetrics.FromLayout(layout, segmentCount: 43));

        Assert.True(double.IsFinite(snapshot.Scale));
        Assert.True(snapshot.Scale > 0);
        Assert.Equal(snapshot.Scale, snapshot.ScaleX);
        Assert.Equal(snapshot.Scale, snapshot.ScaleY);
        Assert.Equal(width, snapshot.LogicalWidth * snapshot.Scale, 6);
        Assert.Equal(height, snapshot.LogicalHeight * snapshot.Scale, 6);
        Assert.False(TimerLayoutSnapshot.HasOverlap(snapshot.Slots));
    }

    [Fact]
    public void Add_and_remove_component_preserves_primary_scale()
    {
        Layout layout = StandardLayoutFactory.Create();
        IReadOnlyList<TimerComponentMetric> before = TimerLayoutMetrics.FromLayout(layout, segmentCount: 43);
        TimerLayoutSnapshot first = TimerLayoutSnapshot.Create(
            LayoutMode.Vertical, 286, 519, before);

        layout.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.SumOfBestPath));
        IReadOnlyList<TimerComponentMetric> after = TimerLayoutMetrics.FromLayout(layout, segmentCount: 43);
        double resizedHeight = BorderlessWindowGeometry.FixSizePrimaryExtent(
            first.OverallSize,
            TimerLayoutSnapshot.CalculateOverallSize(LayoutMode.Vertical, after),
            519);
        TimerLayoutSnapshot second = TimerLayoutSnapshot.Create(
            LayoutMode.Vertical, 286, resizedHeight, after);

        // Window extents are persisted as whole DIPs; FixSize can differ by at most
        // one DIP after preserving the previous scale.
        Assert.InRange(
            Math.Abs(first.Scale - second.Scale),
            0,
            1 / second.OverallSize);

        layout.Components.RemoveAt(layout.Components.Count - 1);
        double restoredHeight = BorderlessWindowGeometry.FixSizePrimaryExtent(
            second.OverallSize,
            TimerLayoutSnapshot.CalculateOverallSize(LayoutMode.Vertical, before),
            resizedHeight);
        Assert.Equal(519, restoredHeight);
    }

    [Fact]
    public void Component_settings_expose_only_the_layout_primary_axis()
    {
        Assert.Equal(
            ComponentSettingsSizeAxis.Height,
            ComponentSettingsSizeAxisPolicy.For(LayoutMode.Vertical));
        Assert.Equal(
            ComponentSettingsSizeAxis.Width,
            ComponentSettingsSizeAxisPolicy.For(LayoutMode.Horizontal));
    }

    [Fact]
    public void Detailed_timer_height_growth_preserves_sibling_scale_and_reaches_surface_end()
    {
        Layout layout = StandardLayoutFactory.Create();
        LayoutComponent detailed =
            StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.DetailedTimerPath);
        layout.Components.Add(detailed);

        IReadOnlyList<TimerComponentMetric> beforeMetrics =
            TimerLayoutMetrics.FromLayout(layout, segmentCount: 43);
        TimerLayoutSnapshot before = TimerLayoutSnapshot.Create(
            LayoutMode.Vertical,
            layout.VerticalWidth,
            layout.VerticalHeight,
            beforeMetrics);
        TimerLayoutSlot timerBefore = before.Slots.Single(
            slot => slot.Path == StandardLayoutFactory.TimerPath);

        DetailedTimerComponentSettings settings =
            DetailedTimerComponentSettings.FromInnerXml(detailed.SettingsInnerXml);
        settings.Height += 40;
        detailed.SettingsInnerXml = settings.ToInnerXml();
        IReadOnlyList<TimerComponentMetric> afterMetrics =
            TimerLayoutMetrics.FromLayout(layout, segmentCount: 43);
        double resizedHeight = BorderlessWindowGeometry.FixSizePrimaryExtent(
            before.OverallSize,
            TimerLayoutSnapshot.CalculateOverallSize(LayoutMode.Vertical, afterMetrics),
            layout.VerticalHeight);
        TimerLayoutSnapshot after = TimerLayoutSnapshot.Create(
            LayoutMode.Vertical,
            layout.VerticalWidth,
            resizedHeight,
            afterMetrics);
        TimerLayoutSlot timerAfter = after.Slots.Single(
            slot => slot.Path == StandardLayoutFactory.TimerPath);

        // Windows TimerForm.FixSize rounds the new height to whole pixels
        // ((int)(ratio * Height + 0.5)), and FixSizePrimaryExtent does the same, so the
        // scale is preserved up to half a pixel of window height, not exactly.
        double halfPixelScale = 0.5 / after.OverallSize;
        Assert.InRange(Math.Abs(after.Scale - before.Scale), 0, halfPixelScale);
        Assert.InRange(
            Math.Abs((timerAfter.Bounds.Height * after.Scale) - (timerBefore.Bounds.Height * before.Scale)),
            0,
            timerAfter.Bounds.Height * halfPixelScale);
        Assert.Equal(after.OverallSize, after.Slots[^1].Bounds.Bottom, 6);
        Assert.False(TimerLayoutSnapshot.HasOverlap(after.Slots));
    }

    [Fact]
    public void Detailed_timer_ratio_does_not_rescale_sibling_components()
    {
        Layout layout = StandardLayoutFactory.Create();
        LayoutComponent detailed =
            StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.DetailedTimerPath);
        layout.Components.Add(detailed);
        IReadOnlyList<TimerComponentMetric> before =
            TimerLayoutMetrics.FromLayout(layout, segmentCount: 43);

        DetailedTimerComponentSettings settings =
            DetailedTimerComponentSettings.FromInnerXml(detailed.SettingsInnerXml);
        settings.SegmentTimerSizeRatio = 80;
        detailed.SettingsInnerXml = settings.ToInnerXml();
        IReadOnlyList<TimerComponentMetric> after =
            TimerLayoutMetrics.FromLayout(layout, segmentCount: 43);

        Assert.Equal(before.ToArray(), after.ToArray());
    }

    [Fact]
    public void Nested_dialog_topmost_state_keeps_the_owner_and_restores_exactly()
    {
        var rootToEditor = new OwnedDialogTopmostState(
            OwnerBefore: true,
            ChildBefore: false);
        Assert.True(rootToEditor.OwnerWhileOpen);
        Assert.True(rootToEditor.ChildWhileOpen);

        var editorToComponent = new OwnedDialogTopmostState(
            OwnerBefore: rootToEditor.ChildWhileOpen,
            ChildBefore: false);
        Assert.True(editorToComponent.OwnerWhileOpen);
        Assert.True(editorToComponent.ChildWhileOpen);
        Assert.True(editorToComponent.OwnerBefore);
        Assert.False(editorToComponent.ChildBefore);

        Assert.True(rootToEditor.OwnerBefore);
        Assert.False(rootToEditor.ChildBefore);
    }

    [Fact]
    public void Vertical_horizontal_vertical_has_independent_deterministic_geometry()
    {
        Layout layout = StandardLayoutFactory.Create();
        IReadOnlyList<TimerComponentMetric> metrics = TimerLayoutMetrics.FromLayout(layout, segmentCount: 43);
        TimerLayoutSnapshot vertical = TimerLayoutSnapshot.Create(LayoutMode.Vertical, 286, 519, metrics);
        TimerLayoutSnapshot horizontal = TimerLayoutSnapshot.Create(
            LayoutMode.Horizontal,
            TimerLayoutSnapshot.CalculateOverallSize(LayoutMode.Horizontal, metrics),
            45,
            metrics);
        TimerLayoutSnapshot verticalAgain = TimerLayoutSnapshot.Create(LayoutMode.Vertical, 286, 519, metrics);

        Assert.Equal(1, horizontal.Scale, 6);
        Assert.Equal(45, horizontal.LogicalHeight, 6);
        Assert.Equal(vertical.Mode, verticalAgain.Mode);
        Assert.Equal(vertical.WindowWidth, verticalAgain.WindowWidth);
        Assert.Equal(vertical.WindowHeight, verticalAgain.WindowHeight);
        Assert.Equal(vertical.OverallSize, verticalAgain.OverallSize);
        Assert.Equal(vertical.Scale, verticalAgain.Scale);
        Assert.Equal(vertical.Slots, verticalAgain.Slots);
        Assert.False(TimerLayoutSnapshot.HasOverlap(horizontal.Slots));
    }

    [Fact]
    public void Restart_round_trips_geometry_through_the_layout()
    {
        // Windows keeps the timer's position and size in the .lsl, and so do we.
        Layout layout = StandardLayoutFactory.Create();
        layout.X = 236;
        layout.Y = 281;
        layout.VerticalWidth = 690;
        layout.VerticalHeight = 157;

        using var stream = new MemoryStream();
        XmlLayoutSerializer.Save(layout, stream);
        stream.Position = 0;
        Layout restored = XmlLayoutSerializer.Load(stream);

        Assert.Equal(236, restored.X);
        Assert.Equal(281, restored.Y);
        Assert.Equal(690, restored.VerticalWidth);
        Assert.Equal(157, restored.VerticalHeight);
    }

    [Theory]
    [InlineData(BorderlessResizeEdge.North)]
    [InlineData(BorderlessResizeEdge.South)]
    [InlineData(BorderlessResizeEdge.East)]
    [InlineData(BorderlessResizeEdge.West)]
    [InlineData(BorderlessResizeEdge.NorthWest)]
    [InlineData(BorderlessResizeEdge.NorthEast)]
    [InlineData(BorderlessResizeEdge.SouthWest)]
    [InlineData(BorderlessResizeEdge.SouthEast)]
    public void Mac_interaction_resize_smoke_covers_every_direction(BorderlessResizeEdge edge)
    {
        BorderlessInteractionSession session = BorderlessInteractionSession.StartResize(
            edge,
            windowX: 200,
            windowY: 150,
            width: 690,
            height: 157,
            pointerScreenX: 400,
            pointerScreenY: 300);

        BorderlessWindowRect rect = session.ResizeTo(
            pointerScreenX: 425,
            pointerScreenY: 320,
            minWidth: 25,
            minHeight: 25);

        Assert.True(rect.Width >= 25);
        Assert.True(rect.Height >= 25);
        Assert.All(new[] { rect.X, rect.Y, rect.Width, rect.Height }, AssertFinite);
    }

    [Fact]
    public void Mac_interaction_move_and_right_click_routing_are_window_space_only()
    {
        BorderlessInteractionSession move = BorderlessInteractionSession.StartMove(
            windowX: 236,
            windowY: 281,
            pointerScreenX: 500,
            pointerScreenY: 400);

        BorderlessWindowRect moved = move.MoveTo(pointerScreenX: 530, pointerScreenY: 455);
        Assert.Equal(new BorderlessWindowRect(266, 336, 0, 0), moved);

        Assert.Equal(
            BorderlessPointerAction.ContextMenu,
            BorderlessInteractionSession.RoutePointerPress(
                isLeftButton: false,
                isRightButton: true,
                localX: 200,
                localY: 80,
                width: 690,
                height: 157,
                allowMoving: true,
                allowResizing: true));
    }

    [Fact]
    public void Retina_resize_normalizes_screen_pixels_to_dips()
    {
        const double scale = 2;
        BorderlessInteractionSession session = BorderlessInteractionSession.StartResize(
            BorderlessResizeEdge.SouthEast,
            windowX: 400 / scale,
            windowY: 300 / scale,
            width: 286,
            height: 519,
            pointerScreenX: 900 / scale,
            pointerScreenY: 700 / scale);

        BorderlessWindowRect rect = session.ResizeTo(
            pointerScreenX: 950 / scale,
            pointerScreenY: 740 / scale,
            minWidth: 25,
            minHeight: 25);

        Assert.Equal(311, rect.Width);
        Assert.Equal(539, rect.Height);
        Assert.Equal(400, rect.X * scale);
        Assert.Equal(300, rect.Y * scale);
    }

    private static void AssertFinite(double value) => Assert.True(double.IsFinite(value));
}
