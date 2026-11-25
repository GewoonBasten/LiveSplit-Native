using System.Globalization;
using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Serialization;

/// <summary>
/// Opens a LiveSplit splits file the way Windows LiveSplit 93a11b85 does: livesplit-core's
/// LiveSplit parser (lib/livesplit-core 61070c4, src/run/parser/livesplit.rs) followed by
/// StandardFormatsRunFactory, which turns segment groups back into subsplit names ("-" members,
/// "{Group} " on the last one). Everything that makes livesplit-core reject a file throws
/// <see cref="InvalidDataException"/> (or <see cref="XmlException"/> for broken XML), as Windows
/// then cannot open it. Elements livesplit-core does not read are kept as unknown elements.
/// </summary>
internal static class LssParser
{
    private const string RacePrefix = "[Race]";

    /// <summary>livesplit-core's comparison generators (comparison::default_generators).</summary>
    private static readonly HashSet<string> GeneratorNames = new(StringComparer.Ordinal)
    {
        "Best Segments", "Best Split Times", "Average Segments", "Median Segments",
        "Worst Segments", "Balanced PB", "Latest Run", "None",
    };

    private static readonly char[] AsciiWhitespace = [' ', '\t', '\n', '\f', '\r'];

    private static readonly LssVersion V1_3 = new(1, 3, 0, 0);
    private static readonly LssVersion V1_4_1 = new(1, 4, 1, 0);
    private static readonly LssVersion V1_5 = new(1, 5, 0, 0);
    private static readonly LssVersion V1_6 = new(1, 6, 0, 0);
    private static readonly LssVersion V1_7 = new(1, 7, 0, 0);
    private static readonly LssVersion V1_8_1 = new(1, 8, 1, 0);

    public static Run Parse(XmlDocument document)
    {
        XmlElement root = document.DocumentElement ?? throw Invalid("There is no <Run>.");
        if (root.Name != "Run")
        {
            throw Invalid($"Expected <Run>, got <{root.Name}>.");
        }

        LssVersion version = root.HasAttribute("version") ? LssVersion.Parse(root.GetAttribute("version")) : new(1, 0, 0, 0);
        var run = new Run { Version = root.HasAttribute("version") ? root.GetAttribute("version") : "1.7.0" };
        List<SegmentGroup>? nativeGroups = null;
        int required = 0;

        foreach (XmlElement element in root.ChildNodes.OfType<XmlElement>())
        {
            switch (element.Name)
            {
                case "GameIcon":
                    required |= 1;
                    run.GameIconXml = element.InnerXml;
                    break;
                case "GameName":
                    required |= 1 << 1;
                    run.GameName = Text(element);
                    break;
                case "CategoryName":
                    required |= 1 << 2;
                    run.CategoryName = Text(element);
                    break;
                case "Offset":
                    required |= 1 << 3;
                    run.Offset = LssTime.Parse(Text(element));
                    break;
                case "AttemptCount":
                    required |= 1 << 4;
                    run.AttemptCount = unchecked((int)ParseUnsigned(Text(element), uint.MaxValue));
                    break;
                case "AttemptHistory":
                    if (version >= V1_5)
                    {
                        ParseAttemptHistory(element, version, run);
                    }

                    break;
                case "RunHistory":
                    if (version < V1_5)
                    {
                        ParseRunHistory(element, version, run);
                    }

                    break;
                case "Metadata":
                    if (version >= V1_6)
                    {
                        ParseMetadata(element, run.Metadata);
                    }

                    break;
                case "Segments":
                    required |= 1 << 5;
                    foreach (XmlElement segment in element.ChildNodes.OfType<XmlElement>().Where(e => e.Name == "Segment"))
                    {
                        run.AddSegment(ParseSegment(segment, version, run));
                    }

                    break;
                case "SegmentGroups":
                    // from_vec_lossy with the segments read so far (groups before <Segments> are lost).
                    nativeGroups = Repair(ParseSegmentGroups(element), run.Segments.Count);
                    break;
                case "AutoSplitterSettings":
                    // reencode_children writes every element with an end tag (<X></X>, not <X />).
                    foreach (XmlElement descendant in element.GetElementsByTagName("*").OfType<XmlElement>())
                    {
                        descendant.IsEmpty = false;
                    }

                    run.AutoSplitterSettingsXml = element.InnerXml;
                    break;
                case "LayoutPath":
                    run.LayoutPath = Text(element);
                    break;
                default:
                    run.UnknownRootOuterXml.Add(element.OuterXml);
                    break;
            }
        }

        if (required != (1 << 6) - 1)
        {
            throw Invalid("A required element is missing.");
        }

        // StandardFormatsRunFactory: a run needs at least one segment.
        if (run.Segments.Count < 1)
        {
            throw Invalid("The run has no segments.");
        }

        List<SegmentGroup> groups;
        if (nativeGroups is not null)
        {
            groups = Repair(nativeGroups, run.Segments.Count);
        }
        else if (version < V1_8_1)
        {
            groups = ImportLegacySubsplits(run);
        }
        else
        {
            groups = [];
        }

        ApplyLegacySegmentNames(run, groups);
        return run;
    }

