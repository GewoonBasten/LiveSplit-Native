using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Timing;
using static LiveSplitNative.Core.Localization.UiText;

namespace LiveSplitNative.Core.UI;

/// <summary>What the Previous Segment component shows: name (with its shorter forms), value and colors.</summary>
public sealed record PreviousSegmentView(
    string Name,
    IReadOnlyList<string> AlternateNames,
    string Value,
    ArgbColor NameColor,
    ArgbColor ValueColor);

/// <summary>LiveSplit.PreviousSegment PreviousSegment.Update / GetPossibleTimeSave (93a11b85).</summary>
public static class PreviousSegmentLogic
{
    private const string CurrentComparison = "Current Comparison";

    public static PreviousSegmentView Compute(LiveSplitState state, PreviousSegmentComponentSettings settings, LayoutSettings layout)
    {
        string comparison = ComparisonSwitching.ResolveComponentComparison(state, settings.Comparison);
        string comparisonName = DetailedTimerLogic.ShortComparisonName(comparison);
        string suffix = settings.Comparison == CurrentComparison ? "" : " (" + comparisonName + ")";
        string name = T("Previous Segment") + suffix;
        TimingMethod method = state.CurrentTimingMethod;

        // DeltaTimeFormatter (plus sign, dash for none), then the settings' accuracy.
        var deltaFormatter = new GeneralTimeFormatter
        {
            Accuracy = settings.DeltaAccuracy,
            DropDecimals = settings.DropDecimals,
            ShowPlus = true,
            NullFormat = NullFormat.Dash,
        };
        // PossibleTimeSaveFormatter.
        var timeSaveFormatter = new GeneralTimeFormatter
        {
            Accuracy = settings.TimeSaveAccuracy,
            DropDecimals = false,
            NullFormat = NullFormat.Dash,
        };

        TimeSpan? timeChange = null;
        TimeSpan? timeSave = null;
        ArgbColor textColor = settings.OverrideTextColor ? settings.TextColor : layout.TextColor;
        ArgbColor valueColor = textColor;
        TimeSpan? liveSegment = LiveSplitStateHelper.CheckLiveDelta(state, layout, false, comparison, method);
        if (state.CurrentPhase != TimerPhase.NotRunning)
        {
            if (liveSegment is not null)
            {
                timeChange = liveSegment;
                timeSave = GetPossibleTimeSave(state, state.CurrentSplitIndex, comparison);
                name = T("Live Segment") + suffix;
            }
            else if (state.CurrentSplitIndex > 0)
            {
                timeChange = LiveSplitStateHelper.GetPreviousSegmentDelta(state, state.CurrentSplitIndex - 1, comparison, method);
                timeSave = GetPossibleTimeSave(state, state.CurrentSplitIndex - 1, comparison);
            }

            valueColor = timeChange is not null
                ? (liveSegment is not null
                    ? LiveSplitStateHelper.GetSplitColor(state, layout, timeChange, state.CurrentSplitIndex, false, false, comparison, method)
                    : LiveSplitStateHelper.GetSplitColor(state, layout, timeChange, state.CurrentSplitIndex - 1, false, true, comparison, method))
                    ?? textColor
                : LiveSplitStateHelper.GetSplitColor(state, layout, null, state.CurrentSplitIndex - 1, true, true, comparison, method)
                    ?? textColor;
        }

        string[] alternates = liveSegment is not null
            ? [T("Live Segment"), T("Live Seg.")]
            : [T("Previous Segment"), T("Prev. Segment"), T("Prev. Seg.")];
        string value = deltaFormatter.Format(timeChange)
            + (settings.ShowPossibleTimeSave ? " / " + timeSaveFormatter.Format(timeSave) : "");
        return new PreviousSegmentView(name, alternates, value, textColor, valueColor);
    }

    /// <summary>PreviousSegment.GetPossibleTimeSave.</summary>
    public static TimeSpan? GetPossibleTimeSave(LiveSplitState state, int splitIndex, string comparison)
    {
        TimingMethod method = state.CurrentTimingMethod;
        TimeSpan prevTime = TimeSpan.Zero;
        TimeSpan? bestSegments = state.Run[splitIndex].BestSegmentTime[method];
        while (splitIndex > 0 && bestSegments is not null)
        {
            TimeSpan? splitTime = ComparisonTime(state.Run[splitIndex - 1], comparison, method);
            if (splitTime is not null)
            {
                prevTime = splitTime.Value;
                break;
            }

            splitIndex--;
            bestSegments += state.Run[splitIndex].BestSegmentTime[method];
        }

        TimeSpan? time = ComparisonTime(state.Run[splitIndex], comparison, method) - prevTime - bestSegments;
        return time < TimeSpan.Zero ? TimeSpan.Zero : time;
    }

    private static TimeSpan? ComparisonTime(Segment segment, string comparison, TimingMethod method) =>
        segment.Comparisons.TryGetValue(comparison, out SplitTime time) ? time[method] : null;
}
