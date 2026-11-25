using Avalonia;
using Avalonia.Media;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>Renders Splits rows from ColumnData list (Windows Splits component column model).</summary>
internal static class SplitsColumnRenderer
{
    internal static double ColumnWidth(ColumnData column) =>
        column.Type is ColumnType.Delta or ColumnType.SegmentDelta or ColumnType.DeltaorSplitTime
            ? LayoutMetrics.DeltaColumnWidth
            : LayoutMetrics.TimeColumnWidth;

    internal static IBrush CurrentSplitBrush(SplitsComponentSettings splitsSettings)
    {
        var top = splitsSettings.CurrentSplitTopColor;
        var bottom = splitsSettings.CurrentSplitBottomColor;
        Color c1 = Color.FromArgb(top.A, top.R, top.G, top.B);
        Color c2 = Color.FromArgb(bottom.A, bottom.R, bottom.G, bottom.B);
        string g = splitsSettings.CurrentSplitGradient;
        if (string.Equals(g, "Plain", StringComparison.OrdinalIgnoreCase))
        {
            return new SolidColorBrush(c1);
        }

        bool horizontal = string.Equals(g, "Horizontal", StringComparison.OrdinalIgnoreCase);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = horizontal
                ? new RelativePoint(1, 0, RelativeUnit.Relative)
                : new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(c1, 0),
                new GradientStop(c2, 1),
            ],
        };
    }
}
