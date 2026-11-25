using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.Tests;

public class TimeFormatterTests
{
    [Fact]
    public void Default_Timer_Format_Uses_Hundredths()
    {
        var formatter = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Hundredths,
            DigitsFormat = DigitsFormat.SingleDigitSeconds,
            NullFormat = NullFormat.ZeroWithAccuracy,
        };
        Assert.Equal("0.00", formatter.Format(null));
        Assert.Equal("5.50", formatter.Format(TimeSpan.FromMilliseconds(5500)));
        Assert.Equal("1:05.50", formatter.Format(TimeSpan.FromMilliseconds(65500)));
    }

    [Fact]
    public void SplitTimeFormatter_Uses_SingleDigitMinutes_Like_Upstream()
    {
        // LiveSplit.TimeFormatters.SplitTimeFormatter → DigitsFormat.SingleDigitMinutes.
        var formatter = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Seconds,
            DigitsFormat = DigitsFormat.SingleDigitMinutes,
            NullFormat = NullFormat.Dash,
        };
        Assert.Equal("-", formatter.Format(null));
        Assert.Equal("0:09", formatter.Format(TimeSpan.FromSeconds(9)));
        Assert.Equal("0:38", formatter.Format(TimeSpan.FromSeconds(38)));
        Assert.Equal("1:05", formatter.Format(TimeSpan.FromSeconds(65)));
        Assert.Equal("1:09", formatter.Format(TimeSpan.FromSeconds(69)));
    }

    [Fact]
    public void DeltaFormatter_Keeps_SingleDigitSeconds_For_Short_Deltas()
    {
        // Upstream DeltaSplitTimeFormatter leaves DigitsFormat at SingleDigitSeconds default.
        var formatter = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Tenths,
            DigitsFormat = DigitsFormat.SingleDigitSeconds,
            NullFormat = NullFormat.Dash,
            DropDecimals = true,
        };
        Assert.Equal("4.0", formatter.Format(TimeSpan.FromSeconds(4)));
        // DropDecimals: no fractional part once TotalMinutes >= 1.
        Assert.Equal("1:05", formatter.Format(TimeSpan.FromSeconds(65)));
    }
}
