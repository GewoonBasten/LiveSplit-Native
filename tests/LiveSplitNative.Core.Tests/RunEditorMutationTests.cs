using LiveSplitNative.Core.Model;

namespace LiveSplitNative.Core.Tests;

public class RunEditorMutationTests
{
    [Fact]
    public void Insert_Remove_Move_Segments()
    {
        Run run = Run.CreateStandard();
        Assert.Equal(1, run.Count);

        run.AddSegment("B");
        run.InsertSegment(0, new Segment("A"));
        Assert.Equal(["A", "", "B"], run.Segments.Select(s => s.Name));

        run.MoveSegment(0, 1);
        Assert.Equal(["", "A", "B"], run.Segments.Select(s => s.Name));

        run.RemoveSegmentAt(0);
        Assert.Equal(["A", "B"], run.Segments.Select(s => s.Name));
        Assert.True(run.HasChanged);
    }

    [Fact]
    public void Remove_Last_Segment_Throws()
    {
        Run run = Run.CreateStandard();
        Assert.Throws<InvalidOperationException>(() => run.RemoveSegmentAt(0));
    }
}
