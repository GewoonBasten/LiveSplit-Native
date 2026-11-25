using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.Tests;

public class TimeSpanParserTests
{
    [Theory]
    [InlineData("1:23.45", 83.45)]
    [InlineData("12.34", 12.34)]
    [InlineData("1:02:03.5", 3723.5)]
    [InlineData("-1:00.00", -60.0)]
    public void Parse_Matches_Expected_Seconds(string input, double expectedSeconds)
    {
        TimeSpan parsed = TimeSpanParser.Parse(input);
        Assert.Equal(expectedSeconds, parsed.TotalSeconds, precision: 3);
    }

    [Theory]
    [InlineData(" 1:00", 60.0)]
    [InlineData("1:00 ", 60.0)]
    [InlineData(" 1:00 ", 60.0)]
    [InlineData("\t5", 5.0)]
    [InlineData("5\n", 5.0)]
    public void Surrounding_Whitespace_Does_Not_Make_The_Time_Negative(string input, double expectedSeconds)
    {
        // Found by the upstream differential test: Trim() shortened the text, which the
        // sign check read as a removed minus sign.
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), TimeSpanParser.Parse(input));
    }

    [Fact]
    public void ParseNullable_Empty_Or_Dash_Is_Null()
    {
        Assert.Null(TimeSpanParser.ParseNullable(""));
        Assert.Null(TimeSpanParser.ParseNullable("-"));
        Assert.Null(TimeSpanParser.ParseNullable(null));
    }

    [Theory]
    [InlineData("00:10:19.680000000", 619.68)]
    [InlineData("00:10:22.822192589", 622.8221925)]
    [InlineData("00:10:21.000000000", 621.0)]
    public void TryParse_Accepts_NineDigit_Fractional_Seconds(string input, double expectedSeconds)
    {
        Assert.True(TimeSpanParser.TryParse(input, out TimeSpan parsed));
        Assert.Equal(expectedSeconds, parsed.TotalSeconds, precision: 6);
    }

    [Fact]
    public void BclTryParse_Rejects_NonZero_NineDigit_Fraction_That_LiveSplit_Writes()
    {
        // Root cause of empty PB column on SMS Any% (9).lss.
        const string pb = "00:10:19.680000000";
        Assert.False(TimeSpan.TryParse(pb, System.Globalization.CultureInfo.InvariantCulture, out _));
        Assert.True(TimeSpanParser.TryParse(pb, out TimeSpan parsed));
        Assert.Equal(619.68, parsed.TotalSeconds, precision: 6);
    }
}