    private static Segment ParseSegment(XmlElement element, LssVersion version, Run run)
    {
        var segment = new Segment("");
        foreach (XmlElement child in element.ChildNodes.OfType<XmlElement>())
        {
            switch (child.Name)
            {
                case "Name":
                    segment.Name = Text(child);
                    break;
                case "Icon":
                    segment.IconXml = child.InnerXml;
                    break;
                case "SplitTimes":
                    if (version >= V1_3)
                    {
                        foreach (XmlElement splitTime in child.ChildNodes.OfType<XmlElement>().Where(e => e.Name == "SplitTime"))
                        {
                            string name = RequiredAttribute(splitTime, "name");
                            segment.Comparisons[name] = Time(splitTime, version);
                            AddCustomComparison(run, name);
                        }
                    }

                    break;
                case "PersonalBestSplitTime":
                    if (version < V1_3)
                    {
                        segment.Comparisons[Run.PersonalBestComparisonName] = OldTime(child);
                    }

                    break;
                case "BestSegmentTime":
                    segment.BestSegmentTime = Time(child, version);
                    break;
                case "SegmentHistory":
                    foreach (XmlElement time in child.ChildNodes.OfType<XmlElement>())
                    {
                        int index = (int)ParseSigned(RequiredAttribute(time, "id"), int.MinValue, int.MaxValue);
                        SplitTime value = Time(time, version);
                        // SegmentHistory::insert keeps the first time of an index.
                        segment.SegmentHistory.TryAdd(index, value);
                    }

                    break;
                default:
                    segment.UnknownOuterXml.Add(child.OuterXml);
                    break;
            }
        }

        // StandardFormatsRunFactory copies only the run's custom comparisons into a segment.
        foreach (string name in segment.Comparisons.Keys.Where(k => !run.CustomComparisons.Contains(k, StringComparer.Ordinal)).ToList())
        {
            segment.Comparisons.Remove(name);
        }

        return segment;
    }

    /// <summary>Run.add_custom_comparison: "[Race]" names make the file unreadable; existing names are ignored.</summary>
    private static void AddCustomComparison(Run run, string name)
    {
        if (name.StartsWith(RacePrefix, StringComparison.Ordinal))
        {
            throw Invalid($"The comparison \"{name}\" starts with {RacePrefix}.");
        }

        if (!GeneratorNames.Contains(name) && !run.CustomComparisons.Contains(name, StringComparer.Ordinal))
        {
            run.CustomComparisons.Add(name);
        }
    }

