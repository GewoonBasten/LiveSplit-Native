using Avalonia.Controls;

namespace LiveSplitNative.App;

/// <summary>
/// A settings-row slider: Fluent's horizontal Slider is 50 high (15 above and below its 20 high
/// track), twice a WinForms TrackBar row; the lightweight-styling resources bring it to 28 so
/// rows of trackbars (Sound Effects' Volume Mixer, Image Opacity / Blur) keep Windows' 29 rows.
/// </summary>
internal static class SliderChrome
{
    public static Slider Compact(Slider slider)
    {
        slider.Resources["SliderPreContentMargin"] = new GridLength(4);
        slider.Resources["SliderPostContentMargin"] = new GridLength(4);
        slider.Resources["SliderHorizontalHeight"] = 28d;
        return slider;
    }
}
