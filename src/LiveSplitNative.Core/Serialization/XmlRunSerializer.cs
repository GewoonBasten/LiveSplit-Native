using System.Globalization;
using System.Xml;
using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.Serialization;

/// <summary>
/// Load/save LiveSplit .lss XML as written by XMLRunSaver / consumed by livesplit-core.
/// Opaque icons/autosplitter XML and unknown elements are preserved (no silent drops).
/// </summary>
public static class XmlRunSerializer
{
    /// <summary>
    /// Opens splits as Windows LiveSplit does (<see cref="LssParser"/>: livesplit-core's parser
    /// and StandardFormatsRunFactory). Throws when Windows could not open the file either.
    /// </summary>
    public static Run Load(Stream stream)
    {
        var document = new XmlDocument();
        document.Load(stream);
        return LssParser.Parse(document);
    }

    public static Run LoadFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Load(stream);
    }

    public static void Save(Run run, Stream stream)
    {
        var document = new XmlDocument();
        XmlNode decl = document.CreateXmlDeclaration("1.0", "UTF-8", null);
        document.AppendChild(decl);

        XmlElement parent = document.CreateElement("Run");
        // XMLRunSaver always writes 1.7.0 (subsplits as "-" / "{Group} " names, which
        // livesplit-core reads back as segment groups for files before 1.8.1).
        parent.SetAttribute("version", "1.7.0");
        document.AppendChild(parent);

        AppendOpaque(document, parent, "GameIcon", run.GameIconXml);
        AppendText(document, parent, "GameName", run.GameName);
        AppendText(document, parent, "CategoryName", run.CategoryName);
        AppendText(document, parent, "LayoutPath", run.LayoutPath);

        XmlElement metadata = document.CreateElement("Metadata");
        XmlElement runElement = document.CreateElement("Run");
        runElement.SetAttribute("id", run.Metadata.RunId ?? "");
        metadata.AppendChild(runElement);

        XmlElement platform = document.CreateElement("Platform");
        platform.SetAttribute("usesEmulator", run.Metadata.UsesEmulator ? "True" : "False");
        platform.InnerText = run.Metadata.PlatformName;
        metadata.AppendChild(platform);

        AppendText(document, metadata, "Region", run.Metadata.RegionName);

        XmlElement variables = document.CreateElement("Variables");
        foreach ((string name, string value) in run.Metadata.VariableValueNames)
        {
            XmlElement variable = document.CreateElement("Variable");
            variable.SetAttribute("name", name);
            variable.InnerText = value;
            variables.AppendChild(variable);
        }

        metadata.AppendChild(variables);

        XmlElement customVariables = document.CreateElement("CustomVariables");
        foreach ((string name, string value) in run.Metadata.PermanentCustomVariables)
        {
            XmlElement variable = document.CreateElement("Variable");
            variable.SetAttribute("name", name);
            variable.InnerText = value;
            customVariables.AppendChild(variable);
        }

        metadata.AppendChild(customVariables);
        parent.AppendChild(metadata);

        AppendText(document, parent, "Offset", run.Offset.ToString());
        AppendText(document, parent, "AttemptCount", run.AttemptCount.ToString(CultureInfo.InvariantCulture));

        XmlElement runHistory = document.CreateElement("AttemptHistory");
        foreach (Attempt attempt in run.AttemptHistory)
        {
            runHistory.AppendChild(attempt.ToXml(document));
        }

        parent.AppendChild(runHistory);

        XmlElement segmentsEl = document.CreateElement("Segments");
        parent.AppendChild(segmentsEl);
        foreach (Segment segment in run.Segments)
        {
            XmlElement splitElement = document.CreateElement("Segment");
            segmentsEl.AppendChild(splitElement);
            AppendText(document, splitElement, "Name", segment.Name);
            AppendOpaque(document, splitElement, "Icon", segment.IconXml);

            XmlElement splitTimes = document.CreateElement("SplitTimes");
            foreach (string comparison in run.CustomComparisons)
            {
                SplitTime time = segment.Comparisons.TryGetValue(comparison, out SplitTime existing)
                    ? existing
                    : default;
                XmlElement splitTime = time.ToXml(document, "SplitTime");
                splitTime.SetAttribute("name", comparison);
                splitTimes.AppendChild(splitTime);
            }

            splitElement.AppendChild(splitTimes);
            splitElement.AppendChild(segment.BestSegmentTime.ToXml(document, "BestSegmentTime"));

            XmlElement history = document.CreateElement("SegmentHistory");
            foreach ((int id, SplitTime time) in segment.SegmentHistory)
            {
                XmlElement timeEl = time.ToXml(document, "Time");
                timeEl.SetAttribute("id", id.ToString(CultureInfo.InvariantCulture));
                history.AppendChild(timeEl);
            }

            splitElement.AppendChild(history);

            foreach (string unknown in segment.UnknownOuterXml)
            {
                AppendOuterXml(document, splitElement, unknown);
            }
        }

        AppendOpaque(document, parent, "AutoSplitterSettings", run.AutoSplitterSettingsXml);

        foreach (string unknown in run.UnknownRootOuterXml)
        {
            AppendOuterXml(document, parent, unknown);
        }

        document.Save(stream);
    }

    public static void SaveFile(Run run, string path)
    {
        SaveFileAtomic(run, path);
    }

    /// <summary>
    /// Write via temp file + replace so a crash mid-save cannot truncate the destination.
    /// Prefer this (and test copies) over overwriting a user's original .lss during experiments.
    /// </summary>
    public static void SaveFileAtomic(Run run, string path)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = File.Create(tempPath))
            {
                Save(run, stream);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }
    }

    private static void AppendOuterXml(XmlDocument document, XmlElement parent, string outerXml)
    {
        var tmp = new XmlDocument();
        tmp.LoadXml(outerXml);
        if (tmp.DocumentElement is null)
        {
            return;
        }

        XmlNode imported = document.ImportNode(tmp.DocumentElement, deep: true);
        parent.AppendChild(imported);
    }

    private static void AppendText(XmlDocument document, XmlElement parent, string name, string value)
    {
        XmlElement el = document.CreateElement(name);
        el.InnerText = value ?? "";
        parent.AppendChild(el);
    }

    private static void AppendOpaque(XmlDocument document, XmlElement parent, string name, string? innerXml)
    {
        XmlElement el = document.CreateElement(name);
        if (!string.IsNullOrEmpty(innerXml))
        {
            el.InnerXml = innerXml;
        }

        parent.AppendChild(el);
    }
}
