using System.Globalization;
using Avalonia;
using Avalonia.Media;
using LiveSplitNative.ComponentHost.Compat;
using DrawingColor = System.Drawing.Color;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSize = System.Drawing.Size;
using DrawingSizeF = System.Drawing.SizeF;

namespace LiveSplitNative.App;

/// <summary>
/// Where a component DLL draws (its System.Drawing.Graphics): the timer's DrawingContext, at the
/// component's place in the layout. Text is laid out as GDI+ DrawString does it (alignment in the
/// layout box, no wrapping, an ellipsis when cut, clipped to the box).
/// </summary>
internal sealed class AvaloniaHostCanvas : IHostCanvas
{
    private readonly DrawingContext _context;
    private readonly Stack<DrawingContext.PushedState> _clips = new();

    public AvaloniaHostCanvas(DrawingContext context, bool antiAliasText)
    {
        _context = context;
        AntiAliasText = antiAliasText;
    }

    public float DpiY => 96f;

    public bool AntiAliasText { get; }

    public void Fill(HostBrush brush, DrawingRectangleF rect)
    {
        if (rect.Width > 0 && rect.Height > 0)
        {
            _context.FillRectangle(ToBrush(brush), ToRect(rect));
        }
    }

    public void FillPolygon(HostBrush brush, IReadOnlyList<DrawingPointF> points)
    {
        if (points.Count < 3)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (StreamGeometryContext g = geometry.Open())
        {
            g.BeginFigure(new Point(points[0].X, points[0].Y), true);
            for (int i = 1; i < points.Count; i++)
            {
                g.LineTo(new Point(points[i].X, points[i].Y));
            }

            g.EndFigure(true);
        }

        _context.DrawGeometry(ToBrush(brush), null, geometry);
    }

    public void DrawLine(DrawingColor color, float width, DrawingPointF from, DrawingPointF to) =>
        _context.DrawLine(new Pen(new SolidColorBrush(ToColor(color)), width), new Point(from.X, from.Y), new Point(to.X, to.Y));

    public void DrawRectangle(DrawingColor color, float width, DrawingRectangleF rect) =>
        _context.DrawRectangle(null, new Pen(new SolidColorBrush(ToColor(color)), width), ToRect(rect));

    public void DrawString(string text, HostFont font, HostBrush brush, DrawingRectangleF layout, HostStringFormat format)
    {
        if (Place(text, font, ToBrush(brush), layout, format) is { } placed)
        {
            using (_context.PushClip(Clip(layout)))
            {
                _context.DrawText(placed.Text, placed.Origin);
            }
        }
    }

    public void DrawStringPath(string text, HostFont font, DrawingRectangleF layout, HostStringFormat format, HostBrush? fill, DrawingColor outline, float outlineWidth)
    {
        IBrush? fillBrush = fill is { } f ? ToBrush(f) : null;
        if (Place(text, font, fillBrush ?? Brushes.Black, layout, format) is not { } placed
            || placed.Text.BuildGeometry(placed.Origin) is not { } geometry)
        {
            return;
        }

        Pen? pen = outline.A > 0 && outlineWidth > 0
            ? new Pen(new SolidColorBrush(ToColor(outline)), outlineWidth, lineJoin: PenLineJoin.Round)
            : null;
        using (_context.PushClip(Clip(layout)))
        {
            _context.DrawGeometry(fillBrush, pen, geometry);
        }
    }

    public DrawingSizeF MeasureString(string text, HostFont font, float maxWidth, HostStringFormat format)
    {
        FormattedText formatted = Format(string.IsNullOrEmpty(text) ? " " : text, font, Brushes.Black);
        double width = string.IsNullOrEmpty(text) ? 0 : formatted.WidthIncludingTrailingWhitespace;
        return new DrawingSizeF((float)Math.Min(width, maxWidth), (float)formatted.Height);
    }

    public DrawingSize MeasureText(string text, HostFont font)
    {
        if (string.IsNullOrEmpty(text))
        {
            return DrawingSize.Empty;
        }

        FormattedText formatted = Format(text, font, Brushes.Black);
        return new DrawingSize((int)Math.Ceiling(formatted.WidthIncludingTrailingWhitespace), (int)Math.Ceiling(formatted.Height));
    }

    public void PushClip(DrawingRectangleF rect) => _clips.Push(_context.PushClip(Clip(rect)));

    public void PopClip()
    {
        if (_clips.Count > 0)
        {
            _clips.Pop().Dispose();
        }
    }

    public void Dispose()
    {
        while (_clips.Count > 0)
        {
            _clips.Pop().Dispose();
        }
    }

