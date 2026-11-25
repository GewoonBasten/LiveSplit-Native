namespace LiveSplitNative.App;

/// <summary>
/// Shared horizontal metrics so Splits columns, Timer, Title attempts, and
/// Previous Segment / SoB / Text values share a Windows-like right edge.
/// Timer pad is intentionally a hair tighter than the split Time column (Win GDI).
/// </summary>
internal static class LayoutMetrics
{
    public const double PadLeft = 5;
    /// <summary>Right inset for split Time / delta cells and info-row values (~6px on Wind Waker ref).</summary>
    public const double SplitsPadRight = 6;
    /// <summary>Big timer right inset — match Time column (Windows uses ~6–7px adjustValue).</summary>
    public const double TimerPadRight = 6;
    /// <summary>Title attempt counter right inset (Windows Title uses width-5).</summary>
    public const double TitlePadRight = 5;
    public const double PadRight = SplitsPadRight;
    public const double DeltaColumnWidth = 52;
    public const double TimeColumnWidth = 56;
    public const double InfoValueWidth = TimeColumnWidth;
    /// <summary>
    /// Fallback when settings are unavailable. Windows single-row:
    /// VerticalHeight = SplitHeight + 25 (default SplitHeight 3.6 → ~28.6).
    /// </summary>
    public const double SplitRowHeight = 28.6;
    /// <summary>Windows SplitsSettings.IconSize default (square box; Stretch=Uniform inside).</summary>
    public const double IconSize = 24;
    /// <summary>Title game icon — square slot matching Windows title icon box.</summary>
    public const double TitleIconSize = 32;

    /// <summary>
    /// Windows SplitComponent.DrawVertical (single-row): SplitHeight + 25.
    /// Icons are drawn centered in this height and must not change it.
    /// </summary>
    public static double RowHeightFor(float splitHeight) =>
        Math.Max(18, splitHeight + 25);

    /// <summary>
    /// Clamp icon draw size so it fits inside the row with a 1px breathing room.
    /// </summary>
    public static double IconDrawSize(float iconSize, double rowHeight) =>
        Math.Clamp(iconSize, 8, Math.Max(8, rowHeight - 2));
}
