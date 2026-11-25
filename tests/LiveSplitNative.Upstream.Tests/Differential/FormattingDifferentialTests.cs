using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using Xunit;
using N = LiveSplitNative.Core.Model;
using NF = LiveSplitNative.Core.Formatting;
using U = LiveSplit.Model;
using UF = LiveSplit.TimeFormatters;

namespace LiveSplitNative.Upstream.Tests.Differential;

/// <summary>
/// GeneralTimeFormatter and TimeSpanParser compared with upstream on the same inputs:
/// every option combination of the formatter over edge and random times, and the parser
/// over formatter output, .lss-style strings and mangled variants.
/// </summary>
public class FormattingDifferentialTests
{
    private static readonly bool[] Bools = { false, true };

    private static List<TimeSpan?> Times()
    {
        var times = new List<TimeSpan?> { null, TimeSpan.Zero, TimeSpan.MaxValue, TimeSpan.MinValue };
        long[] edgeTicks =
        {
            1, 9_999, 10_000, 99_999, 100_000, 999_999, 1_000_000, 9_999_999,
            TimeSpan.TicksPerSecond, 99_999_999, 100_000_000, 599_999_999, 600_000_000, 610_000_000,
            5_999_999_999, 6_000_000_000, 35_999_999_999, 36_000_000_000, 359_999_999_999, 360_000_000_000,
            TimeSpan.TicksPerDay - 1, TimeSpan.TicksPerDay, TimeSpan.TicksPerDay + 1,
            TimeSpan.TicksPerHour * 25, TimeSpan.TicksPerHour * 99, TimeSpan.TicksPerHour * 100,
            TimeSpan.TicksPerHour * 1000, TimeSpan.TicksPerHour * 100_000,
        };
        foreach (long ticks in edgeTicks)
        {
            times.Add(TimeSpan.FromTicks(ticks));
            times.Add(TimeSpan.FromTicks(-ticks));
        }

        var rng = new Random(1234);
        for (int i = 0; i < 400; i++)
        {
            // Log-uniform magnitude from 1 tick to about 30 days.
            long ticks = (long)Math.Pow(10, rng.NextDouble() * 13.4);
            times.Add(TimeSpan.FromTicks(rng.Next(4) == 0 ? -ticks : ticks));
        }

        return times;
    }

    private static string Try(Func<string> format)
    {
        try
        {
            return format();
        }
        catch (Exception ex)
        {
            return "throws " + ex.GetType().Name;
        }
    }

