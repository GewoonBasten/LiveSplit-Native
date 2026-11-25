using System.Globalization;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// The text of the Title component, as LiveSplit.Title's Title.Update builds it (93a11b85): the
/// category is the extended category name (variables, and region / platform when shown), and the
/// finished-runs count includes the attempt that just ended.
/// </summary>
public static class TitleLogic
{
    public static string CategoryText(Run run, TitleComponentSettings settings) =>
        run.GetExtendedCategoryName(settings.ShowRegion, settings.ShowPlatform, settings.ShowVariables);

    /// <summary>Finished attempts (real time) in the history, plus the current one once it has ended.</summary>
    public static int FinishedRunsCount(Run run, TimerPhase phase) =>
        run.AttemptHistory.Count(x => x.Time.RealTime != null) + (phase == TimerPhase.Ended ? 1 : 0);

    /// <summary>"finished/attempts", one of them, or "" as the two count settings choose.</summary>
    public static string CountText(Run run, TimerPhase phase, TitleComponentSettings settings)
    {
        if (settings.ShowAttemptCount && settings.ShowFinishedRunsCount)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", FinishedRunsCount(run, phase), run.AttemptCount);
        }

        if (settings.ShowAttemptCount)
        {
            return run.AttemptCount.ToString(CultureInfo.InvariantCulture);
        }

        return settings.ShowFinishedRunsCount
            ? FinishedRunsCount(run, phase).ToString(CultureInfo.InvariantCulture)
            : "";
    }
}
