using System.Globalization;
using System.Xml;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.Model;

public sealed class Attempt
{
    public Attempt(int index, SplitTime time, AtomicDateTime? started, AtomicDateTime? ended, TimeSpan? pauseTime)
    {
        Index = index;
        Time = time;
        Started = started;
        Ended = ended;
        PauseTime = pauseTime;
    }

    public int Index { get; set; }
    public SplitTime Time { get; set; }
    public AtomicDateTime? Started { get; set; }
    public AtomicDateTime? Ended { get; set; }
    public TimeSpan? PauseTime { get; set; }

    /// <summary>
    /// Upstream Attempt.Duration: the real time from start to end (LiveSplit 1.6 and later), else
    /// the attempt's real time (a finished run from before 1.6; null for a reset one).
    /// </summary>
    public TimeSpan? Duration => Ended is { } ended && Started is { } started ? ended - started : Time.RealTime;

    public XmlElement ToXml(XmlDocument document)
    {
        XmlElement attempt = document.CreateElement("Attempt");
        XmlElement time = Time.ToXml(document);
        attempt.InnerXml = time.InnerXml;

        XmlAttribute id = document.CreateAttribute("id");
        id.InnerText = Index.ToString(CultureInfo.InvariantCulture);
        attempt.Attributes.Append(id);

        if (Started is { } started)
        {
            XmlAttribute startedAttr = document.CreateAttribute("started");
            startedAttr.InnerText = started.Time.ToUniversalTime().ToString(CultureInfo.InvariantCulture);
            attempt.Attributes.Append(startedAttr);
            attempt.Attributes.Append(CreateBoolAttr(document, "isStartedSynced", started.SyncedWithAtomicClock));
        }

        if (Ended is { } ended)
        {
            XmlAttribute endedAttr = document.CreateAttribute("ended");
            endedAttr.InnerText = ended.Time.ToUniversalTime().ToString(CultureInfo.InvariantCulture);
            attempt.Attributes.Append(endedAttr);
            attempt.Attributes.Append(CreateBoolAttr(document, "isEndedSynced", ended.SyncedWithAtomicClock));
        }

        if (PauseTime is { } pause)
        {
            XmlElement pauseTime = document.CreateElement("PauseTime");
            pauseTime.InnerText = pause.ToString();
            attempt.AppendChild(pauseTime);
        }

        return attempt;
    }

    public static Attempt ParseXml(XmlElement node)
    {
        SplitTime time = SplitTime.FromXml(node);
        int index = int.Parse(node.Attributes["id"]!.InnerText, CultureInfo.InvariantCulture);
        AtomicDateTime? started = null;
        AtomicDateTime? ended = null;

        if (node.HasAttribute("started"))
        {
            var startedTime = DateTime.Parse(node.Attributes["started"]!.InnerText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            bool synced = node.HasAttribute("isStartedSynced") && bool.Parse(node.Attributes["isStartedSynced"]!.InnerText);
            started = new AtomicDateTime(startedTime, synced);
        }

        if (node.HasAttribute("ended"))
        {
            var endedTime = DateTime.Parse(node.Attributes["ended"]!.InnerText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            bool synced = node.HasAttribute("isEndedSynced") && bool.Parse(node.Attributes["isEndedSynced"]!.InnerText);
            ended = new AtomicDateTime(endedTime, synced);
        }

        TimeSpan? pauseTime = null;
        if (node["PauseTime"] is { } pt && TimeSpanParser.TryParse(pt.InnerText, out TimeSpan parsedPause))
        {
            pauseTime = parsedPause;
        }

        return new Attempt(index, time, started, ended, pauseTime);
    }

    private static XmlAttribute CreateBoolAttr(XmlDocument document, string name, bool value)
    {
        XmlAttribute attr = document.CreateAttribute(name);
        attr.InnerText = value ? "True" : "False";
        return attr;
    }
}
