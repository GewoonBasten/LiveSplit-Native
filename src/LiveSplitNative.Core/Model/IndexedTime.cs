namespace LiveSplitNative.Core.Model;

/// <summary>Port of LiveSplit.Model.IndexedTime used by Sum of Best predictions.</summary>
public readonly struct IndexedTime
{
    public IndexedTime(SplitTime time, int index)
    {
        Time = time;
        Index = index;
    }

    public SplitTime Time { get; }
    public int Index { get; }
}
