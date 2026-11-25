using LiveSplitNative.Core.Appearance;

namespace LiveSplitNative.Core.UI;

/// <summary>Port of LiveSplit.Options.LayoutSettings (portable fields; fonts as family/size).</summary>
public sealed class LayoutSettings
{
    public ArgbColor TextColor { get; set; }
    public ArgbColor BackgroundColor { get; set; }
    public ArgbColor BackgroundColor2 { get; set; }
    public ArgbColor ThinSeparatorsColor { get; set; }
    public ArgbColor SeparatorsColor { get; set; }
    public ArgbColor PersonalBestColor { get; set; }
    public ArgbColor AheadGainingTimeColor { get; set; }
    public ArgbColor AheadLosingTimeColor { get; set; }
    public ArgbColor BehindGainingTimeColor { get; set; }
    public ArgbColor BehindLosingTimeColor { get; set; }
    public ArgbColor BestSegmentColor { get; set; }
    public ArgbColor NotRunningColor { get; set; }
    public ArgbColor PausedColor { get; set; }
    public ArgbColor TextOutlineColor { get; set; }
    public ArgbColor ShadowsColor { get; set; }

    public string TimerFontFamily { get; set; } = DefaultLayoutAppearance.TimerFontFamily;
    public float TimerFontSizePx { get; set; } = DefaultLayoutAppearance.TimerFontSizePx;
    /// <summary>Windows default TimerFont is Century Gothic Bold.</summary>
    public bool TimerFontBold { get; set; } = true;
    public bool TimerFontItalic { get; set; }
    public bool TimesFontItalic { get; set; }
    public bool TextFontItalic { get; set; }
    public string TimesFontFamily { get; set; } = "Segoe UI";

    /// <summary>
    /// LiveSplitNative only (Basten 18 Oct; Windows has no such setting and ignores it in a
    /// layout): the space added to a 25 high single-line component (Previous Segment, Sum of
    /// Best, Possible Time Save, Text), like Split Height for split rows. 6 is Windows' 31 high.
    /// </summary>
    public float ComponentSpacing { get; set; } = DefaultComponentSpacing;

    public const float DefaultComponentSpacing = 6f;
    public float TimesFontSizePx { get; set; } = 16f;
    /// <summary>Windows default TimesFont is Segoe UI Bold.</summary>
    public bool TimesFontBold { get; set; } = true;
    public string TextFontFamily { get; set; } = "Segoe UI";
    public float TextFontSizePx { get; set; } = 16f;
    /// <summary>Windows default TextFont is Segoe UI Regular.</summary>
    public bool TextFontBold { get; set; }

    /// <summary>Opaque BinaryFormatter font blobs from .lsl when present (round-trip).</summary>
    public string? TimerFontXml { get; set; }
    public string? TimesFontXml { get; set; }
    public string? TextFontXml { get; set; }

