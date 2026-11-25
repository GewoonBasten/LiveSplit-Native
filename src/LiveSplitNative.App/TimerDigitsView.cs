using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace LiveSplitNative.App;

/// <summary>
/// Windows LiveSplit Timer digit strip. Digits use fixed advances.
/// Big + fraction share one <b>painted ink bottom</b>: each size’s “0” is
/// rasterized via the same DrawText path, bottommost luminous pixel is read,
/// then <c>drawY = sharedInkFloor − inkBottom(glyph)</c>.
/// Do not trust FormattedText.Baseline / Height alone for Century Gothic Bold —
/// Avalonia Baseline-align (Windows ascent formula) floats fraction ink high.
/// </summary>
internal sealed class TimerDigitsView : Control
{
    public static readonly StyledProperty<string> BigTextProperty =
        AvaloniaProperty.Register<TimerDigitsView, string>(nameof(BigText), "0");

    public static readonly StyledProperty<string> SmallTextProperty =
        AvaloniaProperty.Register<TimerDigitsView, string>(nameof(SmallText), ".00");

    public static readonly StyledProperty<double> BigFontSizeProperty =
        AvaloniaProperty.Register<TimerDigitsView, double>(nameof(BigFontSize), 40);

    public static readonly StyledProperty<double> SmallFontSizeProperty =
        AvaloniaProperty.Register<TimerDigitsView, double>(nameof(SmallFontSize), 28);

    public static readonly StyledProperty<IBrush?> DigitBrushProperty =
        AvaloniaProperty.Register<TimerDigitsView, IBrush?>(nameof(DigitBrush));

    public static readonly StyledProperty<FontFamily> DigitFontFamilyProperty =
        AvaloniaProperty.Register<TimerDigitsView, FontFamily>(
            nameof(DigitFontFamily),
            new FontFamily(AppFonts.TimerFamily));

    public static readonly StyledProperty<FontWeight> DigitFontWeightProperty =
        AvaloniaProperty.Register<TimerDigitsView, FontWeight>(nameof(DigitFontWeight), FontWeight.Bold);

    public static readonly StyledProperty<FontStyle> DigitFontStyleProperty =
        AvaloniaProperty.Register<TimerDigitsView, FontStyle>(nameof(DigitFontStyle), FontStyle.Normal);

    private static readonly Dictionary<string, double> InkCache = new(StringComparer.Ordinal);

    /// <summary>Channel sum above this on black bg counts as solid ink (skips faint AA).</summary>
    private const int LumaThreshold = 280;

    static TimerDigitsView()
    {
        AffectsRender<TimerDigitsView>(
            BigTextProperty, SmallTextProperty, BigFontSizeProperty, SmallFontSizeProperty,
            DigitBrushProperty, DigitFontFamilyProperty, DigitFontWeightProperty, DigitFontStyleProperty);
        AffectsMeasure<TimerDigitsView>(
            BigTextProperty, SmallTextProperty, BigFontSizeProperty, SmallFontSizeProperty,
            DigitFontFamilyProperty, DigitFontWeightProperty, DigitFontStyleProperty);
    }

