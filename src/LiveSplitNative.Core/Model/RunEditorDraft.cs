namespace LiveSplitNative.Core.Model;

/// <summary>
/// Editable snapshot of a <see cref="Run"/> for the Splits Editor.
/// Holds full Real Time + Game Time per comparison so OK/Cancel never drops the other method,
/// and preserves segment history / unknown segment XML.
/// </summary>
public sealed class RunEditorDraft
{
    public string GameName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public TimeSpan Offset { get; set; }
    public int AttemptCount { get; set; }
    public string? Version { get; set; }
    public string LayoutPath { get; set; } = "";
    public string? GameIconXml { get; set; }
    public string? AutoSplitterSettingsXml { get; set; }
    public string? RunId { get; set; }
    public string PlatformName { get; set; } = "";
    public string RegionName { get; set; } = "";
    public bool UsesEmulator { get; set; }

    public List<string> Comparisons { get; } = [];
    public List<RunEditorSegmentDraft> Segments { get; } = [];
    public List<Attempt> AttemptHistory { get; } = [];
    public List<string> UnknownRootOuterXml { get; } = [];
    public Dictionary<string, string> VariableValueNames { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> PermanentCustomVariables { get; } = new(StringComparer.Ordinal);

    public static RunEditorDraft FromRun(Run run)
    {
        var draft = new RunEditorDraft
        {
            GameName = run.GameName,
            CategoryName = run.CategoryName,
            Offset = run.Offset,
            AttemptCount = run.AttemptCount,
            Version = run.Version,
            LayoutPath = run.LayoutPath,
            GameIconXml = run.GameIconXml,
            AutoSplitterSettingsXml = run.AutoSplitterSettingsXml,
            RunId = run.Metadata.RunId,
            PlatformName = run.Metadata.PlatformName,
            RegionName = run.Metadata.RegionName,
            UsesEmulator = run.Metadata.UsesEmulator,
        };

        draft.Comparisons.AddRange(run.CustomComparisons);
        if (!draft.Comparisons.Contains(Run.PersonalBestComparisonName, StringComparer.Ordinal))
        {
            draft.Comparisons.Insert(0, Run.PersonalBestComparisonName);
        }

        draft.AttemptHistory.AddRange(run.AttemptHistory);
        draft.UnknownRootOuterXml.AddRange(run.UnknownRootOuterXml);
        foreach ((string k, string v) in run.Metadata.VariableValueNames)
        {
            draft.VariableValueNames[k] = v;
        }

        foreach ((string k, string v) in run.Metadata.PermanentCustomVariables)
        {
            draft.PermanentCustomVariables[k] = v;
        }

        foreach (Segment segment in run.Segments)
        {
            draft.Segments.Add(RunEditorSegmentDraft.FromSegment(segment, draft.Comparisons));
        }

        if (draft.Segments.Count == 0)
        {
            draft.Segments.Add(RunEditorSegmentDraft.CreateEmpty(draft.Comparisons));
        }

        return draft;
    }

    public Run ToRun(bool preserveAttemptHistory)
    {
        var run = new Run
        {
            GameName = GameName,
            CategoryName = CategoryName,
            Offset = Offset,
            AttemptCount = AttemptCount,
            HasChanged = true,
            Version = Version,
            LayoutPath = LayoutPath,
            GameIconXml = GameIconXml,
            AutoSplitterSettingsXml = AutoSplitterSettingsXml,
        };
        run.Metadata.RunId = RunId;
        run.Metadata.PlatformName = PlatformName;
        run.Metadata.RegionName = RegionName;
        run.Metadata.UsesEmulator = UsesEmulator;
        foreach ((string k, string v) in VariableValueNames)
        {
            run.Metadata.VariableValueNames[k] = v;
        }

        foreach ((string k, string v) in PermanentCustomVariables)
        {
            run.Metadata.PermanentCustomVariables[k] = v;
        }

        foreach (string unknown in UnknownRootOuterXml)
        {
            run.UnknownRootOuterXml.Add(unknown);
        }

        run.CustomComparisons.Clear();
        foreach (string comparison in Comparisons)
        {
            run.EnsureComparison(comparison);
        }

        if (preserveAttemptHistory)
        {
            run.AttemptHistory.AddRange(AttemptHistory);
        }

        foreach (RunEditorSegmentDraft draftSeg in Segments)
        {
            run.AddSegment(draftSeg.ToSegment(Comparisons));
        }

        if (run.Count == 0)
        {
            run.AddSegment("");
        }

        return run;
    }

    public void EnsureComparisonColumn(string name)
    {
        if (Comparisons.Contains(name, StringComparer.Ordinal))
        {
            return;
        }

        Comparisons.Add(name);
        foreach (RunEditorSegmentDraft segment in Segments)
        {
            segment.Comparisons[name] = default;
        }
    }

    public void ClearTimesKeepPbName()
    {
        AttemptHistory.Clear();
        AttemptCount = 0;
        RunId = null;
        Comparisons.Clear();
        Comparisons.Add(Run.PersonalBestComparisonName);
        foreach (RunEditorSegmentDraft segment in Segments)
        {
            segment.Comparisons.Clear();
            segment.Comparisons[Run.PersonalBestComparisonName] = default;
            segment.BestSegmentTime = default;
            segment.SegmentHistory.Clear();
        }
    }
}

public sealed class RunEditorSegmentDraft
{
    public string Name { get; set; } = "";
    public string? IconXml { get; set; }
    public Dictionary<string, SplitTime> Comparisons { get; } = new(StringComparer.Ordinal);
    public SplitTime BestSegmentTime { get; set; }
    public SortedDictionary<int, SplitTime> SegmentHistory { get; } = [];
    public List<string> UnknownOuterXml { get; } = [];

