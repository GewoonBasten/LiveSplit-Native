using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;

namespace LiveSplitNative.Core.Serialization;

/// <summary>
/// Port of LiveSplit's ExcelRunSaver (93a11b85), which Share's Excel uses: six sheets (Splits,
/// Attempt History and Segment History, each for Real Time and Game Time) with the same colors,
/// borders and number formats. Upstream writes them with Codaxy.Xlio; this writes the .xlsx
/// parts itself, and sizes the columns to their text as Xlio's AutoFit does.
/// </summary>
public static class ExcelRunSaver
{
    private const string DateFormat = "dd mmm yy hh:mm:ss";
    private const string TimeFormat = "[HH]:MM:SS.000";

    private static readonly Rgb HeaderFill = new(128, 128, 128);
    private static readonly Rgb OddRow = new(221, 221, 221);
    private static readonly Rgb EvenRow = new(238, 238, 238);
    private static readonly Rgb OddBestTime = new(201, 231, 201);
    private static readonly Rgb EvenBestTime = new(218, 248, 218);
    private static readonly Rgb OddBestSegment = new(241, 231, 181);
    private static readonly Rgb EvenBestSegment = new(255, 245, 198);

    public static void Save(Run run, Stream stream)
    {
        var styles = new StyleTable();
        var sheets = new List<Sheet>
        {
            SplitTimesSheet("Splits (Real Time)", run, TimingMethod.RealTime, styles),
            AttemptHistorySheet("Attempt History (Real Time)", run, TimingMethod.RealTime, styles),
            SegmentHistorySheet("Segment History (Real Time)", run, TimingMethod.RealTime, styles),
            SplitTimesSheet("Splits (Game Time)", run, TimingMethod.GameTime, styles),
            AttemptHistorySheet("Attempt History (Game Time)", run, TimingMethod.GameTime, styles),
            SegmentHistorySheet("Segment History (Game Time)", run, TimingMethod.GameTime, styles),
        };
        Write(stream, sheets, styles);
    }

    private static Rgb RowFill(int rowIndex) => (rowIndex & 1) == 1 ? OddRow : EvenRow;

    private static Style Header(bool leftBorder) =>
        new(Bold: true, Fill: HeaderFill, Align: "center", Format: null, LeftBorder: leftBorder, BottomBorder: true);

    private static Sheet AttemptHistorySheet(string name, Run run, TimingMethod method, StyleTable styles)
    {
        var sheet = new Sheet(name);
        sheet.Set(0, 0, "Attempt ID", styles.Id(Header(false)));
        sheet.Set(0, 1, "Started", styles.Id(Header(true)));
        sheet.Set(0, 2, "Ended", styles.Id(Header(true)));
        sheet.Set(0, 3, "Time", styles.Id(Header(true)));

        int rowIndex = 1;
        TimeSpan bestTime = TimeSpan.MaxValue;
        foreach (Attempt attempt in run.AttemptHistory)
        {
            Rgb fill = RowFill(rowIndex);
            sheet.Set(rowIndex, 0, attempt.Index, styles.Id(new Style(false, fill, null, null, false, false)));
            int dateStyle = styles.Id(new Style(false, fill, "right", DateFormat, true, false));
            sheet.Set(rowIndex, 1, attempt.Started is { } started ? new DateValue(started.Time) : null, dateStyle);
            sheet.Set(rowIndex, 2, attempt.Ended is { } ended ? new DateValue(ended.Time) : null, dateStyle);

            Rgb timeFill = fill;
            object? timeValue = null;
            if (attempt.Time[method] is { } time)
            {
                timeValue = new TimeValue(time);
                if (time < bestTime)
                {
                    bestTime = time;
                    timeFill = (rowIndex & 1) == 1 ? OddBestTime : EvenBestTime;
                }
            }

            sheet.Set(rowIndex, 3, timeValue, styles.Id(new Style(false, timeFill, "right", TimeFormat, true, false)));
            ++rowIndex;
        }

        return sheet;
    }

