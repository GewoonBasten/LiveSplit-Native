using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;

namespace LiveSplitNative.Core.UI;

/// <summary>One split column cell as Windows draws it: its text and its color.</summary>
public readonly record struct SplitCellText(string Text, ArgbColor Color);

/// <summary>
/// The text and colors of a split row (LiveSplit.Splits SplitComponent.UpdateAll / UpdateColumn
/// and SplitsComponent.Prepare, 93a11b85), including the Splits settings' name, time and delta
/// colors.
/// </summary>
public static class SplitColumnLogic
{
    /// <summary>SplitTimeFormatter: one-digit minutes, a dash for no time. Drop Decimals is not used here.</summary>
    public static GeneralTimeFormatter TimeFormatter(TimeAccuracy accuracy) => new()
    {
        Accuracy = accuracy,
        NullFormat = NullFormat.Dash,
        DigitsFormat = DigitsFormat.SingleDigitMinutes,
    };

    /// <summary>DeltaSplitTimeFormatter: with a plus sign, a dash for no time.</summary>
    public static GeneralTimeFormatter DeltaFormatter(TimeAccuracy accuracy, bool dropDecimals) => new()
    {
        Accuracy = accuracy,
        DropDecimals = dropDecimals,
        NullFormat = NullFormat.Dash,
        ShowPlus = true,
    };

    /// <summary>UpdateAll: the name color, by whether the split is done, current or to come.</summary>
    public static ArgbColor NameColor(LiveSplitState state, int splitIndex, SplitsComponentSettings settings, LayoutSettings layout) =>
        !settings.OverrideTextColor ? layout.TextColor
        : splitIndex < state.CurrentSplitIndex ? settings.BeforeNamesColor
        : IsCurrentSplit(state, splitIndex) ? settings.CurrentNamesColor
        : settings.AfterNamesColor;

    /// <summary>
    /// UpdateColumn for one column of split <paramref name="splitIndex"/>. Subsplits' UpdateColumn
    /// is the same with its own IsActive in place of Split == state.CurrentSplit
    /// (<paramref name="isActive"/>).
    /// </summary>
    public static SplitCellText Column(
        LiveSplitState state,
        int splitIndex,
        ColumnData data,
        SplitsComponentSettings settings,
        LayoutSettings layout,
        GeneralTimeFormatter timeFormatter,
        GeneralTimeFormatter deltaFormatter,
        bool? isActive = null)
    {
        Segment split = state.Run[splitIndex];
        string comparison = ComparisonSwitching.ResolveComponentComparison(state, data.Comparison);
        TimingMethod method = TimerComponentLogic.ResolveTimingMethod(data.TimingMethod, state.CurrentTimingMethod);
        ColumnType type = data.Type;
        TimeSpan? ComparisonTime(Segment segment) =>
            segment.Comparisons.TryGetValue(comparison, out SplitTime time) ? time[method] : null;

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
                    ColumnType.SegmentTime => timeFormatter.Format(LiveSplitStateHelper.GetPreviousSegmentTime(state, splitIndex, method)),
                    _ => split.CustomVariableValues.TryGetValue(data.Name, out string? value) ? value : "",
                };
            }

            if (type is ColumnType.DeltaorSplitTime or ColumnType.Delta)
            {
                TimeSpan? deltaTime = split.SplitTime[method] - ComparisonTime(split);
                color = LiveSplitStateHelper.GetSplitColor(state, layout, deltaTime, splitIndex, true, true, comparison, method) ?? before;
                text = type == ColumnType.DeltaorSplitTime && deltaTime is null
                    ? timeFormatter.Format(split.SplitTime[method])
                    : deltaFormatter.Format(deltaTime);
            }
            else if (type is ColumnType.SegmentDeltaorSegmentTime or ColumnType.SegmentDelta)
            {
                TimeSpan? segmentDelta = LiveSplitStateHelper.GetPreviousSegmentDelta(state, splitIndex, comparison, method);
                color = LiveSplitStateHelper.GetSplitColor(state, layout, segmentDelta, splitIndex, false, true, comparison, method) ?? before;
                text = type == ColumnType.SegmentDeltaorSegmentTime && segmentDelta is null
                    ? timeFormatter.Format(LiveSplitStateHelper.GetPreviousSegmentTime(state, splitIndex, method))
                    : deltaFormatter.Format(segmentDelta);
            }

            return new SplitCellText(text, color);
        }

        bool isCurrent = isActive ?? IsCurrentSplit(state, splitIndex);
        if (type is ColumnType.SplitTime or ColumnType.SegmentTime or ColumnType.DeltaorSplitTime
            or ColumnType.SegmentDeltaorSegmentTime or ColumnType.CustomVariable)
        {
            color = isCurrent
                ? settings.OverrideTimesColor ? settings.CurrentTimesColor : layout.TextColor
                : settings.OverrideTimesColor ? settings.AfterTimesColor : layout.TextColor;
            if (type is ColumnType.SplitTime or ColumnType.DeltaorSplitTime)
            {
                text = timeFormatter.Format(ComparisonTime(split));
            }
            else if (type is ColumnType.SegmentTime or ColumnType.SegmentDeltaorSegmentTime)
            {
                TimeSpan previousTime = TimeSpan.Zero;
                for (int index = splitIndex - 1; index >= 0; index--)
                {
                    if (ComparisonTime(state.Run[index]) is { } comparisonTime)
                    {
                        previousTime = comparisonTime;
                        break;
                    }
                }

                text = timeFormatter.Format(ComparisonTime(split) - previousTime);
            }
            else
            {
                text = splitIndex == state.CurrentSplitIndex
                    ? state.Run.Metadata.CustomVariableValue(data.Name) ?? ""
                    : "";
            }
        }

        // Live delta on the current split.
        bool splitDelta = type is ColumnType.DeltaorSplitTime or ColumnType.Delta;
        TimeSpan? bestDelta = LiveSplitStateHelper.CheckLiveDelta(state, layout, splitDelta, comparison, method);
        if (bestDelta is not null
            && isCurrent
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

    /// <summary>Split == state.CurrentSplit.</summary>
    private static bool IsCurrentSplit(LiveSplitState state, int splitIndex) =>
        splitIndex == state.CurrentSplitIndex && splitIndex >= 0 && splitIndex < state.Run.Count;
}