    public TimerDigitsView()
    {
        DigitFontFamily = new FontFamily(AppFonts.TimerFamily);
        DigitFontWeight = FontWeight.Bold;
        DigitBrush = new SolidColorBrush(Color.FromRgb(0x7A, 0x7A, 0x7A));
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    public string BigText
    {
        get => GetValue(BigTextProperty);
        set => SetValue(BigTextProperty, value);
    }

    public string SmallText
    {
        get => GetValue(SmallTextProperty);
        set => SetValue(SmallTextProperty, value);
    }

    public double BigFontSize
    {
        get => GetValue(BigFontSizeProperty);
        set => SetValue(BigFontSizeProperty, value);
    }

    public double SmallFontSize
    {
        get => GetValue(SmallFontSizeProperty);
        set => SetValue(SmallFontSizeProperty, value);
    }

    public IBrush? DigitBrush
    {
        get => GetValue(DigitBrushProperty);
        set => SetValue(DigitBrushProperty, value);
    }

    public IBrush? Foreground
    {
        get => DigitBrush;
        set => DigitBrush = value;
    }

    /// <summary>
    /// Timer Show Gradient: the digits' top and bottom colors (Timer.DrawUnscaled), drawn instead
    /// of <see cref="DigitBrush"/> by a paint that asks for it; null for plain digits.
    /// </summary>
    public (Color Top, Color Bottom)? DigitGradient
    {
        get => _digitGradient;
        set
        {
            _digitGradient = value;
            InvalidateVisual();
        }
    }

    private (Color Top, Color Bottom)? _digitGradient;
    private bool _paintGradient;

    public FontFamily DigitFontFamily
    {
        get => GetValue(DigitFontFamilyProperty);
        set => SetValue(DigitFontFamilyProperty, value);
    }

    public FontFamily FontFamily
    {
        get => DigitFontFamily;
        set => DigitFontFamily = value;
    }

    public FontWeight DigitFontWeight
    {
        get => GetValue(DigitFontWeightProperty);
        set => SetValue(DigitFontWeightProperty, value);
    }

    /// <summary>The layout's Timer font italic or not.</summary>
    public FontStyle DigitFontStyle
    {
        get => GetValue(DigitFontStyleProperty);
        set => SetValue(DigitFontStyleProperty, value);
    }

    public FontWeight FontWeight
    {
        get => DigitFontWeight;
        set => DigitFontWeight = value;
    }

    public const string WidthMeasureSample = "88:88:88";

    /// <summary>Shared painted ink-bottom Y (big “0” solid pixels at bigDrawY).</summary>
    public double SharedBaselineY { get; private set; }

    public double SmallClusterTopY { get; private set; }

    /// <summary>
    /// Painted ink-bottom delta: (smallDrawY + smallInk) − (bigDrawY + bigInk).
    /// Zero when small digits share the big digit floor.
    /// </summary>
    public double InkBottomDelta { get; private set; }

    /// <summary>Raster ink bottom of big “0” relative to DrawText origin (diagnostics).</summary>
    public double BigInkBottom { get; private set; }

    /// <summary>Raster ink bottom of small “0” relative to DrawText origin (diagnostics).</summary>
    public double SmallInkBottom { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        // Never create RenderTargetBitmap during measure — Avalonia/macOS can
        // tear down the process (empty stderr, no .ips) when Skia probes run
        // before the compositor is ready. Geometry is enough for layout size;
        // ink calibrate runs on first Render.
        Layout(out double width, out double height, draw: null, allowRasterInk: false);
        return new Size(width, height);
    }

    public override void Render(DrawingContext context) =>
        Layout(out _, out _, context, allowRasterInk: true);

    /// <summary>
    /// Measures the fixed-advance digit strip without adding this painter to the visual tree.
    /// The timer surface owns one instance and paints it directly.
    /// </summary>
    internal Size MeasureContent()
    {
        Layout(out double width, out double height, draw: null, allowRasterInk: false);
        return new Size(width, height);
    }

    /// <summary>Paints the digit strip directly into the owning timer surface.</summary>
    internal void PaintAt(DrawingContext context, Point origin, bool withGradient = false)
    {
        _paintGradient = withGradient;
        try
        {
            using (context.PushTransform(Matrix.CreateTranslation(origin.X, origin.Y)))
            {
                Layout(out _, out _, context, allowRasterInk: true);
            }
        }
        finally
        {
            _paintGradient = false;
        }
    }

    /// <summary>
    /// Timer.DrawUnscaled's gradient spans in this view's coordinates: the big digits' from the
    /// top of the big font's cell (GDI cell: usWinAscent above the baseline) to its bottom; the
    /// decimals' from the top of their own cell, as tall as the big cell less the difference of
    /// the two font sizes.
    /// </summary>
    private ((double Top, double Bottom) Big, (double Top, double Bottom) Small) GradientSpans()
    {
        (double ascent, double descent) = WindowsCellMetricsPerEm();
        double baseline = BigBaselineOffset;
        double bigTop = baseline - (ascent * BigFontSize);
        double bigCell = (ascent + descent) * BigFontSize;
        double smallTop = baseline - (ascent * SmallFontSize);
        return ((bigTop, bigTop + bigCell), (smallTop, smallTop + bigCell + SmallFontSize - BigFontSize));
    }

    /// <summary>
    /// A vertical gradient over <paramref name="span"/> for text drawn at
    /// <paramref name="drawY"/> (a text brush's absolute points start at the text's own origin).
    /// </summary>
    private static IBrush GradientAt((Color Top, Color Bottom) colors, (double Top, double Bottom) span, double drawY) =>
        new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, span.Top - drawY, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(0, Math.Max(span.Top + 1, span.Bottom) - drawY, RelativeUnit.Absolute),
            GradientStops =
            [
                new GradientStop(colors.Top, 0),
                new GradientStop(colors.Bottom, 1),
            ],
        };

