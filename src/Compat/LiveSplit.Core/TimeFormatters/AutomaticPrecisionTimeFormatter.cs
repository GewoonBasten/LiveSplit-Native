// Port of LiveSplit.Core TimeFormatters/AutomaticPrecisionTimeFormatter.cs @ 93a11b85 (MIT).
namespace LiveSplit.TimeFormatters;

public class AutomaticPrecisionTimeFormatter : GeneralTimeFormatter
{
    public AutomaticPrecisionTimeFormatter()
    {
        NullFormat = NullFormat.ZeroWithAccuracy;
        DigitsFormat = DigitsFormat.SingleDigitMinutes;
        Accuracy = TimeAccuracy.Hundredths;
        AutomaticPrecision = true;
    }
}
