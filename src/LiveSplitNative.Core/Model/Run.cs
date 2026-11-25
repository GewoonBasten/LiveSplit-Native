using LiveSplitNative.Core.Model.Comparisons;

namespace LiveSplitNative.Core.Model;

public sealed class Run
{
    public const string PersonalBestComparisonName = "Personal Best";

    private readonly List<Segment> _segments = [];

    /// <summary>A new run gets the standard generators (Best Segments, Average Segments), as upstream Run(factory).</summary>
    public Run()
    {
        ComparisonGenerators = [.. StandardComparisonGeneratorsFactory.Create(this)];
    }

    public string GameName { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string LayoutPath { get; set; } = "";
    public string? Version { get; set; } = "1.7.0";
    public string? GameIconXml { get; set; }
    public string? AutoSplitterSettingsXml { get; set; }

    /// <summary>Unknown child elements under &lt;Run&gt; preserved for round-trip.</summary>
    public List<string> UnknownRootOuterXml { get; } = [];

    public TimeSpan Offset { get; set; }
    public int AttemptCount { get; set; }
    public bool HasChanged { get; set; }
    public RunMetadata Metadata { get; } = new();
    public List<Attempt> AttemptHistory { get; } = [];
    public List<string> CustomComparisons { get; } = [PersonalBestComparisonName];

    /// <summary>Generated comparisons (upstream IRun.ComparisonGenerators); never saved to the .lss.</summary>
    public List<IComparisonGenerator> ComparisonGenerators { get; }

    public IReadOnlyList<Segment> Segments => _segments;
    public int Count => _segments.Count;
    public Segment this[int index] => _segments[index];
    public Segment this[Index index] => _segments[index];

    /// <summary>Upstream Run.Comparisons: custom comparisons first, then the generated ones.</summary>
    public IEnumerable<string> Comparisons => [.. CustomComparisons, .. ComparisonGenerators.Select(x => x.Name)];

    /// <summary>Run.Comparisons.Contains(name), without building the list (it runs per cell per frame).</summary>
    public bool HasComparison(string name)
    {
        if (CustomComparisons.Contains(name, StringComparer.Ordinal))
        {
            return true;
        }

        foreach (IComparisonGenerator generator in ComparisonGenerators)
        {
            if (string.Equals(generator.Name, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Runs every generator in <see cref="ComparisonGenerators"/>, as Windows TimerForm.RegenerateComparisons
    /// does after a reset, a file load or a splits edit.
    /// </summary>
    public void RegenerateComparisons(ComparisonGeneratorSettings settings)
    {
        foreach (IComparisonGenerator generator in ComparisonGenerators)
        {
            generator.Generate(settings);
        }
    }

    /// <summary>
    /// Upstream Run.Clone: segments, attempts, comparisons and metadata are copied, so changing
    /// the copy (for example resetting it to save, as TimerForm.SaveSplits does) leaves this run
    /// alone. Like upstream, the copy shares this run's comparison generator objects; they still
    /// generate into this run, so do not regenerate comparisons on a copy.
    /// </summary>
    public Run Clone()
    {
        var copy = new Run
        {
            GameName = GameName,
            CategoryName = CategoryName,
            LayoutPath = LayoutPath,
            Version = Version,
            GameIconXml = GameIconXml,
            AutoSplitterSettingsXml = AutoSplitterSettingsXml,
            Offset = Offset,
            AttemptCount = AttemptCount,
            HasChanged = HasChanged,
        };
        copy.UnknownRootOuterXml.AddRange(UnknownRootOuterXml);
        copy.Metadata.RunId = Metadata.RunId;
        copy.Metadata.PlatformName = Metadata.PlatformName;
        copy.Metadata.UsesEmulator = Metadata.UsesEmulator;
        copy.Metadata.RegionName = Metadata.RegionName;
        foreach ((string name, string value) in Metadata.VariableValueNames)
        {
            copy.Metadata.VariableValueNames[name] = value;
        }

        foreach ((string name, string value) in Metadata.PermanentCustomVariables)
        {
            copy.Metadata.PermanentCustomVariables[name] = value;
        }

        foreach (Attempt attempt in AttemptHistory)
        {
            copy.AttemptHistory.Add(new Attempt(attempt.Index, attempt.Time, attempt.Started, attempt.Ended, attempt.PauseTime));
        }

        copy.CustomComparisons.Clear();
        copy.CustomComparisons.AddRange(CustomComparisons);
        copy.ComparisonGenerators.Clear();
        copy.ComparisonGenerators.AddRange(ComparisonGenerators);
        foreach (Segment segment in _segments)
        {
            copy._segments.Add(segment.Clone());
        }

        return copy;
    }

    public void AddSegment(Segment segment) => _segments.Add(segment);

    public void AddSegment(string name) => _segments.Add(new Segment(name));

    public void InsertSegment(int index, Segment segment)
    {
        if (index < 0 || index > _segments.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _segments.Insert(index, segment);
        HasChanged = true;
    }

    public void RemoveSegmentAt(int index)
    {
        if (_segments.Count <= 1)
        {
            throw new InvalidOperationException("A run must keep at least one segment.");
        }

        _segments.RemoveAt(index);
        HasChanged = true;
    }

    public void MoveSegment(int index, int offset)
    {
        int target = index + offset;
        if (index < 0 || index >= _segments.Count || target < 0 || target >= _segments.Count)
        {
            return;
        }

        Segment item = _segments[index];
        _segments.RemoveAt(index);
        _segments.Insert(target, item);
        HasChanged = true;
    }

    public void ClearSegments() => _segments.Clear();

    /// <summary>Blank run matching Windows StandardRunFactory (empty game/category, one blank segment).</summary>
    public static Run CreateStandard()
    {
        var run = new Run
        {
            GameName = "",
            CategoryName = "",
        };
        run.AddSegment("");
        return run;
    }

    /// <summary>Port of IRun.ClearHistory — clears attempt + segment history only.</summary>
    public void ClearHistory()
    {
        AttemptHistory.Clear();
        foreach (Segment segment in _segments)
        {
            segment.SegmentHistory.Clear();
        }

        HasChanged = true;
    }

    /// <summary>Port of IRun.ClearTimes — history, comparisons (keep PB name), golds, attempts, RunId.</summary>
    public void ClearTimes()
    {
        ClearHistory();
        CustomComparisons.Clear();
        CustomComparisons.Add(PersonalBestComparisonName);
        foreach (Segment segment in _segments)
        {
            segment.Comparisons.Clear();
            segment.BestSegmentTime = default;
            segment.SplitTime = default;
        }

        AttemptCount = 0;
        Metadata.RunId = null;
        HasChanged = true;
    }

    /// <summary>
    /// Port of AddComparisonFromRun (name-matched segments; last segment always maps to target last).
    /// Returns false if name already exists or is a race comparison.
    /// </summary>
    public bool TryImportComparison(Run comparisonRun, string name, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Comparison name is required.";
            return false;
        }

        name = name.Trim();
        if (name.StartsWith("[Race]", StringComparison.OrdinalIgnoreCase))
        {
            error = "A Comparison name cannot start with [Race].";
            return false;
        }

        if (CustomComparisons.Contains(name, StringComparer.Ordinal))
        {
            error = "A Comparison with this name already exists.";
            return false;
        }

        CustomComparisons.Add(name);
        int maxMatched = -1;
        for (int i = 0; i < comparisonRun.Count; i++)
        {
            Segment source = comparisonRun[i];
            if (i == comparisonRun.Count - 1 && Count > 0)
            {
                this[^1].Comparisons[name] = source.PersonalBestSplitTime;
                continue;
            }

            int matchingIndex = FindMatchingSegmentIndex(source.Name, maxMatched + 1);
            if (matchingIndex >= 0)
            {
                this[matchingIndex].Comparisons[name] = source.PersonalBestSplitTime;
                maxMatched = matchingIndex;
            }
        }

        HasChanged = true;
        FixSplits();
        return true;
    }

    private int FindMatchingSegmentIndex(string name, int startIndex)
    {
        string needle = name.Trim().ToLowerInvariant();
        for (int i = startIndex; i < Count; i++)
        {
            if (_segments[i].Name.Trim().ToLowerInvariant() == needle)
            {
                return i;
            }
        }

        return -1;
    }

    public void EnsureComparison(string name)
    {
        if (!CustomComparisons.Contains(name, StringComparer.Ordinal))
        {
            CustomComparisons.Add(name);
        }
    }

    public int GetMinSegmentHistoryIndex()
    {
        if (_segments.Count == 0)
        {
            return 1;
        }

        // Upstream SegmentHistory.GetMinIndex: Min(keys.Min(), 1) or 1 if empty.
        return _segments.Min(segment => segment.SegmentHistory.Count == 0
            ? 1
            : Math.Min(segment.SegmentHistory.Keys.Min(), 1));
    }

    /// <summary>Port of LiveSplit IRun.ImportSegmentHistory — copies PB segments into history before promoting a new PB.</summary>
    public void ImportSegmentHistory()
    {
        int minIndex = GetMinSegmentHistoryIndex();
        ImportSegmentHistory(TimingMethod.RealTime, minIndex - 1);
        ImportSegmentHistory(TimingMethod.GameTime, minIndex - 2);
    }

    private void ImportSegmentHistory(TimingMethod method, int index)
    {
        TimeSpan prevTime = TimeSpan.Zero;
        foreach (Segment segment in _segments)
        {
            segment.SegmentHistory[index] = new SplitTime(method, segment.PersonalBestSplitTime[method] - prevTime);
            if (segment.PersonalBestSplitTime[method].HasValue)
            {
                prevTime = segment.PersonalBestSplitTime[method]!.Value;
            }
        }
    }

    /// <summary>Port of LiveSplit IRun.FixSplits — comparison monotonicity, golds, history hygiene.</summary>
    public void FixSplits()
    {
        FixWithMethod(TimingMethod.RealTime);
        FixWithMethod(TimingMethod.GameTime);
        RemoveNullValues();
        ReattachUnattachedSegmentHistoryElements();
    }

    private void FixWithMethod(TimingMethod method)
    {
        FixComparisonTimesAndHistory(method);
        RemoveDuplicates(method);
    }

    private void FixComparisonTimesAndHistory(TimingMethod method)
    {
        foreach (Segment curSplit in _segments)
        {
            if (curSplit.BestSegmentTime[method] < TimeSpan.Zero)
            {
                SplitTime newTime = curSplit.BestSegmentTime;
                newTime[method] = null;
                curSplit.BestSegmentTime = newTime;
            }
        }

        int maxIndex = AttemptHistory.Count == 0 ? 0 : AttemptHistory.Max(x => x.Index);

        foreach (Segment curSplit in _segments)
        {
            int minIndex = curSplit.SegmentHistory.Count == 0 ? 1 : curSplit.SegmentHistory.Keys.Min();
            FixHistoryFromNullBestSegments(curSplit, method, minIndex, maxIndex);
        }

        foreach (string comparison in CustomComparisons.ToList())
        {
            TimeSpan previousTime = TimeSpan.Zero;
            foreach (Segment curSplit in _segments)
            {
                if (!curSplit.Comparisons.TryGetValue(comparison, out SplitTime cmp))
                {
                    continue;
                }

                if (cmp[method] is not { } cmpTime)
                {
                    continue;
                }

                if (cmpTime < previousTime)
                {
                    cmp[method] = previousTime;
                    curSplit.Comparisons[comparison] = cmp;
                    cmpTime = previousTime;
                }

                if (comparison == PersonalBestComparisonName)
                {
                    TimeSpan? currentSegment = cmpTime - previousTime;
                    if (curSplit.BestSegmentTime[method] is null || curSplit.BestSegmentTime[method] > currentSegment)
                    {
                        SplitTime newTime = curSplit.BestSegmentTime;
                        newTime[method] = currentSegment;
                        curSplit.BestSegmentTime = newTime;
                    }
                }

                previousTime = cmpTime;
            }
        }

        foreach (Segment curSplit in _segments)
        {
            int minIndex = curSplit.SegmentHistory.Count == 0 ? 1 : curSplit.SegmentHistory.Keys.Min();
            FixHistoryFromBestSegmentTimes(curSplit, method, minIndex, maxIndex);
        }
    }

    private static void FixHistoryFromNullBestSegments(Segment curSplit, TimingMethod method, int minIndex, int maxIndex)
    {
        if (curSplit.BestSegmentTime[method] is not null)
        {
            return;
        }

        for (int runIndex = minIndex; runIndex <= maxIndex; runIndex++)
        {
            if (curSplit.SegmentHistory.TryGetValue(runIndex, out SplitTime historyTime)
                && historyTime[method] is not null)
            {
                curSplit.SegmentHistory.Remove(runIndex);
            }
        }
    }

    private static void FixHistoryFromBestSegmentTimes(Segment curSplit, TimingMethod method, int minIndex, int maxIndex)
    {
        if (curSplit.BestSegmentTime[method] is null)
        {
            return;
        }

        for (int runIndex = minIndex; runIndex <= maxIndex; runIndex++)
        {
            if (!curSplit.SegmentHistory.TryGetValue(runIndex, out SplitTime historyTime))
            {
                continue;
            }

            if (historyTime[method] < curSplit.BestSegmentTime[method])
            {
                historyTime[method] = curSplit.BestSegmentTime[method];
                curSplit.SegmentHistory[runIndex] = historyTime;
            }
        }
    }

    private void RemoveNullValues()
    {
        var cache = new List<int>();
        int maxIndex = AttemptHistory.Count == 0 ? 0 : AttemptHistory.Max(x => x.Index);
        for (int runIndex = GetMinSegmentHistoryIndex(); runIndex <= maxIndex; runIndex++)
        {
            for (int index = 0; index < Count; index++)
            {
                if (!_segments[index].SegmentHistory.TryGetValue(runIndex, out SplitTime element))
                {
                    RemoveItemsFromCache(index, cache);
                }
                else if (element.RealTime is null && element.GameTime is null)
                {
                    cache.Add(runIndex);
                }
                else
                {
                    cache.Clear();
                }
            }

            RemoveItemsFromCache(Count, cache);
        }
    }

    // Upstream keeps one set per timing method and ignores the method argument, so a PB
    // segment imported into history only counts as a duplicate of an attempt's segment
    // when the same timing method matches.
    private void RemoveDuplicates(TimingMethod method)
    {
        _ = method;
        var rtaSet = new HashSet<TimeSpan>();
        var igtSet = new HashSet<TimeSpan>();
        foreach (Segment segment in _segments)
        {
            rtaSet.Clear();
            igtSet.Clear();
            foreach (Attempt attempt in AttemptHistory)
            {
                if (segment.SegmentHistory.TryGetValue(attempt.Index, out SplitTime element))
                {
                    if (element.RealTime is { } attemptRta)
                    {
                        rtaSet.Add(attemptRta);
                    }

                    if (element.GameTime is { } attemptIgt)
                    {
                        igtSet.Add(attemptIgt);
                    }
                }
            }

            int minIndex = segment.SegmentHistory.Count == 0 ? 1 : segment.SegmentHistory.Keys.Min();
            for (int runIndex = minIndex; runIndex <= 0; runIndex++)
            {
                if (!segment.SegmentHistory.TryGetValue(runIndex, out SplitTime element))
                {
                    continue;
                }

                bool isNull = true;
                bool isUnique = false;
                if (element.RealTime is { } rta)
                {
                    isUnique |= rtaSet.Add(rta);
                    isNull = false;
                }

                if (element.GameTime is { } igt)
                {
                    isUnique |= igtSet.Add(igt);
                    isNull = false;
                }

                if (!isUnique && !isNull)
                {
                    segment.SegmentHistory.Remove(runIndex);
                }
            }
        }
    }

    private void RemoveItemsFromCache(int index, IList<int> cache)
    {
        int ind = index - cache.Count;
        foreach (int item in cache)
        {
            _segments[ind].SegmentHistory.Remove(item);
            ind++;
        }

        cache.Clear();
    }

    private void ReattachUnattachedSegmentHistoryElements()
    {
        int maxId = AttemptHistory.Count == 0 ? 0 : AttemptHistory.Max(x => x.Index);
        int minId = GetMinSegmentHistoryIndex();

        while (true)
        {
            int unattachedId = _segments
                .Select(seg => seg.SegmentHistory.Count == 0 ? 0 : seg.SegmentHistory.Keys.Max())
                .Where(i => i > maxId)
                .DefaultIfEmpty(0)
                .Max();
            if (unattachedId <= 0)
            {
                break;
            }

            int reassignId = minId - 1;
            foreach (Segment segment in _segments)
            {
                if (segment.SegmentHistory.TryGetValue(unattachedId, out SplitTime time))
                {
                    segment.SegmentHistory[reassignId] = time;
                    segment.SegmentHistory.Remove(unattachedId);
                }
            }

            minId = reassignId;
        }
    }
}
