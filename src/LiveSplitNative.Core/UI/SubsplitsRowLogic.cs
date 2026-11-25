using System.Text.RegularExpressions;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// What one Subsplits row draws, from LiveSplit.Subsplits SplitComponent.UpdateAll (93a11b85)
/// and SplitsComponent.Prepare: the name and its color, the flags DrawGeneral / DrawHeader use,
/// the column cells of a split row, and a header's total section time (TimeLabel) and section
/// timer (DeltaLabel).
/// </summary>
public sealed record SubsplitsRowView(
    SubsplitsRowKind Kind,
    int SplitIndex,
    string Name,
    ArgbColor NameColor,
    bool IsActive,
    bool IsHighlight,
    bool IsSubsplit,
    bool Indent,
    bool SubsplitColor,
    bool DisplayIcon,
    bool OddSplit,
    IReadOnlyList<SplitCellText> Columns,
    string HeaderTime,
    ArgbColor HeaderTimeColor,
    string SectionTimer,
    ArgbColor SectionTimerColor,
    bool SectionTimerGradient);

/// <summary>LiveSplit.Subsplits SplitComponent.UpdateAll / UpdateColumn / UpdateCollapsedColumn.</summary>
public static partial class SubsplitsRowLogic
{
    /// <summary>
    /// The row views for a frame. <paramref name="timeFormatter"/> and
    /// <paramref name="deltaFormatter"/> are the component's SplitTimeFormatter(Split Times
    /// accuracy) and DeltaSplitTimeFormatter(Deltas accuracy, Drop Decimals).
    /// </summary>
    public static IReadOnlyList<SubsplitsRowView> Views(
        LiveSplitState state,
        SubsplitsFrame frame,
        SubsplitsComponentSettings settings,
        LayoutSettings layout,
        IReadOnlyList<ColumnData> columns,
        GeneralTimeFormatter timeFormatter,
        GeneralTimeFormatter deltaFormatter)
    {
        Run run = state.Run;
        GeneralTimeFormatter headerFormatter = SplitColumnLogic.TimeFormatter(settings.HeaderAccuracy);
        GeneralTimeFormatter sectionTimerFormatter = SplitColumnLogic.TimeFormatter(settings.SectionTimerAccuracy);
        // SplitsComponent.Prepare: icons are drawn only when some split of the run has one.
        bool iconsNotBlank = run.Segments.Any(s => !string.IsNullOrWhiteSpace(s.IconXml));
        SplitsComponentSettings splitsSettings = settings.AsSplitsSettings();
        var views = new List<SubsplitsRowView>(frame.Rows.Count);
        foreach (SubsplitsRow row in frame.Rows)
        {
            views.Add(View(state, frame, row, settings, splitsSettings, layout, columns, timeFormatter, deltaFormatter, headerFormatter, sectionTimerFormatter, iconsNotBlank));
        }

        return views;
    }

