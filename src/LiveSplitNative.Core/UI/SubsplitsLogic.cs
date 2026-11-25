using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// SplitsComponent.SectionList (LiveSplit.Subsplits, 93a11b85): a section is the subsplits
/// ("-" names) before a split and that split, numbered from the top. The split ending a section
/// is its major split; a section with no subsplits is just that split.
/// </summary>
public sealed class SubsplitsSectionList
{
    /// <summary>SectionList.Section: the section's first split and its major (last) split.</summary>
    public readonly record struct Section(int StartIndex, int EndIndex)
    {
        public bool SplitInRange(int splitIndex) => splitIndex >= StartIndex && splitIndex <= EndIndex;

        /// <summary>getSubsplitCount.</summary>
        public int SubsplitCount => EndIndex - StartIndex;
    }

    private readonly List<Section> _sections = [];
    private readonly int[] _sectionOf;

    /// <summary>UpdateSplits.</summary>
    public SubsplitsSectionList(IReadOnlyList<Segment> splits)
    {
        for (int splitIndex = splits.Count - 1; splitIndex >= 0; splitIndex--)
        {
            int sectionIndex = splitIndex;
            while (splitIndex > 0 && SubsplitsLogic.IsSubsplitName(splits[splitIndex - 1].Name))
            {
                splitIndex--;
            }

            _sections.Insert(0, new Section(splitIndex, sectionIndex));
        }

        _sectionOf = new int[splits.Count];
        for (int section = 0; section < _sections.Count; section++)
        {
            for (int i = _sections[section].StartIndex; i <= _sections[section].EndIndex; i++)
            {
                _sectionOf[i] = section;
            }
        }
    }

    public IReadOnlyList<Section> Sections => _sections;

    /// <summary>getSection: the section of a split, -1 for an index outside the run.</summary>
    public int GetSection(int splitIndex) =>
        splitIndex >= 0 && splitIndex < _sectionOf.Length ? _sectionOf[splitIndex] : -1;

    /// <summary>isMajorSplit: the split ends its section (true outside the run).</summary>
    public bool IsMajorSplit(int splitIndex)
    {
        int section = GetSection(splitIndex);
        return section == -1 || splitIndex == _sections[section].EndIndex;
    }

    /// <summary>getMajorSplit: the split that ends this split's section.</summary>
    public int GetMajorSplit(int splitIndex)
    {
        int section = GetSection(splitIndex);
        return section == -1 ? splitIndex : _sections[section].EndIndex;
    }
}

/// <summary>What a Subsplits row shows: nothing (a blank row), a section header or a split.</summary>
public enum SubsplitsRowKind
{
    Blank,
    Header,
    Split,
}

/// <summary>
/// One of the Subsplits component's SplitComponents after SplitsComponent.Update: Header, Split
/// (the section's major split for a header), TopSplit (the section's first split for a header
/// or a collapsed split), CollapsedSplit, ForceIndent and oddSplit (Alternating: true takes
/// Background Color, false Background Color 2).
/// </summary>
public readonly record struct SubsplitsRow(
    SubsplitsRowKind Kind,
    int SplitIndex,
    int TopSplit,
    bool CollapsedSplit,
    bool ForceIndent,
    bool OddSplit);

/// <summary>
/// The result of one SplitsComponent.Update: the rows, top to bottom, and what the rows read
/// from the component: SplitsSettings.HilightSplit and SectionSplit (split indexes, -1 for
/// none), lastSplitOfSection, and the separator before the last row (DisplayedSize and
/// UseSeparatorColor; only when Always Show Last Split and Show Separator Before Last Split).
/// </summary>
public sealed record SubsplitsFrame(
    IReadOnlyList<SubsplitsRow> Rows,
    int HighlightSplit,
    int SectionSplit,
    int LastSplitOfSection,
    int CurrentSection,
    float LastSeparatorSize,
    bool LastSeparatorUsesSeparatorColor);

/// <summary>
/// LiveSplit.Subsplits SplitsComponent (93a11b85): which split each row shows, and the scroll
/// position. One instance per Subsplits component, as ScrollOffset belongs to the component.
/// </summary>
public sealed class SubsplitsLogic
{
    /// <summary>The scroll position: rows moved down (positive) or up from the current split.</summary>
    public int ScrollOffset { get; private set; }

    /// <summary>state_OnScrollUp.</summary>
    public void ScrollUp() => ScrollOffset--;

    /// <summary>state_OnScrollDown.</summary>
    public void ScrollDown() => ScrollOffset++;

