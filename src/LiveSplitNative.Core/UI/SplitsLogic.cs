using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// LiveSplit.Splits SplitsComponent.Update (93a11b85): which splits the split rows show, and the
/// scroll position. One instance per Splits component, as ScrollOffset belongs to the component.
/// </summary>
public sealed class SplitsLogic
{
    /// <summary>The scroll position: splits moved down (positive) or up from where the current split puts the list.</summary>
    public int ScrollOffset { get; private set; }

    /// <summary>state_OnScrollUp.</summary>
    public void ScrollUp() => ScrollOffset--;

    /// <summary>state_OnScrollDown.</summary>
    public void ScrollDown() => ScrollOffset++;

    /// <summary>On start, split, skip split, undo split and reset the list goes back to the current split.</summary>
    public void ResetScroll() => ScrollOffset = 0;

    /// <summary>
    /// The split index of each split row, top to bottom: Min(splits, Total Splits) rows starting
    /// at skipCount (enough rows above the current split to show Upcoming Splits below it), and
    /// with Always Show Last Split the run's last split in the last row.
    /// </summary>
    public IReadOnlyList<int> Update(LiveSplitState state, SplitsComponentSettings settings)
    {
        int count = state.Run.Count;
        // SplitsComponent.Prepare.
        int visualSplitCount = Math.Min(count, settings.VisualSplitCount);
        if (visualSplitCount <= 0)
        {
            return [];
        }

        int skipCount = Math.Min(
            Math.Max(
                0,
                state.CurrentSplitIndex - (visualSplitCount - 2 - settings.SplitPreviewCount + (settings.AlwaysShowLastSplit ? 0 : 1))),
            count - visualSplitCount);
        ScrollOffset = Math.Min(Math.Max(ScrollOffset, -skipCount), count - skipCount - visualSplitCount);
        skipCount += ScrollOffset;

        var rows = new List<int>(visualSplitCount);
        int take = visualSplitCount - 1 + (settings.AlwaysShowLastSplit ? 0 : 1);
        for (int i = skipCount; i < count && rows.Count < take; i++)
        {
            rows.Add(i);
        }

        if (settings.AlwaysShowLastSplit)
        {
            rows.Add(count - 1);
        }

        return rows;
    }
}