    private static SubsplitsRowView View(
        LiveSplitState state,
        SubsplitsFrame frame,
        SubsplitsRow row,
        SubsplitsComponentSettings settings,
        SplitsComponentSettings splitsSettings,
        LayoutSettings layout,
        IReadOnlyList<ColumnData> columns,
        GeneralTimeFormatter timeFormatter,
        GeneralTimeFormatter deltaFormatter,
        GeneralTimeFormatter headerFormatter,
        GeneralTimeFormatter sectionTimerFormatter,
        bool iconsNotBlank)
    {
        Run run = state.Run;
        bool hasSplit = row.Kind != SubsplitsRowKind.Blank && row.SplitIndex >= 0 && row.SplitIndex < run.Count;
        if (!hasSplit)
        {
            // A blank row: only the Alternating background (IsSubsplit is reset).
            return new SubsplitsRowView(
                SubsplitsRowKind.Blank, -1, "", layout.TextColor, false, false, false,
                Indent: false, SubsplitColor: false, DisplayIcon: false, row.OddSplit, [], "", layout.TextColor, "", layout.TextColor, false);
        }

        int splitIndex = row.SplitIndex;
        Segment split = run[splitIndex];
        bool header = row.Kind == SubsplitsRowKind.Header;

        // SplitsComponent.Prepare: the icon slot.
        bool hideIconSectionSplit = !settings.ShowIconSectionSplit && splitIndex == frame.LastSplitOfSection;
        bool shouldIndent = !string.IsNullOrWhiteSpace(split.IconXml) || settings.IndentBlankIcons;
        bool displayIcon = header
            ? settings.ShowSectionIcon && shouldIndent && iconsNotBlank
            : settings.DisplayIcons && !hideIconSectionSplit && iconsNotBlank && shouldIndent;

        // UpdateAll.
        bool running = state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused;
        bool isActive = running
            && ((!settings.HideSubsplits && state.CurrentSplitIndex == splitIndex)
                || (frame.SectionSplit >= 0 && frame.SectionSplit == splitIndex));
        bool isHighlight = frame.HighlightSplit >= 0 && frame.HighlightSplit == splitIndex;
        bool isSubsplit = SubsplitsLogic.IsSubsplitName(split.Name) && splitIndex != run.Count - 1;
        string name = DisplayName(split.Name, isSubsplit, row.CollapsedSplit || header);
        bool indent = settings.IndentSubsplits && (isSubsplit || row.ForceIndent);
        bool subsplitColor = (isSubsplit || row.ForceIndent) && settings.OverrideSubsplitColor;

        if (header)
        {
            string comparison = ComparisonSwitching.ResolveComponentComparison(state, settings.HeaderComparison);
            TimingMethod method = TimerComponentLogic.ResolveTimingMethod(settings.HeaderTimingMethod, state.CurrentTimingMethod);
            TimeSpan? deltaTime = SectionDelta(state, splitIndex, row.TopSplit, comparison, method);
            if (splitIndex >= state.CurrentSplitIndex && deltaTime < TimeSpan.Zero)
            {
                deltaTime = null;
            }

            ArgbColor headerTimesColor = settings.OverrideHeaderColor ? settings.HeaderTimesColor : layout.TextColor;
            ArgbColor timeColor = SectionColor(layout, null, deltaTime) ?? headerTimesColor;
            ArgbColor nameColor = settings.OverrideHeaderColor ? settings.HeaderTextColor : layout.TextColor;
            string time = deltaTime is not null
                ? deltaFormatter.Format(deltaTime)
                : splitIndex < state.CurrentSplitIndex
                    ? "-"
                    : headerFormatter.Format(SectionComparison(state, splitIndex, row.TopSplit, comparison, method));
            string sectionTimer = sectionTimerFormatter.Format(SectionTime(state, splitIndex, row.TopSplit, method));
            ArgbColor sectionTimerColor = splitIndex < state.CurrentSplitIndex ? headerTimesColor : settings.SectionTimerColor;

            return new SubsplitsRowView(
                SubsplitsRowKind.Header,
                splitIndex,
                settings.HeaderText ? name : "",
                nameColor,
                isActive,
                isHighlight,
                isSubsplit,
                indent,
                subsplitColor,
                displayIcon,
                row.OddSplit,
                [],
                settings.HeaderTimes ? time : "",
                timeColor,
                settings.SectionTimer ? sectionTimer : "",
                sectionTimerColor,
                // DrawHeader: Show Gradient shades the section timer while its section is not done.
                settings.SectionTimer && settings.SectionTimerGradient && splitIndex >= state.CurrentSplitIndex);
        }

        ArgbColor splitNameColor = splitIndex < state.CurrentSplitIndex
            ? settings.OverrideTextColor ? settings.BeforeNamesColor : layout.TextColor
            : isActive
                ? settings.OverrideTextColor ? settings.CurrentNamesColor : layout.TextColor
                : settings.OverrideTextColor ? settings.AfterNamesColor : layout.TextColor;

        var cells = new List<SplitCellText>(columns.Count);
        foreach (ColumnData column in columns)
        {
            cells.Add(row.CollapsedSplit
                ? CollapsedColumn(state, splitIndex, row.TopSplit, isActive, column, settings, layout, timeFormatter, deltaFormatter)
                : SplitColumnLogic.Column(state, splitIndex, column, splitsSettings, layout, timeFormatter, deltaFormatter, isActive));
        }

        return new SubsplitsRowView(
            SubsplitsRowKind.Split,
            splitIndex,
            name,
            splitNameColor,
            isActive,
            isHighlight,
            isSubsplit,
            indent,
            subsplitColor,
            displayIcon,
            row.OddSplit,
            cells,
            "",
            layout.TextColor,
            "",
            layout.TextColor,
            false);
    }

