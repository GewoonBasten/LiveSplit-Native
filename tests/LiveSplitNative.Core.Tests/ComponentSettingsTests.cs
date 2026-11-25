using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class ComponentSettingsTests
{
    [Fact]
    public void TitleSettings_RoundTrip()
    {
        var settings = new TitleComponentSettings
        {
            ShowGameName = true,
            ShowCategoryName = false,
            ShowAttemptCount = true,
            SingleLine = true,
            BackgroundGradient = "Horizontal",
            DisplayGameIcon = false,
        };
        TitleComponentSettings again = TitleComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.True(again.ShowGameName);
        Assert.False(again.ShowCategoryName);
        Assert.True(again.ShowAttemptCount);
        Assert.True(again.SingleLine);
        Assert.Equal("Horizontal", again.BackgroundGradient);
        Assert.False(again.DisplayGameIcon);
    }

    [Fact]
    public void TimerAndSplitsSettings_RoundTrip()
    {
        var timer = new TimerComponentSettings
        {
            TimerHeight = 80,
            TimerAccuracy = TimeAccuracy.Tenths,
            CenterTimer = true,
            ShowGradient = false,
        };
        TimerComponentSettings timer2 = TimerComponentSettings.FromInnerXml(timer.ToInnerXml());
        Assert.Equal(80, timer2.TimerHeight);
        Assert.Equal(TimeAccuracy.Tenths, timer2.TimerAccuracy);
        Assert.True(timer2.CenterTimer);
        Assert.False(timer2.ShowGradient);

        var splits = new SplitsComponentSettings
        {
            VisualSplitCount = 10,
            DeltasAccuracy = TimeAccuracy.Hundredths,
            AlwaysShowLastSplit = false,
            CurrentSplitGradient = "Horizontal",
            CurrentSplitTopColor = ArgbColor.FromArgb(255, 72, 140, 255),
            CurrentSplitBottomColor = ArgbColor.FromArgb(255, 12, 32, 88),
        };
        SplitsComponentSettings splits2 = SplitsComponentSettings.FromInnerXml(splits.ToInnerXml());
        Assert.Equal(10, splits2.VisualSplitCount);
        Assert.Equal(TimeAccuracy.Hundredths, splits2.DeltasAccuracy);
        Assert.False(splits2.AlwaysShowLastSplit);
        Assert.Equal("Horizontal", splits2.CurrentSplitGradient);
        Assert.Equal(splits.CurrentSplitTopColor, splits2.CurrentSplitTopColor);
        Assert.Equal(splits.CurrentSplitBottomColor, splits2.CurrentSplitBottomColor);
    }

    [Fact]
    public void DefaultLayoutFixture_Parses_Builtin_Component_Settings()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "default-layout.lsl");
        Layout layout = XmlLayoutSerializer.LoadFile(path);
        TitleComponentSettings title = TitleComponentSettings.FromInnerXml(layout.Components[0].SettingsInnerXml);
        SplitsComponentSettings splits = SplitsComponentSettings.FromInnerXml(layout.Components[1].SettingsInnerXml);
        TimerComponentSettings timer = TimerComponentSettings.FromInnerXml(layout.Components[2].SettingsInnerXml);
        PreviousSegmentComponentSettings prev = PreviousSegmentComponentSettings.FromInnerXml(layout.Components[3].SettingsInnerXml);

        Assert.True(title.ShowAttemptCount);
        Assert.True(title.DisplayGameIcon);
        Assert.Equal(16, splits.VisualSplitCount);
        Assert.True(splits.AlwaysShowLastSplit);
        Assert.Equal(69, timer.TimerHeight, precision: 0);
        Assert.Equal(TimeAccuracy.Hundredths, timer.TimerAccuracy);
        Assert.Equal(TimeAccuracy.Tenths, prev.DeltaAccuracy);
        Assert.True(prev.DropDecimals);
    }
}