    private static void ParseAttemptHistory(XmlElement element, LssVersion version, Run run)
    {
        foreach (XmlElement attempt in element.ChildNodes.OfType<XmlElement>())
        {
            int? index = null;
            DateTime? started = null;
            DateTime? ended = null;
            bool startedSynced = false;
            bool endedSynced = false;
            foreach (XmlAttribute attribute in attempt.Attributes)
            {
                switch (attribute.Name)
                {
                    case "id":
                        index = (int)ParseSigned(attribute.Value, int.MinValue, int.MaxValue);
                        break;
                    case "started":
                        started = ParseDateTime(attribute.Value);
                        break;
                    case "isStartedSynced":
                        startedSynced = ParseBool(attribute.Value);
                        break;
                    case "ended":
                        ended = ParseDateTime(attribute.Value);
                        break;
                    case "isEndedSynced":
                        endedSynced = ParseBool(attribute.Value);
                        break;
                }
            }

            if (index is null)
            {
                throw Invalid("An attempt has no id.");
            }

            var time = new SplitTime();
            TimeSpan? pauseTime = null;
            foreach (XmlElement child in attempt.ChildNodes.OfType<XmlElement>())
            {
                switch (child.Name)
                {
                    case "RealTime":
                        time.RealTime = LssTime.ParseOptional(Text(child));
                        break;
                    case "GameTime":
                        time.GameTime = LssTime.ParseOptional(Text(child));
                        break;
                    case "PauseTime":
                        pauseTime = LssTime.ParseOptional(Text(child));
                        break;
                }
            }

            // Old LiveSplit versions could write an end before the start: that end is dropped.
            if (version <= V1_7 && ended is { } e && started is { } s && e < s)
            {
                ended = null;
            }

            run.AttemptHistory.Add(new Attempt(
                index.Value,
                time,
                started is { } st ? new AtomicDateTime(st, startedSynced) : null,
                ended is { } en ? new AtomicDateTime(en, endedSynced) : null,
                pauseTime));
        }
    }

    /// <summary>RunHistory (files before 1.5): attempts with a time and no dates.</summary>
    private static void ParseRunHistory(XmlElement element, LssVersion version, Run run)
    {
        foreach (XmlElement time in element.ChildNodes.OfType<XmlElement>())
        {
            int index = (int)ParseSigned(RequiredAttribute(time, "id"), int.MinValue, int.MaxValue);
            run.AttemptHistory.Add(new Attempt(index, Time(time, version), null, null, null));
        }
    }

    private static void ParseMetadata(XmlElement element, RunMetadata metadata)
    {
        foreach (XmlElement child in element.ChildNodes.OfType<XmlElement>())
        {
            switch (child.Name)
            {
                case "Run":
                    string id = RequiredAttribute(child, "id");
                    metadata.RunId = id.Length == 0 ? null : id;
                    break;
                case "Platform":
                    metadata.UsesEmulator = ParseBool(RequiredAttribute(child, "usesEmulator"));
                    metadata.PlatformName = Text(child);
                    break;
                case "Region":
                    metadata.RegionName = Text(child);
                    break;
                case "Variables":
                case "SpeedrunComVariables":
                    foreach (XmlElement variable in child.ChildNodes.OfType<XmlElement>())
                    {
                        string name = RequiredAttribute(variable, "name");
                        metadata.VariableValueNames[name] = Text(variable);
                    }

                    break;
                case "CustomVariables":
                    foreach (XmlElement variable in child.ChildNodes.OfType<XmlElement>())
                    {
                        string name = RequiredAttribute(variable, "name");
                        metadata.PermanentCustomVariables[name] = Text(variable);
                    }

                    break;
            }
        }
    }

    private static List<SegmentGroup> ParseSegmentGroups(XmlElement element)
    {
        var groups = new List<SegmentGroup>();
        foreach (XmlElement group in element.ChildNodes.OfType<XmlElement>().Where(e => e.Name == "SegmentGroup"))
        {
            long start = (long)Math.Min(ParseUnsigned(RequiredAttribute(group, "start"), ulong.MaxValue), int.MaxValue);
            long end = (long)Math.Min(ParseUnsigned(RequiredAttribute(group, "end"), ulong.MaxValue), int.MaxValue);
            string name = "";
            foreach (XmlElement child in group.ChildNodes.OfType<XmlElement>())
            {
                if (child.Name == "Name")
                {
                    name = Text(child);
                }
            }

            groups.Add(new SegmentGroup((int)start, (int)end, name.Length == 0 ? null : name));
        }

        return groups;
    }

