using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Timing;
using static LiveSplitNative.Core.Localization.UiText;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// The Update methods of the upstream information components (LiveSplit components @ 93a11b85):
/// Run Prediction, Delta, Comparison Time, Current Comparison and Total Playtime. Each returns
/// what the component shows; the app only draws it.
/// </summary>
public static class InfoComponentLogic
{
    private const string CurrentComparisonSetting = ComparisonSwitching.CurrentComparisonSetting;
    /// <summary>RunPrediction.GetDisplayedName: the component's name for a comparison.</summary>
    public static string RunPredictionName(string comparison) => comparison switch
    {
        CurrentComparisonSetting or Run.PersonalBestComparisonName => T("Current Pace"),
        BestSegmentsComparisonGenerator.ComparisonName => T("Best Possible Time"),
        WorstSegmentsComparisonGenerator.ComparisonName => T("Worst Possible Time"),
        AverageSegmentsComparisonGenerator.ComparisonName => T("Predicted Time"),
        _ => T("Current Pace") + " (" + StandardComparisonGeneratorsFactory.GetShortComparisonName(comparison) + ")",
    };

    /// <summary>RunPrediction.SetAlternateText: the shorter names, by comparison.</summary>
    public static IReadOnlyList<string> RunPredictionAlternateNames(string comparison) => comparison switch
    {
        CurrentComparisonSetting or Run.PersonalBestComparisonName => [T("Cur. Pace"), T("Pace")],
        BestSegmentsComparisonGenerator.ComparisonName => [T("Best Poss. Time"), T("Best Time"), "BPT"],
        WorstSegmentsComparisonGenerator.ComparisonName => [T("Worst Poss. Time"), T("Worst Time")],
        AverageSegmentsComparisonGenerator.ComparisonName => [T("Pred. Time")],
        _ => [T("Current Pace"), T("Cur. Pace"), T("Pace")],
    };

    /// <summary>
    /// RunPrediction.Update: the final time if the rest of the run goes like the comparison
    /// (the last delta, or the live one once it is bigger, plus the comparison's final time).
    /// "Current Pace" shows nothing before the run starts; the other names show the comparison's
    /// final time; once the run ended, its final time.
    /// </summary>
    public static InfoComponentView RunPrediction(
        LiveSplitState state,
        RunPredictionComponentSettings settings,
        LayoutSettings layout)
    {
        string comparison = ComparisonSwitching.ResolveComponentComparison(state, settings.Comparison);
        string name = RunPredictionName(comparison);
        TimingMethod method = state.CurrentTimingMethod;
        TimeSpan? value = null;
        if (state.Run.Count == 0
            || (name.StartsWith(T("Current Pace"), StringComparison.Ordinal) && state.CurrentPhase == TimerPhase.NotRunning))
        {
            value = null;
        }
        else if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            TimeSpan? delta = LiveSplitStateHelper.GetLastDelta(state, state.CurrentSplitIndex, comparison, method) ?? TimeSpan.Zero;
            TimeSpan? liveDelta = state.CurrentTime[method] - TimeOf(state.CurrentSplit, comparison, method);
            if (liveDelta > delta)
            {
                delta = liveDelta;
            }

            value = delta + TimeOf(state.Run[^1], comparison, method);
        }
        else
        {
            value = state.CurrentPhase == TimerPhase.Ended
                ? state.Run[^1].SplitTime[method]
                : TimeOf(state.Run[^1], comparison, method);
        }

