// Brushes, pens, string formats, regions and images of System.Drawing, as descriptions the
// app's renderer draws.
using System.Drawing.Drawing2D;
using LiveSplitNative.ComponentHost.Compat;

namespace System.Drawing
{
    public abstract class Brush : MarshalByRefObject, ICloneable, IDisposable
    {
        public abstract object Clone();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        internal abstract HostBrush ToHost(Func<PointF, PointF> transform);
    }

    public sealed class SolidBrush : Brush
    {
        public SolidBrush(Color color) => Color = color;

        public Color Color { get; set; }

        public override object Clone() => new SolidBrush(Color);

        internal override HostBrush ToHost(Func<PointF, PointF> transform) => HostBrush.Solid(Color);
    }

    public static class Brushes
    {
        public static Brush Black => new SolidBrush(Color.Black);

        public static Brush White => new SolidBrush(Color.White);

        public static Brush Transparent => new SolidBrush(Color.Transparent);

        public static Brush Red => new SolidBrush(Color.Red);

        public static Brush Gray => new SolidBrush(Color.Gray);
    }

    public sealed class Pen : MarshalByRefObject, ICloneable, IDisposable
    {
        public Pen(Color color)
            : this(color, 1f)
        {
        }

        public Pen(Color color, float width)
        {
            Color = color;
            Width = width;
        }

        public Pen(Brush brush)
            : this(brush, 1f)
        {
        }

        public Pen(Brush brush, float width)
            : this(brush is SolidBrush solid ? solid.Color : Color.Black, width)
        {
            Brush = brush;
        }

        public Color Color { get; set; }

        public float Width { get; set; }

        public Brush Brush { get; set; }

        public LineJoin LineJoin { get; set; }

        /// <summary>The line's ends (drawn as the app draws lines: Graph's round caps look flat).</summary>
        public LineCap StartCap { get; set; }

        public LineCap EndCap { get; set; }

        public object Clone() => new Pen(Color, Width) { LineJoin = LineJoin, StartCap = StartCap, EndCap = EndCap };

        public void Dispose()
        {
        }
    }

    public static class Pens
    {
        public static Pen Black => new(Color.Black);

        public static Pen White => new(Color.White);
    }

    public sealed class StringFormat : MarshalByRefObject, ICloneable, IDisposable
    {
        public StringFormat()
        {
        }

        public StringFormat(StringFormatFlags options) => FormatFlags = options;

        public StringFormat(StringFormat format)
        {
            Alignment = format.Alignment;
            LineAlignment = format.LineAlignment;
            FormatFlags = format.FormatFlags;
            Trimming = format.Trimming;
        }

        public StringAlignment Alignment { get; set; }

        public StringAlignment LineAlignment { get; set; }

        public StringFormatFlags FormatFlags { get; set; }

        public StringTrimming Trimming { get; set; } = StringTrimming.Character;

        public static StringFormat GenericDefault => new();

        public static StringFormat GenericTypographic => new(StringFormatFlags.FitBlackBox | StringFormatFlags.LineLimit | StringFormatFlags.NoClip)
        {
            Trimming = StringTrimming.None,
        };

        public object Clone() => new StringFormat(this);

        public void Dispose()
        {
        }

        internal HostStringFormat ToHost() => new(
            (int)Alignment,
            (int)LineAlignment,
            (FormatFlags & StringFormatFlags.NoWrap) != 0,
            Trimming == StringTrimming.EllipsisCharacter || Trimming == StringTrimming.EllipsisWord);
    }

    public sealed class Region : MarshalByRefObject, IDisposable
    {
        public Region()
            : this(new RectangleF(-4194304, -4194304, 8388608, 8388608))
        {
        }

        public Region(RectangleF rect) => Bounds = rect;

        public Region(Rectangle rect)
            : this((RectangleF)rect)
        {
        }

        internal RectangleF Bounds { get; private set; }

        public RectangleF GetBounds(Graphics g) => Bounds;

        public bool IsVisible(RectangleF rect) => Bounds.IntersectsWith(rect);

        public bool IsVisible(Rectangle rect) => Bounds.IntersectsWith(rect);

        public bool IsVisible(PointF point) => Bounds.Contains(point);

        public void Intersect(RectangleF rect) => Bounds = RectangleF.Intersect(Bounds, rect);

        public void Intersect(Rectangle rect) => Intersect((RectangleF)rect);

        public void MakeInfinite() => Bounds = new RectangleF(-4194304, -4194304, 8388608, 8388608);

        public Region Clone() => new(Bounds);

        public void Dispose()
        {
        }
    }

    public abstract class Image : MarshalByRefObject, ICloneable, IDisposable
    {
        public abstract int Width { get; }

