namespace LiveSplitNative.Core.UI;

/// <summary>
/// Converts the supported classic components into the same primary-axis metrics
/// used by Windows LiveSplit's ComponentRenderer. No Avalonia measurement is an
/// input, so text, animation, and window size cannot feed back into OverallSize.
/// </summary>
public static class TimerLayoutMetrics
{
    /// <summary>The Title's height with Windows' default Text font (Segoe UI, 16 px).</summary>
    public const double TitleVerticalHeight = 36;
    public const double TitleHorizontalWidth = 160;
    public const double InfoVerticalHeight = 31;
    public const double InfoHorizontalWidth = 160;
    public const double TextVerticalHeight = 31;
    public const double TextHorizontalWidth = 160;

    public static IReadOnlyList<TimerComponentMetric> FromLayout(
        Layout layout,
        int segmentCount)
    {
        var metrics = new TimerComponentMetric[layout.Components.Count];
        for (int i = 0; i < layout.Components.Count; i++)
        {
            LayoutComponent component = layout.Components[i];
            metrics[i] = component.Path switch
            {
                StandardLayoutFactory.TitlePath => new(
                    component.Path,
                    TitleHeight(layout.Settings.TextFontSizePx),
                    TitleHorizontalWidth,
                    PaddingLeft: 7,
                    PaddingRight: 7),
                StandardLayoutFactory.SplitsPath => SplitsMetric(
                    component.Path,
                    SplitsComponentSettings.FromInnerXml(component.SettingsInnerXml),
                    segmentCount),
                StandardLayoutFactory.SubsplitsPath => SubsplitsMetric(
                    component.Path,
                    SubsplitsComponentSettings.FromInnerXml(component.SettingsInnerXml),
                    segmentCount),
                StandardLayoutFactory.TimerPath => TimerMetric(component),
                StandardLayoutFactory.DetailedTimerPath => DetailedTimerMetric(component),
                StandardLayoutFactory.PreviousSegmentPath => InfoMetric(
                    component.Path,
                    PreviousSegmentComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: false),
                StandardLayoutFactory.SumOfBestPath => InfoMetric(
                    component.Path,
                    SumOfBestComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: false),
                StandardLayoutFactory.PossibleTimeSavePath => InfoMetric(
                    component.Path,
                    PossibleTimeSaveComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: false),
                StandardLayoutFactory.TextPath => InfoMetric(
                    component.Path,
                    TextComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: true),
                StandardLayoutFactory.RunPredictionPath => InfoMetric(
                    component.Path,
                    RunPredictionComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: false),
                StandardLayoutFactory.DeltaPath => InfoMetric(
                    component.Path,
                    DeltaComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: false),
                StandardLayoutFactory.ComparisonTimePath => InfoMetric(
                    component.Path,
                    ComparisonTimeComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: false),
                StandardLayoutFactory.TotalPlaytimePath => InfoMetric(
                    component.Path,
                    TotalPlaytimeComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: false),
                // CurrentComparison is an InfoTextComponent: the comparison is in the Text font.
                StandardLayoutFactory.CurrentComparisonPath => InfoMetric(
                    component.Path,
                    CurrentComparisonComponentSettings.FromInnerXml(component.SettingsInnerXml).Display2Rows,
                    layout.Settings,
                    valueInTextFont: true),
                StandardLayoutFactory.CounterPath => CounterMetric(component.Path, layout.Settings),
                // ContextualSlideshowComponent: the tallest of its components (Possible Time Save,
                // Previous Segment and Run Prediction with their defaults: one information row).
                StandardLayoutFactory.ContextualSlideshowPath => InfoMetric(
                    component.Path,
                    twoRows: false,
                    layout.Settings,
                    valueInTextFont: false),
                // WorldRecordComponent draws with an InfoTextComponent: the value in the Text font
                // (the Times font with LiveSplitNative's Separate Time).
                StandardLayoutFactory.WorldRecordPath => WorldRecordMetric(component, layout),
                StandardLayoutFactory.HotkeyIndicatorPath => HotkeyIndicatorMetric(component),
                StandardLayoutFactory.GraphPath => GraphMetric(component),
                // Manual Game Time, Sound Effects and therun.gg are LogicComponents: they take no room.
                StandardLayoutFactory.ManualGameTimePath => new TimerComponentMetric(component.Path, 0, 0),
                StandardLayoutFactory.SoundPath => new TimerComponentMetric(component.Path, 0, 0),
                StandardLayoutFactory.TheRunPath => new TimerComponentMetric(component.Path, 0, 0),
                StandardLayoutFactory.AutoSplitIntegrationPath => new TimerComponentMetric(component.Path, 0, 0),
                StandardLayoutFactory.AutoSplittingRuntimePath => new TimerComponentMetric(component.Path, 0, 0),
                StandardLayoutFactory.BlankSpacePath => BlankMetric(component),
                // SeparatorComponent: 2 high (vertical) / 2 wide (horizontal), no padding.
                StandardLayoutFactory.SeparatorPath => new TimerComponentMetric(component.Path, 2, 2),
                // A component DLL: its own VerticalHeight / HorizontalWidth and paddings.
                _ when component.RuntimeState is IExternalComponent external => new TimerComponentMetric(
                    component.Path,
                    external.VerticalHeight,
                    external.HorizontalWidth,
                    external.PaddingTop,
                    external.PaddingBottom,
                    external.PaddingLeft,
                    external.PaddingRight),
                _ => new TimerComponentMetric(component.Path, 0, 0),
            };
        }

        return Array.AsReadOnly(metrics);
    }