    /// <summary>
    /// Width of a digit strip at the given sizes, with the same fixed digit advances as the paint
    /// (Windows draws timer digits monospaced), without changing this view.
    /// </summary>
    internal double StripWidth(string big, string small, double bigSize, double smallSize)
    {
        double width = 0;
        double digitBig = Math.Max(1, Make(bigSize, "0").Width);
        foreach (char ch in big)
        {
            width += char.IsDigit(ch) ? digitBig : Math.Max(Make(bigSize, ch.ToString()).Width, 1);
        }

        if (small.Length > 0)
        {
            double digitSmall = Math.Max(1, Make(smallSize, "0").Width);
            foreach (char ch in small)
            {
                width += char.IsDigit(ch) ? digitSmall : Math.Max(Make(smallSize, ch.ToString()).Width, 1);
            }
        }

        return width;
    }

    public double BigDigitAdvance => Math.Max(1, Make(BigFontSize, "0").Width);

    public double SmallDigitAdvance => Math.Max(1, Make(SmallFontSize, "0").Width);

    private void Layout(out double width, out double height, DrawingContext? draw, bool allowRasterInk)
    {
        IBrush brush = DigitBrush ?? Brushes.White;
        (Color Top, Color Bottom)? gradient = draw is not null && _paintGradient ? DigitGradient : null;
        var spans = gradient is not null ? GradientSpans() : default;
        FormattedText zeroBig = Make(BigFontSize, "0", brush);
        FormattedText zeroSmall = Make(SmallFontSize, "0", brush);
        GetCellMetrics(BigFontSize, out double bigAscent, out double bigDescent, out double bigCell);

        double bigInk;
        double smallInk;
        double bigDrawY = 0;
        double smallDrawY;
        double sharedFloor;

        if (allowRasterInk)
        {
            // Raster solid-ink bottoms of “0” at each size (same DrawText path as live paint).
            bigInk = SafeRasterInkBottom(BigFontSize, "0");
            smallInk = SafeRasterInkBottom(SmallFontSize, "0");

            // bigDrawY = 0 → sharedFloor is big ink bottom in control space.
            // smallDrawY = sharedFloor − smallInk → small solid bottom lands on sharedFloor.
            sharedFloor = bigInk;
            smallDrawY = sharedFloor - smallInk;

            // Closed-loop: co-render both “0”s and correct until solid bottoms match ≤1px.
            // Separate probes disagree with live CG Bold paint; paired floors are authority.
            double pairedBigFloor = sharedFloor;
            double pairedSmallFloor = sharedFloor;
            for (int iter = 0; iter < 4; iter++)
            {
                if (!TryMeasurePairedInkFloors(
                        BigFontSize, SmallFontSize, bigDrawY, smallDrawY,
                        out pairedBigFloor, out pairedSmallFloor))
                {
                    break;
                }

                double measuredDelta = pairedSmallFloor - pairedBigFloor;
                InkBottomDelta = measuredDelta;
                if (Math.Abs(measuredDelta) <= 1.0)
                {
                    break;
                }

                // Negative delta ⇒ small sits high ⇒ increase smallDrawY (move down).
                smallDrawY -= measuredDelta;
            }

            if (TryMeasurePairedInkFloors(
                    BigFontSize, SmallFontSize, bigDrawY, smallDrawY,
                    out pairedBigFloor, out pairedSmallFloor))
            {
                InkBottomDelta = pairedSmallFloor - pairedBigFloor;
            }

            // Red guide / SharedBaselineY must use the paired big floor, not a solo probe.
            sharedFloor = pairedBigFloor;
            bigInk = pairedBigFloor - bigDrawY;
            smallInk = Math.Max(0.1, pairedSmallFloor - smallDrawY);
        }
        else
        {
            // Measure pass: FormattedText geometry only (no Skia offscreen).
            bigInk = GeomBottomOr(zeroBig, BigFontSize);
            smallInk = GeomBottomOr(zeroSmall, SmallFontSize);
            sharedFloor = bigInk;
            smallDrawY = sharedFloor - smallInk;
            InkBottomDelta = 0;
        }

        BigInkBottom = bigInk;
        SmallInkBottom = smallInk;
        SmallClusterTopY = smallDrawY;
        // Guide through co-rendered big “0” ink bottom (paired when available).
        SharedBaselineY = sharedFloor;

        double digitBig = Math.Max(1, zeroBig.Width);
        double digitSmall = Math.Max(1, zeroSmall.Width);

        double x = 0;
        foreach (char ch in BigText ?? "")
        {
            FormattedText ft = Make(BigFontSize, ch.ToString(), brush);
            double advance = char.IsDigit(ch) ? digitBig : Math.Max(ft.Width, 1);
            if (draw is not null)
            {
                // Digits share one cluster Y (calibrated). Non-digits ink-align to same floor.
                double drawY = char.IsDigit(ch)
                    ? bigDrawY
                    : SharedBaselineY - (allowRasterInk
                        ? SafeRasterInkBottom(BigFontSize, ch.ToString())
                        : GeomBottomOr(ft, BigFontSize));
                double drawX = x + (advance - ft.Width) / 2;
                if (gradient is { } colors)
                {
                    ft.SetForegroundBrush(GradientAt(colors, spans.Big, drawY));
                }

                draw.DrawText(ft, new Point(drawX, drawY));
            }

            x += advance;
        }

        foreach (char ch in SmallText ?? "")
        {
            FormattedText ft = Make(SmallFontSize, ch.ToString(), brush);
            double advance = char.IsDigit(ch) ? digitSmall : Math.Max(ft.Width, 1);
            if (draw is not null)
            {
                // All small-cluster glyphs share calibrated smallDrawY so “.” cannot float.
                double drawY = smallDrawY;
                double drawX = x + (advance - ft.Width) / 2;
                if (gradient is { } colors)
                {
                    ft.SetForegroundBrush(GradientAt(colors, spans.Small, drawY));
                }

                draw.DrawText(ft, new Point(drawX, drawY));
            }

            x += advance;
        }

        width = x;
        height = Math.Max(bigCell, Math.Max(sharedFloor + 2, smallDrawY + zeroSmall.Height + 2));
        height = Math.Max(height, bigAscent + bigDescent);
    }

