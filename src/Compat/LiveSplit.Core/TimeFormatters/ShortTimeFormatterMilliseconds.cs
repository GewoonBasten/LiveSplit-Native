// Port of LiveSplit.Core TimeFormatters/ShortTimeFormatterMilliseconds.cs @ 93a11b85 (MIT).
namespace LiveSplit.TimeFormatters;

public class ShortTimeFormatterMilliseconds : GeneralTimeFormatter
{
    public ShortTimeFormatterMilliseconds(DigitsFormat format = DigitsFormat.SingleDigitSeconds)
    {
        Accuracy = TimeAccuracy.Milliseconds;
        NullFormat = NullFormat.ZeroWithAccuracy;
        DigitsFormat = format;
    }
}
