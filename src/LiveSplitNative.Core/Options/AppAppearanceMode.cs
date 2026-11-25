namespace LiveSplitNative.Core.Options;

/// <summary>
/// Application chrome appearance (settings / OS UI). Independent of layout/.lsl timer colors.
/// Linux later: map via platform seam (GTK/Qt), same enum.
/// </summary>
public enum AppAppearanceMode
{
    System = 0,
    Light = 1,
    Dark = 2,
}
