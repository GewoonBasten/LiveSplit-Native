using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.Tests;

/// <summary>Expected strings follow upstream GeneralTimeFormatter (93a11b85) line by line.</summary>
public class TimeFormatterParityTests
{
    private static GeneralTimeFormatter Timer() => new()
    {
        Accuracy = TimeAccuracy.Hundredths,
        DigitsFormat = DigitsFormat.SingleDigitSeconds,
        NullFormat = NullFormat.ZeroWithAccuracy,
    };

    [Theory]
    [InlineData(25, 0, 0, 0, "25:00:00.00")]
    [InlineData(47, 59, 10, 0, "47:59:10.00")]
    [InlineData(100, 0, 1, 500, "100:00:01.50")]
    [InlineData(23, 59, 59, 999, "23:59:59.99")]
    public void Hours_Of_24_And_More_Keep_Their_Days(int hours, int minutes, int seconds, int ms, string expected)
    {
        var time = new TimeSpan(0, hours, minutes, seconds, ms);
        Assert.Equal(expected, Timer().Format(time));
    }

    [Fact]
    public void ShowDays_Writes_Days_Separately()
    {
        GeneralTimeFormatter f = Timer();
        f.ShowDays = true;
        Assert.Equal("1d 1:00:00.00", f.Format(TimeSpan.FromHours(25)));

        f.DigitsFormat = DigitsFormat.DoubleDigitHours;
        Assert.Equal("1d 01:00:00.00", f.Format(TimeSpan.FromHours(25)));
    }

    [Fact]
    public void Times_Are_Truncated_Not_Rounded()
    {
        GeneralTimeFormatter f = Timer();
        Assert.Equal("1.99", f.Format(TimeSpan.FromMilliseconds(1999)));
        Assert.Equal("59.99", f.Format(TimeSpan.FromMilliseconds(59_999)));
        Assert.Equal("1:00.00", f.Format(TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void Signs_Use_The_Minus_Sign_And_Optional_Plus()
    {
        GeneralTimeFormatter f = Timer();
        Assert.Equal("−1.50", f.Format(TimeSpan.FromMilliseconds(-1500)));
        Assert.Equal("1.50", f.Format(TimeSpan.FromMilliseconds(1500)));

        f.ShowPlus = true;
        Assert.Equal("+1.50", f.Format(TimeSpan.FromMilliseconds(1500)));
        Assert.Equal("0.00", f.Format(TimeSpan.Zero));
    }

    [Fact]
    public void Null_Formats_Match_Upstream()
    {
        var f = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Seconds,
            DigitsFormat = DigitsFormat.SingleDigitMinutes,
        };

        f.NullFormat = NullFormat.Dash;
        Assert.Equal("-", f.Format(null));
        f.NullFormat = NullFormat.ZeroWithAccuracy;
        Assert.Equal("0", f.Format(null));
        f.NullFormat = NullFormat.ZeroDotZeroZero;
        Assert.Equal("0.00", f.Format(null));
        f.NullFormat = NullFormat.ZeroValue;
        Assert.Equal("0:00", f.Format(null));
        f.NullFormat = NullFormat.Dashes;
        Assert.Equal("-:--", f.Format(null));
    }

    [Fact]
    public void AutomaticPrecision_Drops_Trailing_Zero_Places()
    {
        var f = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Milliseconds,
            AutomaticPrecision = true,
        };

        Assert.Equal("1", f.Format(TimeSpan.FromSeconds(1)));
        Assert.Equal("1.5", f.Format(TimeSpan.FromMilliseconds(1500)));
        Assert.Equal("1.25", f.Format(TimeSpan.FromMilliseconds(1250)));
        Assert.Equal("1.125", f.Format(TimeSpan.FromMilliseconds(1125)));
        Assert.Equal("0", f.Format(null));
    }

    [Fact]
    public void DropDecimals_Applies_From_One_Minute()
    {
        var f = new GeneralTimeFormatter
        {
            Accuracy = TimeAccuracy.Tenths,
            NullFormat = NullFormat.Dash,
            DropDecimals = true,
        };

        Assert.Equal("59.9", f.Format(TimeSpan.FromMilliseconds(59_950)));
        Assert.Equal("1:00", f.Format(TimeSpan.FromSeconds(60)));
    }
}
