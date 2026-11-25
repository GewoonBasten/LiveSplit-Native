namespace LiveSplitNative.Core.UI;

/// <summary>
/// Permanent color-picker preview policy (Basten 2026-10-09).
/// Bottom preview = Avalonia ColorPreviewer segmented strip + raised selected color.
/// Custom continuous bars (GrayscalePreview / ShadePreview) are forbidden.
/// </summary>
public static class ColorPreviewPolicy
{
    public const string PermanentRule =
        "The color picker's bottom preview is a segmented strip of discrete color shades with a raised selected-color preview, matching the user's screenshot. It must never be replaced by a continuous grayscale gradient or a solid-color rectangle.";

    public static readonly string[] ForbiddenCustomPreviewTags =
    [
        "GrayscalePreview",
        "ShadePreview",
    ];

    public static bool IsForbiddenCustomPreviewTag(object? tag) =>
        tag is string s
        && ForbiddenCustomPreviewTags.Contains(s, StringComparer.Ordinal);

    /// <summary>
    /// Continuous LinearGradientBrush with only achromatic stops = rejected B&amp;W ramp.
    /// </summary>
    public static bool IsRejectedContinuousBlackWhiteRamp(IReadOnlyList<(byte R, byte G, byte B)> stops) =>
        stops.Count >= 2
        && stops.All(s => s.R == s.G && s.G == s.B)
        && stops.Select(s => s.R).Distinct().Count() >= 2;
}
