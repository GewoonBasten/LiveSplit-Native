using System.Xml;

namespace LiveSplitNative.Core.Model;

/// <summary>Port of LiveSplit.Model.Time (RealTime + GameTime).</summary>
public struct SplitTime
{
    public static readonly SplitTime Zero = new(TimeSpan.Zero, TimeSpan.Zero);

    public TimeSpan? RealTime { get; set; }
    public TimeSpan? GameTime { get; set; }

    public SplitTime(TimeSpan? realTime = null, TimeSpan? gameTime = null)
    {
        RealTime = realTime;
        GameTime = gameTime;
    }

    public SplitTime(TimingMethod method, TimeSpan? time)
        : this()
    {
        this[method] = time;
    }

    public TimeSpan? this[TimingMethod method]
    {
        readonly get => method == TimingMethod.RealTime ? RealTime : GameTime;
        set
        {
            if (method == TimingMethod.RealTime)
            {
                RealTime = value;
            }
            else
            {
                GameTime = value;
            }
        }
    }

    public static SplitTime FromXml(XmlElement element)
    {
        var time = new SplitTime();
        bool hasNested = false;
        // Use TimeSpanParser (not TimeSpan.TryParse): Windows / livesplit-core write
        // fractional seconds with up to 9 digits; BCL TryParse rejects non-zero >7-digit
        // fractions, which emptied Personal Best on real SMS .lss while whole-second
        // custom comparisons (…000000000) still loaded.
        if (element["RealTime"] is { } rt && TimeSpanParser.TryParse(rt.InnerText, out TimeSpan rtv))
        {
            time.RealTime = rtv;
            hasNested = true;
        }

        if (element["GameTime"] is { } gt && TimeSpanParser.TryParse(gt.InnerText, out TimeSpan gtv))
        {
            time.GameTime = gtv;
            hasNested = true;
        }

        // livesplit-core time_old (Run version < 1.4.1): plain element text is RealTime.
        // Example: <SplitTime name="Personal Best">00:01:39.3900000</SplitTime>
        if (!hasNested && !element.ChildNodes.OfType<XmlElement>().Any()
            && TimeSpanParser.TryParse(element.InnerText, out TimeSpan plain))
        {
            time.RealTime = plain;
        }

        return time;
    }

    public readonly XmlElement ToXml(XmlDocument document, string name = "Time")
    {
        XmlElement parent = document.CreateElement(name);
        if (RealTime is { } real)
        {
            XmlElement el = document.CreateElement("RealTime");
            el.InnerText = real.ToString();
            parent.AppendChild(el);
        }

        if (GameTime is { } game)
        {
            XmlElement el = document.CreateElement("GameTime");
            el.InnerText = game.ToString();
            parent.AppendChild(el);
        }

        return parent;
    }

    public static SplitTime operator +(SplitTime a, SplitTime b) =>
        new(a.RealTime + b.RealTime, a.GameTime + b.GameTime);

    public static SplitTime operator -(SplitTime a, SplitTime b) =>
        new(a.RealTime - b.RealTime, a.GameTime - b.GameTime);

    public override readonly string ToString() => $"{RealTime} | {GameTime}";
}