        public abstract int Height { get; }

        public Size Size => new(Width, Height);

        public abstract object Clone();

        public void Dispose()
        {
        }

        /// <summary>Image.FrameDimensionsList: one dimension, time (images here have one frame).</summary>
        public Guid[] FrameDimensionsList => [Imaging.FrameDimension.Time.Guid];

        /// <summary>Image.GetFrameCount: one (an animated GIF shows its first frame).</summary>
        public int GetFrameCount(Imaging.FrameDimension dimension) => 1;

        public int SelectActiveFrame(Imaging.FrameDimension dimension, int frameIndex) => 0;

        public static Image FromStream(IO.Stream stream) => throw new NotSupportedException("Images are not supported for components yet.");

        public static Image FromFile(string filename) => throw new NotSupportedException("Images are not supported for components yet.");
    }

    public sealed class Bitmap : Image
    {
        private readonly int _width;
        private readonly int _height;

        public Bitmap(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public Bitmap(Image original)
            : this(original.Width, original.Height)
        {
        }

        public Bitmap(Image original, int width, int height)
            : this(width, height)
        {
        }

        public override int Width => _width;

        public override int Height => _height;

        public override object Clone() => new Bitmap(_width, _height);
    }
}

namespace System.Drawing.Drawing2D
{
    public sealed class LinearGradientBrush : Brush
    {
        private readonly PointF _start;
        private readonly PointF _end;

        public LinearGradientBrush(PointF point1, PointF point2, Color color1, Color color2)
        {
            _start = point1;
            _end = point2;
            LinearColors = [color1, color2];
        }

        public LinearGradientBrush(Point point1, Point point2, Color color1, Color color2)
            : this((PointF)point1, (PointF)point2, color1, color2)
        {
        }

        public LinearGradientBrush(RectangleF rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
            : this(
                rect.Location,
                linearGradientMode switch
                {
                    LinearGradientMode.Vertical => new PointF(rect.Left, rect.Bottom),
                    LinearGradientMode.ForwardDiagonal => new PointF(rect.Right, rect.Bottom),
                    LinearGradientMode.BackwardDiagonal => new PointF(rect.Left, rect.Bottom),
                    _ => new PointF(rect.Right, rect.Top),
                },
                color1,
                color2)
        {
        }

        public LinearGradientBrush(Rectangle rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
            : this((RectangleF)rect, color1, color2, linearGradientMode)
        {
        }

        public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle)
            : this(
                rect.Location,
                new PointF(
                    rect.Left + (rect.Width * MathF.Cos(angle * MathF.PI / 180f)),
                    rect.Top + (rect.Height * MathF.Sin(angle * MathF.PI / 180f))),
                color1,
                color2)
        {
        }

        public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle)
            : this((RectangleF)rect, color1, color2, angle)
        {
        }

        public Color[] LinearColors { get; set; }

        public override object Clone() => new LinearGradientBrush(_start, _end, LinearColors[0], LinearColors[1]);

        internal override HostBrush ToHost(Func<PointF, PointF> transform) =>
            new(LinearColors[0], LinearColors[1], transform(_start), transform(_end), true);
    }

    public sealed class Matrix : MarshalByRefObject, IDisposable
    {
        public Matrix()
            : this(1, 0, 0, 1, 0, 0)
        {
        }

        public Matrix(float m11, float m12, float m21, float m22, float dx, float dy) =>
            Elements = [m11, m12, m21, m22, dx, dy];

        public float[] Elements { get; private set; }

        public float OffsetX => Elements[4];

        public float OffsetY => Elements[5];

        public bool IsIdentity => Elements is [1, 0, 0, 1, 0, 0];

        public bool IsInvertible => (Elements[0] * Elements[3]) - (Elements[1] * Elements[2]) != 0;

        public Matrix Clone() => new(Elements[0], Elements[1], Elements[2], Elements[3], Elements[4], Elements[5]);

        public void Reset() => Elements = [1, 0, 0, 1, 0, 0];

        public void Translate(float offsetX, float offsetY) => Translate(offsetX, offsetY, MatrixOrder.Prepend);

        public void Translate(float offsetX, float offsetY, MatrixOrder order) =>
            Multiply(new Matrix(1, 0, 0, 1, offsetX, offsetY), order);

        public void Scale(float scaleX, float scaleY) => Scale(scaleX, scaleY, MatrixOrder.Prepend);

        public void Scale(float scaleX, float scaleY, MatrixOrder order) =>
            Multiply(new Matrix(scaleX, 0, 0, scaleY, 0, 0), order);

        public void Multiply(Matrix matrix) => Multiply(matrix, MatrixOrder.Prepend);

