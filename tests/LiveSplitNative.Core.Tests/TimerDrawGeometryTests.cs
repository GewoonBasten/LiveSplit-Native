using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>Timer.DrawGeneral's sizing (LiveSplit.Timer, 93a11b85), used by both Detailed Timer timers.</summary>
public class TimerDrawGeometryTests
{
    // "88:88:88" at the default 43.75 px Century Gothic Bold is about 168 wide; ".23" at 35 / 50.
    private const double Measure = 168;
    private const double Decimals = 42;

    [Fact]
    public void Decimals_Font_Is_A_Fiftieth_Of_The_Timer_Font_Per_Step()
    {
        Assert.Equal(43.75, TimerDrawGeometry.DecimalsFontSize(43.75, 50), 6);
        Assert.Equal(30.625, TimerDrawGeometry.DecimalsFontSize(43.75, 35), 6);
        Assert.Equal(8.75, TimerDrawGeometry.DecimalsFontSize(43.75, 10), 6);
    }

    [Fact]
    public void Height_Decides_Until_The_88_88_88_Box_Would_Not_Fit()
    {
        double box = TimerDrawGeometry.UnscaledWidth(Measure, Decimals);
        Assert.Equal(221, box);

        // Default Detailed Timer (Height 75, Segment Timer Size 40) in a 286 wide window:
        // main band 45 -> scale 1, segment band 30 -> 2/3.
        Assert.Equal(1, TimerDrawGeometry.Scale(286, 45, box), 6);
        Assert.Equal(30 / 45.0, TimerDrawGeometry.Scale(286, 30, box), 6);

        // Narrow: the width decides, the same for both bands, whatever their height.
        double narrow = (150 - 14) / (box - 14);
        Assert.Equal(narrow, TimerDrawGeometry.Scale(150, 90, box), 6);
        Assert.Equal(narrow, TimerDrawGeometry.Scale(150, 120, box), 6);
    }

    [Fact]
    public void Bigger_Decimals_Only_Shrink_A_Width_Limited_Timer()
    {
        double small = TimerDrawGeometry.UnscaledWidth(Measure, 12);
        double big = TimerDrawGeometry.UnscaledWidth(Measure, 60);
        // Height-limited: the decimals do not matter.
        Assert.Equal(TimerDrawGeometry.Scale(400, 45, small), TimerDrawGeometry.Scale(400, 45, big), 6);
        // Width-limited: bigger decimals, smaller timer.
        Assert.True(TimerDrawGeometry.Scale(200, 90, big) < TimerDrawGeometry.Scale(200, 90, small));
    }

    [Fact]
    public void Text_Ends_Seven_In_From_The_Right_Plus_The_Scale()
    {
        Assert.Equal(286 - 7 + 1.5, TimerDrawGeometry.TextRight(286, 221, 1.5, centerTimer: false), 6);
        // Centered: the whole box (with its 88:88:88 room) is centered; the text ends 6 in from it.
        Assert.Equal(((286 + 221) / 2.0) - 6, TimerDrawGeometry.TextRight(286, 221, 1, centerTimer: true), 6);
    }

    [Fact]
    public void Baseline_Sits_Below_The_Band_Middle_By_Half_Ascent_Minus_Descent()
    {
        // Bundled Century Gothic: usWinAscent 1122, usWinDescent 307 per 1000. A 30 px segment
        // band at scale 2/3 draws 29.17 px digits: baseline 15 + 0.4075 x 29.17 = 26.9.
        Assert.Equal(15 + (0.4075 * 29.17), TimerDrawGeometry.BaselineY(0, 30, 29.17, 1.122, 0.307), 6);
        Assert.Equal(100 + 22.5, TimerDrawGeometry.BaselineY(100, 45, 10, 1, 1), 6);
    }

    [Fact]
    public void Display_Two_Rows_Makes_Info_Components_Taller_As_Upstream()
    {
        var layout = StandardLayoutFactory.Create();
        LayoutComponent sob = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.SumOfBestPath);
        layout.Components.Clear();
        layout.Components.Add(sob);
        double one = TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize;
        var settings = SumOfBestComponentSettings.FromInnerXml(sob.SettingsInnerXml);
        settings.Display2Rows = true;
        sob.SettingsInnerXml = settings.ToInnerXml();
        double two = TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize;
        Assert.Equal(31, one);
        double expected = 0.9 * (TimerLayoutMetrics.LineHeight(layout.Settings.TimesFontSizePx) + TimerLayoutMetrics.LineHeight(layout.Settings.TextFontSizePx));
        Assert.Equal(expected, two, 6);
    }

    [Fact]
    public void Column_Labels_Row_Is_As_High_As_A_Split_Row()
    {
        // LabelsComponent.VerticalHeight = 25 + Split Height (Basten 17 Oct: the labels row).
        var layout = StandardLayoutFactory.Create();
        LayoutComponent splits = layout.Components.Single(c => c.Path == StandardLayoutFactory.SplitsPath);
        layout.Components.Clear();
        layout.Components.Add(splits);
        var settings = SplitsComponentSettings.FromInnerXml(splits.SettingsInnerXml);
        settings.ShowThinSeparators = false;
        settings.ShowBlankSplits = true;
        settings.SplitHeight = 6;
        splits.SettingsInnerXml = settings.ToInnerXml();
        double without = TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize;
        settings.ShowColumnLabels = true;
        splits.SettingsInnerXml = settings.ToInnerXml();
        double with = TimerLayoutMetrics.FromLayout(layout, 3)[0].VerticalSize;
        Assert.Equal(31, with - without, 6);
    }
}
