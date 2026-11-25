using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Xunit;
using Xunit.Abstractions;
using LiveSplitNative.Core.Imaging;
using N = LiveSplitNative.Core.Model;
using NT = LiveSplitNative.Core.Timing;
using U = LiveSplit.Model;
using UC = LiveSplit.Model.Comparisons;
using UF = LiveSplit.Model.RunFactories;

namespace LiveSplitNative.Upstream.Tests.Differential;

/// <summary>
/// M-B file corpus: every .lss in tests/fixtures and tests/fixtures/corpus (Basten's own splits,
/// livesplit-core's test files) is opened the way Windows LiveSplit 93a11b85 opens splits
/// (upstream StandardFormatsRunFactory over livesplit-core's C API, liblivesplit_core beside the
/// tests: native/README.md) and with our XmlRunSerializer, and the two runs are compared field
/// by field. Without the library the comparison is skipped.
/// </summary>
public class LssCorpusDifferentialTests
{
    private const int MaxReportedDifferences = 60;

    private readonly ITestOutputHelper _output;

    public LssCorpusDifferentialTests(ITestOutputHelper output) => _output = output;

    private static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "fixtures");

    private static readonly Lazy<bool> LibraryAvailable = new(() =>
        NativeLibrary.TryLoad("livesplit_core", typeof(LssCorpusDifferentialTests).Assembly, null, out _));

    public static IEnumerable<object[]> Files() =>
        Directory.GetFiles(FixtureDirectory, "*.lss")
            .Concat(Directory.Exists(Path.Combine(FixtureDirectory, "corpus"))
                ? Directory.GetFiles(Path.Combine(FixtureDirectory, "corpus"), "*.lss")
                : Array.Empty<string>())
            .Select(path => Path.GetRelativePath(FixtureDirectory, path))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(Files))]
    public void Our_Load_Matches_Windows_LiveSplit(string file)
    {
        if (!LibraryAvailable.Value)
        {
            _output.WriteLine("liblivesplit_core is not built: skipping the corpus comparison.");
            return;
        }

        string path = Path.Combine(FixtureDirectory, file);
        List<string> windows = DumpWindows(path);
        List<string> ours = DumpOurs(path);

        // Each line is "key: value" with a unique key; values are compared key by key.
        Dictionary<string, string> windowsByKey = ByKey(windows);
        Dictionary<string, string> oursByKey = ByKey(ours);
        var differences = new List<string>();
        foreach (string key in windowsByKey.Keys.Concat(oursByKey.Keys.Where(k => !windowsByKey.ContainsKey(k))))
        {
            string w = windowsByKey.TryGetValue(key, out string wv) ? wv : "(none)";
            string o = oursByKey.TryGetValue(key, out string ov) ? ov : "(none)";
            if (w != o)
            {
                differences.Add($"  {key}: Windows {w} | ours {o}");
            }
        }

        _output.WriteLine($"{file}: {windows.Count} values, {differences.Count} different");
        Assert.True(
            differences.Count == 0,
            $"{file}: {differences.Count} of {windowsByKey.Count} values differ:\n" + string.Join("\n", differences.Take(MaxReportedDifferences)));
    }

    /// <summary>
    /// What we write is what Windows reads back: each file opened and saved by us, then opened
    /// by Windows LiveSplit, gives the same run as Windows opening the original.
    /// </summary>
    [Theory]
    [MemberData(nameof(Files))]
    public void Windows_Loads_What_We_Save_The_Same(string file)
    {
        if (!LibraryAvailable.Value)
        {
            _output.WriteLine("liblivesplit_core is not built: skipping the corpus comparison.");
            return;
        }

        string path = Path.Combine(FixtureDirectory, file);
        N.Run run;
        try
        {
            run = LiveSplitNative.Core.Serialization.XmlRunSerializer.LoadFile(path);
        }
        catch (Exception e) when (e is InvalidDataException or XmlException)
        {
            return;
        }

        string saved = Path.Combine(Path.GetTempPath(), $"lsn-corpus-{Guid.NewGuid():N}.lss");
        try
        {
            LiveSplitNative.Core.Serialization.XmlRunSerializer.SaveFile(run, saved);
            Dictionary<string, string> original = ByKey(DumpWindows(path));
            Dictionary<string, string> again = ByKey(DumpWindows(saved));
            List<string> differences = original.Keys.Concat(again.Keys.Where(k => !original.ContainsKey(k)))
                .Where(k => (original.TryGetValue(k, out string a) ? a : "(none)") != (again.TryGetValue(k, out string b) ? b : "(none)"))
                .Select(k => $"  {k}: original {(original.TryGetValue(k, out string a) ? a : "(none)")} | saved by us {(again.TryGetValue(k, out string b) ? b : "(none)")}")
                .ToList();
            Assert.True(
                differences.Count == 0,
                $"{file}: {differences.Count} values differ after our save:\n" + string.Join("\n", differences.Take(MaxReportedDifferences)));
        }
        finally
        {
            File.Delete(saved);
        }
    }

    private static Dictionary<string, string> ByKey(List<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            int colon = line.IndexOf(": ", StringComparison.Ordinal);
            values[colon < 0 ? line : line[..colon]] = colon < 0 ? "" : line[(colon + 2)..];
        }

        return values;
    }

    /// <summary>Windows LiveSplit's TimerForm.LoadRunFromFile: StandardFormatsRunFactory.Create.</summary>
    private static List<string> DumpWindows(string path)
    {
        U.IRun run;
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(path));
            run = new UF.StandardFormatsRunFactory(stream, path).Create(new UC.StandardComparisonGeneratorsFactory());
        }
        catch (Exception e)
        {
            return ["opened: no"];
        }

        var lines = new List<string>
        {
            "opened: yes",
            "game: " + run.GameName,
            "category: " + run.CategoryName,
            "offset: " + T(run.Offset),
            "attempt count: " + run.AttemptCount,
            "layout path: " + (run.LayoutPath ?? ""),
            "game icon: " + (run.GameIcon is not null),
            "run id: " + (run.Metadata.RunID ?? ""),
            "platform: " + run.Metadata.PlatformName,
            "uses emulator: " + run.Metadata.UsesEmulator,
            "region: " + run.Metadata.RegionName,
        };
        lines.AddRange(run.Metadata.VariableValueNames.Select(v => $"variable {v.Key}: {v.Value}"));
        lines.AddRange(run.Metadata.CustomVariables.Where(v => v.Value.IsPermanent).Select(v => $"custom variable {v.Key}: {v.Value.Value}"));
        lines.AddRange(run.AttemptHistory.Select((a, n) =>
            $"attempt #{n}: id {a.Index} {T(a.Time)} started {D(a.Started)} ended {D(a.Ended)} pause {T(a.PauseTime)}"));
        lines.Add("comparisons: " + string.Join(" | ", run.CustomComparisons));
        for (int i = 0; i < run.Count; i++)
        {
            U.ISegment segment = run[i];
            lines.Add($"segment {i} name: {segment.Name}");
            lines.Add($"segment {i} icon: {segment.Icon is not null}");
            lines.Add($"segment {i} best: {T(segment.BestSegmentTime)}");
            lines.AddRange(run.CustomComparisons.Select(c => $"segment {i} {c}: {T(segment.Comparisons[c])}"));
            lines.AddRange(segment.SegmentHistory.OrderBy(h => h.Key).Select(h => $"segment {i} history {h.Key}: {T(h.Value)}"));
        }

        lines.Add("auto splitter settings: " + Canonical(run.AutoSplitterSettings?.InnerXml));
        return lines;
    }

    private static List<string> DumpOurs(string path)
    {
        N.Run run;
        try
        {
            run = LiveSplitNative.Core.Serialization.XmlRunSerializer.LoadFile(path);
        }
        catch (Exception e)
        {
            return ["opened: no"];
        }

        var lines = new List<string>
        {
            "opened: yes",
            "game: " + run.GameName,
            "category: " + run.CategoryName,
            "offset: " + T(run.Offset),
            "attempt count: " + run.AttemptCount,
            "layout path: " + run.LayoutPath,
            "game icon: " + (IconBlobDecoder.TryDecodeImageBytes(run.GameIconXml) is not null),
            "run id: " + (run.Metadata.RunId ?? ""),
            "platform: " + run.Metadata.PlatformName,
            "uses emulator: " + run.Metadata.UsesEmulator,
            "region: " + run.Metadata.RegionName,
        };
        lines.AddRange(run.Metadata.VariableValueNames.Select(v => $"variable {v.Key}: {v.Value}"));
        lines.AddRange(run.Metadata.PermanentCustomVariables.Select(v => $"custom variable {v.Key}: {v.Value}"));
        lines.AddRange(run.AttemptHistory.Select((a, n) =>
            $"attempt #{n}: id {a.Index} {T(a.Time)} started {D(a.Started)} ended {D(a.Ended)} pause {T(a.PauseTime)}"));
        lines.Add("comparisons: " + string.Join(" | ", run.CustomComparisons));
        for (int i = 0; i < run.Segments.Count; i++)
        {
            N.Segment segment = run.Segments[i];
            lines.Add($"segment {i} name: {segment.Name}");
            lines.Add($"segment {i} icon: {IconBlobDecoder.TryDecodeImageBytes(segment.IconXml) is not null}");
            lines.Add($"segment {i} best: {T(segment.BestSegmentTime)}");
            lines.AddRange(run.CustomComparisons.Select(c =>
                $"segment {i} {c}: {T(segment.Comparisons.TryGetValue(c, out N.SplitTime time) ? time : default)}"));
            lines.AddRange(segment.SegmentHistory.OrderBy(h => h.Key).Select(h => $"segment {i} history {h.Key}: {T(h.Value)}"));
        }

        lines.Add("auto splitter settings: " + Canonical(run.AutoSplitterSettingsXml));
        return lines;
    }

    private static string T(TimeSpan? time) =>
        time is { } t ? t.Ticks.ToString(CultureInfo.InvariantCulture) : "-";

    private static string T(U.Time time) => $"{T(time.RealTime)}/{T(time.GameTime)}";

    private static string T(N.SplitTime time) => $"{T(time.RealTime)}/{T(time.GameTime)}";

    private static string D(U.AtomicDateTime? time) =>
        time is { } t ? D(t.Time, t.SyncedWithAtomicClock) : "-";

    private static string D(NT.AtomicDateTime? time) =>
        time is { } t ? D(t.Time, t.SyncedWithAtomicClock) : "-";

    private static string D(DateTime time, bool synced)
    {
        DateTime utc = time.Kind == DateTimeKind.Local ? time.ToUniversalTime() : time;
        return utc.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + (synced ? " synced" : "");
    }

    /// <summary>The settings' XML without insignificant whitespace, so both writers' text compares.</summary>
    private static string Canonical(string innerXml)
    {
        if (string.IsNullOrWhiteSpace(innerXml))
        {
            return "";
        }

        try
        {
            var document = new XmlDocument { PreserveWhitespace = false };
            document.LoadXml("<AutoSplitterSettings>" + innerXml + "</AutoSplitterSettings>");
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment }))
            {
                document.DocumentElement!.WriteContentTo(writer);
            }

            return builder.ToString();
        }
        catch (XmlException)
        {
            return "(not XML) " + innerXml;
        }
    }
}
