using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class LayoutAssignTests
{
    [Fact]
    public void Assign_Replaces_Settings_And_Components()
    {
        Layout live = StandardLayoutFactory.Create();
        Layout snapshot = live.Clone();
        live.Settings.TextColor = ArgbColor.FromArgb(255, 1, 2, 3);
        live.Components.Clear();
        live.Components.Add(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.TimerPath));
        live.Mode = LayoutMode.Horizontal;

        live.Assign(snapshot);

        Assert.Equal(snapshot.Mode, live.Mode);
        Assert.Equal(snapshot.Settings.TextColor, live.Settings.TextColor);
        Assert.Equal(snapshot.Components.Count, live.Components.Count);
        Assert.Equal(snapshot.Components[0].Path, live.Components[0].Path);
        Assert.NotSame(snapshot.Components[0], live.Components[0]);
    }
}
