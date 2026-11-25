// System.Drawing.Graphics over the app's renderer (IHostCanvas). Transforms are applied here
// (translation and scale, as LiveSplit components use them), so the canvas gets final
// coordinates.
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using LiveSplitNative.ComponentHost.Compat;

namespace System.Drawing
{
    public sealed class Graphics : MarshalByRefObject, IDeviceContext, IDisposable
    {
        private readonly IHostCanvas _canvas;
        private readonly Stack<GraphicsState> _saved = new();
        private int _clips;
        private Matrix _transform = new();
        private Region _clip;

        private Graphics(IHostCanvas canvas, RectangleF clip)
        {
            _canvas = canvas;
            _clip = new Region(clip);
        }

        /// <summary>A Graphics drawing into the app's canvas (for the app, not for components).</summary>
        public static Graphics FromHostCanvas(IHostCanvas canvas, RectangleF clip) => new(canvas, clip);

        public static Graphics FromImage(Image image) => throw new NotSupportedException("Drawing into images is not supported for components yet.");

        public static Graphics FromHwnd(IntPtr hwnd) => throw new NotSupportedException("Window handles do not exist on this platform.");

        public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.AntiAlias;

        public SmoothingMode SmoothingMode { get; set; }

        public InterpolationMode InterpolationMode { get; set; }

        public PixelOffsetMode PixelOffsetMode { get; set; }

        public CompositingQuality CompositingQuality { get; set; }

        public GraphicsUnit PageUnit { get; set; } = GraphicsUnit.Display;

        public float PageScale { get; set; } = 1f;

        public float DpiX => _canvas.DpiY;

        public float DpiY => _canvas.DpiY;

        public Matrix Transform
        {
            get => _transform.Clone();
            set => _transform = value?.Clone() ?? new Matrix();
        }

        public Region Clip
        {
            get => _clip.Clone();
            set
            {
                _clip = value?.Clone() ?? new Region();
                SetCanvasClip();
            }
        }

        public RectangleF ClipBounds => _clip.Bounds;

        public RectangleF VisibleClipBounds => _clip.Bounds;

        public bool IsClipEmpty => _clip.Bounds.Width <= 0 || _clip.Bounds.Height <= 0;

        public void TranslateTransform(float dx, float dy) => _transform.Translate(dx, dy);

        public void TranslateTransform(float dx, float dy, MatrixOrder order) => _transform.Translate(dx, dy, order);

        public void ScaleTransform(float sx, float sy) => _transform.Scale(sx, sy);

        public void ScaleTransform(float sx, float sy, MatrixOrder order) => _transform.Scale(sx, sy, order);

        public void MultiplyTransform(Matrix matrix) => _transform.Multiply(matrix);

        public void ResetTransform() => _transform = new Matrix();

        public void SetClip(RectangleF rect) => Clip = new Region(Map(rect));

        public void SetClip(Rectangle rect) => SetClip((RectangleF)rect);

        public void SetClip(Region region) => Clip = region;

        public void SetClip(Graphics g) => Clip = g.Clip;

        public void IntersectClip(RectangleF rect)
        {
            _clip.Intersect(Map(rect));
            SetCanvasClip();
        }

        public void IntersectClip(Rectangle rect) => IntersectClip((RectangleF)rect);

        public void ResetClip() => Clip = new Region();

        public GraphicsState Save()
        {
            var state = new GraphicsState(_transform.Clone(), _clip.Clone());
            _saved.Push(state);
            return state;
        }

        public void Restore(GraphicsState state)
        {
            while (_saved.Count > 0 && !ReferenceEquals(_saved.Pop(), state))
            {
            }

            _transform = state.Transform.Clone();
            Clip = state.Clip;
        }

        public void Clear(Color color)
        {
        }

        public void FillRectangle(Brush brush, float x, float y, float width, float height) =>
            _canvas.Fill(brush.ToHost(Map), Map(new RectangleF(x, y, width, height)));

