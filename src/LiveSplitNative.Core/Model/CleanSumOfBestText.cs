using System.Globalization;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Localization;

namespace LiveSplitNative.Core.Model;

/// <summary>
/// The texts of RunEditorDialog.cleanSumOfBestToolStripMenuItem_Click (93a11b85): the question
/// asked about each time Clean Sum of Best finds, and the message when it finds none.
/// </summary>
public static class CleanSumOfBestText
{
    public static string QuestionTitle => UiText.T("Remove Time From Segment History?");

    public static string NothingTitle => UiText.T("No times to clean");

    public static string NothingText =>
        UiText.T("No times to clean. There are no potentially invalid segment history elements in the Sum of Best.");

    /// <summary>
    /// The time and where it was ("5.00 between A and C, which is faster than the Combined Best
    /// Segments of 20.00 in a run on 3/14/2026"): Windows asks once for each different text.
    /// </summary>
    public static string Description(SumOfBest.CleanUpCallbackParameters parameters)
    {
        // ShortTimeFormatter: hundredths, 0.00 for nothing, seconds as one digit.
        var formatter = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Hundredths,
            NullFormat = NullFormat.ZeroWithAccuracy,
            DigitsFormat = DigitsFormat.SingleDigitSeconds,
        };
        string text = formatter.Format(parameters.TimeBetween) + " " + UiText.T("between") + " "
            + (parameters.StartingSegment is { } start ? start.Name : UiText.T("the start of the run"))
            + " " + UiText.T("and") + " " + parameters.EndingSegment.Name
            + (parameters.CombinedSumOfBest is { } combined
                ? UiText.T(", which is faster than the Combined Best Segments of ") + formatter.Format(combined)
                : "");
        if (parameters.Attempt?.Ended is { } ended)
        {
            text += UiText.T(" in a run on ") + ended.Time.ToLocalTime().ToString("M/d/yyyy", CultureInfo.CurrentCulture);
        }

        return text;
    }

    /// <summary>The question for one time.</summary>
    public static string Question(SumOfBest.CleanUpCallbackParameters parameters) =>
        string.Format(
            CultureInfo.CurrentCulture,
            UiText.T("You had a {0} segment time of {1}. Do you think that this segment time is inaccurate and should be removed?"),
            parameters.Method == TimingMethod.RealTime ? UiText.T("Real Time") : UiText.T("Game Time"),
            Description(parameters));
}