    private static Sheet SegmentHistorySheet(string name, Run run, TimingMethod method, StyleTable styles)
    {
        var sheet = new Sheet(name);
        sheet.Set(0, 0, "Attempt ID", styles.Id(Header(false)));
        int columnIndex = 1;
        foreach (Segment segment in run.Segments)
        {
            sheet.Set(0, columnIndex++, segment.Name, styles.Id(Header(true)));
        }

        int rowIndex = 1;
        foreach (Attempt attempt in run.AttemptHistory)
        {
            Rgb fill = RowFill(rowIndex);
            sheet.Set(rowIndex, 0, attempt.Index, styles.Id(new Style(false, fill, null, null, false, false)));
            columnIndex = 1;
            int timeStyle = styles.Id(new Style(false, fill, "right", TimeFormat, true, false));
            foreach (Segment segment in run.Segments)
            {
                object? value = segment.SegmentHistory.TryGetValue(attempt.Index, out SplitTime element) && element[method] is { } time
                    ? new TimeValue(time)
                    : null;
                sheet.Set(rowIndex, columnIndex++, value, timeStyle);
            }

            ++rowIndex;
        }

        return sheet;
    }

    private static Sheet SplitTimesSheet(string name, Run run, TimingMethod method, StyleTable styles)
    {
        var sheet = new Sheet(name);
        List<string> comparisons = [.. run.Comparisons.Where(x => x != NoneComparisonGenerator.ComparisonName)];
        sheet.Set(0, 0, "Segment", styles.Id(Header(false)));
        int columnIndex = 1;
        foreach (string comparison in comparisons)
        {
            sheet.Set(0, columnIndex++, comparison, styles.Id(Header(true)));
        }

        TimeSpan lastTime = TimeSpan.Zero;
        int rowIndex = 1;
        foreach (Segment segment in run.Segments)
        {
            Rgb fill = RowFill(rowIndex);
            sheet.Set(rowIndex, 0, segment.Name, styles.Id(new Style(false, fill, null, null, false, false)));
            columnIndex = 1;
            foreach (string comparison in comparisons)
            {
                TimeSpan? time = segment.Comparisons.TryGetValue(comparison, out SplitTime split) ? split[method] : null;
                bool bestSegment = comparison == Run.PersonalBestComparisonName && time.HasValue
                    && segment.BestSegmentTime[method] == time.Value - lastTime;
                Rgb cellFill = bestSegment ? ((rowIndex & 1) == 1 ? OddBestSegment : EvenBestSegment) : fill;
                sheet.Set(rowIndex, columnIndex++, time is { } t ? new TimeValue(t) : null,
                    styles.Id(new Style(false, cellFill, "right", TimeFormat, true, false)));
                if (comparison == Run.PersonalBestComparisonName && time.HasValue)
                {
                    lastTime = time.Value;
                }
            }

            ++rowIndex;
        }

        return sheet;
    }

