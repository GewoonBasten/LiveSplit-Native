using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// ContextualSlideshowComponent (LiveSplit.ContextualSlideshow 1.6): which of Possible Time Save
/// (0), Previous Segment (1) and Run Prediction (2) it shows over time.
/// </summary>
public class ContextualSlideshowTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static readonly bool[] None = [false, false, false];

    [Fact]
    public void The_First_Update_Queues_All_In_Order_And_Shows_The_First()
    {
        var slideshow = new ContextualSlideshowLogic(3, S(0));
        Assert.Null(slideshow.Shown);
        slideshow.Update([true, true, true], S(0));
        Assert.Equal(0, slideshow.Shown);
        Assert.True(slideshow.Invalidated);
    }

    [Fact]
    public void A_Shown_Component_That_Stops_Changing_Gives_Way_After_3_Seconds()
    {
        var slideshow = new ContextualSlideshowLogic(3, S(0));
        slideshow.Update([true, true, true], S(0));
        slideshow.Update(None, S(3));
        Assert.Equal(0, slideshow.Shown);
        slideshow.Update(None, S(3.1));
        Assert.Equal(1, slideshow.Shown);
        Assert.True(slideshow.Invalidated);
        slideshow.Update(None, S(6.2));
        Assert.Equal(2, slideshow.Shown);

        // The last one in the queue stays: nothing else wants a turn.
        slideshow.Update(None, S(9.5));
        Assert.Equal(2, slideshow.Shown);
    }

    [Fact]
    public void A_Changing_Component_Keeps_Its_Turn_For_8_Seconds()
    {
        var slideshow = new ContextualSlideshowLogic(3, S(0));
        slideshow.Update([true, true, true], S(0));
        for (double t = 1; t <= 8; t++)
        {
            slideshow.Update([true, false, false], S(t));
            Assert.Equal(0, slideshow.Shown);
        }

        slideshow.Update([true, false, false], S(8.1));
        Assert.Equal(1, slideshow.Shown);
    }

    [Fact]
    public void A_Component_That_Changes_Joins_The_End_Of_The_Queue()
    {
        var slideshow = new ContextualSlideshowLogic(3, S(0));
        slideshow.Update([false, true, false], S(0));
        Assert.Equal(1, slideshow.Shown);
        slideshow.Update([false, false, true], S(1));
        Assert.Equal(1, slideshow.Shown);
        slideshow.Update(None, S(4.1));
        Assert.Equal(2, slideshow.Shown);
    }

    [Fact]
    public void After_12_Seconds_Without_A_Change_Of_Turn_The_Longest_Waiting_Joins_Again()
    {
        var slideshow = new ContextualSlideshowLogic(3, S(0));
        slideshow.Update([true, true, true], S(0));
        slideshow.Update(None, S(3.1)); // 0 gives way at 3.1
        slideshow.Update(None, S(6.2)); // 1 gives way at 6.2; 2 is shown, alone in the queue
        Assert.Equal(2, slideshow.Shown);
        slideshow.Update(None, S(18.1));
        Assert.Equal(2, slideshow.Shown);

        // 12 s after the last change of turn (6.2): the one out longest (0, since 3.1) joins.
        slideshow.Update(None, S(18.3));
        Assert.Equal(2, slideshow.Shown);
        slideshow.Update(None, S(18.4));
        Assert.Equal(0, slideshow.Shown);
    }

    [Fact]
    public void It_Has_No_Settings_And_Is_A_Media_Component()
    {
        StandardLayoutFactory.BuiltinComponentInfo info = Assert.Single(
            StandardLayoutFactory.BuiltinCatalog, c => c.Path == StandardLayoutFactory.ContextualSlideshowPath);
        Assert.Equal("Contextual Slideshow", info.DisplayName);
        Assert.Equal(StandardLayoutFactory.ComponentCategory.Media, info.Category);
        LayoutComponent component = StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.ContextualSlideshowPath);
        Assert.False(component.HasSettings);
        Assert.Equal("Contextual Slideshow", component.DisplayName);

        // No background by default (as Windows draws it); a row background round-trips.
        ContextualSlideshowComponentSettings settings = ContextualSlideshowComponentSettings.FromInnerXml(component.SettingsInnerXml);
        Assert.Equal(0, settings.BackgroundColor.A);
        settings.BackgroundColor = ArgbColor.FromArgb(255, 1, 2, 3);
        Assert.Equal(settings.BackgroundColor, ContextualSlideshowComponentSettings.FromInnerXml(settings.ToInnerXml()).BackgroundColor);
    }
}