    [Fact]
    public void GeneralTimeFormatter_Matches_Upstream_For_Every_Option_Combination()
    {
        List<TimeSpan?> times = Times();
        var failures = new List<string>();
        int checkedCount = 0;

        for (int accuracy = 0; accuracy < 4; accuracy++)
        {
            for (int digits = 0; digits < 6; digits++)
            {
                for (int nullFormat = 0; nullFormat < 5; nullFormat++)
                {
                    foreach (bool showDays in Bools)
                    {
                        foreach (bool showPlus in Bools)
                        {
                            foreach (bool dropDecimals in Bools)
                            {
                                foreach (bool automaticPrecision in Bools)
                                {
                                    var upstream = new UF.GeneralTimeFormatter
                                    {
                                        Accuracy = (UF.TimeAccuracy)accuracy,
                                        DigitsFormat = (UF.DigitsFormat)digits,
                                        NullFormat = (UF.NullFormat)nullFormat,
                                        ShowDays = showDays,
                                        ShowPlus = showPlus,
                                        DropDecimals = dropDecimals,
                                        AutomaticPrecision = automaticPrecision,
                                    };
                                    var native = new NF.GeneralTimeFormatter
                                    {
                                        Accuracy = (NF.TimeAccuracy)accuracy,
                                        DigitsFormat = (NF.DigitsFormat)digits,
                                        NullFormat = (NF.NullFormat)nullFormat,
                                        ShowDays = showDays,
                                        ShowPlus = showPlus,
                                        DropDecimals = dropDecimals,
                                        AutomaticPrecision = automaticPrecision,
                                    };

                                    foreach (TimeSpan? time in times)
                                    {
                                        checkedCount++;
                                        string u = Try(() => upstream.Format(time));
                                        string n = Try(() => native.Format(time));
                                        if (u != n && failures.Count < 40)
                                        {
                                            failures.Add(
                                                $"{(UF.TimeAccuracy)accuracy}, {(UF.DigitsFormat)digits}, {(UF.NullFormat)nullFormat}, "
                                                + $"days={showDays}, plus={showPlus}, drop={dropDecimals}, auto={automaticPrecision}, "
                                                + $"time {TwinTimer.T(time)}: upstream \"{u}\", native \"{n}\"");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, $"{checkedCount} checks; first differences:\n" + string.Join("\n", failures));
    }

    private static IEnumerable<string> ParserInputs()
    {
        var upstreamFormatter = new UF.GeneralTimeFormatter();
        var inputs = new List<string>
        {
            "0", "1", "1.5", "59.99", "1:00", "1:00.5", "10:00:00", "1.00:00:00", "1.02:03:04.5",
            "00:01:39.3900000", "00:01:39.390000000", "00:01:39.39000000001", "01:23:45.6789012",
            "-1.5", "−1.5", "–1.5", "--1", "−-1", "+1.5", "-0:01.5", "−1:00:00",
            "1:2:3", "1:2:3:4", "100:00:00", "25:00:00.00", "1d 1:00:00.00",
            " 1:00", "1:00 ", " 1:00 ", "\t5", "5\n", " -1:00", "- 1:00",
            "", " ", "-", "−", ".", ":", "1:", ":1", "1..5", "1.5.5", "a", "1:aa", "1e3", "0x10",
            "99999999999999", "9223372036854775807", "1.", ".5",
        };

        var rng = new Random(99);
        for (int i = 0; i < 300; i++)
        {
            long ticks = (long)Math.Pow(10, rng.NextDouble() * 13.4);
            var time = TimeSpan.FromTicks(rng.Next(4) == 0 ? -ticks : ticks);
            inputs.Add(time.ToString("c", CultureInfo.InvariantCulture));
            upstreamFormatter.Accuracy = (UF.TimeAccuracy)rng.Next(4);
            upstreamFormatter.DigitsFormat = (UF.DigitsFormat)rng.Next(6);
            inputs.Add(upstreamFormatter.Format(time));
        }

        return inputs;
    }

    [Fact]
    public void TimeSpanParser_Agrees_With_Upstream_Wherever_Upstream_Accepts_The_Text()
    {
        var failures = new List<string>();
        var lenient = new List<string>();
        foreach (string text in ParserInputs())
        {
            string u = Try(() => TwinTimer.T(U.TimeSpanParser.ParseNullable(text)));
            string n = Try(() => TwinTimer.T(N.TimeSpanParser.ParseNullable(text)));
            if (u == n)
            {
                continue;
            }

            string line = $"\"{Escape(text)}\": upstream {u}, native {n}";
            if (u.StartsWith("throws", StringComparison.Ordinal))
            {
                // Native accepts more than upstream on purpose (9-digit fractions, "-" as empty).
                lenient.Add(line);
            }
            else
            {
                failures.Add(line);
            }
        }

        Assert.True(
            failures.Count == 0,
            "Parsed differently from upstream:\n" + string.Join("\n", failures)
            + "\n\nAccepted by native only (allowed):\n" + string.Join("\n", lenient));
    }

    [Fact]
    public void Split_Times_In_Lss_Xml_Read_Like_Upstream()
    {
        // .lss files store every time in TimeSpan's "c" format. Upstream reads them with
        // TimeSpan.TryParse; native uses TimeSpanParser so that it also accepts the
        // 9-digit fractions livesplit-core writes. On "c" text both must agree exactly.
        var rng = new Random(7);
        var failures = new List<string>();
        var document = new XmlDocument();
        for (int i = 0; i < 2000; i++)
        {
            long ticks = (long)Math.Pow(10, rng.NextDouble() * 13.4);
            var realTime = TimeSpan.FromTicks(rng.Next(5) == 0 ? -ticks : ticks);
            var gameTime = TimeSpan.FromTicks((long)(ticks * rng.NextDouble()));
            XmlElement element = document.CreateElement("SplitTime");
            if (rng.Next(4) != 0)
            {
                XmlElement rt = document.CreateElement("RealTime");
                rt.InnerText = realTime.ToString("c", CultureInfo.InvariantCulture);
                element.AppendChild(rt);
            }

            if (rng.Next(3) == 0)
            {
                XmlElement gt = document.CreateElement("GameTime");
                gt.InnerText = gameTime.ToString("c", CultureInfo.InvariantCulture);
                element.AppendChild(gt);
            }

            U.Time upstream = U.Time.FromXml(element);
            N.SplitTime native = N.SplitTime.FromXml(element);
            if (upstream.RealTime != native.RealTime || upstream.GameTime != native.GameTime)
            {
                failures.Add($"{element.OuterXml}: upstream {TwinTimer.T(upstream.RealTime)} | {TwinTimer.T(upstream.GameTime)}, "
                    + $"native {TwinTimer.T(native.RealTime)} | {TwinTimer.T(native.GameTime)}");
            }
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(20)));
    }

    private static string Escape(string text)
    {
        var builder = new StringBuilder();
        foreach (char c in text)
        {
            builder.Append(c switch
            {
                '\t' => "\\t",
                '\n' => "\\n",
                _ => c.ToString(),
            });
        }

        return builder.ToString();
    }
}
