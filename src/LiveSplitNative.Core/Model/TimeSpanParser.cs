using System.Globalization;

namespace LiveSplitNative.Core.Model;

/// <summary>Port of LiveSplit.Model.TimeSpanParser (colon-separated split times).</summary>
public static class TimeSpanParser
{
    private static readonly char[] Separators = [':'];
    private static readonly char[] Dot = ['.'];
    private static readonly char[] NegativeSigns = ['-', '−', '–'];

    public static TimeSpan? ParseNullable(string? timeString)
    {
        if (string.IsNullOrWhiteSpace(timeString) || timeString.Trim() == "-")
        {
            return null;
        }

        return Parse(timeString);
    }

    /// <summary>
    /// Like <see cref="ParseNullable"/> but never throws — used for .lss XML load where
    /// BCL <c>TimeSpan.TryParse</c> is insufficient (9-digit fractional seconds).
    /// </summary>
    public static bool TryParse(string? timeString, out TimeSpan result)
    {
        result = default;
        try
        {
            TimeSpan? parsed = ParseNullable(timeString);
            if (parsed is null)
            {
                return false;
            }

            result = parsed.Value;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static TimeSpan Parse(string timeString)
    {
        // No Trim(): it shortened the text, which the length check below then read as a
        // minus sign (" 1:00" became -1:00). long.Parse already skips the surrounding
        // whitespace of each section, exactly as upstream relies on.
        int originalLength = timeString.Length;
        timeString = timeString.TrimStart(NegativeSigns);
        long factor = timeString.Length < originalLength ? -1L : 1L;

        long ticks = 0L;
        string[] sections = timeString.Split(Separators, 3);

        if (sections.Length > 2)
        {
            string[] daysDotHours = sections[0].Split(Dot, 2);
            if (daysDotHours.Length == 2)
            {
                ticks += long.Parse(daysDotHours[0], CultureInfo.InvariantCulture) * TimeSpan.TicksPerDay;
            }

            ticks += long.Parse(daysDotHours[^1], CultureInfo.InvariantCulture) * TimeSpan.TicksPerHour;
        }

        if (sections.Length > 1)
        {
            ticks += long.Parse(sections[^2], CultureInfo.InvariantCulture) * TimeSpan.TicksPerMinute;
        }

        string[] seconds = sections[^1].Split(Dot, 3);
        ticks += long.Parse(seconds[0], CultureInfo.InvariantCulture) * TimeSpan.TicksPerSecond;
        if (seconds.Length > 1)
        {
            ticks += ParseFractionAsTicks(seconds[1]);
        }

        return TimeSpan.FromTicks(factor * ticks);
    }

    private static long ParseFractionAsTicks(string fractionText)
    {
        if (fractionText.Length > 7)
        {
            fractionText = fractionText[..7];
        }

        return long.Parse(fractionText, NumberStyles.Integer, CultureInfo.InvariantCulture)
               * PowersOfTen[7 - fractionText.Length];
    }

    private static readonly long[] PowersOfTen =
    [
        1L, 10L, 100L, 1000L, 10000L, 100000L, 1000000L, 10000000L,
    ];
}
