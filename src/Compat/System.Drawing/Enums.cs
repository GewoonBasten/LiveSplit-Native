// Enums of System.Drawing (values as .NET Framework's).
using System;

namespace System.Drawing
{
    [Flags]
    public enum FontStyle
    {
        Regular = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
        Strikeout = 8,
    }

    public enum GraphicsUnit
    {
        World = 0,
        Display = 1,
        Pixel = 2,
        Point = 3,
        Inch = 4,
        Document = 5,
        Millimeter = 6,
    }

    public enum StringAlignment
    {
        Near = 0,
        Center = 1,
        Far = 2,
    }

    [Flags]
    public enum StringFormatFlags
    {
        DirectionRightToLeft = 0x0001,
        DirectionVertical = 0x0002,
        FitBlackBox = 0x0004,
        DisplayFormatControl = 0x0020,
        NoFontFallback = 0x0400,
        MeasureTrailingSpaces = 0x0800,
        NoWrap = 0x1000,
        LineLimit = 0x2000,
        NoClip = 0x4000,
    }

    public enum StringTrimming
    {
        None = 0,
        Character = 1,
        Word = 2,
        EllipsisCharacter = 3,
        EllipsisWord = 4,
        EllipsisPath = 5,
    }

    public enum ContentAlignment
    {
        TopLeft = 0x001,
        TopCenter = 0x002,
        TopRight = 0x004,
        MiddleLeft = 0x010,
        MiddleCenter = 0x020,
        MiddleRight = 0x040,
        BottomLeft = 0x100,
        BottomCenter = 0x200,
        BottomRight = 0x400,
    }

    public enum RotateFlipType
    {
        RotateNoneFlipNone = 0,
    }
}

namespace System.Drawing.Drawing2D
{
    public enum LineCap
    {
        Flat = 0,
        Square = 1,
        Round = 2,
        Triangle = 3,
        NoAnchor = 16,
        SquareAnchor = 17,
        RoundAnchor = 18,
        DiamondAnchor = 19,
        ArrowAnchor = 20,
        Custom = 255,
        AnchorMask = 240,
    }

    public enum LineJoin
    {
        Miter = 0,
        Bevel = 1,
        Round = 2,
        MiterClipped = 3,
    }

    public enum SmoothingMode
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        None = 3,
        AntiAlias = 4,
    }

    public enum InterpolationMode
    {
        Invalid = -1,
        Default = 0,
        Low = 1,
        High = 2,
        Bilinear = 3,
        Bicubic = 4,
        NearestNeighbor = 5,
        HighQualityBilinear = 6,
        HighQualityBicubic = 7,
    }

    public enum PixelOffsetMode
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        None = 3,
        Half = 4,
    }

    public enum CompositingQuality
    {
        Invalid = -1,
        Default = 0,
        HighSpeed = 1,
        HighQuality = 2,
        GammaCorrected = 3,
        AssumeLinear = 4,
    }

    public enum LinearGradientMode
    {
        Horizontal = 0,
        Vertical = 1,
        ForwardDiagonal = 2,
        BackwardDiagonal = 3,
    }

    public enum MatrixOrder
    {
        Prepend = 0,
        Append = 1,
    }

    public enum FillMode
    {
        Alternate = 0,
        Winding = 1,
    }
}

namespace System.Drawing.Text
{
    public enum TextRenderingHint
    {
        SystemDefault = 0,
        SingleBitPerPixelGridFit = 1,
        SingleBitPerPixel = 2,
        AntiAliasGridFit = 3,
        AntiAlias = 4,
        ClearTypeGridFit = 5,
    }
}

namespace System.Drawing
{
    /// <summary>System.Drawing.IDeviceContext (Graphics is one).</summary>
    public interface IDeviceContext : IDisposable
    {
        IntPtr GetHdc();

        void ReleaseHdc();
    }
}