    public static RunEditorSegmentDraft FromSegment(Segment segment, IReadOnlyList<string> comparisons)
    {
        var draft = new RunEditorSegmentDraft
        {
            Name = segment.Name,
            IconXml = segment.IconXml,
            BestSegmentTime = segment.BestSegmentTime,
        };
        foreach (string comparison in comparisons)
        {
            segment.Comparisons.TryGetValue(comparison, out SplitTime time);
            draft.Comparisons[comparison] = time;
        }

        foreach ((int id, SplitTime time) in segment.SegmentHistory)
        {
            draft.SegmentHistory[id] = time;
        }

        draft.UnknownOuterXml.AddRange(segment.UnknownOuterXml);
        return draft;
    }

    public static RunEditorSegmentDraft CreateEmpty(IReadOnlyList<string> comparisons)
    {
        var draft = new RunEditorSegmentDraft();
        foreach (string comparison in comparisons)
        {
            draft.Comparisons[comparison] = default;
        }

        return draft;
    }

    public Segment ToSegment(IReadOnlyList<string> comparisons)
    {
        var segment = new Segment(Name ?? "")
        {
            IconXml = IconXml,
            BestSegmentTime = BestSegmentTime,
        };
        foreach (string comparison in comparisons)
        {
            Comparisons.TryGetValue(comparison, out SplitTime time);
            segment.Comparisons[comparison] = time;
        }

        foreach ((int id, SplitTime time) in SegmentHistory)
        {
            segment.SegmentHistory[id] = time;
        }

        foreach (string unknown in UnknownOuterXml)
        {
            segment.UnknownOuterXml.Add(unknown);
        }

        return segment;
    }

    public void SetComparisonTime(string comparison, TimingMethod method, TimeSpan? value)
    {
        Comparisons.TryGetValue(comparison, out SplitTime time);
        time[method] = value;
        Comparisons[comparison] = time;
    }

    public void SetBestSegmentTime(TimingMethod method, TimeSpan? value)
    {
        SplitTime best = BestSegmentTime;
        best[method] = value;
        BestSegmentTime = best;
    }
}
