// Port of LiveSplit.Core TimeFormatters/PossibleTimeSaveFormatter.cs @ 93a11b85 (MIT).
namespace LiveSplit.TimeFormatters;

public class PossibleTimeSaveFormatter : GeneralTimeFormatter
{
    public PossibleTimeSaveFormatter()
    {
        Accuracy = TimeAccuracy.Seconds;
        DropDecimals = false;
        NullFormat = NullFormat.Dash;
    }
}