    /// <summary>SegmentGroups::repair: sorted by start, clamped to the segments, overlaps cut, empty groups dropped.</summary>
    private static List<SegmentGroup> Repair(List<SegmentGroup> groups, int segmentCount)
    {
        var repaired = new List<SegmentGroup>();
        int minStart = 0;
        foreach (SegmentGroup group in groups.OrderBy(g => g.Start))
        {
            int start = Math.Min(Math.Max(group.Start, minStart), segmentCount);
            int end = Math.Min(Math.Max(group.End, start), segmentCount);
            if (end > start)
            {
                repaired.Add(group with { Start = start, End = end });
                minStart = end;
            }
        }

        return repaired;
    }

    /// <summary>
    /// import_legacy_subsplits (files before 1.8.1 without native groups): "-" segments open a
    /// group that the next segment closes; a "{Name}" prefix there names it.
    /// </summary>
    private static List<SegmentGroup> ImportLegacySubsplits(Run run)
    {
        int count = run.Segments.Count;
        var groups = new List<SegmentGroup>();
        int? groupStart = null;
        for (int index = 0; index < count; index++)
        {
            bool isFinal = index + 1 == count;
            Segment segment = run.Segments[index];
            string oldName = segment.Name;
            if (!isFinal && oldName.StartsWith('-'))
            {
                groupStart ??= index;
                segment.Name = oldName[1..];
                continue;
            }

            string? groupName = null;
            string? segmentName = null;
            if (oldName.StartsWith('{') && oldName.IndexOf('}', 1) is int close and >= 0)
            {
                groupName = oldName[1..close];
                segmentName = oldName[(close + 1)..].TrimStart();
            }

            if (groupStart is { } start)
            {
                groupStart = null;
                groups = Repair([.. groups, new SegmentGroup(start, index + 1, string.IsNullOrEmpty(groupName) ? null : groupName)], count);
                if (segmentName is not null)
                {
                    segment.Name = segmentName;
                }
            }
        }

        return groups;
    }

    /// <summary>StandardFormatsRunFactory.GetLegacySegmentNames: groups become "-" and "{Name} " prefixes.</summary>
    private static void ApplyLegacySegmentNames(Run run, List<SegmentGroup> groups)
    {
        foreach (SegmentGroup group in groups)
        {
            for (int i = group.Start; i < group.End - 1; i++)
            {
                run.Segments[i].Name = "-" + run.Segments[i].Name;
            }

            if (!string.IsNullOrEmpty(group.Name))
            {
                Segment last = run.Segments[group.End - 1];
                last.Name = $"{{{group.Name}}} {last.Name}";
            }
        }
    }

    /// <summary>time / time_old: RealTime and GameTime children from 1.4.1 on, the text as real time before.</summary>
    private static SplitTime Time(XmlElement element, LssVersion version)
    {
        if (version < V1_4_1)
        {
            return OldTime(element);
        }

        var time = new SplitTime();
        foreach (XmlElement child in element.ChildNodes.OfType<XmlElement>())
        {
            if (child.Name == "RealTime")
            {
                time.RealTime = LssTime.ParseOptional(Text(child));
            }
            else if (child.Name == "GameTime")
            {
                time.GameTime = LssTime.ParseOptional(Text(child));
            }
        }

        return time;
    }

    private static SplitTime OldTime(XmlElement element) => new() { RealTime = LssTime.ParseOptional(Text(element)) };

    /// <summary>The element's text, trimmed of ASCII whitespace as livesplit-core's XML reader does.</summary>
    private static string Text(XmlElement element) => element.InnerText.Trim(AsciiWhitespace);

