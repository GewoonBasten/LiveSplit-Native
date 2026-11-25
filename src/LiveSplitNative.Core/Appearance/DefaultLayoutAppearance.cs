namespace LiveSplitNative.Core.Appearance;

/// <summary>
/// Colors/fonts from LiveSplit StandardLayoutSettingsFactory + Timer component defaults.
/// ARGB values verified against upstream StandardLayoutSettingsFactory.cs.
/// </summary>
public static class DefaultLayoutAppearance
{
    public const string TimerFontFamily = "Century Gothic";
    public const float TimerFontSizePx = 43.75f;
    public const float TimerHeight = 50f;
    public const float TimerWidth = 225f;
    public const float DecimalsSize = 35f;

    public static readonly (byte A, byte R, byte G, byte B) TextColor = (255, 255, 255, 255);
    public static readonly (byte A, byte R, byte G, byte B) PersonalBestColor = (255, 22, 166, 255);
    public static readonly (byte A, byte R, byte G, byte B) AheadGainingTimeColor = (255, 41, 204, 84);
    public static readonly (byte A, byte R, byte G, byte B) AheadLosingTimeColor = (255, 112, 204, 137);
    public static readonly (byte A, byte R, byte G, byte B) BehindGainingTimeColor = (255, 204, 120, 112);
    public static readonly (byte A, byte R, byte G, byte B) BehindLosingTimeColor = (255, 204, 55, 41);
    public static readonly (byte A, byte R, byte G, byte B) BestSegmentColor = (255, 216, 175, 31);
    public static readonly (byte A, byte R, byte G, byte B) NotRunningColor = (255, 122, 122, 122);
    public static readonly (byte A, byte R, byte G, byte B) PausedColor = (255, 122, 122, 122);
    public static readonly (byte A, byte R, byte G, byte B) WindowBackground = (255, 0, 0, 0);

    public static string ToHex((byte A, byte R, byte G, byte B) c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
}
