namespace LiveSplitNative.Core.Model;

public sealed class Segment
{
    public Segment(string name) => Name = name;

    public string Name { get; set; }

    /// <summary>Opaque icon XML payload (InnerXml) preserved for round-trip.</summary>
    public string? IconXml { get; set; }

    /// <summary>Unknown child elements under &lt;Segment&gt; preserved for round-trip.</summary>
    public List<string> UnknownOuterXml { get; } = [];

    public SplitTime SplitTime { get; set; }
    public SplitTime BestSegmentTime { get; set; }
    public Dictionary<string, SplitTime> Comparisons { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<int, SplitTime> SegmentHistory { get; } = [];
    public Dictionary<string, string> CustomVariableValues { get; } = new(StringComparer.Ordinal);

    public SplitTime PersonalBestSplitTime
    {
        get => Comparisons.TryGetValue(Run.PersonalBestComparisonName, out SplitTime t) ? t : default;
        set => Comparisons[Run.PersonalBestComparisonName] = value;
    }

    /// <summary>Upstream Segment.Clone: a deep copy (history, comparisons and variables are new collections).</summary>
    public Segment Clone()
    {
        var copy = new Segment(Name)
        {
            IconXml = IconXml,
            SplitTime = SplitTime,
            BestSegmentTime = BestSegmentTime,
        };
        copy.UnknownOuterXml.AddRange(UnknownOuterXml);
        foreach ((string name, SplitTime time) in Comparisons)
        {
            copy.Comparisons[name] = time;
        }

        foreach ((int id, SplitTime time) in SegmentHistory)
        {
            copy.SegmentHistory[id] = time;
        }

        foreach ((string name, string value) in CustomVariableValues)
        {
            copy.CustomVariableValues[name] = value;
        }

        return copy;
    }
}