        public void Multiply(Matrix matrix, MatrixOrder order)
        {
            float[] a = order == MatrixOrder.Prepend ? matrix.Elements : Elements;
            float[] b = order == MatrixOrder.Prepend ? Elements : matrix.Elements;
            Elements =
            [
                (a[0] * b[0]) + (a[1] * b[2]),
                (a[0] * b[1]) + (a[1] * b[3]),
                (a[2] * b[0]) + (a[3] * b[2]),
                (a[2] * b[1]) + (a[3] * b[3]),
                (a[4] * b[0]) + (a[5] * b[2]) + b[4],
                (a[4] * b[1]) + (a[5] * b[3]) + b[5],
            ];
        }

        public PointF Apply(PointF p) => new(
            (p.X * Elements[0]) + (p.Y * Elements[2]) + Elements[4],
            (p.X * Elements[1]) + (p.Y * Elements[3]) + Elements[5]);

        public void TransformPoints(PointF[] pts)
        {
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = Apply(pts[i]);
            }
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A path: the texts, rectangles and lines added to it, drawn by the app.</summary>
    public sealed class GraphicsPath : MarshalByRefObject, ICloneable, IDisposable
    {
        internal readonly List<PathText> Texts = [];
        internal readonly List<RectangleF> Rectangles = [];

        public GraphicsPath()
        {
        }

        public GraphicsPath(FillMode fillMode)
        {
        }

        public FillMode FillMode { get; set; }

        public void AddString(string s, FontFamily family, int style, float emSize, RectangleF layoutRect, StringFormat format) =>
            Texts.Add(new PathText(s, family, (FontStyle)style, emSize, layoutRect, format ?? new StringFormat()));

        public void AddString(string s, FontFamily family, int style, float emSize, Rectangle layoutRect, StringFormat format) =>
            AddString(s, family, style, emSize, (RectangleF)layoutRect, format);

        public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat format) =>
            AddString(s, family, style, emSize, new RectangleF(origin, new SizeF(float.MaxValue / 4, float.MaxValue / 4)), format);

        public void AddString(string s, FontFamily family, int style, float emSize, Point origin, StringFormat format) =>
            AddString(s, family, style, emSize, (PointF)origin, format);

        public void AddRectangle(RectangleF rect) => Rectangles.Add(rect);

        public void AddRectangle(Rectangle rect) => Rectangles.Add(rect);

        public void Reset()
        {
            Texts.Clear();
            Rectangles.Clear();
        }

        public object Clone()
        {
            var clone = new GraphicsPath();
            clone.Texts.AddRange(Texts);
            clone.Rectangles.AddRange(Rectangles);
            return clone;
        }

        public void Dispose()
        {
        }

        internal readonly record struct PathText(string Text, FontFamily Family, FontStyle Style, float EmSize, RectangleF Layout, StringFormat Format);
    }
}

namespace System.Drawing
{
    public sealed class Icon : MarshalByRefObject, ICloneable, IDisposable
    {
        public Icon(IO.Stream stream)
        {
        }

        public Icon(string fileName)
        {
        }

        public int Width => 32;

        public int Height => 32;

        public Size Size => new(Width, Height);

        public Bitmap ToBitmap() => new(Width, Height);

        public object Clone() => new Icon((string)null);

        public void Dispose()
        {
        }

        public static Icon ExtractAssociatedIcon(string filePath) => new(filePath);
    }
}

namespace System.Drawing.Imaging
{
    public sealed class FrameDimension
    {
        public FrameDimension(Guid guid) => Guid = guid;

        public Guid Guid { get; }

        public static FrameDimension Time { get; } = new(new Guid("6aedbd6d-3fb5-418a-83a6-7f45229dc872"));

        public static FrameDimension Resolution { get; } = new(new Guid("84236f7b-3bd3-428f-8dab-4ea1439ca315"));

        public static FrameDimension Page { get; } = new(new Guid("7462dc86-6180-4c7e-8e3f-ee7333a7a483"));

        public override bool Equals(object obj) => obj is FrameDimension other && other.Guid == Guid;

        public override int GetHashCode() => Guid.GetHashCode();
    }
}

namespace System.Drawing
{
    /// <summary>ImageAnimator: images here have one frame, so nothing animates.</summary>
    public static class ImageAnimator
    {
        public static bool CanAnimate(Image image) => false;

        public static void Animate(Image image, EventHandler onFrameChangedHandler)
        {
        }

        public static void StopAnimate(Image image, EventHandler onFrameChangedHandler)
        {
        }

        public static void UpdateFrames()
        {
        }

        public static void UpdateFrames(Image image)
        {
        }
    }
}