        public void FillRectangle(Brush brush, RectangleF rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

        public void FillRectangle(Brush brush, Rectangle rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

        public void FillRectangle(Brush brush, int x, int y, int width, int height) => FillRectangle(brush, (float)x, y, width, height);

        public void FillRectangles(Brush brush, RectangleF[] rects)
        {
            foreach (RectangleF rect in rects)
            {
                FillRectangle(brush, rect);
            }
        }

        public void FillPolygon(Brush brush, PointF[] points) =>
            _canvas.FillPolygon(brush.ToHost(Map), points.Select(Map).ToArray());

        public void FillPolygon(Brush brush, Point[] points) => FillPolygon(brush, points.Select(p => (PointF)p).ToArray());

        public void FillEllipse(Brush brush, float x, float y, float width, float height)
        {
            const int steps = 32;
            var points = new PointF[steps];
            for (int i = 0; i < steps; i++)
            {
                float angle = i * 2 * MathF.PI / steps;
                points[i] = new PointF(x + (width / 2) + (width / 2 * MathF.Cos(angle)), y + (height / 2) + (height / 2 * MathF.Sin(angle)));
            }

            FillPolygon(brush, points);
        }

        public void FillEllipse(Brush brush, RectangleF rect) => FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);

        public void FillEllipse(Brush brush, Rectangle rect) => FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2) =>
            _canvas.DrawLine(pen.Color, pen.Width * Scale, Map(new PointF(x1, y1)), Map(new PointF(x2, y2)));

        public void DrawLine(Pen pen, PointF pt1, PointF pt2) => DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);