    /// <summary>
    /// UpdateAll's name: a subsplit without its "-"; a "{Section} Name" split shows Name, or the
    /// section name when it stands for its whole section (collapsed, or the header).
    /// </summary>
    public static string DisplayName(string name, bool isSubsplit, bool sectionName)
    {
        if (isSubsplit)
        {
            return name[1..];
        }

        Match match = SectionNameRegex().Match(name);
        return match.Success
            ? sectionName ? match.Groups[1].Value : match.Groups[2].Value
            : name;
    }

    /// <summary>getSectionTime: the time spent in the section so far, null before it starts.</summary>
    public static TimeSpan? SectionTime(LiveSplitState state, int splitNumber, int topNumber, TimingMethod method)
    {
        if (topNumber > state.CurrentSplitIndex)
        {
            return null;
        }

        TimeSpan? start = topNumber > 0 ? state.Run[topNumber - 1].SplitTime[method] : TimeSpan.Zero;
        if (splitNumber < state.CurrentSplitIndex)
        {
            return state.Run[splitNumber].SplitTime[method] - start;
        }

        return state.CurrentTime[method] - start;
    }

    /// <summary>getSectionComparison: the section's length in the comparison.</summary>
    public static TimeSpan? SectionComparison(LiveSplitState state, int splitNumber, int topNumber, string comparison, TimingMethod method)
    {
        TimeSpan? start = topNumber > 0 ? ComparisonTime(state.Run[topNumber - 1], comparison, method) : TimeSpan.Zero;
        return ComparisonTime(state.Run[splitNumber], comparison, method) - start;
    }

    /// <summary>getSectionDelta.</summary>
    public static TimeSpan? SectionDelta(LiveSplitState state, int splitNumber, int topNumber, string comparison, TimingMethod method) =>
        SectionTime(state, splitNumber, topNumber, method) - SectionComparison(state, splitNumber, topNumber, comparison, method);

    /// <summary>GetSectionColor: ahead / behind by the split delta, gaining / losing by the section delta.</summary>
    public static ArgbColor? SectionColor(LayoutSettings layout, TimeSpan? timeDifference, TimeSpan? delta)
    {
        if (timeDifference is not null)
        {
            if (timeDifference < TimeSpan.Zero)
            {
                return delta is not null && delta > TimeSpan.Zero ? layout.AheadLosingTimeColor : layout.AheadGainingTimeColor;
            }

            return delta is not null && delta < TimeSpan.Zero ? layout.BehindGainingTimeColor : layout.BehindLosingTimeColor;
        }

        if (delta is not null)
        {
            return delta < TimeSpan.Zero ? layout.AheadGainingTimeColor : layout.BehindLosingTimeColor;
        }

        return null;
    }