    private double SafeRasterInkBottom(double fontSize, string text)
    {
        try
        {
            return MeasureRasterInkBottom(fontSize, text);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"TimerDigitsView ink probe failed ({fontSize}/{text}): {ex.GetType().Name}: {ex.Message}");
            FormattedText ft = Make(fontSize, text, Brushes.White);
            return GeomBottomOr(ft, fontSize);
        }
    }

    private bool TryMeasurePairedInkFloors(
        double bigSize,
        double smallSize,
        double bigDrawY,
        double smallDrawY,
        out double bigFloor,
        out double smallFloor)
    {
        bigFloor = 0;
        smallFloor = 0;
        try
        {
            return MeasurePairedInkFloors(bigSize, smallSize, bigDrawY, smallDrawY, out bigFloor, out smallFloor);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"TimerDigitsView paired ink probe failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    private static double GeomBottomOr(FormattedText ft, double fontSize)
    {
        double geom = GeomBottom(ft);
        if (geom > 0.5 && geom <= fontSize * 1.25)
        {
            return geom;
        }

        return Math.Max(0.1, Math.Min(ft.Height, fontSize * 1.1));
    }

    /// <summary>
    /// Paint big+small “0” at the given drawY offsets on one bitmap.
    /// Floors are Y in control space (pad removed). Returns false if no solid ink.
    /// </summary>
    private bool MeasurePairedInkFloors(
        double bigSize,
        double smallSize,
        double bigDrawY,
        double smallDrawY,
        out double bigFloor,
        out double smallFloor)
    {
        bigFloor = 0;
        smallFloor = 0;
        string key =
            $"pairF|{DigitFontFamily}|{(int)DigitFontWeight}|{DigitFontStyle}|{bigSize:0.###}|{smallSize:0.###}|{bigDrawY:0.###}|{smallDrawY:0.###}|L{LumaThreshold}";
        lock (InkCache)
        {
            if (InkCache.TryGetValue(key + "|b", out double cachedBig)
                && InkCache.TryGetValue(key + "|s", out double cachedSmall))
            {
                bigFloor = cachedBig;
                smallFloor = cachedSmall;
                return true;
            }
        }

        FormattedText bigFt = Make(bigSize, "0", Brushes.White);
        FormattedText smallFt = Make(smallSize, "0", Brushes.White);
        const int pad = 4;
        int gap = 8;
        int w = Math.Max(
            16,
            (int)Math.Ceiling(bigFt.Width + smallFt.Width) + gap + pad * 2 + 8);
        int h = Math.Max(
            16,
            (int)Math.Ceiling(Math.Max(bigDrawY + bigSize * 1.35, smallDrawY + smallSize * 1.35)) + pad * 2 + 8);

        var probe = new PairedInkProbe(bigFt, smallFt, bigDrawY, smallDrawY, pad, gap, w, h);
        probe.Measure(new Size(w, h));
        probe.Arrange(new Rect(0, 0, w, h));
        var bmp = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        bmp.Render(probe);

        using var stream = new MemoryStream();
        bmp.Save(stream);
        stream.Position = 0;
        var decoded = WriteableBitmap.Decode(stream);
        using var fb = decoded.Lock();
        int stride = fb.RowBytes;
        int bw = fb.Size.Width;
        int bh = fb.Size.Height;
        var pixels = new byte[stride * bh];
        Marshal.Copy(fb.Address, pixels, 0, pixels.Length);

        int splitX = pad + (int)Math.Ceiling(bigFt.Width) + gap / 2;
        int bigBottom = ScanRegionBottom(pixels, stride, bw, bh, 0, Math.Min(splitX, bw));
        int smallBottom = ScanRegionBottom(pixels, stride, bw, bh, Math.Max(0, splitX), bw);
        if (bigBottom < 0 || smallBottom < 0)
        {
            return false;
        }

        // Convert bitmap Y → control Y (DrawText origin space).
        bigFloor = bigBottom - pad;
        smallFloor = smallBottom - pad;

        lock (InkCache)
        {
            InkCache[key + "|b"] = bigFloor;
            InkCache[key + "|s"] = smallFloor;
        }

        return true;
    }

    private static int ScanRegionBottom(byte[] pixels, int stride, int bw, int bh, int x0, int x1)
    {
        x0 = Math.Clamp(x0, 0, bw);
        x1 = Math.Clamp(x1, 0, bw);
        for (int y = bh - 1; y >= 0; y--)
        {
            int row = y * stride;
            for (int x = x0; x < x1; x++)
            {
                int i = row + x * 4;
                if (pixels[i] + pixels[i + 1] + pixels[i + 2] >= LumaThreshold)
                {
                    return y;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Bottommost solid-ink Y after DrawText at (0,0), matching live paint.
    /// Renders white glyph on opaque black and scans luminous pixels — avoids
    /// Baseline fallback when PNG alpha is lost, and matches Skia ink better than
    /// FormattedText.Baseline / Height for CG Bold.
    /// </summary>
    private double MeasureRasterInkBottom(double fontSize, string text)
    {
        string key = $"{DigitFontFamily}|{(int)DigitFontWeight}|{DigitFontStyle}|{fontSize:0.###}|{text}|L{LumaThreshold}";
        lock (InkCache)
        {
            if (InkCache.TryGetValue(key, out double cached))
            {
                return cached;
            }
        }

        FormattedText ft = Make(fontSize, text, Brushes.White);
        const int pad = 4;
        // Tight probe: do not size from inflated Height/Overhang alone — that made
        // earlier scans latch onto the bitmap edge and report ~fontSize*1.5.
        int w = Math.Max(8, (int)Math.Ceiling(Math.Max(ft.Width, 1)) + pad * 2 + 4);
        int h = Math.Max(8, (int)Math.Ceiling(fontSize * 1.35) + pad * 2 + 4);

        var probe = new InkProbe(ft, w, h, pad);
        probe.Measure(new Size(w, h));
        probe.Arrange(new Rect(0, 0, w, h));

        // 96 DPI logical coords match DrawText in the live control (Avalonia layout units).
        var bmp = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        bmp.Render(probe);

        // Always PNG round-trip: luminance on opaque black is reliable; CopyPixels
        // on some Avalonia/macOS paths returned edge noise that inflated ink Y.
        string? dumpPath = null;
        if (string.Equals(Environment.GetEnvironmentVariable("LSN_DEBUG_INK"), "1", StringComparison.Ordinal))
        {
            dumpPath = Path.Combine(
                Path.GetTempPath(),
                $"lsn-ink-{fontSize:0.#}-{SanitizeFileToken(text)}.png");
            bmp.Save(dumpPath);
        }

        double result = ScanInkBottomPng(bmp, pad, ft, fontSize, dumpPath);

        lock (InkCache)
        {
            InkCache[key] = result;
        }

        return result;
    }

    private static string SanitizeFileToken(string text)
    {
        Span<char> buf = stackalloc char[Math.Min(text.Length, 8)];
        int n = 0;
        foreach (char ch in text)
        {
            if (n >= buf.Length)
            {
                break;
            }

            buf[n++] = char.IsLetterOrDigit(ch) ? ch : '_';
        }

        return n == 0 ? "x" : new string(buf[..n]);
    }

    private static double ScanInkBottomPng(
        RenderTargetBitmap bmp, int pad, FormattedText ft, double fontSize, string? alreadySavedPath)
    {
        using var stream = new MemoryStream();
        if (alreadySavedPath is null)
        {
            bmp.Save(stream);
        }
        else
        {
            using var fs = File.OpenRead(alreadySavedPath);
            fs.CopyTo(stream);
        }

        stream.Position = 0;
        var decoded = WriteableBitmap.Decode(stream);
        using var fb = decoded.Lock();
        int stride = fb.RowBytes;
        int bw = fb.Size.Width;
        int bh = fb.Size.Height;
        var pixels = new byte[stride * bh];
        Marshal.Copy(fb.Address, pixels, 0, pixels.Length);

        int top = -1;
        int bottom = -1;
        int inkCount = 0;
        for (int y = 0; y < bh; y++)
        {
            int row = y * stride;
            for (int x = 0; x < bw; x++)
            {
                int i = row + x * 4;
                // PNG decode is typically Bgra8888; white glyph on black → high RGB.
                int b = pixels[i];
                int g = pixels[i + 1];
                int r = pixels[i + 2];
                if (r + g + b < LumaThreshold)
                {
                    continue;
                }

                inkCount++;
                if (top < 0)
                {
                    top = y;
                }

                bottom = y;
            }
        }

        if (bottom >= 0 && inkCount >= 4)
        {
            double ink = bottom - pad;
            // Sanity: CG Bold “0” ink bottom is near fontSize, never ~1.5×+.
            if (ink > 0.5 && ink <= fontSize * 1.25)
            {
                return ink;
            }

            // If probe caught fringe past the glyph, clamp using geometry when sane.
            double geom = GeomBottom(ft);
            if (geom > 0.5 && geom <= fontSize * 1.25)
            {
                return geom;
            }

            return Math.Min(ink, fontSize * 1.05);
        }

        // Geometry ink bounds — never Baseline alone (floats CG Bold fraction).
        double geomFallback = GeomBottom(ft);
        if (geomFallback > 0.5 && geomFallback <= fontSize * 1.25)
        {
            return geomFallback;
        }

        // Last resort: Height box (tends slightly low, not high).
        return Math.Max(0.1, Math.Min(ft.Height, fontSize * 1.1));
    }

    private static double GeomBottom(FormattedText ft)
    {
        try
        {
            Geometry? geo = ft.BuildGeometry(new Point(0, 0));
            if (geo is not null)
            {
                Rect b = geo.Bounds;
                if (b.Height > 0.01 && !double.IsNaN(b.Bottom) && !double.IsInfinity(b.Bottom))
                {
                    return b.Bottom;
                }
            }
        }
        catch
        {
            // fall through
        }

        return 0;
    }

    /// <summary>
    /// The digit font's cell ascent and descent per em as GDI reports them (OS/2 usWinAscent /
    /// usWinDescent over unitsPerEm), which is what Windows Timer.DrawUnscaled centers on. They
    /// differ a lot from the hhea metrics Avalonia lays out with (bundled face: 1.122 / 0.307
    /// against 0.750 / 0.208), which is why centering our own text box sat the digits high.
    /// </summary>
    internal (double Ascent, double Descent) WindowsCellMetricsPerEm()
    {
        var typeface = new Typeface(DigitFontFamily, DigitFontStyle, DigitFontWeight);
        if (FontManager.Current.TryGetGlyphTypeface(typeface, out IGlyphTypeface? glyph) && glyph is not null)
        {
            if (glyph.TryGetTable(0x4F532F32 /* OS/2 */, out byte[] os2)
                && glyph.TryGetTable(0x68656164 /* head */, out byte[] head)
                && os2.Length >= 78
                && head.Length >= 20)
            {
                double unitsPerEm = (head[18] << 8) | head[19];
                double winAscent = (os2[74] << 8) | os2[75];
                double winDescent = (os2[76] << 8) | os2[77];
                if (unitsPerEm > 0 && winAscent > 0)
                {
                    return (winAscent / unitsPerEm, winDescent / unitsPerEm);
                }
            }

            double em = Math.Max(1.0, (double)glyph.Metrics.DesignEmHeight);
            return (Math.Abs((double)glyph.Metrics.Ascent) / em, Math.Abs((double)glyph.Metrics.Descent) / em);
        }

        return (1, 0.25);
    }

    /// <summary>
    /// Distance from the paint origin to the big digits' baseline as drawn (they are drawn at
    /// y 0): the outline bottom of "1", which sits exactly on the baseline in Century Gothic.
    /// FormattedText.Baseline is not used: for the bundled face it reports a baseline about
    /// 0.08 em above where the glyphs land.
    /// </summary>
    internal double BigBaselineOffset
    {
        get
        {
            FormattedText one = Make(BigFontSize, "1");
            double bottom = GeomBottom(one);
            return bottom > 0.5 ? bottom : one.Baseline;
        }
    }

    private void GetCellMetrics(double fontSize, out double ascent, out double descent, out double cellHeight)
    {
        var typeface = new Typeface(DigitFontFamily, DigitFontStyle, DigitFontWeight);
        if (FontManager.Current.TryGetGlyphTypeface(typeface, out IGlyphTypeface? glyph) && glyph is not null)
        {
            double em = Math.Max(1.0, (double)glyph.Metrics.DesignEmHeight);
            ascent = Math.Abs((double)glyph.Metrics.Ascent) * fontSize / em;
            descent = Math.Abs((double)glyph.Metrics.Descent) * fontSize / em;
            cellHeight = ascent + descent;
            if (ascent > 0.1 && cellHeight > 0.1)
            {
                return;
            }
        }

        FormattedText ft = Make(fontSize, "0");
        ascent = Math.Max(0.1, ft.Baseline);
        descent = Math.Max(0.1, ft.Height - ft.Baseline);
        cellHeight = ascent + descent;
    }

    private FormattedText Make(double size, string text, IBrush? brush = null) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(DigitFontFamily, DigitFontStyle, DigitFontWeight),
            size,
            brush ?? DigitBrush ?? Brushes.White);

    private sealed class InkProbe : Control
    {
        private readonly FormattedText _ft;
        private readonly int _pad;

        public InkProbe(FormattedText ft, double width, double height, int pad)
        {
            _ft = ft;
            _pad = pad;
            Width = width;
            Height = height;
        }

        public override void Render(DrawingContext context)
        {
            // Opaque black so luminance scan works even if alpha is lost.
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            context.DrawText(_ft, new Point(_pad, _pad));
        }
    }

    private sealed class PairedInkProbe : Control
    {
        private readonly FormattedText _big;
        private readonly FormattedText _small;
        private readonly double _bigDrawY;
        private readonly double _smallDrawY;
        private readonly int _pad;
        private readonly int _gap;

        public PairedInkProbe(
            FormattedText big,
            FormattedText small,
            double bigDrawY,
            double smallDrawY,
            int pad,
            int gap,
            double width,
            double height)
        {
            _big = big;
            _small = small;
            _bigDrawY = bigDrawY;
            _smallDrawY = smallDrawY;
            _pad = pad;
            _gap = gap;
            Width = width;
            Height = height;
        }

        public override void Render(DrawingContext context)
        {
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            context.DrawText(_big, new Point(_pad, _pad + _bigDrawY));
            double smallX = _pad + _big.Width + _gap;
            context.DrawText(_small, new Point(smallX, _pad + _smallDrawY));
        }
    }
}