        public void DrawLine(Pen pen, Point pt1, Point pt2) => DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);

        public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) => DrawLine(pen, (float)x1, y1, x2, y2);

        public void DrawLines(Pen pen, PointF[] points)
        {
            for (int i = 1; i < points.Length; i++)
            {
                DrawLine(pen, points[i - 1], points[i]);
            }
        }

        public void DrawRectangle(Pen pen, float x, float y, float width, float height) =>
            _canvas.DrawRectangle(pen.Color, pen.Width * Scale, Map(new RectangleF(x, y, width, height)));

        public void DrawRectangle(Pen pen, Rectangle rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawRectangle(Pen pen, RectangleF rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

        public void DrawRectangle(Pen pen, int x, int y, int width, int height) => DrawRectangle(pen, (float)x, y, width, height);

        public void DrawImage(Image image, float x, float y, float width, float height)
        {
        }

        public void DrawImage(Image image, RectangleF rect)
        {
        }

        public void DrawImage(Image image, Rectangle rect)
        {
        }

        public void DrawImage(Image image, int x, int y, int width, int height)
        {
        }

        public void DrawImage(Image image, float x, float y)
        {
        }

        public void DrawImage(Image image, PointF point)
        {
        }

        public void DrawString(string s, Font font, Brush brush, float x, float y) =>
            DrawString(s, font, brush, new RectangleF(x, y, 0, 0), new StringFormat());

        public void DrawString(string s, Font font, Brush brush, PointF point) =>
            DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), new StringFormat());

        public void DrawString(string s, Font font, Brush brush, float x, float y, StringFormat format) =>
            DrawString(s, font, brush, new RectangleF(x, y, 0, 0), format);

        public void DrawString(string s, Font font, Brush brush, PointF point, StringFormat format) =>
            DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), format);

        public void DrawString(string s, Font font, Brush brush, RectangleF layoutRectangle) =>
            DrawString(s, font, brush, layoutRectangle, new StringFormat());

        public void DrawString(string s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat format)
        {
            if (string.IsNullOrEmpty(s))
            {
                return;
            }

            // A zero-size rectangle has no box: the text starts at its corner.
            RectangleF layout = layoutRectangle.Width == 0 && layoutRectangle.Height == 0
                ? new RectangleF(layoutRectangle.Location, new SizeF(float.MaxValue / 4, float.MaxValue / 4))
                : layoutRectangle;
            _canvas.DrawString(s, font.ToHost(Scale), brush.ToHost(Map), MapLayout(layout), (format ?? new StringFormat()).ToHost());
        }

        public SizeF MeasureString(string text, Font font) => MeasureString(text, font, 0, new StringFormat());

        public SizeF MeasureString(string text, Font font, int width) => MeasureString(text, font, width, new StringFormat());

        public SizeF MeasureString(string text, Font font, SizeF layoutArea) => MeasureString(text, font, (int)layoutArea.Width, new StringFormat());

        public SizeF MeasureString(string text, Font font, SizeF layoutArea, StringFormat stringFormat) =>
            MeasureString(text, font, (int)layoutArea.Width, stringFormat);

        public SizeF MeasureString(string text, Font font, PointF origin, StringFormat stringFormat) =>
            MeasureString(text, font, 0, stringFormat);

        public SizeF MeasureString(string text, Font font, int width, StringFormat format) =>
            _canvas.MeasureString(text ?? "", font.ToHost(), width <= 0 ? float.MaxValue : width, (format ?? new StringFormat()).ToHost());

        public void FillPath(Brush brush, GraphicsPath path)
        {
            foreach (GraphicsPath.PathText text in path.Texts)
            {
                _canvas.DrawStringPath(text.Text, PathFont(text), MapLayout(text.Layout), text.Format.ToHost(), brush.ToHost(Map), Color.Transparent, 0);
            }

            foreach (RectangleF rect in path.Rectangles)
            {
                FillRectangle(brush, rect);
            }
        }

        public void DrawPath(Pen pen, GraphicsPath path)
        {
            foreach (GraphicsPath.PathText text in path.Texts)
            {
                _canvas.DrawStringPath(text.Text, PathFont(text), MapLayout(text.Layout), text.Format.ToHost(), null, pen.Color, pen.Width * Scale);
            }

            foreach (RectangleF rect in path.Rectangles)
            {
                DrawRectangle(pen, rect);
            }
        }

        public IntPtr GetHdc() => throw new NotSupportedException("GDI device contexts do not exist on this platform.");

        public void ReleaseHdc(IntPtr hdc)
        {
        }

        public void ReleaseHdc()
        {
        }

        /// <summary>TextRenderer.MeasureText on this Graphics (for the WinForms look-alike).</summary>
        internal Size MeasureText(string text, Font font) => _canvas.MeasureText(text ?? "", font.ToHost());

        public void Flush()
        {
        }

        public void Dispose()
        {
            while (_clips > 0)
            {
                _canvas.PopClip();
                _clips--;
            }
        }

        private float Scale => MathF.Sqrt(MathF.Abs((_transform.Elements[0] * _transform.Elements[3]) - (_transform.Elements[1] * _transform.Elements[2])));

        private HostFont PathFont(GraphicsPath.PathText text) =>
            new(text.Family.Name, text.EmSize * Scale, true, (text.Style & FontStyle.Bold) != 0, (text.Style & FontStyle.Italic) != 0, false, false);

        private PointF Map(PointF point) => _transform.Apply(point);

        private RectangleF Map(RectangleF rect)
        {
            PointF a = Map(rect.Location);
            PointF b = Map(new PointF(rect.Right, rect.Bottom));
            return RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        private RectangleF MapLayout(RectangleF rect)
        {
            PointF origin = Map(rect.Location);
            return new RectangleF(origin, new SizeF(rect.Width * Scale, rect.Height * Scale));
        }

        private void SetCanvasClip()
        {
            while (_clips > 0)
            {
                _canvas.PopClip();
                _clips--;
            }

            _canvas.PushClip(_clip.Bounds);
            _clips++;
        }
    }
}

namespace System.Drawing.Drawing2D
{
    public sealed class GraphicsState : MarshalByRefObject
    {
        internal GraphicsState(Matrix transform, Region clip)
        {
            Transform = transform;
            Clip = clip;
        }

        internal Matrix Transform { get; }

        internal Region Clip { get; }
    }
}
