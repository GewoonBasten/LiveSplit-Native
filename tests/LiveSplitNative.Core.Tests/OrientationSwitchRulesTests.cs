using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// Windows TimerForm.editor_OrientationSwitched / editor_LayoutResized / SetLayout, and the
/// maximum size WinForms gives a form (Basten, 12 Oct 2026: back in vertical the timer was below
/// the screen and rendering failed).
/// </summary>
public class OrientationSwitchRulesTests
{
    private static readonly BorderlessWindowRect Main = new(0, 0, 1920, 1080);
    private static readonly BorderlessWindowRect MainWork = new(0, 30, 1920, 1050);
    private static readonly BorderlessWindowRect Side = new(1920, 0, 2560, 1440);
    private static readonly BorderlessWindowRect SideWork = new(1920, 30, 2560, 1410);

    [Fact]
    public void Switching_To_Horizontal_Keeps_The_Vertical_Size_And_Starts_At_Overall_By_45()
    {
        var layout = new Layout { Mode = LayoutMode.Horizontal, VerticalWidth = 286, VerticalHeight = 360 };
        WindowSizeRules.SwitchOrientation(layout, 286, 519, 3098.6);
        Assert.Equal((286, 519), (layout.VerticalWidth, layout.VerticalHeight));
        Assert.Equal((3099, 45), (layout.HorizontalWidth, layout.HorizontalHeight));
    }

    [Fact]
    public void Switching_To_Vertical_Keeps_The_Horizontal_Size_And_Starts_At_300_By_Overall()
    {
        var layout = new Layout
        {
            Mode = LayoutMode.Vertical,
            VerticalWidth = WindowSizeRules.InvalidSize,
            VerticalHeight = WindowSizeRules.InvalidSize,
        };
        WindowSizeRules.SwitchOrientation(layout, 1200, 60, 610.6);
        Assert.Equal((1200, 60), (layout.HorizontalWidth, layout.HorizontalHeight));
        Assert.Equal((300, 611), (layout.VerticalWidth, layout.VerticalHeight));
    }

    [Fact]
    public void A_Saved_Size_For_The_New_Orientation_Is_Used_As_Is()
    {
        var layout = new Layout
        {
            Mode = LayoutMode.Vertical,
            VerticalWidth = 250,
            VerticalHeight = 700,
            HorizontalWidth = 900,
            HorizontalHeight = 80,
        };
        WindowSizeRules.SwitchOrientation(layout, 950, 90, 610.6);
        Assert.Equal((250, 700), (layout.VerticalWidth, layout.VerticalHeight));
        Assert.Equal((950, 90), (layout.HorizontalWidth, layout.HorizontalHeight));
    }

    [Fact]
    public void Adding_Or_Removing_A_Component_Drops_The_Other_Orientations_Size()
    {
        var vertical = new Layout { Mode = LayoutMode.Vertical, HorizontalWidth = 900, HorizontalHeight = 80 };
        WindowSizeRules.InvalidateOtherOrientation(vertical);
        Assert.Equal((-1, -1), (vertical.HorizontalWidth, vertical.HorizontalHeight));
        Assert.Equal((286, 360), (vertical.VerticalWidth, vertical.VerticalHeight));

        var horizontal = new Layout { Mode = LayoutMode.Horizontal, HorizontalWidth = 900, HorizontalHeight = 80 };
        WindowSizeRules.InvalidateOtherOrientation(horizontal);
        Assert.Equal((-1, -1), (horizontal.VerticalWidth, horizontal.VerticalHeight));
        Assert.Equal((900, 80), (horizontal.HorizontalWidth, horizontal.HorizontalHeight));
    }

    [Fact]
    public void The_Window_Is_Never_Larger_Than_All_Screens_Together()
    {
        Assert.Equal((4480, 1440), WindowSizeRules.MaximumSize([Main, Side]));
        Assert.Equal((1920, 1080), WindowSizeRules.MaximumSize([Main]));
        Assert.Equal((double.PositiveInfinity, double.PositiveInfinity), WindowSizeRules.MaximumSize([]));
    }

    [Fact]
    public void Set_Layout_Moves_Only_A_Window_That_Is_Hardly_On_Any_Screen()
    {
        // 40 px visible: stays where it is.
        Assert.Equal((100d, 1040d), WindowSizeRules.KeepVisible(new(100, 1040, 286, 519), [Main], [MainWork]));
        // 20 px visible: into the working area.
        Assert.Equal((100d, 561d), WindowSizeRules.KeepVisible(new(100, 1060, 286, 519), [Main], [MainWork]));
        // Off every screen to the right of the side screen: into the side screen's working area.
        Assert.Equal((4194d, 200d), WindowSizeRules.KeepVisible(new(5000, 200, 286, 519), [Main, Side], [MainWork, SideWork]));
    }

    [Fact]
    public void After_A_Switch_The_Window_Moves_Inside_Its_Screen()
    {
        // A horizontal bar at the bottom of the side screen becomes a 286 x 1198 vertical window:
        // it moves up so its bottom is the working area's bottom (Basten's case).
        Assert.Equal((1960d, 242d), WindowSizeRules.FitInWorkingArea(new(1960, 1296, 286, 1198), [Main, Side], [MainWork, SideWork]));
        // Already inside: unchanged.
        Assert.Equal((100d, 100d), WindowSizeRules.FitInWorkingArea(new(100, 100, 286, 519), [Main, Side], [MainWork, SideWork]));
        // Taller than the working area: its top at the working area's top.
        Assert.Equal((100d, 30d), WindowSizeRules.FitInWorkingArea(new(100, 400, 286, 1300), [Main], [MainWork]));
        // Wider than the screen: its left at the working area's left.
        Assert.Equal((0d, 900d), WindowSizeRules.FitInWorkingArea(new(300, 900, 3099, 45), [Main], [MainWork]));
    }
}
