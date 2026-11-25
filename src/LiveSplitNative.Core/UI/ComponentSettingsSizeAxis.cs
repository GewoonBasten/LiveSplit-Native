namespace LiveSplitNative.Core.UI;

/// <summary>
/// Windows LiveSplit exposes only the component's primary-axis size:
/// Height in vertical layouts and Width in horizontal layouts.
/// </summary>
public enum ComponentSettingsSizeAxis
{
    Height,
    Width,
}

public static class ComponentSettingsSizeAxisPolicy
{
    public static ComponentSettingsSizeAxis For(LayoutMode mode) =>
        mode == LayoutMode.Horizontal
            ? ComponentSettingsSizeAxis.Width
            : ComponentSettingsSizeAxis.Height;
}
