using System.Globalization;

namespace LiveSplitNative.Core.Formatting;

public enum TimeAccuracy
{
    Seconds,
    Tenths,
    Hundredths,
    Milliseconds,
}

public enum DigitsFormat
{
    SingleDigitSeconds,
    DoubleDigitSeconds,
    SingleDigitMinutes,
    DoubleDigitMinutes,
    SingleDigitHours,
    DoubleDigitHours,
}

/// <summary>How a missing time is shown. Same members and order as upstream LiveSplit.TimeFormatters.NullFormat.</summary>
public enum NullFormat
{
    /// <summary>"-".</summary>
    Dash,

    /// <summary>"0", "0.0", "0.00" or "0.000", from <see cref="TimeAccuracy"/>.</summary>
    ZeroWithAccuracy,

    /// <summary>Always "0.00".</summary>
    ZeroDotZeroZero,

    /// <summary>Formatted exactly like <see cref="TimeSpan.Zero"/>.</summary>
    ZeroValue,

    /// <summary>Like <see cref="ZeroValue"/> with every zero replaced by a dash, e.g. "-:--".</summary>
    Dashes,
}

public static class TimeFormatConstants
{
    /// <summary>U+2212 MINUS SIGN, as upstream.</summary>
    public const string Minus = "−";

    public const string Dash = "-";
}

/// <summary>
/// Port of LiveSplit.TimeFormatters.GeneralTimeFormatter (upstream 93a11b85).
/// Times are truncated, never rounded, exactly as <see cref="TimeSpan.ToString(string, IFormatProvider)"/> does upstream.
/// Defaults differ from upstream only where LiveSplitNative callers relied on them
/// (Hundredths and ZeroWithAccuracy); every caller in the app sets them explicitly.
/// </summary>
public sealed class GeneralTimeFormatter
{
    private static readonly CultureInfo Ic = CultureInfo.InvariantCulture;

    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Hundredths;

    public DigitsFormat DigitsFormat { get; set; } = DigitsFormat.SingleDigitSeconds;

    public NullFormat NullFormat { get; set; } = NullFormat.ZeroWithAccuracy;

    /// <summary>Show "1d 23:59:10" instead of "47:59:10" for durations of 24 hours or more.</summary>
    public bool ShowDays { get; set; }

    /// <summary>Prefix positive, non-zero times with "+".</summary>
    public bool ShowPlus { get; set; }

    /// <summary>Hide decimals once the absolute time is a minute or more.</summary>
    public bool DropDecimals { get; set; }

    /// <summary>Hide trailing zero decimal places.</summary>
    public bool AutomaticPrecision { get; set; }

    public string Format(TimeSpan? timeNullable)
    {
        bool isNull = !timeNullable.HasValue;
        if (isNull)
        {
            switch (NullFormat)
            {
                case NullFormat.Dash:
                    return TimeFormatConstants.Dash;
                case NullFormat.ZeroWithAccuracy:
                    return ZeroWithAccuracy();
                case NullFormat.ZeroDotZeroZero:
                    return "0.00";
                default:
                    timeNullable = TimeSpan.Zero;
                    break;
            }
        }

        TimeSpan time = timeNullable!.Value;
        string minusString;
        if (time == TimeSpan.Zero)
        {
            minusString = "";
        }
        else if (time < TimeSpan.Zero)
        {
            minusString = TimeFormatConstants.Minus;
            time = -time;
        }
        else
        {
            minusString = ShowPlus ? "+" : "";
        }

        string decimalFormat = DecimalFormat(time);
        string formatted;
        if (time.TotalDays >= 1 && !ShowDays)
        {
            // Days roll into the hour count, e.g. "47:59:10".
            formatted = minusString
                + ((int)time.TotalHours).ToString(Ic)
                + time.ToString(@"\:mm\:ss" + decimalFormat, Ic);
        }
        else
        {
            // A larger actual magnitude forces a bigger unit than DigitsFormat would pick,
            // so those guards come before the enum arms (same order as upstream).
            string format = DigitsFormat switch
            {
                DigitsFormat.DoubleDigitHours when time.TotalDays >= 1 => @"d\d\ hh\:mm\:ss",
                _ when time.TotalDays >= 1 => @"d\d\ h\:mm\:ss",
                DigitsFormat.DoubleDigitHours => @"hh\:mm\:ss",
                _ when time.TotalHours >= 1 => @"h\:mm\:ss",
                DigitsFormat.SingleDigitHours => @"h\:mm\:ss",
                DigitsFormat.DoubleDigitMinutes => @"mm\:ss",
                _ when time.TotalMinutes >= 1 => @"m\:ss",
                DigitsFormat.SingleDigitMinutes => @"m\:ss",
                DigitsFormat.DoubleDigitSeconds => @"ss",
                _ => @"%s",
            };
            formatted = minusString + time.ToString(format + decimalFormat, Ic);
        }

        if (isNull && NullFormat == NullFormat.Dashes)
        {
            formatted = formatted.Replace('0', '-');
        }

        return formatted;
    }

    private string DecimalFormat(TimeSpan time)
    {
        if (AutomaticPrecision)
        {
            double totalSeconds = time.TotalSeconds;
            if (Accuracy == TimeAccuracy.Seconds || totalSeconds % 1 == 0)
            {
                return "";
            }

            if (Accuracy == TimeAccuracy.Tenths || 10 * totalSeconds % 1 == 0)
            {
                return @"\.f";
            }

            if (Accuracy == TimeAccuracy.Hundredths || 100 * totalSeconds % 1 == 0)
            {
                return @"\.ff";
            }

            return @"\.fff";
        }

        if (DropDecimals && time.TotalMinutes >= 1)
        {
            return "";
        }

        return Accuracy switch
        {
            TimeAccuracy.Seconds => "",
            TimeAccuracy.Tenths => @"\.f",
            TimeAccuracy.Hundredths => @"\.ff",
            TimeAccuracy.Milliseconds => @"\.fff",
            _ => "",
        };
    }

    private string ZeroWithAccuracy()
    {
        if (AutomaticPrecision || Accuracy == TimeAccuracy.Seconds)
        {
            return "0";
        }

        return Accuracy switch
        {
            TimeAccuracy.Tenths => "0.0",
            TimeAccuracy.Milliseconds => "0.000",
            _ => "0.00",
        };
    }
}
