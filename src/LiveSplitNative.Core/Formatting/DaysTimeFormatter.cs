using System.Globalization;
using System.Text;

namespace LiveSplitNative.Core.Formatting;

/// <summary>
/// LiveSplit.TotalPlaytime DaysTimeFormatter: "2d 3:04:05", "3:04:05" or "4:05" (whole seconds,
/// truncated), "0" for no time.
/// </summary>
public static class DaysTimeFormatter
{
    public static string Format(TimeSpan? time)
    {
        if (time is not { } value)
        {
            return "0";
        }

        var builder = new StringBuilder();
        if (value.TotalDays >= 1)
        {
            builder.Append((int)value.TotalDays).Append("d ");
        }

        builder.Append(value.ToString(value.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
