// System.Drawing.Font and FontFamily: a description of a font; the app draws and measures it.
using System.Drawing.Text;
using LiveSplitNative.ComponentHost.Compat;

namespace System.Drawing
{
    public sealed class FontFamily : MarshalByRefObject, IDisposable
    {
        public FontFamily(string name) => Name = name ?? "";

        public FontFamily(GenericFontFamilies genericFamily) => Name = genericFamily switch
        {
            GenericFontFamilies.Serif => "Times New Roman",
            GenericFontFamilies.Monospace => "Courier New",
            _ => "Microsoft Sans Serif",
        };

        public string Name { get; }

        public static FontFamily GenericSansSerif => new(GenericFontFamilies.SansSerif);

        public static FontFamily GenericSerif => new(GenericFontFamilies.Serif);

        public static FontFamily GenericMonospace => new(GenericFontFamilies.Monospace);

        public static FontFamily[] Families => [];

        public bool IsStyleAvailable(FontStyle style) => true;

        /// <summary>FontFamily.GetCellAscent: the face's ascent in design units (OS/2 winAscent), as the app has the face.</summary>
        public int GetCellAscent(FontStyle style) => Metrics(style).Ascent;

        /// <summary>FontFamily.GetCellDescent: the face's descent in design units (OS/2 winDescent).</summary>
        public int GetCellDescent(FontStyle style) => Metrics(style).Descent;

        /// <summary>FontFamily.GetEmHeight: the face's units per em.</summary>
        public int GetEmHeight(FontStyle style) => Metrics(style).EmHeight;

        /// <summary>FontFamily.GetLineSpacing: ascent and descent (GDI+ adds the line gap, which these faces mostly lack).</summary>
        public int GetLineSpacing(FontStyle style)
        {
            (int ascent, int descent, _) = Metrics(style);
            return ascent + descent;
        }

        private (int Ascent, int Descent, int EmHeight) Metrics(FontStyle style) =>
            HostText.CellMetrics(Name, (style & FontStyle.Bold) != 0, (style & FontStyle.Italic) != 0);

        public void Dispose()
        {
        }

        public override bool Equals(object obj) => obj is FontFamily other && other.Name == Name;

        public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);

        public override string ToString() => $"[FontFamily: Name={Name}]";
    }

    public sealed class Font : MarshalByRefObject, ICloneable, IDisposable
    {
        public Font(string familyName, float emSize)
            : this(new FontFamily(familyName), emSize, FontStyle.Regular, GraphicsUnit.Point)
        {
        }

        public Font(string familyName, float emSize, FontStyle style)
            : this(new FontFamily(familyName), emSize, style, GraphicsUnit.Point)
        {
        }

        public Font(string familyName, float emSize, GraphicsUnit unit)
            : this(new FontFamily(familyName), emSize, FontStyle.Regular, unit)
        {
        }

        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
            : this(new FontFamily(familyName), emSize, style, unit)
        {
        }

        public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
            : this(new FontFamily(familyName), emSize, style, unit)
        {
        }

        public Font(FontFamily family, float emSize)
            : this(family, emSize, FontStyle.Regular, GraphicsUnit.Point)
        {
        }

        public Font(FontFamily family, float emSize, FontStyle style)
            : this(family, emSize, style, GraphicsUnit.Point)
        {
        }

        public Font(FontFamily family, float emSize, GraphicsUnit unit)
            : this(family, emSize, FontStyle.Regular, unit)
        {
        }

        public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit)
        {
            if (emSize <= 0 || float.IsInfinity(emSize) || float.IsNaN(emSize))
            {
                throw new ArgumentException("Value of 'emSize' is not valid.", nameof(emSize));
            }

            FontFamily = family ?? throw new ArgumentNullException(nameof(family));
            Size = emSize;
            Style = style;
            Unit = unit;
        }

        public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
            : this(family, emSize, style, unit)
        {
        }

        public Font(Font prototype, FontStyle newStyle)
            : this(prototype.FontFamily, prototype.Size, newStyle, prototype.Unit)
        {
        }

        public FontFamily FontFamily { get; }

        public string Name => FontFamily.Name;

        public string OriginalFontName => Name;

        public float Size { get; }

        public FontStyle Style { get; }

        public GraphicsUnit Unit { get; }

        public bool Bold => (Style & FontStyle.Bold) != 0;

        public bool Italic => (Style & FontStyle.Italic) != 0;

        public bool Underline => (Style & FontStyle.Underline) != 0;

        public bool Strikeout => (Style & FontStyle.Strikeout) != 0;

        public byte GdiCharSet => 1;

        public bool GdiVerticalFont => false;

        public bool IsSystemFont => false;

        public string SystemFontName => "";

        /// <summary>The size in points.</summary>
        public float SizeInPoints => Unit switch
        {
            GraphicsUnit.Point => Size,
            GraphicsUnit.Inch => Size * 72f,
            GraphicsUnit.Document => Size * 72f / 300f,
            GraphicsUnit.Millimeter => Size * 72f / 25.4f,
            _ => Size * 72f / 96f,
        };

        /// <summary>The line spacing in pixels, rounded up (Font.Height).</summary>
        public int Height => (int)Math.Ceiling(GetHeight());

        public float GetHeight() => HostText.Measure("A", ToHost()).Height;

        public float GetHeight(float dpi) => GetHeight() * dpi / 96f;

        public float GetHeight(Graphics graphics) => GetHeight();

        public object Clone() => new Font(FontFamily, Size, Style, Unit);

        public void Dispose()
        {
        }

        public override bool Equals(object obj) =>
            obj is Font other && other.Name == Name && other.Size == Size && other.Style == Style && other.Unit == Unit;

        public override int GetHashCode() => HashCode.Combine(Name, Size, Style, Unit);

        public override string ToString() => $"[Font: Name={Name}, Size={Size}, Units={(int)Unit}, GdiCharSet=1, GdiVerticalFont=False]";

        internal HostFont ToHost(float scale = 1f) => new(
            Name,
            Unit == GraphicsUnit.Point ? Size * scale : SizeInPoints * 96f / 72f * scale,
            Unit != GraphicsUnit.Point,
            Bold,
            Italic,
            Underline,
            Strikeout);
    }
}

namespace System.Drawing.Text
{
    public enum GenericFontFamilies
    {
        Serif = 0,
        SansSerif = 1,
        Monospace = 2,
    }

    public abstract class FontCollection : IDisposable
    {
        public FontFamily[] Families => [];

        public void Dispose()
        {
        }
    }

    public sealed class InstalledFontCollection : FontCollection
    {
    }

    public sealed class PrivateFontCollection : FontCollection
    {
        public void AddFontFile(string filename)
        {
        }
    }
}