    private readonly record struct Rgb(byte R, byte G, byte B)
    {
        public string Argb => $"FF{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>A cell's look: Xlio's Style with the parts ExcelRunSaver sets.</summary>
    private readonly record struct Style(bool Bold, Rgb Fill, string? Align, string? Format, bool LeftBorder, bool BottomBorder);

    private sealed record DateValue(DateTime Time);

    private sealed record TimeValue(TimeSpan Time);

    private sealed class StyleTable
    {
        public List<Style> Styles { get; } = [];

        public int Id(Style style)
        {
            int index = Styles.IndexOf(style);
            if (index < 0)
            {
                Styles.Add(style);
                index = Styles.Count - 1;
            }

            return index + 1; // 0 is the workbook's default style.
        }
    }

    private sealed class Sheet(string name)
    {
        public string Name { get; } = name;

        public SortedDictionary<int, SortedDictionary<int, (object? Value, int Style)>> Rows { get; } = [];

        public void Set(int row, int column, object? value, int style)
        {
            if (!Rows.TryGetValue(row, out SortedDictionary<int, (object?, int)>? cells))
            {
                Rows[row] = cells = [];
            }

            cells[column] = (value, style);
        }
    }

    private static void Write(Stream stream, List<Sheet> sheets, StyleTable styles)
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        Part(zip, "[Content_Types].xml", w =>
        {
            w.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
            Default(w, "rels", "application/vnd.openxmlformats-package.relationships+xml");
            Default(w, "xml", "application/xml");
            Override(w, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
            Override(w, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
            for (int i = 1; i <= sheets.Count; i++)
            {
                Override(w, $"/xl/worksheets/sheet{i}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            }

            w.WriteEndElement();
        });
        Part(zip, "_rels/.rels", w =>
        {
            w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            Relationship(w, "rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml");
            w.WriteEndElement();
        });
        Part(zip, "xl/_rels/workbook.xml.rels", w =>
        {
            w.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
            for (int i = 1; i <= sheets.Count; i++)
            {
                Relationship(w, $"rId{i}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", $"worksheets/sheet{i}.xml");
            }

            Relationship(w, $"rId{sheets.Count + 1}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml");
            w.WriteEndElement();
        });
        Part(zip, "xl/workbook.xml", w =>
        {
            w.WriteStartElement("workbook", Main);
            w.WriteAttributeString("xmlns", "r", null, RelNs);
            w.WriteStartElement("sheets", Main);
            for (int i = 1; i <= sheets.Count; i++)
            {
                w.WriteStartElement("sheet", Main);
                w.WriteAttributeString("name", sheets[i - 1].Name);
                w.WriteAttributeString("sheetId", Num(i));
                w.WriteAttributeString("id", RelNs, $"rId{i}");
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndElement();
        });
        Part(zip, "xl/styles.xml", w => WriteStyles(w, styles));
        for (int i = 1; i <= sheets.Count; i++)
        {
            Sheet sheet = sheets[i - 1];
            Part(zip, $"xl/worksheets/sheet{i}.xml", w => WriteSheet(w, sheet));
        }
    }

    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static void WriteStyles(XmlWriter w, StyleTable styles)
    {
        w.WriteStartElement("styleSheet", Main);
        w.WriteStartElement("numFmts", Main);
        w.WriteAttributeString("count", "2");
        NumFmt(w, 164, DateFormat);
        NumFmt(w, 165, TimeFormat);
        w.WriteEndElement();

        // Fonts: 0 regular, 1 bold white.
        w.WriteStartElement("fonts", Main);
        w.WriteAttributeString("count", "2");
        Font(w, bold: false);
        Font(w, bold: true);
        w.WriteEndElement();

        List<Rgb> fills = [.. styles.Styles.Select(s => s.Fill).Distinct()];
        w.WriteStartElement("fills", Main);
        w.WriteAttributeString("count", Num(fills.Count + 2));
        PatternFill(w, "none", null);
        PatternFill(w, "gray125", null);
        foreach (Rgb fill in fills)
        {
            PatternFill(w, "solid", fill);
        }

        w.WriteEndElement();

        // Borders: index = left thin + 2 * bottom medium, both white.
        w.WriteStartElement("borders", Main);
        w.WriteAttributeString("count", "4");
        for (int i = 0; i < 4; i++)
        {
            w.WriteStartElement("border", Main);
            Edge(w, "left", (i & 1) == 1 ? "thin" : null);
            Edge(w, "right", null);
            Edge(w, "top", null);
            Edge(w, "bottom", (i & 2) == 2 ? "medium" : null);
            w.WriteElementString("diagonal", Main, "");
            w.WriteEndElement();
        }

        w.WriteEndElement();

        w.WriteStartElement("cellStyleXfs", Main);
        w.WriteAttributeString("count", "1");
        w.WriteStartElement("xf", Main);
        w.WriteAttributeString("numFmtId", "0");
        w.WriteAttributeString("fontId", "0");
        w.WriteAttributeString("fillId", "0");
        w.WriteAttributeString("borderId", "0");
        w.WriteEndElement();
        w.WriteEndElement();

        w.WriteStartElement("cellXfs", Main);
        w.WriteAttributeString("count", Num(styles.Styles.Count + 1));
        w.WriteStartElement("xf", Main);
        w.WriteAttributeString("numFmtId", "0");
        w.WriteAttributeString("fontId", "0");
        w.WriteAttributeString("fillId", "0");
        w.WriteAttributeString("borderId", "0");
        w.WriteAttributeString("xfId", "0");
        w.WriteEndElement();
        foreach (Style style in styles.Styles)
        {
            int numFmt = style.Format switch
            {
                DateFormat => 164,
                TimeFormat => 165,
                _ => 0,
            };
            w.WriteStartElement("xf", Main);
            w.WriteAttributeString("numFmtId", Num(numFmt));
            w.WriteAttributeString("fontId", style.Bold ? "1" : "0");
            w.WriteAttributeString("fillId", Num(fills.IndexOf(style.Fill) + 2));
            w.WriteAttributeString("borderId", Num((style.LeftBorder ? 1 : 0) + (style.BottomBorder ? 2 : 0)));
            w.WriteAttributeString("xfId", "0");
            if (numFmt != 0)
            {
                w.WriteAttributeString("applyNumberFormat", "1");
            }

            w.WriteAttributeString("applyFont", "1");
            w.WriteAttributeString("applyFill", "1");
            w.WriteAttributeString("applyBorder", "1");
            if (style.Align is { } align)
            {
                w.WriteAttributeString("applyAlignment", "1");
                w.WriteStartElement("alignment", Main);
                w.WriteAttributeString("horizontal", align);
                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void WriteSheet(XmlWriter w, Sheet sheet)
    {
        w.WriteStartElement("worksheet", Main);
        int columns = sheet.Rows.Values.Select(r => r.Keys.DefaultIfEmpty(-1).Max() + 1).DefaultIfEmpty(0).Max();
        if (columns > 0)
        {
            // AutoFit: each column as wide as its longest text, header text in bold.
            w.WriteStartElement("cols", Main);
            for (int column = 0; column < columns; column++)
            {
                double width = 8.43;
                foreach ((int row, SortedDictionary<int, (object? Value, int Style)> cells) in sheet.Rows)
                {
                    if (cells.TryGetValue(column, out (object? Value, int Style) cell))
                    {
                        double length = DisplayText(cell.Value).Length * (row == 0 ? 1.1 : 1.0);
                        width = Math.Max(width, length + 2);
                    }
                }

                w.WriteStartElement("col", Main);
                w.WriteAttributeString("min", Num(column + 1));
                w.WriteAttributeString("max", Num(column + 1));
                w.WriteAttributeString("width", Math.Round(width, 2).ToString(CultureInfo.InvariantCulture));
                w.WriteAttributeString("customWidth", "1");
                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        w.WriteStartElement("sheetData", Main);
        foreach ((int row, SortedDictionary<int, (object? Value, int Style)> cells) in sheet.Rows)
        {
            w.WriteStartElement("row", Main);
            w.WriteAttributeString("r", Num(row + 1));
            foreach ((int column, (object? value, int style)) in cells)
            {
                w.WriteStartElement("c", Main);
                w.WriteAttributeString("r", ColumnName(column) + Num(row + 1));
                w.WriteAttributeString("s", Num(style));
                switch (value)
                {
                    case string text:
                        w.WriteAttributeString("t", "inlineStr");
                        w.WriteStartElement("is", Main);
                        w.WriteStartElement("t", Main);
                        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1])))
                        {
                            w.WriteAttributeString("xml", "space", null, "preserve");
                        }

                        w.WriteString(text);
                        w.WriteEndElement();
                        w.WriteEndElement();
                        break;
                    case int number:
                        w.WriteElementString("v", Main, Num(number));
                        break;
                    case DateValue date:
                        w.WriteElementString("v", Main, date.Time.ToOADate().ToString("R", CultureInfo.InvariantCulture));
                        break;
                    case TimeValue time:
                        w.WriteElementString("v", Main, time.Time.TotalDays.ToString("R", CultureInfo.InvariantCulture));
                        break;
                }

                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        w.WriteEndElement();
        w.WriteEndElement();
    }

    /// <summary>The text a cell shows, for the column widths.</summary>
    private static string DisplayText(object? value) => value switch
    {
        string text => text,
        int number => Num(number),
        DateValue date => date.Time.ToString("dd MMM yy HH:mm:ss", CultureInfo.InvariantCulture),
        TimeValue time => $"{(int)time.Time.TotalHours:00}:{time.Time.Minutes:00}:{time.Time.Seconds:00}.{time.Time.Milliseconds:000}",
        _ => "",
    };

    private static string ColumnName(int column)
    {
        var name = new StringBuilder();
        for (int n = column + 1; n > 0; n = (n - 1) / 26)
        {
            name.Insert(0, (char)('A' + ((n - 1) % 26)));
        }

        return name.ToString();
    }

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static void NumFmt(XmlWriter w, int id, string code)
    {
        w.WriteStartElement("numFmt", Main);
        w.WriteAttributeString("numFmtId", Num(id));
        w.WriteAttributeString("formatCode", code);
        w.WriteEndElement();
    }

    private static void Font(XmlWriter w, bool bold)
    {
        w.WriteStartElement("font", Main);
        if (bold)
        {
            w.WriteElementString("b", Main, "");
        }

        w.WriteStartElement("sz", Main);
        w.WriteAttributeString("val", "11");
        w.WriteEndElement();
        if (bold)
        {
            w.WriteStartElement("color", Main);
            w.WriteAttributeString("rgb", "FFFFFFFF");
            w.WriteEndElement();
        }

        w.WriteStartElement("name", Main);
        w.WriteAttributeString("val", "Calibri");
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void PatternFill(XmlWriter w, string pattern, Rgb? color)
    {
        w.WriteStartElement("fill", Main);
        w.WriteStartElement("patternFill", Main);
        w.WriteAttributeString("patternType", pattern);
        if (color is { } rgb)
        {
            w.WriteStartElement("fgColor", Main);
            w.WriteAttributeString("rgb", rgb.Argb);
            w.WriteEndElement();
            w.WriteStartElement("bgColor", Main);
            w.WriteAttributeString("indexed", "64");
            w.WriteEndElement();
        }

        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void Edge(XmlWriter w, string side, string? style)
    {
        w.WriteStartElement(side, Main);
        if (style is not null)
        {
            w.WriteAttributeString("style", style);
            w.WriteStartElement("color", Main);
            w.WriteAttributeString("rgb", "FFFFFFFF");
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void Default(XmlWriter w, string extension, string type)
    {
        w.WriteStartElement("Default", "http://schemas.openxmlformats.org/package/2006/content-types");
        w.WriteAttributeString("Extension", extension);
        w.WriteAttributeString("ContentType", type);
        w.WriteEndElement();
    }

    private static void Override(XmlWriter w, string part, string type)
    {
        w.WriteStartElement("Override", "http://schemas.openxmlformats.org/package/2006/content-types");
        w.WriteAttributeString("PartName", part);
        w.WriteAttributeString("ContentType", type);
        w.WriteEndElement();
    }

    private static void Relationship(XmlWriter w, string id, string type, string target)
    {
        w.WriteStartElement("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships");
        w.WriteAttributeString("Id", id);
        w.WriteAttributeString("Type", type);
        w.WriteAttributeString("Target", target);
        w.WriteEndElement();
    }

    private static void Part(ZipArchive zip, string name, Action<XmlWriter> write)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        writer.WriteStartDocument(standalone: true);
        write(writer);
        writer.WriteEndDocument();
    }
}
