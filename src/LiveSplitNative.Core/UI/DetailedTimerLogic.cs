using System.Text.RegularExpressions;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.UI;

/// <summary>Pure helpers mirroring LiveSplit.DetailedTimer update/paint rules.</summary>
public static class DetailedTimerLogic
{
    private static readonly Regex SubsplitRegex = new(@"^{(.+)}\s*(.+)$", RegexOptions.Compiled);

    public static DigitsFormat ParseDigitsFormat(string? value) =>
        value switch
        {
            "00:01" => DigitsFormat.DoubleDigitMinutes,
            "0:00:01" => DigitsFormat.SingleDigitHours,
            "00:00:01" => DigitsFormat.DoubleDigitHours,
            _ => DigitsFormat.SingleDigitSeconds,
        };

    public static string DigitsFormatString(DigitsFormat format) =>
        format switch
        {
            DigitsFormat.DoubleDigitMinutes => "00:01",
            DigitsFormat.SingleDigitHours => "0:00:01",
            DigitsFormat.DoubleDigitHours => "00:00:01",
            _ => "1",
        };

    public static TimeAccuracy ParseAccuracyToken(string? value) =>
        value switch
        {
            ".234" => TimeAccuracy.Milliseconds,
            ".23" => TimeAccuracy.Hundredths,
            ".2" => TimeAccuracy.Tenths,
            _ => TimeAccuracy.Seconds,
        };

    public static string AccuracyToken(TimeAccuracy accuracy) =>
        accuracy switch
        {
            TimeAccuracy.Milliseconds => ".234",
            TimeAccuracy.Hundredths => ".23",
            TimeAccuracy.Tenths => ".2",
            _ => "",
        };

    public static void SplitTimerFormat(string? combined, out string digitsFormat, out string accuracyToken)
    {
        if (string.IsNullOrEmpty(combined))
        {
            digitsFormat = "1";
            accuracyToken = ".23";
            return;
        }

        int decimalIndex = combined.IndexOf('.');
        if (decimalIndex < 0)
        {
            digitsFormat = combined;
            accuracyToken = "";
        }
        else
        {
            digitsFormat = combined[..decimalIndex];
            accuracyToken = combined[decimalIndex..];
        }
    }

    public static string CombineTimerFormat(string digitsFormat, TimeAccuracy accuracy) =>
        digitsFormat + AccuracyToken(accuracy);

    public static string ShortComparisonName(string comparison) =>
        comparison switch
        {
            "Personal Best" => "PB",
            "Best Segments" => "Best",
            "Average Segments" => "Average",
            "Median Segments" => "Median",
            "Worst Segments" => "Worst",
            "Balanced PB" => "Balanced",
            "Latest Run" => "Latest",
            "Golf HCP" => "HCP",
            "None" => "",
            _ => comparison.Length <= 12 ? comparison : comparison[..12],
        };

    public static string FormatSplitName(string? name, int splitIndex, int runCount)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "";
        }

        bool isSubsplit = name.StartsWith('-') && splitIndex < runCount - 1;
        if (isSubsplit)
        {
            return name[1..];
        }

        Match match = SubsplitRegex.Match(name);
        return match.Success ? match.Groups[2].Value : name;
    }

    public readonly record struct ComparisonResolution(
        string Comparison,
        string Comparison2,
        bool HideComparison);

    /// <summary>Mirrors DetailedTimer.Update comparison fallback rules.</summary>
    public static ComparisonResolution ResolveComparisons(
        string settingsComparison,
        string settingsComparison2,
        bool hideComparison,
        string currentComparison,
        IReadOnlyCollection<string> availableComparisons)
    {
        string comparison = settingsComparison == "Current Comparison" ? currentComparison : settingsComparison;
        string comparison2 = settingsComparison2 == "Current Comparison" ? currentComparison : settingsComparison2;
        bool hide = hideComparison;

        // Upstream DetailedTimer: state.Run.Comparisons.Contains(name) (custom + active generated).
        bool Has(string name) => name != "None" && availableComparisons.Contains(name);

        if (hide || !Has(comparison2) || comparison2 == "None")
        {
            hide = true;
            if (!Has(comparison) || comparison == "None")
            {
                comparison = currentComparison;
            }
        }
        else if (!Has(comparison) || comparison == "None")
        {
            hide = true;
            comparison = comparison2;
        }
        else if (comparison == comparison2)
        {
            hide = true;
        }

        return new ComparisonResolution(comparison, comparison2, hide);
    }

    public static TimeSpan? SegmentComparisonTime(
        Run run,
        int currentSplitIndex,
        string comparison,
        TimingMethod method)
    {
        if (string.Equals(comparison, "None", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        int lastSplitOffset = currentSplitIndex == run.Count ? -1 : 0;
        int index = currentSplitIndex + lastSplitOffset;
        if (index < 0 || index >= run.Count)
        {
            return null;
        }

        if (string.Equals(comparison, "Best Segments", StringComparison.OrdinalIgnoreCase))
        {
            return run[index].BestSegmentTime[method];
        }

        if (currentSplitIndex == 0 || (currentSplitIndex == 1 && lastSplitOffset == -1))
        {
            return run[0].Comparisons.TryGetValue(comparison, out var first)
                ? first[method]
                : null;
        }

        if (currentSplitIndex > 0)
        {
            int curIdx = currentSplitIndex + lastSplitOffset;
            int prevIdx = currentSplitIndex - 1 + lastSplitOffset;
            if (!run[curIdx].Comparisons.TryGetValue(comparison, out var cur) || cur[method] is null)
            {
                return null;
            }

            TimeSpan? prev = run[prevIdx].Comparisons.TryGetValue(comparison, out var p) ? p[method] : null;
            return cur[method] - (prev ?? TimeSpan.Zero);
        }

        return null;
    }

    /// <summary>Segment timer value — mirrors SegmentTimer.GetTime.</summary>
    public static TimeSpan? SegmentTimerTime(LiveSplitState state, TimingMethod method)
    {
        TimeSpan? lastSplit = TimeSpan.Zero;
        int runEndedDelay = state.CurrentPhase == TimerPhase.Ended ? 1 : 0;
        if (state.CurrentSplitIndex > runEndedDelay)
        {
            lastSplit = state.Run[state.CurrentSplitIndex - 1 - runEndedDelay].SplitTime[method];
        }

        if (state.CurrentPhase == TimerPhase.NotRunning)
        {
            return state.Run.Offset;
        }

        return (state.CurrentTime[method] ?? state.CurrentTime.RealTime) - lastSplit;
    }
}
