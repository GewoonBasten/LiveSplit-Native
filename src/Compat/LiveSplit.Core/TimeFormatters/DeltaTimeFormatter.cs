// Port of LiveSplit.Core TimeFormatters/DeltaTimeFormatter.cs @ 93a11b85 (MIT).
namespace LiveSplit.TimeFormatters;

public class DeltaTimeFormatter : GeneralTimeFormatter
{
    public DeltaTimeFormatter()
    {
        Accuracy = TimeAccuracy.Tenths;
        DropDecimals = true;
        ShowPlus = true;
    }
}