    /// <summary>
    /// FontFamily.GetCellAscent / GetCellDescent / GetEmHeight of the face the app draws for this
    /// family: OS/2 winAscent and winDescent and head unitsPerEm, which GDI+ reports; the face's
    /// own ascent and descent when it has no OS/2 table.
    /// </summary>
    public static (int Ascent, int Descent, int EmHeight) CellMetrics(string family, bool bold, bool italic)
    {
        var typeface = new Typeface(
            new FontFamily(family.Contains(',') ? family : AppFonts.ForTextFont(family)),
            italic ? FontStyle.Italic : FontStyle.Normal,
            bold ? FontWeight.Bold : FontWeight.Normal);
        if (!FontManager.Current.TryGetGlyphTypeface(typeface, out IGlyphTypeface? glyph) || glyph is null)
        {
            return (1854, 434, 2048);
        }

        if (glyph.TryGetTable(0x4F532F32 /* OS/2 */, out byte[] os2)
            && glyph.TryGetTable(0x68656164 /* head */, out byte[] head)
            && os2.Length >= 78
            && head.Length >= 20)
        {
            int unitsPerEm = (head[18] << 8) | head[19];
            int winAscent = (os2[74] << 8) | os2[75];
            int winDescent = (os2[76] << 8) | os2[77];
            if (unitsPerEm > 0 && winAscent > 0)
            {
                return (winAscent, winDescent, unitsPerEm);
            }
        }

        int em = Math.Max(1, (int)glyph.Metrics.DesignEmHeight);
        return (Math.Abs(glyph.Metrics.Ascent), Math.Abs(glyph.Metrics.Descent), em);
    }

    /// <summary>The text in the app's font for the font's family (the layout's Windows names map to the bundled faces).</summary>
    public static FormattedText Format(string text, HostFont font, IBrush brush) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                new FontFamily(font.FamilyName.Contains(',') ? font.FamilyName : AppFonts.ForTextFont(font.FamilyName)),
                font.Italic ? FontStyle.Italic : FontStyle.Normal,
                font.Bold ? FontWeight.Bold : FontWeight.Normal),
            Math.Max(1, font.SizeInPixels),
            brush);

    /// <summary>StringFormat: horizontal Alignment and LineAlignment in the box; NoWrap with an ellipsis when cut.</summary>
    private static (FormattedText Text, Point Origin)? Place(string text, HostFont font, IBrush brush, DrawingRectangleF layout, HostStringFormat format)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        FormattedText formatted = Format(text, font, brush);
        bool bounded = layout.Width > 0 && layout.Width < float.MaxValue / 8;
        if (bounded && format.EllipsisCharacter && formatted.WidthIncludingTrailingWhitespace > layout.Width)
        {
            formatted.MaxTextWidth = layout.Width;
            formatted.Trimming = TextTrimming.CharacterEllipsis;
            formatted.MaxLineCount = 1;
        }

        double width = formatted.WidthIncludingTrailingWhitespace;
        double x = !bounded ? layout.X : format.Alignment switch
        {
            1 => layout.X + ((layout.Width - width) / 2),
            2 => layout.X + layout.Width - width,
            _ => layout.X,
        };
        bool boundedHeight = layout.Height > 0 && layout.Height < float.MaxValue / 8;
        double y = !boundedHeight ? layout.Y : format.LineAlignment switch
        {
            1 => layout.Y + ((layout.Height - formatted.Height) / 2),
            2 => layout.Y + layout.Height - formatted.Height,
            _ => layout.Y,
        };
        return (formatted, new Point(x, y));
    }

    private static Rect Clip(DrawingRectangleF layout) =>
        new(
            layout.X,
            layout.Y,
            layout.Width > 0 && layout.Width < float.MaxValue / 8 ? layout.Width : 100000,
            layout.Height > 0 && layout.Height < float.MaxValue / 8 ? layout.Height : 100000);

    private static IBrush ToBrush(HostBrush brush)
    {
        if (!brush.IsGradient)
        {
            return new SolidColorBrush(ToColor(brush.Color1));
        }

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(brush.Start.X, brush.Start.Y, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(brush.End.X, brush.End.Y, RelativeUnit.Absolute),
            GradientStops =
            {
                new GradientStop(ToColor(brush.Color1), 0),
                new GradientStop(ToColor(brush.Color2), 1),
            },
        };
    }

    private static Color ToColor(DrawingColor color) => Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B);

    private static Rect ToRect(DrawingRectangleF rect) => new(rect.X, rect.Y, rect.Width, rect.Height);
}
