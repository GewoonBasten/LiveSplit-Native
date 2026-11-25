// Port of LiveSplit.Core TimeFormatters/RegularTimeFormatter.cs @ 93a11b85 (MIT).
namespace LiveSplit.TimeFormatters;

public class RegularTimeFormatter : GeneralTimeFormatter
{
    public RegularTimeFormatter(TimeAccuracy accuracy = TimeAccuracy.Seconds)
    {
        Accuracy = accuracy;
        NullFormat = NullFormat.ZeroWithAccuracy;
        DigitsFormat = DigitsFormat.SingleDigitMinutes;
    }
}