    private static TimerComponentMetric WorldRecordMetric(LayoutComponent component, Layout layout)
    {
        var settings = WorldRecordComponentSettings.FromInnerXml(component.SettingsInnerXml);
        bool centered = settings.CenteredText && !settings.Display2Rows && layout.Mode != LayoutMode.Horizontal;
        return InfoMetric(component.Path, settings.Display2Rows, layout.Settings, valueInTextFont: centered || !settings.SeparateTime);
    }

    private static TimerComponentMetric SplitsMetric(
        string path,
        SplitsComponentSettings settings,
        int segmentCount)
    {
        int availableRows = settings.ShowBlankSplits
            ? settings.VisualSplitCount
            : Math.Min(Math.Max(0, segmentCount), settings.VisualSplitCount);
        int rows = Math.Max(1, availableRows);
        double rowHeight = LayoutMetricsRowHeight(settings.SplitHeight);
        double vertical = rows * rowHeight;
        if (settings.ShowColumnLabels)
        {
            // LabelsComponent.VerticalHeight: 25 + Split Height, like a split row.
            vertical += rowHeight;
        }

        if (settings.ShowThinSeparators)
        {
            // Preserved project decision: a thin slot also follows the final visible split.
            // When the thick last-split separator is enabled, it replaces the penultimate thin slot.
            vertical += settings.SeparatorLastSplit && rows > 1 ? rows - 1 : rows;
        }

        if (settings.SeparatorLastSplit && rows > 1)
        {
            vertical += 2;
        }

        double columns = 0;
        IReadOnlyList<ColumnData> configured = settings.Columns.Count > 0
            ? settings.Columns
            : ColumnData.CreateDefault();
        foreach (ColumnData column in configured)
        {
            columns += column.Type is ColumnType.Delta or ColumnType.SegmentDelta or ColumnType.DeltaorSplitTime
                ? 52
                : 56;
        }

        double icon = settings.DisplayIcons ? Math.Max(8, settings.IconSize) + 7 : 0;
        const double windowsSplitWidth = 20;
        double perSplit = Math.Max(80, windowsSplitWidth + columns + icon);
        double horizontal = rows * perSplit;
        if (settings.ShowThinSeparators)
        {
            horizontal += settings.SeparatorLastSplit && rows > 1 ? rows - 1 : rows;
        }

        if (settings.SeparatorLastSplit && rows > 1)
        {
            horizontal += 2;
        }

        return new TimerComponentMetric(path, vertical, horizontal);
    }

    /// <summary>
    /// Subsplits (SplitsComponent.RebuildVisualSplits, LiveSplit.Subsplits): always Total Splits
    /// rows; the 2 high separator before the last row is there only with Always Show Last Split
    /// and Show Separator Before Last Split (else a thin one, with Show Thin Separators).
    /// </summary>
    private static TimerComponentMetric SubsplitsMetric(
        string path,
        SubsplitsComponentSettings settings,
        int segmentCount)
    {
        SplitsComponentSettings rows = settings.AsSplitsSettings();
        rows.SeparatorLastSplit = settings.AlwaysShowLastSplit && settings.SeparatorLastSplit;
        return SplitsMetric(path, rows, segmentCount);
    }

    private static TimerComponentMetric TimerMetric(LayoutComponent component)
    {
        TimerComponentSettings settings = TimerComponentSettings.FromInnerXml(component.SettingsInnerXml);
        return new TimerComponentMetric(
            component.Path,
            Math.Max(1, settings.TimerHeight),
            Math.Max(1, settings.TimerWidth),
            PaddingLeft: 7,
            PaddingRight: 7);
    }