    private static string RequiredAttribute(XmlElement element, string name) =>
        element.GetAttributeNode(name)?.Value ?? throw Invalid($"<{element.Name}> has no {name} attribute.");

    private static bool ParseBool(string text) => text switch
    {
        "True" => true,
        "False" => false,
        _ => throw Invalid($"\"{text}\" is not True or False."),
    };

    /// <summary>parse_date_time: "MM/dd/yyyy HH:mm:ss" in UTC.</summary>
    private static DateTime ParseDateTime(string text)
    {
        try
        {
            string[] date = Split(text, '/', out string rest1, 2);
            string year = Before(rest1, ' ', out string rest2);
            string hour = Before(rest2, ':', out string rest3);
            string minute = Before(rest3, ':', out string second);
            return new DateTime(
                (int)ParseSigned(year, int.MinValue, int.MaxValue),
                (int)ParseUnsigned(date[0], byte.MaxValue),
                (int)ParseUnsigned(date[1], byte.MaxValue),
                (int)ParseUnsigned(hour, byte.MaxValue),
                (int)ParseUnsigned(minute, byte.MaxValue),
                (int)ParseUnsigned(second, byte.MaxValue),
                DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw Invalid($"\"{text}\" is not a date.");
        }

        static string[] Split(string text, char separator, out string rest, int count)
        {
            var parts = new string[count];
            rest = text;
            for (int i = 0; i < count; i++)
            {
                parts[i] = Before(rest, separator, out rest);
            }

            return parts;
        }

        static string Before(string text, char separator, out string rest)
        {
            int at = text.IndexOf(separator);
            if (at < 0)
            {
                throw Invalid($"\"{text}\" is not a date.");
            }

            rest = text[(at + 1)..];
            return text[..at];
        }
    }

    /// <summary>Rust's unsigned integer parsing: an optional "+", then digits only.</summary>
    internal static ulong ParseUnsigned(string text, ulong max)
    {
        string digits = text.StartsWith('+') ? text[1..] : text;
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit)
            || !ulong.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) || value > max)
        {
            throw Invalid($"\"{text}\" is not a number.");
        }

        return value;
    }

    /// <summary>Rust's signed integer parsing: an optional sign, then digits only.</summary>
    internal static long ParseSigned(string text, long min, long max)
    {
        bool negative = text.StartsWith('-');
        string digits = negative || text.StartsWith('+') ? text[1..] : text;
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit)
            || !decimal.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out decimal magnitude))
        {
            throw Invalid($"\"{text}\" is not a number.");
        }

        decimal value = negative ? -magnitude : magnitude;
        if (value < min || value > max)
        {
            throw Invalid($"\"{text}\" is out of range.");
        }

        return (long)value;
    }

    internal static InvalidDataException Invalid(string message) => new(message);

    private sealed record SegmentGroup(int Start, int End, string? Name);
}

/// <summary>A splits file version (livesplit-core Version: up to four numbers, missing ones 0, the first 1).</summary>
internal readonly record struct LssVersion(uint Major, uint Minor, uint Build, uint Revision) : IComparable<LssVersion>
{
    public static LssVersion Parse(string text)
    {
        uint[] parts = [1, 0, 0, 0];
        string[] pieces = text.Split('.');
        for (int i = 0; i < Math.Min(4, pieces.Length); i++)
        {
            parts[i] = (uint)LssParser.ParseUnsigned(pieces[i], uint.MaxValue);
        }

        return new LssVersion(parts[0], parts[1], parts[2], parts[3]);
    }

    public int CompareTo(LssVersion other) =>
        (Major, Minor, Build, Revision).CompareTo((other.Major, other.Minor, other.Build, other.Revision));

    public static bool operator <(LssVersion a, LssVersion b) => a.CompareTo(b) < 0;

    public static bool operator >(LssVersion a, LssVersion b) => a.CompareTo(b) > 0;

    public static bool operator <=(LssVersion a, LssVersion b) => a.CompareTo(b) <= 0;

    public static bool operator >=(LssVersion a, LssVersion b) => a.CompareTo(b) >= 0;
}