    /// <summary>UpdateCollapsedColumn: a collapsed split's column, over its whole section (TopSplit to the split).</summary>
    public static SplitCellText CollapsedColumn(
        LiveSplitState state,
        int splitIndex,
        int topSplit,
        bool isActive,
        ColumnData data,
        SubsplitsComponentSettings settings,
        LayoutSettings layout,
        GeneralTimeFormatter timeFormatter,
        GeneralTimeFormatter deltaFormatter)
    {
        Segment split = state.Run[splitIndex];
        string comparison = ComparisonSwitching.ResolveComponentComparison(state, data.Comparison);
        TimingMethod method = TimerComponentLogic.ResolveTimingMethod(data.TimingMethod, state.CurrentTimingMethod);
        ColumnType type = data.Type;
        string text = "";
        ArgbColor color = layout.TextColor;

        if (splitIndex < state.CurrentSplitIndex)
        {
            ArgbColor before = settings.OverrideTimesColor ? settings.BeforeTimesColor : layout.TextColor;
            if (type is ColumnType.SplitTime or ColumnType.SegmentTime or ColumnType.CustomVariable)
            {
                color = before;
                text = type switch
                {
                    ColumnType.SplitTime => timeFormatter.Format(split.SplitTime[method]),
                    ColumnType.SegmentTime => timeFormatter.Format(SectionTime(state, splitIndex, topSplit, method)),
                    _ => split.CustomVariableValues.TryGetValue(data.Name, out string? value) ? value ?? "" : "",
                };
            }

            if (type is ColumnType.DeltaorSplitTime or ColumnType.Delta)
            {
                TimeSpan? deltaTime = split.SplitTime[method] - ComparisonTime(split, comparison, method);
                TimeSpan? segmentDelta = SectionDelta(state, splitIndex, topSplit, comparison, method);
                color = SectionColor(layout, deltaTime, segmentDelta) ?? before;
                text = type == ColumnType.DeltaorSplitTime && deltaTime is null
                    ? timeFormatter.Format(split.SplitTime[method])
                    : deltaFormatter.Format(deltaTime);
            }
            else if (type is ColumnType.SegmentDeltaorSegmentTime or ColumnType.SegmentDelta)
            {
                TimeSpan? segmentDelta = SectionDelta(state, splitIndex, topSplit, comparison, method);
                color = SectionColor(layout, null, segmentDelta) ?? before;
                text = type == ColumnType.SegmentDeltaorSegmentTime && segmentDelta is null
                    ? timeFormatter.Format(SectionTime(state, splitIndex, topSplit, method))
                    : deltaFormatter.Format(segmentDelta);
            }

            return new SplitCellText(text, color);
        }

        if (type is ColumnType.SplitTime or ColumnType.SegmentTime or ColumnType.DeltaorSplitTime
            or ColumnType.SegmentDeltaorSegmentTime or ColumnType.CustomVariable)
        {
            color = isActive
                ? settings.OverrideTimesColor ? settings.CurrentTimesColor : layout.TextColor
                : settings.OverrideTimesColor ? settings.AfterTimesColor : layout.TextColor;
            if (type is ColumnType.SplitTime or ColumnType.DeltaorSplitTime)
            {
                text = timeFormatter.Format(ComparisonTime(split, comparison, method));
            }
            else if (type is ColumnType.SegmentTime or ColumnType.SegmentDeltaorSegmentTime)
            {
                TimeSpan? previousTime = topSplit > 0 ? ComparisonTime(state.Run[topSplit - 1], comparison, method) : TimeSpan.Zero;
                text = timeFormatter.Format(ComparisonTime(split, comparison, method) - previousTime);
            }
            else
            {
                text = splitIndex == state.CurrentSplitIndex
                    ? state.Run.Metadata.CustomVariableValue(data.Name) ?? ""
                    : "";
            }
        }

        // Live delta over the whole section.
        bool splitDelta = type is ColumnType.DeltaorSplitTime or ColumnType.Delta;
        TimeSpan? bestDelta = LiveDeltaCollapsed(state, splitIndex, topSplit, splitDelta, comparison, method);
        if (bestDelta is not null
            && isActive
            && type is ColumnType.DeltaorSplitTime or ColumnType.Delta or ColumnType.SegmentDeltaorSegmentTime or ColumnType.SegmentDelta)
        {
            text = deltaFormatter.Format(bestDelta);
            color = settings.OverrideDeltasColor ? settings.DeltasColor : layout.TextColor;
        }
        else if (type is ColumnType.Delta or ColumnType.SegmentDelta)
        {
            text = "";
        }

        return new SplitCellText(text, color);
    }

    /// <summary>CheckLiveDeltaCollapsed: shown once the split delta or the section delta is behind.</summary>
    private static TimeSpan? LiveDeltaCollapsed(LiveSplitState state, int splitIndex, int topSplit, bool splitDelta, string comparison, TimingMethod method)
    {
        if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            TimeSpan? curSplit = ComparisonTime(state.Run[splitIndex], comparison, method);
            TimeSpan? currentTime = state.CurrentTime[method];
            TimeSpan? segmentDelta = SectionDelta(state, splitIndex, topSplit, comparison, method);
            if ((splitDelta && currentTime > curSplit) || segmentDelta > TimeSpan.Zero)
            {
                return splitDelta ? currentTime - curSplit : segmentDelta;
            }
        }

        return null;
    }

    private static TimeSpan? ComparisonTime(Segment segment, string comparison, TimingMethod method) =>
        segment.Comparisons.TryGetValue(comparison, out SplitTime time) ? time[method] : null;

    /// <summary>SplitComponent.SubsplitRegex.</summary>
    [GeneratedRegex(@"^{(.+)}\s*(.+)$")]
    private static partial Regex SectionNameRegex();
}