    /// <summary>On start, split, skip split, undo split and reset the list goes back to the current split.</summary>
    public void ResetScroll() => ScrollOffset = 0;

    /// <summary>Split.Name.StartsWith("-").</summary>
    public static bool IsSubsplitName(string? name) => (name ?? "").StartsWith('-');

    /// <summary>
    /// SplitsComponent.SectionList.UpdateSplits as section numbers, one per split; the rows'
    /// Alternating background follows these sections.
    /// </summary>
    public static int[] WindowsSections(IReadOnlyList<Segment> segments)
    {
        var list = new SubsplitsSectionList(segments);
        var sections = new int[segments.Count];
        for (int i = 0; i < sections.Length; i++)
        {
            sections[i] = list.GetSection(i);
        }

        return sections;
    }

    /// <summary>SplitsComponent.Update: the rows for the run as it is now.</summary>
    public SubsplitsFrame Update(LiveSplitState state, SubsplitsComponentSettings settings)
    {
        int visualSplitCount = Math.Max(0, settings.VisualSplitCount);
        Run run = state.Run;
        int count = run.Count;
        bool labels = settings.ShowColumnLabels;
        if (count == 0)
        {
            // Windows always has a split; an empty run shows blank rows.
            var blanks = Enumerable.Repeat(new SubsplitsRow(SubsplitsRowKind.Blank, -1, 0, false, false, true), visualSplitCount).ToList();
            return new SubsplitsFrame(blanks, -1, -1, -1, -1, settings.ShowThinSeparators ? 1f : 0f, false);
        }

        var sections = new SubsplitsSectionList(run.Segments);
        int runningSectionIndex = Math.Min(Math.Max(state.CurrentSplitIndex, 0), count - 1);
        ScrollOffset = Math.Min(Math.Max(ScrollOffset, -runningSectionIndex), count - runningSectionIndex - 1);
        int currentSplit = ScrollOffset + runningSectionIndex;
        int currentSection = sections.GetSection(currentSplit);
        runningSectionIndex = sections.GetSection(runningSectionIndex);
        int lastSplitOfSection = sections.Sections[currentSection].SubsplitCount > 0
            ? sections.Sections[currentSection].EndIndex
            : -1;

        int highlightSplit;
        int sectionSplit;
        if (settings.HideSubsplits)
        {
            if (ScrollOffset != 0)
            {
                currentSplit = sections.GetMajorSplit(currentSplit);
                highlightSplit = currentSplit;
            }
            else
            {
                highlightSplit = -1;
            }

            sectionSplit = sections.Sections[runningSectionIndex].EndIndex;
        }
        else
        {
            highlightSplit = ScrollOffset != 0 ? currentSplit : -1;
            sectionSplit = currentSection == runningSectionIndex ? -1 : sections.Sections[runningSectionIndex].EndIndex;
        }

        bool addLast = settings.AlwaysShowLastSplit || currentSplit == count - 1;
        bool addHeader = settings.ShowSectionHeader && sections.Sections[currentSection].SubsplitCount > 0;

        int freeSplits = visualSplitCount - (addLast ? 1 : 0) - (addHeader ? 1 : 0);
        int topSplit = currentSplit - 1;
        int bottomSplit = currentSplit + 1;
        int majorSplitsToAdd = !settings.ShowSubsplits && !settings.HideSubsplits
            ? Math.Min(currentSection, settings.MinimumMajorSplits)
            : 0;

        bool ShouldIncludeSplit(int split) =>
            sections.IsMajorSplit(split)
                ? !settings.OnlyCurrentSection || sections.GetSection(split) == currentSection
                : settings.ShowSubsplits || (!settings.HideSubsplits && sections.GetSection(split) == currentSection);

        List<int> visibleSplits = [];
        if (currentSplit < count - 1 && freeSplits > 0 && (!settings.HideSubsplits || sections.IsMajorSplit(currentSplit)))
        {
            visibleSplits.Add(currentSplit);
            freeSplits--;
        }

        int previewCount = 0;
        while (previewCount < settings.SplitPreviewCount && bottomSplit < count - (addLast ? 1 : 0) && freeSplits > majorSplitsToAdd)
        {
            if (ShouldIncludeSplit(bottomSplit))
            {
                if (bottomSplit == count - 1)
                {
                    addLast = true;
                }
                else
                {
                    visibleSplits.Add(bottomSplit);
                    previewCount++;
                }

                freeSplits--;
            }

            bottomSplit++;
        }

        while (topSplit >= 0 && freeSplits > 0)
        {
            bool isMajor = sections.IsMajorSplit(topSplit);
            if (ShouldIncludeSplit(topSplit) && (freeSplits > majorSplitsToAdd || sections.IsMajorSplit(topSplit)))
            {
                visibleSplits.Insert(0, topSplit);
                freeSplits--;
                if (isMajor)
                {
                    majorSplitsToAdd--;
                }
            }

            topSplit--;
        }

        while (bottomSplit < count - (addLast ? 1 : 0) && freeSplits > 0)
        {
            if (ShouldIncludeSplit(bottomSplit))
            {
                if (bottomSplit == count - 1)
                {
                    addLast = true;
                }
                else
                {
                    visibleSplits.Add(bottomSplit);
                }

                freeSplits--;
            }

            bottomSplit++;
        }

        // The separator before the last row: thin (or none) when blank rows or the split just
        // before the last one are above it, or the previous section ends right above it.
        float lastSeparatorSize = 2f;
        bool lastSeparatorUsesSeparatorColor = true;
        if (settings.AlwaysShowLastSplit && settings.SeparatorLastSplit)
        {
            int lastIndex = count - 1;
            if (freeSplits > 0 || (visibleSplits.Count > 0 && visibleSplits[^1] == lastIndex - 1))
            {
                lastSeparatorSize = settings.ShowThinSeparators ? 1f : 0f;
                lastSeparatorUsesSeparatorColor = false;
            }
            else
            {
                int prevSection = sections.GetSection(lastIndex) - 1;
                if (visibleSplits.Count > 0 && (prevSection <= 0 || visibleSplits[^1] == sections.Sections[prevSection].EndIndex))
                {
                    lastSeparatorSize = settings.ShowThinSeparators ? 1f : 0f;
                    lastSeparatorUsesSeparatorColor = false;
                }
            }
        }

        if (!settings.LockLastSplit && addLast)
        {
            visibleSplits.Add(count - 1);
        }

        for (; freeSplits > 0; freeSplits--)
        {
            visibleSplits.Add(int.MinValue);
        }

        if (settings.LockLastSplit && addLast)
        {
            visibleSplits.Add(count - 1);
        }

        if (addHeader)
        {
            int insertIndex = 0;
            if (currentSection > 0)
            {
                insertIndex = visibleSplits.IndexOf(sections.Sections[currentSection - 1].EndIndex) + 1;
            }

            visibleSplits.Insert(insertIndex, -(currentSection + 1));
        }

        var rows = new List<SubsplitsRow>(visualSplitCount);
        for (int i = 0; i < visualSplitCount; i++)
        {
            if (i >= visibleSplits.Count)
            {
                rows.Add(new SubsplitsRow(SubsplitsRowKind.Blank, -1, 0, false, false, true));
                continue;
            }

            int split = visibleSplits[i];
            bool forceIndent = !settings.ShowSubsplits && !settings.HideSubsplits && settings.IndentSectionSplit && split == lastSplitOfSection;
            if (split == int.MinValue)
            {
                rows.Add(new SubsplitsRow(SubsplitsRowKind.Blank, -1, 0, false, forceIndent, OddSplit: true));
            }
            else if (split < 0)
            {
                SubsplitsSectionList.Section section = sections.Sections[-split - 1];
                rows.Add(new SubsplitsRow(
                    SubsplitsRowKind.Header,
                    section.EndIndex,
                    section.StartIndex,
                    CollapsedSplit: false,
                    forceIndent,
                    OddSplit: ((-split - 1 + (labels ? 1 : 0)) % 2) == 0));
            }
            else
            {
                int sectionIndex = sections.GetSection(split);
                SubsplitsSectionList.Section section = sections.Sections[sectionIndex];
                bool collapsed = (settings.HideSubsplits || sectionIndex != currentSection)
                    && section.SubsplitCount > 0
                    && !settings.ShowSubsplits;
                rows.Add(new SubsplitsRow(
                    SubsplitsRowKind.Split,
                    split,
                    collapsed ? section.StartIndex : split,
                    collapsed,
                    forceIndent,
                    OddSplit: ((sectionIndex + (labels ? 1 : 0)) % 2) == 0));
            }
        }

        return new SubsplitsFrame(
            rows,
            highlightSplit,
            sectionSplit,
            lastSplitOfSection,
            currentSection,
            lastSeparatorSize,
            lastSeparatorUsesSeparatorColor);
    }
}