    public bool ShowBestSegments { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool AntiAliasing { get; set; } = true;
    public bool DropShadows { get; set; } = true;
    public bool UseRainbowColor { get; set; }
    public float Opacity { get; set; } = 1f;
    public bool MousePassThroughWhileRunning { get; set; }
    public bool AllowResizing { get; set; } = true;
    public bool AllowMoving { get; set; } = true;
    /// <summary>"SolidColor", "VerticalGradient", "HorizontalGradient" or "Image" (BackgroundType).</summary>
    public string BackgroundType { get; set; } = "SolidColor";

    /// <summary>
    /// LiveSplitNative: the colors' type kept while <see cref="BackgroundType"/> is Image
    /// ("SolidColor", "VerticalGradient" or "HorizontalGradient"), for when the image is turned
    /// off again. Windows reads Image and skips this.
    /// </summary>
    public string BackgroundColorType { get; set; } = "SolidColor";

    /// <summary>The colors' type: the background type, or the colors under the image.</summary>
    public string ColorLayerType => string.Equals(BackgroundType, "Image", StringComparison.OrdinalIgnoreCase) ? BackgroundColorType : BackgroundType;

    /// <summary>
    /// The background image file (PNG, JPEG, GIF or BMP bytes), kept when another background
    /// type is chosen, as Windows keeps BackgroundImage. Null when there is none.
    /// </summary>
    public byte[]? BackgroundImage { get; set; }

    /// <summary>The BackgroundImage element as it was read, so an unchanged image keeps its bytes.</summary>
    public string? BackgroundImageXml { get; set; }

    /// <summary>ImageOpacity, 0.05-1 (Windows' trackbar 5-100).</summary>
    public float ImageOpacity { get; set; } = 1f;

    /// <summary>ImageBlur, 0-1 (Windows' trackbar 0-100); the blur's sigma is ImageBlur x 10.</summary>
    public float ImageBlur { get; set; }

    /// <summary>Elements of the layout's &lt;Settings&gt; that are not LiveSplit's, kept as they were.</summary>
    public List<string> UnknownOuterXml { get; private init; } = [];

    public static LayoutSettings CreateStandard() => new()
    {
        TextColor = ArgbColor.FromTuple(DefaultLayoutAppearance.TextColor),
        BackgroundColor = ArgbColor.FromArgb(255, 15, 15, 15),
        BackgroundColor2 = ArgbColor.FromArgb(0, 0, 0, 0),
        ThinSeparatorsColor = ArgbColor.FromArgb(3, 255, 255, 255),
        SeparatorsColor = ArgbColor.FromArgb(36, 255, 255, 255),
        PersonalBestColor = ArgbColor.FromTuple(DefaultLayoutAppearance.PersonalBestColor),
        AheadGainingTimeColor = ArgbColor.FromTuple(DefaultLayoutAppearance.AheadGainingTimeColor),
        AheadLosingTimeColor = ArgbColor.FromTuple(DefaultLayoutAppearance.AheadLosingTimeColor),
        BehindGainingTimeColor = ArgbColor.FromTuple(DefaultLayoutAppearance.BehindGainingTimeColor),
        BehindLosingTimeColor = ArgbColor.FromTuple(DefaultLayoutAppearance.BehindLosingTimeColor),
        BestSegmentColor = ArgbColor.FromTuple(DefaultLayoutAppearance.BestSegmentColor),
        NotRunningColor = ArgbColor.FromTuple(DefaultLayoutAppearance.NotRunningColor),
        PausedColor = ArgbColor.FromTuple(DefaultLayoutAppearance.PausedColor),
        TextOutlineColor = ArgbColor.FromArgb(0, 0, 0, 0),
        ShadowsColor = ArgbColor.FromArgb(128, 0, 0, 0),
        AlwaysOnTop = true,
        AllowResizing = true,
        Opacity = 1f,
    };

    public LayoutSettings Clone() => new()
    {
        TextColor = TextColor,
        BackgroundColor = BackgroundColor,
        BackgroundColor2 = BackgroundColor2,
        ThinSeparatorsColor = ThinSeparatorsColor,
        SeparatorsColor = SeparatorsColor,
        PersonalBestColor = PersonalBestColor,
        AheadGainingTimeColor = AheadGainingTimeColor,
        AheadLosingTimeColor = AheadLosingTimeColor,
        BehindGainingTimeColor = BehindGainingTimeColor,
        BehindLosingTimeColor = BehindLosingTimeColor,
        BestSegmentColor = BestSegmentColor,
        NotRunningColor = NotRunningColor,
        PausedColor = PausedColor,
        TextOutlineColor = TextOutlineColor,
        ShadowsColor = ShadowsColor,
        TimerFontFamily = TimerFontFamily,
        TimerFontSizePx = TimerFontSizePx,
        TimerFontBold = TimerFontBold,
        TimerFontItalic = TimerFontItalic,
        TimesFontItalic = TimesFontItalic,
        TextFontItalic = TextFontItalic,
        TimesFontFamily = TimesFontFamily,
        TimesFontSizePx = TimesFontSizePx,
        TimesFontBold = TimesFontBold,
        TextFontFamily = TextFontFamily,
        TextFontSizePx = TextFontSizePx,
        TextFontBold = TextFontBold,
        TimerFontXml = TimerFontXml,
        TimesFontXml = TimesFontXml,
        TextFontXml = TextFontXml,
        ShowBestSegments = ShowBestSegments,
        AlwaysOnTop = AlwaysOnTop,
        AntiAliasing = AntiAliasing,
        DropShadows = DropShadows,
        UseRainbowColor = UseRainbowColor,
        Opacity = Opacity,
        MousePassThroughWhileRunning = MousePassThroughWhileRunning,
        AllowResizing = AllowResizing,
        AllowMoving = AllowMoving,
        BackgroundType = BackgroundType,
        BackgroundColorType = BackgroundColorType,
        BackgroundImage = BackgroundImage,
        BackgroundImageXml = BackgroundImageXml,
        ImageOpacity = ImageOpacity,
        ImageBlur = ImageBlur,
        ComponentSpacing = ComponentSpacing,
        UnknownOuterXml = [.. UnknownOuterXml],
    };

    public void Assign(LayoutSettings other)
    {
        TextColor = other.TextColor;
        BackgroundColor = other.BackgroundColor;
        BackgroundColor2 = other.BackgroundColor2;
        ThinSeparatorsColor = other.ThinSeparatorsColor;
        SeparatorsColor = other.SeparatorsColor;
        PersonalBestColor = other.PersonalBestColor;
        AheadGainingTimeColor = other.AheadGainingTimeColor;
        AheadLosingTimeColor = other.AheadLosingTimeColor;
        BehindGainingTimeColor = other.BehindGainingTimeColor;
        BehindLosingTimeColor = other.BehindLosingTimeColor;
        BestSegmentColor = other.BestSegmentColor;
        NotRunningColor = other.NotRunningColor;
        PausedColor = other.PausedColor;
        TextOutlineColor = other.TextOutlineColor;
        ShadowsColor = other.ShadowsColor;
        TimerFontFamily = other.TimerFontFamily;
        TimerFontSizePx = other.TimerFontSizePx;
        TimerFontBold = other.TimerFontBold;
        TimerFontItalic = other.TimerFontItalic;
        TimesFontItalic = other.TimesFontItalic;
        TextFontItalic = other.TextFontItalic;
        TimesFontFamily = other.TimesFontFamily;
        TimesFontSizePx = other.TimesFontSizePx;
        TimesFontBold = other.TimesFontBold;
        TextFontFamily = other.TextFontFamily;
        TextFontSizePx = other.TextFontSizePx;
        TextFontBold = other.TextFontBold;
        TimerFontXml = other.TimerFontXml;
        TimesFontXml = other.TimesFontXml;
        TextFontXml = other.TextFontXml;
        ShowBestSegments = other.ShowBestSegments;
        AlwaysOnTop = other.AlwaysOnTop;
        AntiAliasing = other.AntiAliasing;
        DropShadows = other.DropShadows;
        UseRainbowColor = other.UseRainbowColor;
        Opacity = other.Opacity;
        MousePassThroughWhileRunning = other.MousePassThroughWhileRunning;
        AllowResizing = other.AllowResizing;
        AllowMoving = other.AllowMoving;
        BackgroundType = other.BackgroundType;
        BackgroundColorType = other.BackgroundColorType;
        BackgroundImage = other.BackgroundImage;
        BackgroundImageXml = other.BackgroundImageXml;
        ImageOpacity = other.ImageOpacity;
        ImageBlur = other.ImageBlur;
        ComponentSpacing = other.ComponentSpacing;
        List<string> unknown = [.. other.UnknownOuterXml];
        UnknownOuterXml.Clear();
        UnknownOuterXml.AddRange(unknown);
    }
}