        return new InfoComponentView(
            name,
            RunPredictionAlternateNames(comparison),
            SplitTimeFormatter(settings.Accuracy).Format(value),
            settings.OverrideTextColor ? settings.TextColor : layout.TextColor,
            settings.OverrideTimeColor ? settings.TimeColor : layout.TextColor);
    }

    /// <summary>DeltaComponent.ComponentName: "Delta", with the comparison's short name when one is chosen.</summary>
    public static string DeltaName(string settingsComparison) =>
        "Delta" + (settingsComparison == CurrentComparisonSetting
            ? ""
            : " (" + StandardComparisonGeneratorsFactory.GetShortComparisonName(settingsComparison) + ")");

    /// <summary>
    /// DeltaComponent.Update: the comparison's name (or the custom label) and the delta of the
    /// last split, or the live delta once that is bigger, in the split colors.
    /// </summary>
    public static InfoComponentView Delta(LiveSplitState state, DeltaComponentSettings settings, LayoutSettings layout)
    {
        string comparison = ComparisonSwitching.ResolveComponentComparison(state, settings.Comparison);
        string comparisonName = comparison.StartsWith("[Race] ", StringComparison.Ordinal) ? comparison[7..] : comparison;
        TimingMethod method = state.CurrentTimingMethod;
        bool useLiveDelta = false;
        TimeSpan? value = null;
        if (state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused)
        {
            TimeSpan? delta = LiveSplitStateHelper.GetLastDelta(state, state.CurrentSplitIndex, comparison, method);
            TimeSpan? liveDelta = state.CurrentTime[method] - TimeOf(state.CurrentSplit, comparison, method);
            if (liveDelta > delta || (delta is null && liveDelta > TimeSpan.Zero))
            {
                delta = liveDelta;
                useLiveDelta = true;
            }

            value = delta;
        }
        else if (state.CurrentPhase == TimerPhase.Ended && state.Run.Count > 0)
        {
            value = state.Run[^1].SplitTime[method] - TimeOf(state.Run[^1], comparison, method);
        }

        string name = comparisonName;
        IReadOnlyList<string> alternates;
        if (settings.OverrideText)
        {
            alternates = [];
            name = settings.DifferentialText && value < TimeSpan.Zero ? settings.CustomTextAhead : settings.CustomText;
        }
        else
        {
            alternates = [StandardComparisonGeneratorsFactory.GetShortComparisonName(comparison)];
        }

        ArgbColor textColor = settings.OverrideTextColor ? settings.TextColor : layout.TextColor;
        ArgbColor valueColor = LiveSplitStateHelper.GetSplitColor(
            state,
            layout,
            value,
            state.CurrentSplitIndex - (useLiveDelta ? 0 : 1),
            showSegmentDeltas: true,
            showBestSegments: false,
            comparison,
            method) ?? textColor;
        // DeltaComponent's GeneralTimeFormatter: a plus for positive deltas and a dash for none.
        var formatter = new GeneralTimeFormatter
        {
            Accuracy = settings.Accuracy,
            DropDecimals = settings.DropDecimals,
            ShowPlus = true,
            NullFormat = NullFormat.Dash,
            DigitsFormat = DigitsFormat.SingleDigitSeconds,
        };
        return new InfoComponentView(name, alternates, formatter.Format(value), textColor, valueColor);
    }

    /// <summary>ComparisonTime.GetComponentName: the chosen comparison and / or timing method in brackets.</summary>
    public static string ComparisonTimeName(ComparisonTimeComponentSettings settings)
    {
        bool comparisonOverride = settings.Comparison != CurrentComparisonSetting;
        bool methodOverride = settings.TimingMethod != "Current Timing Method";
        return comparisonOverride && methodOverride ? $"{T("Comparison Time")} ({T(settings.Comparison)}, {T(settings.TimingMethod)})"
            : comparisonOverride ? $"{T("Comparison Time")} ({T(settings.Comparison)})"
            : methodOverride ? $"{T("Comparison Time")} ({T(settings.TimingMethod)})"
            : T("Comparison Time");
    }

    /// <summary>
    /// ComparisonTime.SetAlternateText. Windows keeps the previous shorter name for a comparison
    /// that has none (a custom one); here such a comparison has no shorter name.
    /// </summary>
    public static IReadOnlyList<string> ComparisonTimeAlternateNames(string comparison) => comparison switch
    {
        Run.PersonalBestComparisonName => ["PB"],
        AverageSegmentsComparisonGenerator.ComparisonName => [AverageSegmentsComparisonGenerator.ShortComparisonName],
        BestSegmentsComparisonGenerator.ComparisonName => [BestSegmentsComparisonGenerator.ShortComparisonName],
        LatestRunComparisonGenerator.ComparisonName => [LatestRunComparisonGenerator.ShortComparisonName],
        MedianSegmentsComparisonGenerator.ComparisonName => [MedianSegmentsComparisonGenerator.ShortComparisonName],
        PercentileComparisonGenerator.ComparisonName => [PercentileComparisonGenerator.ShortComparisonName],
        WorstSegmentsComparisonGenerator.ComparisonName => [WorstSegmentsComparisonGenerator.ShortComparisonName],
        _ => [],
    };

    /// <summary>
    /// ComparisonTime.Update: the comparison's final time, or (once the run started) its time for
    /// the current split or the current segment.
    /// </summary>
    public static InfoComponentView ComparisonTime(
        LiveSplitState state,
        ComparisonTimeComponentSettings settings,
        LayoutSettings layout)
    {
        string comparison = ComparisonSwitching.ResolveComponentComparison(state, settings.Comparison);
        TimingMethod method = settings.TimingMethod switch
        {
            "Real Time" => TimingMethod.RealTime,
            "Game Time" => TimingMethod.GameTime,
            _ => state.CurrentTimingMethod,
        };
        string name = settings.TimingMethod != "Current Timing Method"
            ? $"{comparison} ({settings.TimingMethod})"
            : comparison;
        return new InfoComponentView(
            name,
            ComparisonTimeAlternateNames(comparison),
            SplitTimeFormatter(settings.Accuracy).Format(ComparisonTimeValue(state, settings.Type, comparison, method)),
            settings.OverrideTextColor ? settings.TextColor : layout.TextColor,
            settings.OverrideTimeColor ? settings.TimeColor : layout.TextColor);
    }

    /// <summary>ComparisonTime.GetTimeValue.</summary>
    public static TimeSpan? ComparisonTimeValue(LiveSplitState state, ComparisonTimeType type, string comparison, TimingMethod method)
    {
        Run run = state.Run;
        if (run.Count == 0)
        {
            return null;
        }

        if (type == ComparisonTimeType.FinalTime)
        {
            return TimeOf(run[^1], comparison, method);
        }

        if (state.CurrentPhase == TimerPhase.NotRunning)
        {
            return null;
        }

        bool ended = state.CurrentPhase == TimerPhase.Ended;
        TimeSpan? splitTime = TimeOf(ended ? run[^1] : state.CurrentSplit, comparison, method);
        if (type == ComparisonTimeType.SplitTime)
        {
            return splitTime;
        }

        int previous = ended ? run.Count - 2 : state.CurrentSplitIndex - 1;
        TimeSpan? previousTime = previous < 0 ? TimeSpan.Zero : TimeOf(run[previous], comparison, method);
        return splitTime - previousTime;
    }

    /// <summary>CurrentComparison: "Comparing Against" and the timer's comparison, both in the Text font.</summary>
    public static InfoComponentView CurrentComparison(
        LiveSplitState state,
        CurrentComparisonComponentSettings settings,
        LayoutSettings layout) =>
        new(
            T("Comparing Against"),
            [T("Comparison"), T("Comp.")],
            state.CurrentComparison,
            settings.OverrideTextColor ? settings.TextColor : layout.TextColor,
            settings.OverrideTimeColor ? settings.TimeColor : layout.TextColor,
            ValueInTextFont: true);

    /// <summary>
    /// TotalPlaytimeComponent.CalculateTotalPlaytime, the finished attempts' part: each attempt's
    /// duration less its pauses, or for a reset attempt from before LiveSplit 1.6 (no duration)
    /// the real time of the segments it has in the history.
    /// </summary>
    public static TimeSpan AttemptHistoryPlaytime(Run run)
    {
        TimeSpan total = TimeSpan.Zero;
        foreach (Attempt attempt in run.AttemptHistory)
        {
            if (attempt.Duration is { } duration)
            {
                total += duration - (attempt.PauseTime ?? TimeSpan.Zero);
                continue;
            }

            for (int i = 0; i < run.Count; i++)
            {
                if (run[i].SegmentHistory.TryGetValue(attempt.Index, out SplitTime time) && time.RealTime is { } real)
                {
                    total += real;
                }
            }
        }

        return total;
    }

    /// <summary>
    /// TotalPlaytimeComponent.Update: the finished attempts' playtime (pass
    /// <see cref="AttemptHistoryPlaytime"/>, which only changes with the history) plus the current
    /// attempt's, as "2d 3:04:05", or with Show Total Hours as "51:04:05".
    /// </summary>
    public static InfoComponentView TotalPlaytime(
        LiveSplitState state,
        TotalPlaytimeComponentSettings settings,
        LayoutSettings layout,
        TimeSpan attemptHistoryPlaytime)
    {
        TimeSpan total = attemptHistoryPlaytime + state.CurrentAttemptDuration - (state.PauseTime ?? TimeSpan.Zero);
        // RegularTimeFormatter: whole seconds, "0" for none, single-digit minutes.
        string value = settings.ShowTotalHours
            ? new GeneralTimeFormatter
            {
                Accuracy = TimeAccuracy.Seconds,
                NullFormat = NullFormat.ZeroWithAccuracy,
                DigitsFormat = DigitsFormat.SingleDigitMinutes,
            }.Format(total)
            : DaysTimeFormatter.Format(total);
        return new InfoComponentView(
            "Total Playtime",
            [],
            value,
            settings.OverrideTextColor ? settings.TextColor : layout.TextColor,
            settings.OverrideTimeColor ? settings.TimeColor : layout.TextColor);
    }

    /// <summary>SplitTimeFormatter: the accuracy, a dash for none, single-digit minutes.</summary>
    private static GeneralTimeFormatter SplitTimeFormatter(TimeAccuracy accuracy) => new()
    {
        Accuracy = accuracy,
        NullFormat = NullFormat.Dash,
        DigitsFormat = DigitsFormat.SingleDigitMinutes,
    };

    /// <summary>Segment.Comparisons[comparison][method]: no time when the comparison is missing.</summary>
    private static TimeSpan? TimeOf(Segment? segment, string comparison, TimingMethod method) =>
        segment is not null && segment.Comparisons.TryGetValue(comparison, out SplitTime time) ? time[method] : null;
}