    private static TimerComponentMetric DetailedTimerMetric(LayoutComponent component)
    {
        DetailedTimerComponentSettings settings =
            DetailedTimerComponentSettings.FromInnerXml(component.SettingsInnerXml);
        return new TimerComponentMetric(
            component.Path,
            Math.Max(1, settings.Height),
            Math.Max(1, settings.Width),
            PaddingLeft: 7,
            PaddingRight: 7);
    }

    /// <summary>
    /// InfoTextComponent.DrawVertical: 31 high with PaddingTop = PaddingBottom =
    /// (31 - 0.75 x the taller of the value and name font lines) / 2 (none below 0), or with
    /// Display 2 Rows 0.9 x (value font line + name font line), with no top / bottom padding. One
    /// row is 25 + the layout's Component Spacing here (LiveSplitNative; 6 gives Windows' 31).
    /// </summary>
    private static TimerComponentMetric InfoMetric(string path, bool twoRows, LayoutSettings settings, bool valueInTextFont)
    {
        double valueFont = valueInTextFont ? settings.TextFontSizePx : settings.TimesFontSizePx;
        if (!twoRows)
        {
            double rowHeight = InfoVerticalHeight - LayoutSettings.DefaultComponentSpacing + settings.ComponentSpacing;
            double textHeight = 0.75 * Math.Max(LineHeight(valueFont), LineHeight(settings.TextFontSizePx));
            double padding = Math.Max(0, (rowHeight - textHeight) / 2);
            return new(
                path,
                rowHeight,
                InfoHorizontalWidth,
                PaddingTop: padding,
                PaddingBottom: padding,
                PaddingLeft: 7,
                PaddingRight: 7);
        }

        double height = 0.9 * (LineHeight(valueFont) + LineHeight(settings.TextFontSizePx));
        return new(path, height, InfoHorizontalWidth, PaddingLeft: 7, PaddingRight: 7);
    }

    /// <summary>HotkeyIndicator: its Height (vertical) / Width (horizontal), no padding.</summary>
    private static TimerComponentMetric HotkeyIndicatorMetric(LayoutComponent component)
    {
        HotkeyIndicatorComponentSettings settings = HotkeyIndicatorComponentSettings.FromInnerXml(component.SettingsInnerXml);
        return new(component.Path, settings.IndicatorHeight, settings.IndicatorWidth);
    }

    /// <summary>
    /// GraphCompositeComponent: a 1 high line, the graph, a 1 high line (1 wide lines and the
    /// graph's Width in a horizontal layout), no padding.
    /// </summary>
    private static TimerComponentMetric GraphMetric(LayoutComponent component)
    {
        GraphComponentSettings settings = GraphComponentSettings.FromInnerXml(component.SettingsInnerXml);
        return new(component.Path, settings.GraphHeight + 2, settings.GraphWidth + 2);
    }

    /// <summary>
    /// CounterComponent.DrawGeneral: 1.2 text lines high in the Text font, the text centered
    /// (PaddingTop / Bottom = (height - 0.75 line) / 2). Horizontal: the info components' width,
    /// as the text is not measured here.
    /// </summary>
    private static TimerComponentMetric CounterMetric(string path, LayoutSettings settings)
    {
        double line = LineHeight(settings.TextFontSizePx);
        double height = 1.2 * line;
        double padding = Math.Max(0, (height - 0.75 * line) / 2);
        return new(
            path,
            height,
            InfoHorizontalWidth,
            PaddingTop: padding,
            PaddingBottom: padding,
            PaddingLeft: 7,
            PaddingRight: 7);
    }

    /// <summary>
    /// Title.DrawGeneral: VerticalHeight = MeasureString("A", Title font).Height x 1.7, the
    /// Title font being the layout's Text font.
    /// </summary>
    public static double TitleHeight(double textFontSizePx) => LineHeight(textFontSizePx) * 1.7;

    /// <summary>
    /// Graphics.MeasureString("A", font).Height for the layout fonts: Segoe UI's line spacing
    /// ((ascent + descent) / em = 2724 / 2048) times the size.
    /// </summary>
    public static double LineHeight(double fontSizePx) => Math.Max(1, fontSizePx) * 2724.0 / 2048.0;

    private static TimerComponentMetric BlankMetric(LayoutComponent component)
    {
        BlankSpaceComponentSettings settings =
            BlankSpaceComponentSettings.FromInnerXml(component.SettingsInnerXml);
        return new TimerComponentMetric(
            component.Path,
            Math.Max(1, settings.SpaceHeight),
            Math.Max(1, settings.SpaceWidth));
    }

    private static double LayoutMetricsRowHeight(float splitHeight) =>
        Math.Max(18, splitHeight + 25);
}
