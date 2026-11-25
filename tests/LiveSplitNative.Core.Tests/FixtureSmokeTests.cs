using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class FixtureSmokeTests
{
    [Fact]
    public void SmallRepresentative_Lss_And_DefaultLayout_Lsl_Load_Together()
    {
        string lss = FixturePath("small-representative.lss");
        string lsl = FixturePath("default-layout.lsl");
        Assert.True(File.Exists(lss), lss);
        Assert.True(File.Exists(lsl), lsl);

        Run run = XmlRunSerializer.LoadFile(lss);
        Layout layout = XmlLayoutSerializer.LoadFile(lsl);

        Assert.True(run.Count >= 1);
        Assert.False(string.IsNullOrWhiteSpace(run.GameName));
        Assert.Equal(4, layout.Components.Count);
        Assert.Equal(StandardLayoutFactory.TitlePath, layout.Components[0].Path);
        Assert.Equal(StandardLayoutFactory.SplitsPath, layout.Components[1].Path);
        Assert.Equal(StandardLayoutFactory.TimerPath, layout.Components[2].Path);
        Assert.Equal(StandardLayoutFactory.PreviousSegmentPath, layout.Components[3].Path);

        // Settings colors present (StandardLayout palette round-trip).
        Assert.NotEqual(default, layout.Settings.AheadGainingTimeColor);
        Assert.NotEqual(default, layout.Settings.AheadLosingTimeColor);
        Assert.NotEqual(default, layout.Settings.BehindGainingTimeColor);
        Assert.NotEqual(default, layout.Settings.BehindLosingTimeColor);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);
}