/// <summary>
/// livesplit-core's time parsing for splits files (parse_time_span over TimeSpan::parse): an
/// optional "-" or "−", up to three ":" pieces (seconds and minutes below 60), up to nine
/// fraction digits (more are cut), "d.hh:mm:ss" with days; nanoseconds become ticks rounding
/// toward zero, as StandardFormatsRunFactory.ParseTimeSpan converts them.
/// </summary>
internal static class LssTime
{
    public static TimeSpan? ParseOptional(string text) => text.Length == 0 ? null : Parse(text);

    public static TimeSpan Parse(string text)
    {
        int dot = text.IndexOf('.');
        if (dot >= 0 && text.IndexOf(':', dot + 1) >= 0)
        {
            const long secondsPerDay = 24 * 60 * 60;
            long days = LssParser.ParseSigned(text[..dot], long.MinValue, long.MaxValue);
            if (days > long.MaxValue / secondsPerDay || days < long.MinValue / secondsPerDay)
            {
                throw LssParser.Invalid($"\"{text}\" is not a time.");
            }

            (long seconds, long nanos) = ParseTimeSpan(text[(dot + 1)..]);
            if (seconds < 0 || nanos < 0)
            {
                throw LssParser.Invalid($"\"{text}\" is not a time.");
            }

            long daySeconds = days * secondsPerDay;
            return days < 0
                ? Ticks(daySeconds - seconds, -nanos)
                : Ticks(daySeconds + seconds, nanos);
        }

        (long s, long n) = ParseTimeSpan(text);
        return Ticks(s, n);
    }

    private static (long Seconds, long Nanos) ParseTimeSpan(string text)
    {
        bool negate = false;
        if (text.StartsWith('-'))
        {
            text = text[1..];
            negate = true;
        }
        else if (text.StartsWith('−'))
        {
            text = text[1..];
            negate = true;
        }

        string rest = text;
        long nanos = 0;
        int dot = text.IndexOf('.');
        if (dot >= 0)
        {
            rest = text[..dot];
            string fraction = text[(dot + 1)..];
            if (fraction.Length > 9)
            {
                fraction = fraction[..9];
            }

            nanos = (long)LssParser.ParseUnsigned(fraction, uint.MaxValue) * (long)Math.Pow(10, 9 - fraction.Length);
            if (nanos > uint.MaxValue)
            {
                throw LssParser.Invalid($"\"{text}\" is not a time.");
            }
        }

        ulong seconds = 0;
        ulong factor = 1;
        for (int i = 0; ; i++)
        {
            if (i == 3)
            {
                throw LssParser.Invalid($"\"{text}\" has too many colons.");
            }

            int colon = rest.LastIndexOf(':');
            if (colon >= 0)
            {
                ulong piece = LssParser.ParseUnsigned(rest[(colon + 1)..], ulong.MaxValue);
                rest = rest[..colon];
                if (piece >= 60)
                {
                    throw LssParser.Invalid($"\"{text}\" is not a time.");
                }

                seconds = checked(piece * factor + seconds);
                factor *= 60;
            }
            else
            {
                ulong piece = LssParser.ParseUnsigned(rest, ulong.MaxValue);
                try
                {
                    seconds = checked(piece * factor + seconds);
                }
                catch (OverflowException)
                {
                    throw LssParser.Invalid($"\"{text}\" is not a time.");
                }

                break;
            }
        }

        if (seconds > long.MaxValue || nanos > int.MaxValue)
        {
            throw LssParser.Invalid($"\"{text}\" is not a time.");
        }

        long s = (long)seconds;
        return negate ? (-s, -nanos) : (s, nanos);
    }

    private static TimeSpan Ticks(long seconds, long nanos)
    {
        try
        {
            return new TimeSpan(checked(seconds * TimeSpan.TicksPerSecond + nanos / 100));
        }
        catch (OverflowException)
        {
            throw LssParser.Invalid("The time is too large.");
        }
    }
}
