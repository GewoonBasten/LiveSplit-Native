using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App;

/// <summary>
/// Single retained timer visual. Layout is supplied by an immutable
/// <see cref="TimerLayoutSnapshot"/> and rendering is direct; no component control tree
/// participates in measurement, scaling, hit-testing, or the 16 ms update path.
/// </summary>
internal sealed class TimerSurfaceControl : Control
{
    private readonly IconBitmapCache _icons = new();
    private readonly List<ComponentVisual> _components = [];
    private Layout? _layout;
    private LiveSplitState? _state;
    private AppSettings? _appSettings;
    private TimerLayoutSnapshot? _snapshot;

    public TimerLayoutSnapshot? Snapshot => _snapshot;

    public void Configure(
        Layout layout,
        LiveSplitState state,
        AppSettings appSettings,
        TimerLayoutSnapshot snapshot)
    {
        _layout = layout;
        _state = state;
        _appSettings = appSettings;
        _snapshot = snapshot;
        // A list component that is still there keeps its scroll position (Windows keeps the
        // component object when only its settings or the layout around it change).
        List<ComponentVisual> previous = [.. _components];
        _components.Clear();
        for (int i = 0; i < layout.Components.Count; i++)
        {
            ComponentVisual visual = ComponentVisual.Create(layout.Components[i]);
            if (i < previous.Count && previous[i].Path == visual.Path)
            {
                visual.KeepListState(previous[i]);
            }

            _components.Add(visual);
        }

        RememberHostedSizes();
        UpdateValues();
    }

    public void SetSnapshot(TimerLayoutSnapshot snapshot)
    {
        _snapshot = snapshot;
        RememberHostedSizes();
        InvalidateVisual();
    }

    /// <summary>The component DLLs' sizes this snapshot was measured with.</summary>
    private void RememberHostedSizes()
    {
        foreach (ComponentVisual visual in _components)
        {
            if (visual.Source?.RuntimeState is HostedLayoutComponent hosted)
            {
                hosted.MeasuredVerticalHeight = hosted.VerticalHeight;
                hosted.MeasuredHorizontalWidth = hosted.HorizontalWidth;
            }
        }
    }

    /// <summary>
    /// Updates strings, colors, row bindings, and cached icon references in-place.
    /// Existing component and split-row objects are retained.
    /// </summary>
    public void UpdateValues()
    {
        if (_layout is null || _state is null)
        {
            return;
        }

        // TimerForm.PaintForm: TextRenderingHint AntiAlias, or ClearTypeGridFit with
        // Anti-aliasing off (the closest here is subpixel text).
        RenderOptions.SetTextRenderingMode(
            this,
            _layout.Settings.AntiAliasing ? TextRenderingMode.Antialias : TextRenderingMode.SubpixelAntialias);
        // The layout's Timer font; Inter (bundled) when it is not installed.
        FontFamily timerFont = ResolveTimerFont(_layout.Settings.TimerFontFamily);
        FontWeight timerWeight = _layout.Settings.TimerFontBold ? FontWeight.Bold : FontWeight.Normal;
        FontStyle timerStyle = _layout.Settings.TimerFontItalic ? FontStyle.Italic : FontStyle.Normal;
        foreach (ComponentVisual component in _components)
        {
            component.MainDigits.DigitFontFamily = timerFont;
            component.SegmentDigits.DigitFontFamily = timerFont;
            component.MainDigits.DigitFontWeight = timerWeight;
            component.SegmentDigits.DigitFontWeight = timerWeight;
            component.MainDigits.DigitFontStyle = timerStyle;
            component.SegmentDigits.DigitFontStyle = timerStyle;
        }

        for (int i = 0; i < _components.Count; i++)
        {
            ComponentVisual visual = _components[i];
            switch (visual.Settings)
            {
                case TitleComponentSettings title:
                    UpdateTitle(visual, title);
                    break;
                case SplitsComponentSettings splits:
                    UpdateSplits(visual, splits);
                    break;
                case SubsplitsComponentSettings subsplits:
                    UpdateSubsplits(visual, subsplits);
                    break;
                case TimerComponentSettings timer:
                    UpdateTimer(visual, timer);
                    break;
                case DetailedTimerComponentSettings detailed:
                    UpdateDetailedTimer(visual, detailed);
                    break;
                case PreviousSegmentComponentSettings previous:
                    UpdatePreviousSegment(visual, previous);
                    break;
                case SumOfBestComponentSettings sumOfBest:
                    UpdateSumOfBest(visual, sumOfBest);
                    break;
                case PossibleTimeSaveComponentSettings possible:
                    UpdatePossibleTimeSave(visual, possible);
                    break;
                case TextComponentSettings text:
                    UpdateText(visual, text);
                    break;
                case RunPredictionComponentSettings prediction:
                    ShowInfo(visual, prediction, InfoComponentLogic.RunPrediction(_state, prediction, _layout.Settings));
                    break;
                case DeltaComponentSettings delta:
                    ShowInfo(visual, delta, InfoComponentLogic.Delta(_state, delta, _layout.Settings));
                    break;
                case ComparisonTimeComponentSettings comparisonTime:
                    ShowInfo(visual, comparisonTime, InfoComponentLogic.ComparisonTime(_state, comparisonTime, _layout.Settings));
                    break;
                case CurrentComparisonComponentSettings currentComparison:
                    ShowInfo(visual, currentComparison, InfoComponentLogic.CurrentComparison(_state, currentComparison, _layout.Settings));
                    break;
                case TotalPlaytimeComponentSettings playtime:
                    ShowInfo(visual, playtime, InfoComponentLogic.TotalPlaytime(
                        _state, playtime, _layout.Settings, visual.AttemptHistoryPlaytime(_state.Run)));
                    break;
                case CounterComponentSettings counter:
                    UpdateCounter(visual, counter);
                    break;
                case ContextualSlideshowComponentSettings:
                    UpdateSlideshow(visual);
                    break;
                case GraphComponentSettings graph:
                    visual.Graph = GraphLogic.Calculate(_state, graph, _layout.Settings);
                    break;
                case WorldRecordComponentSettings worldRecord:
                    UpdateWorldRecord(visual, worldRecord);
                    break;
                case HotkeyIndicatorComponentSettings indicator:
                    // HotkeyIndicator.Update: the current hotkey profile's Global Hotkeys.
                    visual.ValueBrush = Brush(_appSettings?.HotkeyProfile.GlobalHotkeysEnabled == true
                        ? indicator.HotkeysOnColor
                        : indicator.HotkeysOffColor);
                    break;
            }

            if (visual.Source?.RuntimeState is HostedLayoutComponent hosted)
            {
                UpdateHosted(hosted, i);
            }
        }

        InvalidateVisual();
    }

    /// <summary>
    /// A component DLL's size changed (InfoTextComponent with Display 2 Rows, say): the layout's
    /// size is worked out again (ComponentRenderer reads the sizes every frame).
    /// </summary>
    public event Action? ExternalComponentResized;

    private bool _externalResizePosted;

    /// <summary>ComponentRenderer.Update: the component updates with its place's size.</summary>
    private void UpdateHosted(HostedLayoutComponent hosted, int index)
    {
        if (_snapshot is null || _layout is null)
        {
            return;
        }

        TimerLayoutSlot? slot = _snapshot.Slots.Cast<TimerLayoutSlot?>().FirstOrDefault(s => s!.Value.ComponentIndex == index);
        bool vertical = _layout.Mode == LayoutMode.Vertical;
        double width = vertical ? _snapshot.LogicalWidth : hosted.HorizontalWidth;
        double height = vertical ? hosted.VerticalHeight : _snapshot.LogicalHeight;
        hosted.Component.Update((float)width, (float)height, _layout.Mode);

        // The size the layout was measured with, against the component's size now.
        double measured = vertical ? hosted.MeasuredVerticalHeight : hosted.MeasuredHorizontalWidth;
        double now = vertical ? hosted.VerticalHeight : hosted.HorizontalWidth;
        if (slot is not null && Math.Abs(measured - now) > 0.01 && !_externalResizePosted)
        {
            _externalResizePosted = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _externalResizePosted = false;
                ExternalComponentResized?.Invoke();
            });
        }
    }

    /// <summary>
    /// The layout's components as component DLLs see them (LiveSplitState.Layout.Components), in
    /// the layout's order: the DLLs' own, and Subsplits (as Windows' LiveSplit.Subsplits).
    /// </summary>
    internal IReadOnlyList<ComponentHost.Compat.HostLayoutEntry> HostLayoutEntries()
    {
        var entries = new List<ComponentHost.Compat.HostLayoutEntry>();
        foreach (ComponentVisual visual in _components)
        {
            if (visual.Source?.RuntimeState is HostedLayoutComponent hosted)
            {
                entries.Add(new ComponentHost.Compat.HostLayoutEntry(hosted.Component, null));
            }
            else if (visual.Subsplits is not null)
            {
                visual.HostSubsplits ??= new SubsplitsHostView(visual);
                entries.Add(new ComponentHost.Compat.HostLayoutEntry(null, visual.HostSubsplits));
            }
        }

        return entries;
    }

    /// <summary>
    /// A Subsplits component as Windows' LiveSplit.Subsplits shows itself to other components:
    /// its rows as last updated, and a color a component gives a row's column (Best World
    /// Segments' gold), drawn until the row is updated again.
    /// </summary>
    private sealed class SubsplitsHostView(ComponentVisual visual) : ComponentHost.Compat.IHostSubsplits
    {
        public IReadOnlyList<ComponentHost.Compat.HostSubsplitsRow> Rows
        {
            get
            {
                IReadOnlyList<SubsplitsRow> frame = visual.SubsplitsFrame?.Rows ?? [];
                var rows = new List<ComponentHost.Compat.HostSubsplitsRow>(frame.Count);
                for (int i = 0; i < frame.Count && i < visual.Rows.Count; i++)
                {
                    SplitRowVisual row = visual.Rows[i];
                    rows.Add(new ComponentHost.Compat.HostSubsplitsRow(
                        frame[i].Kind == SubsplitsRowKind.Blank ? -1 : frame[i].SplitIndex,
                        frame[i].Kind == SubsplitsRowKind.Header,
                        frame[i].CollapsedSplit,
                        row.Columns.Select(c => c.Brush is ISolidColorBrush b
                            ? System.Drawing.Color.FromArgb(b.Color.A, b.Color.R, b.Color.G, b.Color.B)
                            : System.Drawing.Color.White).ToList()));
                }

                return rows;
            }
        }

        public IReadOnlyList<ComponentHost.Compat.HostSubsplitsColumn> Columns =>
            (visual.Settings is SubsplitsComponentSettings { Columns.Count: > 0 } settings ? settings.Columns : ColumnData.CreateDefault())
                .Select(c => new ComponentHost.Compat.HostSubsplitsColumn(c.Name, c.Type.ToString(), c.Comparison, c.TimingMethod))
                .ToList();

        public void SetColumnColor(int row, int column, System.Drawing.Color color)
        {
            if (row >= 0 && row < visual.Rows.Count && column >= 0 && column < visual.Rows[row].Columns.Count)
            {
                visual.Rows[row].Columns[column].Brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            }
        }
    }

    /// <summary>A component DLL draws itself at its place (ComponentRenderer.DrawVertical / DrawHorizontal).</summary>
    private void DrawHosted(DrawingContext context, Rect bounds, HostedLayoutComponent hosted)
    {
        if (_layout is null)
        {
            return;
        }

        using (context.PushTransform(Matrix.CreateTranslation(bounds.X, bounds.Y)))
        {
            var canvas = new AvaloniaHostCanvas(context, _layout.Settings.AntiAliasing);
            try
            {
                if (_layout.Mode == LayoutMode.Vertical)
                {
                    hosted.Component.DrawVertical(canvas, (float)bounds.Width);
                }
                else
                {
                    hosted.Component.DrawHorizontal(canvas, (float)bounds.Height);
                }
            }
            finally
            {
                canvas.Dispose();
            }
        }
    }

    public void DisposeResources() => _icons.Dispose();

    /// <summary>TimerModel.OnScrollUp: every Splits and Subsplits component scrolls up one split.</summary>
    public void ScrollUp()
    {
        foreach (ComponentVisual visual in _components)
        {
            visual.Splits?.ScrollUp();
            visual.Subsplits?.ScrollUp();
        }
    }

    /// <summary>TimerModel.OnScrollDown: every Splits and Subsplits component scrolls down one split.</summary>
    public void ScrollDown()
    {
        foreach (ComponentVisual visual in _components)
        {
            visual.Splits?.ScrollDown();
            visual.Subsplits?.ScrollDown();
        }
    }

    /// <summary>The Subsplits rows as text (kind, name, times), for the screenshot-proof log.</summary>
    internal string DescribeSubsplitsRows()
    {
        ComponentVisual? visual = _components.FirstOrDefault(c => c.Subsplits is not null);
        if (visual is null)
        {
            return "no Subsplits";
        }

        return string.Join(" | ", visual.Rows.Where(r => r.IsVisible).Select(r => r.Kind switch
        {
            SubsplitsRowKind.Blank => "(blank)",
            SubsplitsRowKind.Header => $"[{r.Name} {r.SectionTimer} {r.HeaderTime}]",
            _ => (r.IndentLevel > 0 ? "  " : "") + r.Name
                + (r.IsCurrent ? "*" : "") + (r.IsHighlight ? "^" : "")
                + " " + string.Join(" ", r.Columns.Select(c => c.Text)),
        })) + $" (separator {visual.SubsplitsFrame?.LastSeparatorSize:0})";
    }

    /// <summary>"Name=Value" of each information component and Counter as shown, for the app tests.</summary>
    internal IReadOnlyList<string> InfoTexts() =>
        _components
            .Where(c => c.Settings is IInfoComponentSettings or CounterComponentSettings)
            .Select(c => c.Label + "=" + c.Value)
            .ToList();

    /// <summary>The paint scale and each component's slot in layout units, for proofs.</summary>
    internal string DescribeLayout() =>
        _snapshot is null
            ? "no layout"
            : $"scale {_snapshot.Scale:0.###}, logical {_snapshot.LogicalWidth:0.#}x{_snapshot.LogicalHeight:0.#}: "
                + string.Join(", ", _snapshot.Slots.Select(s =>
                    $"{(string.IsNullOrEmpty(s.Path) ? "separator" : s.Path)} y{s.Bounds.Y:0.#} h{s.Bounds.Height:0.#}"));

    /// <summary>OnStart / OnSplit / OnSkipSplit / OnUndoSplit / OnReset: the lists go back to the current split.</summary>
    public void ResetScroll()
    {
        foreach (ComponentVisual visual in _components)
        {
            visual.Splits?.ResetScroll();
            visual.Subsplits?.ResetScroll();
        }
    }

    /// <summary>
    /// ComponentRenderer.MinimumWidth / MinimumHeight (93a11b85): the largest minimum of the
    /// components, in layout units (before the window's scale), from what each one draws:
    /// <list type="bullet">
    /// <item>Splits: its columns and the icon slot, so the name gets no room
    /// (SplitComponent: labels width + IconWidth + 10).</item>
    /// <item>Title: the icon slot and the attempt count (GameNameLabel.X + attempts + 5).</item>
    /// <item>Timer, Detailed Timer, info rows, Text and Blank Space: 20.</item>
    /// </list>
    /// Call after <see cref="UpdateValues"/>, which sets the texts and icons these depend on.
    /// </summary>
    internal (double Width, double Height) MinimumContentSize()
    {
        double width = 0;
        double height = 0;
        foreach (ComponentVisual visual in _components)
        {
            (double w, double h) = visual.Settings switch
            {
                TitleComponentSettings => (TitleMinimumWidth(visual), TimerLayoutMetrics.TitleHeight(_layout?.Settings.TextFontSizePx ?? 16)),
                SplitsComponentSettings splits => (SplitsMinimumWidth(visual, splits), SplitsMinimumHeight()),
                SubsplitsComponentSettings subsplits => (SplitsMinimumWidth(visual, subsplits.AsSplitsSettings()), SplitsMinimumHeight()),
                TimerComponentSettings or DetailedTimerComponentSettings or BlankSpaceComponentSettings => (20d, 20d),
                PreviousSegmentComponentSettings or SumOfBestComponentSettings
                    or PossibleTimeSaveComponentSettings or TextComponentSettings
                    or IInfoComponentSettings => (20d, 25d),
                // CounterComponent: MinimumWidth = name X + the value's width; its MinimumHeight
                // is never set (0).
                // GraphComponent: MinimumWidth / MinimumHeight 20.
                GraphComponentSettings => (20d, 20d),
                // ContextualSlideshowComponent: the largest of its information rows'.
                ContextualSlideshowComponentSettings => (20d, 25d),
                CounterComponentSettings => (5 + MakeText(visual.Value, TextFontSize, Brushes.White, TextFontWeight, TextFontFamily, TextFontStyle).Width, 0d),
                _ => (0d, 0d),
            };
            width = Math.Max(width, w);
            height = Math.Max(height, h);
        }

        return (width, height);
    }

    /// <summary>Title.MinimumWidth: the game name's start (after the icon) + the attempt count + 5.</summary>
    private double TitleMinimumWidth(ComponentVisual visual)
    {
        double x = 5;
        if (visual.Icon is not null)
        {
            x += TimerLayoutMetrics.TitleHeight(_layout?.Settings.TextFontSizePx ?? 16) + 3;
        }

        double attempts = string.IsNullOrEmpty(visual.Value)
            ? 0
            : MakeText(visual.Value, TextFontSize, Brushes.White, TextFontWeight, TextFontFamily, TextFontStyle).Width;
        return x + attempts + 5;
    }

    private static double SplitsMinimumWidth(ComponentVisual visual, SplitsComponentSettings settings)
    {
        IReadOnlyList<ColumnData> columns = settings.Columns.Count > 0
            ? settings.Columns
            : ColumnData.CreateDefault();
        double width = LayoutMetrics.PadLeft + 4;
        for (int i = 0; i < columns.Count; i++)
        {
            double trailing = i == columns.Count - 1 ? LayoutMetrics.SplitsPadRight : 2;
            width += SplitsColumnRenderer.ColumnWidth(columns[i]) + 4 + trailing;
        }

        if (visual.ReserveSplitIcons)
        {
            width += LayoutMetrics.IconDrawSize(
                settings.IconSize,
                LayoutMetrics.RowHeightFor(settings.SplitHeight)) + 4;
        }

        return width;
    }

    /// <summary>Horizontal SplitComponent: 0.85 x (times font + text font line height).</summary>
    private double SplitsMinimumHeight() =>
        0.85 * (MakeText("A", TimesFontSize, Brushes.White, TimesFontWeight, TimesFontFamily, TimesFontStyle).Height
            + MakeText("A", TextFontSize, Brushes.White, TextFontWeight, TextFontFamily, TextFontStyle).Height);

    /// <summary>The Timer component's digits, for the screenshot-proof harness.</summary>
    internal TimerDigitsView? TimerDigits => FindDigits(StandardLayoutFactory.TimerPath, main: true);

    /// <summary>The Detailed Timer's main digits, for the screenshot-proof harness.</summary>
    internal TimerDigitsView? DetailedMainDigits => FindDigits(StandardLayoutFactory.DetailedTimerPath, main: true);

    /// <summary>The Detailed Timer's segment digits, for the screenshot-proof harness.</summary>
    internal TimerDigitsView? DetailedSegmentDigits => FindDigits(StandardLayoutFactory.DetailedTimerPath, main: false);

    private TimerDigitsView? FindDigits(string path, bool main)
    {
        foreach (ComponentVisual visual in _components)
        {
            if (visual.Path == path)
            {
                return main ? visual.MainDigits : visual.SegmentDigits;
            }
        }

        return null;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_layout is null || _snapshot is null || _state is null)
        {
            return;
        }

        using (context.PushClip(new Rect(Bounds.Size)))
        using (context.PushTransform(Matrix.CreateScale(_snapshot.Scale, _snapshot.Scale)))
        {
            for (int i = 0; i < _snapshot.Slots.Count; i++)
            {
                TimerLayoutSlot slot = _snapshot.Slots[i];
                if ((uint)slot.ComponentIndex >= (uint)_components.Count
                    || slot.Bounds.Width <= 0
                    || slot.Bounds.Height <= 0)
                {
                    continue;
                }

                // Component edges on whole device pixels: a fractional edge was drawn half by each
                // neighbour, so the layout background showed through as a faint line between two
                // components (Basten 18 Oct, between his Sum of Best rows).
                double pixelsPerUnit = _snapshot.Scale * (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
                Rect bounds = SnapToDevicePixels(ToRect(slot.Bounds), pixelsPerUnit);
                // ComponentRenderer: drawn over its whole size, shown only in its place.
                Rect draw = SnapToDevicePixels(ToRect(slot.DrawBounds), pixelsPerUnit);
                using (context.PushClip(bounds))
                {
                    DrawComponent(context, draw, _components[slot.ComponentIndex]);
                }
            }
        }
    }

    /// <summary>The rect with its edges moved to the nearest device pixel (pixels per layout unit).</summary>
    internal static Rect SnapToDevicePixels(Rect rect, double pixelsPerUnit)
    {
        if (pixelsPerUnit <= 0)
        {
            return rect;
        }

        double Snap(double v) => Math.Round(v * pixelsPerUnit, MidpointRounding.AwayFromZero) / pixelsPerUnit;
        double left = Snap(rect.X);
        double top = Snap(rect.Y);
        double right = Snap(rect.Right);
        double bottom = Snap(rect.Bottom);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private void DrawComponent(DrawingContext context, Rect bounds, ComponentVisual visual)
    {
        if (visual.Path == StandardLayoutFactory.SeparatorPath)
        {
            // SeparatorComponent: a line in the layout's Separators color over its 2-high
            // (horizontal: 2-wide) slot (DisplayedSize 2, UseSeparatorColor).
            if (_layout is not null)
            {
                context.FillRectangle(Brush(_layout.Settings.SeparatorsColor), bounds);
            }

            return;
        }

        if (visual.Source?.RuntimeState is HostedLayoutComponent hosted)
        {
            DrawHosted(context, bounds, hosted);
            return;
        }

        switch (visual.Settings)
        {
            case TitleComponentSettings title:
                DrawTitle(context, bounds, visual, title);
                break;
            case SplitsComponentSettings splits:
                DrawSplits(context, bounds, visual, splits);
                break;
            case SubsplitsComponentSettings subsplits:
                DrawSubsplits(context, bounds, visual, subsplits);
                break;
            case TimerComponentSettings timer:
                DrawTimer(context, bounds, visual, timer);
                break;
            case DetailedTimerComponentSettings detailed:
                DrawDetailedTimer(context, bounds, visual, detailed);
                break;
            case PreviousSegmentComponentSettings previous:
                DrawInfo(
                    context,
                    bounds,
                    visual,
                    previous.BackgroundGradient,
                    previous.BackgroundColor,
                    previous.BackgroundColor2);
                break;
            case SumOfBestComponentSettings sumOfBest:
                DrawInfo(
                    context,
                    bounds,
                    visual,
                    sumOfBest.BackgroundGradient,
                    sumOfBest.BackgroundColor,
                    sumOfBest.BackgroundColor2);
                break;
            case PossibleTimeSaveComponentSettings possible:
                DrawInfo(
                    context,
                    bounds,
                    visual,
                    possible.BackgroundGradient,
                    possible.BackgroundColor,
                    possible.BackgroundColor2);
                break;
            case TextComponentSettings text:
                DrawInfo(
                    context,
                    bounds,
                    visual,
                    text.BackgroundGradient,
                    text.BackgroundColor,
                    text.BackgroundColor2);
                break;
            case CounterComponentSettings counter:
                DrawCounter(context, bounds, visual, counter);
                break;
            case ContextualSlideshowComponentSettings slideshow:
                // The row's background (LiveSplitNative's; none by default), then, as
                // ContextualSlideshowComponent.DrawVertical / DrawHorizontal, the shown component.
                FillConfiguredBackground(
                    context,
                    bounds,
                    slideshow.BackgroundGradient,
                    slideshow.BackgroundColor,
                    slideshow.BackgroundColor2,
                    transparentFallback: true);
                if (visual.Slideshow?.Shown is int shown && visual.SlideshowParts is { } parts)
                {
                    if (slideshow.BackgroundColor.A > 0 || slideshow.BackgroundColor2.A > 0)
                    {
                        // With a row background (taken from the row above) the shown component
                        // draws on it without its own default one, so the row stays one piece
                        // (Previous Segment's default is a faint gradient).
                        DrawInfo(context, bounds, parts[shown], "Plain", ArgbColor.FromArgb(0, 0, 0, 0), ArgbColor.FromArgb(0, 0, 0, 0));
                    }
                    else
                    {
                        DrawComponent(context, bounds, parts[shown]);
                    }
                }

                break;
            case GraphComponentSettings graph:
                DrawGraph(context, bounds, visual, graph);
                break;
            case HotkeyIndicatorComponentSettings indicator:
                DrawHotkeyIndicator(context, bounds, visual, indicator);
                break;
            case IInfoComponentSettings info:
                DrawInfo(context, bounds, visual, info.BackgroundGradient, info.BackgroundColor, info.BackgroundColor2);
                break;
            case BlankSpaceComponentSettings blank:
                FillConfiguredBackground(
                    context,
                    bounds,
                    blank.BackgroundGradient,
                    blank.BackgroundColor,
                    blank.BackgroundColor2,
                    transparentFallback: true);
                break;
        }
    }

    private void UpdateTitle(ComponentVisual visual, TitleComponentSettings settings)
    {
        if (_state is null)
        {
            return;
        }

        string game = string.IsNullOrWhiteSpace(_state.Run.GameName) ? "Untitled" : _state.Run.GameName;
        // Title.Update: the extended category name carries variables, region and platform.
        string category = TitleLogic.CategoryText(_state.Run, settings);
        if (settings.SingleLine && settings.ShowGameName && settings.ShowCategoryName)
        {
            visual.Label = $"{game} - {category}".TrimEnd(' ', '-');
            visual.SecondaryLabel = "";
        }
        else
        {
            visual.Label = settings.ShowGameName ? game : "";
            visual.SecondaryLabel = settings.ShowCategoryName ? category : "";
        }

        visual.Value = TitleLogic.CountText(_state.Run, _state.CurrentPhase, settings);
        visual.Icon = settings.DisplayGameIcon ? _icons.Get(_state.Run.GameIconXml) : null;
        visual.LabelBrush = Brush(
            settings.OverrideTitleColor ? settings.TitleColor : _layout!.Settings.TextColor);
        // Title: the game name, the category and the count in the same color.
        visual.SecondaryLabelBrush = visual.LabelBrush;
        visual.ValueBrush = visual.LabelBrush;
    }

    private void UpdateSplits(
        ComponentVisual visual,
        SplitsComponentSettings settings)
    {
        if (_state is null || _layout is null)
        {
            return;
        }

        // SplitsComponent.Prepare: SplitTimeFormatter(SplitTimesAccuracy) and
        // DeltaSplitTimeFormatter(DeltasAccuracy, DropDecimals); Drop Decimals is for deltas only.
        visual.SplitFormatter.Accuracy = settings.SplitTimesAccuracy;
        visual.SplitFormatter.DigitsFormat = DigitsFormat.SingleDigitMinutes;
        visual.SplitFormatter.NullFormat = NullFormat.Dash;
        visual.SplitFormatter.DropDecimals = false;
        visual.DeltaFormatter.Accuracy = settings.DeltasAccuracy;
        visual.DeltaFormatter.DigitsFormat = DigitsFormat.SingleDigitSeconds;
        visual.DeltaFormatter.NullFormat = NullFormat.Dash;
        visual.DeltaFormatter.DropDecimals = settings.DropDecimals;
        visual.DeltaFormatter.ShowPlus = true;
        visual.ColumnWidths = WindowsColumnWidths(
            settings.Columns.Count > 0 ? settings.Columns : ColumnData.CreateDefault(),
            visual.SplitFormatter,
            visual.DeltaFormatter);

        List<RowBinding> bindings = VisibleSplitBindings(visual, settings);
        int renderedRowCount = settings.ShowBlankSplits
            ? Math.Max(Math.Max(1, settings.VisualSplitCount), bindings.Count)
            : bindings.Count;
        visual.ReserveSplitIcons = settings.DisplayIcons
            && bindings.Any(binding =>
                binding.Index >= 0
                && binding.Index < _state.Run.Count
                && !string.IsNullOrWhiteSpace(_state.Run[binding.Index].IconXml));
        visual.EnsureRows(renderedRowCount);
        IReadOnlyList<ColumnData> columns = settings.Columns.Count > 0
            ? settings.Columns
            : ColumnData.CreateDefault();
        // SplitsComponent.RebuildVisualSplits: with blank splits shown, Lock Last Split puts the
        // last split shown on the bottom row and the blank rows above it.
        bool lockLast = settings.ShowBlankSplits
            && settings.LockLastSplit
            && bindings.Count > 0
            && renderedRowCount > bindings.Count;
        for (int rowIndex = 0; rowIndex < visual.Rows.Count; rowIndex++)
        {
            SplitRowVisual row = visual.Rows[rowIndex];
            int bindingIndex = !lockLast || rowIndex < bindings.Count - 1
                ? rowIndex
                : rowIndex == renderedRowCount - 1 ? bindings.Count - 1 : -1;
            if (bindingIndex < 0 || bindingIndex >= bindings.Count)
            {
                row.IsVisible = rowIndex < renderedRowCount;
                row.SegmentIndex = rowIndex;
                row.Name = "";
                row.IndentLevel = 0;
                row.IsCurrent = false;
                // A blank row: Background Color (Run.IndexOf(null) is -1).
                row.AlternateBrush = AlternateBrush(settings, second: false);
                row.SubsplitBrush = null;
                row.Icon = null;
                row.EnsureColumns(columns.Count);
                foreach (SplitCellVisual cell in row.Columns)
                {
                    cell.Text = "";
                }
                continue;
            }

            RowBinding binding = bindings[bindingIndex];
            int index = binding.Index;
            row.IsVisible = true;
            row.SegmentIndex = index;
            Segment segment = _state.Run[index];
            row.Name = binding.DisplayName
                ?? (string.IsNullOrEmpty(segment.Name) ? "—" : segment.Name);
            row.IndentLevel = binding.IndentLevel;
            row.IsCurrent = index == _state.CurrentSplitIndex
                && _state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused;
            int labelsRow = settings.ShowColumnLabels ? 1 : 0;
            // SplitComponent.DrawGeneral, Alternating: Splits alternate by split.
            row.AlternateBrush = AlternateBrush(settings, second: (index % 2) + labelsRow == 1);
            row.SubsplitBrush = null;
            row.Icon = settings.DisplayIcons ? _icons.Get(segment.IconXml) : null;
            row.IconXml = segment.IconXml;
            row.NameBrush = Brush(SplitColumnLogic.NameColor(_state, index, settings, _layout.Settings));
            // SplitComponent.UpdateAll: Automatic Abbreviations gives the name label its
            // abbreviations as alternate texts (worked out again only when the name changes).
            if (!settings.AutomaticAbbreviations)
            {
                row.AlternateNames = [];
                row.AlternatesFor = null;
            }
            else if (row.AlternatesFor != row.Name)
            {
                row.AlternateNames = row.Name.GetAbbreviations().ToArray();
                row.AlternatesFor = row.Name;
            }
            row.EnsureColumns(columns.Count);
            for (int c = 0; c < columns.Count; c++)
            {
                SplitCellText cell = SplitColumnLogic.Column(
                    _state,
                    index,
                    columns[c],
                    settings,
                    _layout.Settings,
                    visual.SplitFormatter,
                    visual.DeltaFormatter);
                row.Columns[c].Text = cell.Text;
                row.Columns[c].Brush = Brush(cell.Color);
                row.Columns[c].Width = visual.ColumnWidths[c];
            }
        }
    }

    /// <summary>SplitsComponent.Update: the splits the split rows show (SplitsLogic, with the scroll position).</summary>
    private List<RowBinding> VisibleSplitBindings(ComponentVisual visual, SplitsComponentSettings settings)
    {
        if (_state is null || _state.Run.Count == 0)
        {
            return [];
        }

        visual.Splits ??= new SplitsLogic();
        return visual.Splits.Update(_state, settings).Select(index => new RowBinding(index)).ToList();
    }

    /// <summary>
    /// LiveSplit.Subsplits (93a11b85): SplitsComponent.Update chooses what each of the Total
    /// Splits rows shows (SubsplitsLogic), SplitComponent.UpdateAll what it draws
    /// (SubsplitsRowLogic).
    /// </summary>
    private void UpdateSubsplits(ComponentVisual visual, SubsplitsComponentSettings settings)
    {
        if (_state is null || _layout is null || visual.Subsplits is null)
        {
            return;
        }

        SplitsComponentSettings splits = settings.AsSplitsSettings();
        visual.SplitFormatter.Accuracy = settings.SplitTimesAccuracy;
        visual.SplitFormatter.DigitsFormat = DigitsFormat.SingleDigitMinutes;
        visual.SplitFormatter.NullFormat = NullFormat.Dash;
        visual.SplitFormatter.DropDecimals = false;
        visual.DeltaFormatter.Accuracy = settings.DeltasAccuracy;
        visual.DeltaFormatter.DigitsFormat = DigitsFormat.SingleDigitSeconds;
        visual.DeltaFormatter.NullFormat = NullFormat.Dash;
        visual.DeltaFormatter.DropDecimals = settings.DropDecimals;
        visual.DeltaFormatter.ShowPlus = true;

        SubsplitsFrame frame = visual.Subsplits.Update(_state, settings);
        visual.SubsplitsFrame = frame;
        IReadOnlyList<ColumnData> columns = settings.Columns.Count > 0
            ? settings.Columns
            : ColumnData.CreateDefault();
        visual.ColumnWidths = WindowsColumnWidths(columns, visual.SplitFormatter, visual.DeltaFormatter);
        IReadOnlyList<SubsplitsRowView> views = SubsplitsRowLogic.Views(
            _state, frame, settings, _layout.Settings, columns, visual.SplitFormatter, visual.DeltaFormatter);

        // SplitsComponent.Prepare: the separator before the last row keeps its line next to the
        // current split while the timer runs (LockToBottom when the last row is the current one).
        if (_state.CurrentPhase is TimerPhase.Running or TimerPhase.Paused && views.Count > 1)
        {
            int current = _state.CurrentSplitIndex;
            if (views[^1].Kind != SubsplitsRowKind.Blank && views[^1].SplitIndex == current)
            {
                visual.LastSeparatorLockToBottom = true;
            }
            else if (views[^2].Kind != SubsplitsRowKind.Blank && views[^2].SplitIndex == current)
            {
                visual.LastSeparatorLockToBottom = false;
            }
        }

        visual.ReserveSplitIcons = views.Any(v => v.DisplayIcon);
        visual.EnsureRows(views.Count);
        IBrush? subsplitBrush = settings.OverrideSubsplitColor
            ? GradientBrush(settings.SubsplitGradient, settings.SubsplitTopColor, settings.SubsplitBottomColor)
            : null;
        IBrush headerBrush = GradientBrush(settings.HeaderGradient, settings.HeaderTopColor, settings.HeaderBottomColor);
        for (int rowIndex = 0; rowIndex < visual.Rows.Count; rowIndex++)
        {
            SplitRowVisual row = visual.Rows[rowIndex];
            if (rowIndex >= views.Count)
            {
                row.IsVisible = false;
                continue;
            }

            SubsplitsRowView view = views[rowIndex];
            row.IsVisible = true;
            row.Kind = view.Kind;
            row.SegmentIndex = view.SplitIndex;
            row.Name = view.Name;
            row.IndentLevel = view.Indent ? 1 : 0;
            row.IsCurrent = view.IsActive && view.Kind == SubsplitsRowKind.Split;
            row.IsHighlight = view.IsHighlight && view.Kind == SubsplitsRowKind.Split;
            // Alternating: oddSplit takes Background Color, the others Background Color 2.
            row.AlternateBrush = AlternateBrush(splits, second: !view.OddSplit);
            // DrawGeneral: Subsplit Color on a subsplit or an indented last split of a section.
            row.SubsplitBrush = view.Kind == SubsplitsRowKind.Split && view.SubsplitColor ? subsplitBrush : null;
            row.HeaderBrush = view.Kind == SubsplitsRowKind.Header ? headerBrush : null;
            row.ReserveIcon = view.DisplayIcon;
            Segment? segment = view.SplitIndex >= 0 && view.SplitIndex < _state.Run.Count ? _state.Run[view.SplitIndex] : null;
            row.Icon = view.DisplayIcon && segment is not null ? _icons.Get(segment.IconXml) : null;
            row.IconXml = segment?.IconXml;
            row.NameBrush = Brush(view.NameColor);
            if (!settings.AutomaticAbbreviation)
            {
                row.AlternateNames = [];
                row.AlternatesFor = null;
            }
            else if (row.AlternatesFor != row.Name)
            {
                row.AlternateNames = row.Name.GetAbbreviations().ToArray();
                row.AlternatesFor = row.Name;
            }

            row.HeaderTime = view.HeaderTime;
            row.HeaderTimeBrush = Brush(view.HeaderTimeColor);
            row.SectionTimer = view.SectionTimer;
            row.SectionTimerBrush = Brush(view.SectionTimerColor);
            if (view.SectionTimerGradient)
            {
                // DrawHeader: from a lighter, paler top to a darker bottom, as the Timer's gradient.
                var (top, bottom) = TimerPaintLogic.DigitGradientColors(view.SectionTimerColor);
                row.SectionTimerGradient = (ToColor(top), ToColor(bottom));
            }
            else
            {
                row.SectionTimerGradient = null;
            }

            row.EnsureColumns(view.Columns.Count);
            for (int c = 0; c < view.Columns.Count; c++)
            {
                row.Columns[c].Text = view.Columns[c].Text;
                row.Columns[c].Brush = Brush(view.Columns[c].Color);
                row.Columns[c].Width = visual.ColumnWidths[c];
            }
        }
    }

    private void UpdateTimer(ComponentVisual visual, TimerComponentSettings settings)
    {
        if (_state is null || _layout is null)
        {
            return;
        }

        visual.TimerFormatter.Accuracy = settings.TimerAccuracy;
        visual.TimerFormatter.DigitsFormat = DetailedTimerLogic.ParseDigitsFormat(settings.DigitsFormat);
        TimingMethod method = TimerComponentLogic.ResolveTimingMethod(
            settings.TimingMethod,
            _state.CurrentTimingMethod);
        // Timer.GetTime: the run's Start Timer value until the attempt starts.
        AssignTimerValue(visual.MainDigits, visual.TimerFormatter, TimerComponentLogic.DisplayedTime(_state, method));
        ArgbColor color = settings.OverrideSplitColors
            ? settings.TimerColor
            : LiveSplitStateHelper.GetTimerColor(_state, _layout.Settings, method);
        visual.TimerColor = color;
        // Timer.DrawUnscaled: Show Gradient shades the digits from light (top) to dark (bottom).
        SetDigitColor(visual.MainDigits, color, settings.ShowGradient);
    }

    private void UpdateDetailedTimer(
        ComponentVisual visual,
        DetailedTimerComponentSettings settings)
    {
        if (_state is null || _layout is null)
        {
            return;
        }

        TimingMethod method = ResolveTimingMethod(settings.TimingMethod);
        visual.TimerFormatter.Accuracy = settings.Accuracy;
        visual.TimerFormatter.DigitsFormat = DetailedTimerLogic.ParseDigitsFormat(settings.DigitsFormat);
        visual.SegmentFormatter.Accuracy = settings.SegmentAccuracy;
        visual.SegmentFormatter.DigitsFormat =
            DetailedTimerLogic.ParseDigitsFormat(settings.SegmentDigitsFormat);
        visual.DetailFormatter.Accuracy = settings.SegmentTimesAccuracy;

        // DetailedTimer's InternalComponent is a Timer: the same GetTime.
        AssignTimerValue(visual.MainDigits, visual.TimerFormatter, TimerComponentLogic.DisplayedTime(_state, method));
        AssignTimerText(
            visual.SegmentDigits,
            visual.SegmentFormatter.Format(DetailedTimerLogic.SegmentTimerTime(_state, method)));
        ArgbColor mainColor = settings.OverrideTimerColors
            ? settings.TimerColor
            : LiveSplitStateHelper.GetTimerColor(_state, _layout.Settings);
        SetDigitColor(visual.MainDigits, mainColor, settings.TimerShowGradient);
        SetDigitColor(visual.SegmentDigits, settings.SegmentTimerColor, settings.SegmentTimerShowGradient);

        int segmentIndex = _state.CurrentSplitIndex == _state.Run.Count
            ? _state.CurrentSplitIndex - 1
            : _state.CurrentSplitIndex;
        if (segmentIndex < 0 || segmentIndex >= _state.Run.Count)
        {
            visual.Label = "";
            visual.Icon = null;
            visual.SecondaryLabel = "";
            visual.SecondaryValue = "";
            visual.TertiaryLabel = "";
            visual.TertiaryValue = "";
            return;
        }

        Segment segment = _state.Run[segmentIndex];
        visual.Label = settings.ShowSplitName
            ? DetailedTimerLogic.FormatSplitName(segment.Name, segmentIndex, _state.Run.Count)
            : "";
        visual.Icon = settings.DisplayIcon ? _icons.Get(segment.IconXml) : null;
        var comparisons = DetailedTimerLogic.ResolveComparisons(
            settings.Comparison,
            settings.Comparison2,
            settings.HideComparison,
            _state.CurrentComparison,
            _state.Run.Comparisons.ToList());
        visual.SecondaryLabel = comparisons.Comparison == "None"
            ? ""
            : DetailedTimerLogic.ShortComparisonName(comparisons.Comparison) + ":";
        visual.SecondaryValue = comparisons.Comparison == "None"
            ? ""
            : visual.DetailFormatter.Format(
                DetailedTimerLogic.SegmentComparisonTime(
                    _state.Run,
                    _state.CurrentSplitIndex,
                    comparisons.Comparison,
                    method));
        visual.HideSecondComparison = comparisons.HideComparison;
        visual.TertiaryLabel = comparisons.HideComparison
            ? ""
            : DetailedTimerLogic.ShortComparisonName(comparisons.Comparison2) + ":";
        visual.TertiaryValue = comparisons.HideComparison
            ? ""
            : visual.DetailFormatter.Format(
                DetailedTimerLogic.SegmentComparisonTime(
                    _state.Run,
                    _state.CurrentSplitIndex,
                    comparisons.Comparison2,
                    method));
    }

    private void UpdatePreviousSegment(
        ComponentVisual visual,
        PreviousSegmentComponentSettings settings)
    {
        if (_state is null || _layout is null)
        {
            return;
        }

        PreviousSegmentView view = PreviousSegmentLogic.Compute(_state, settings, _layout.Settings);
        visual.Label = view.Name;
        visual.AlternateLabels = view.AlternateNames;
        visual.Value = view.Value;
        visual.LabelBrush = Brush(view.NameColor);
        visual.ValueBrush = Brush(view.ValueColor);
        visual.TwoRows = settings.Display2Rows;
    }

    private void UpdateSumOfBest(ComponentVisual visual, SumOfBestComponentSettings settings)
    {
        if (_state is null || _layout is null)
        {
            return;
        }

        // SumOfBestComponent: SplitTimeFormatter, name "Sum of Best Segments" with its shorter forms.
        visual.DetailFormatter.Accuracy = settings.Accuracy;
        visual.DetailFormatter.DropDecimals = false;
        visual.DetailFormatter.NullFormat = NullFormat.Dash;
        visual.DetailFormatter.DigitsFormat = DigitsFormat.SingleDigitMinutes;
        visual.Label = "Sum of Best Segments";
        visual.AlternateLabels = ["Sum of Best", "SoB"];
        visual.TwoRows = settings.Display2Rows;
        visual.Value = visual.DetailFormatter.Format(
            SumOfBest.CalculateSumOfBest(
                _state.Run,
                simpleCalculation: _appSettings?.SimpleSumOfBest ?? false,
                useCurrentRun: true,
                method: _state.CurrentTimingMethod));
        visual.LabelBrush = Brush(
            settings.OverrideTextColor ? settings.TextColor : _layout.Settings.TextColor);
        visual.ValueBrush = Brush(
            settings.OverrideTimeColor ? settings.TimeColor : _layout.Settings.TextColor);
    }

    private void UpdatePossibleTimeSave(
        ComponentVisual visual,
        PossibleTimeSaveComponentSettings settings)
    {
        if (_state is null || _layout is null)
        {
            return;
        }

        visual.DetailFormatter.Accuracy = settings.Accuracy;
        visual.DetailFormatter.DropDecimals = settings.DropDecimals;
        string comparison = ComparisonSwitching.ResolveComponentComparison(_state, settings.Comparison);
        // PossibleTimeSave.Update: the name (with the comparison when one is chosen) and its
        // shorter forms.
        string suffix = settings.Comparison == "Current Comparison"
            ? ""
            : " (" + DetailedTimerLogic.ShortComparisonName(comparison) + ")";
        visual.Label = (settings.TotalTimeSave ? "Total " : "") + "Possible Time Save" + suffix;
        visual.AlternateLabels = settings.TotalTimeSave
            ? ["Total Possible Time Save", "Possible Time Save", "Poss. Time Save", "Time Save"]
            : ["Possible Time Save", "Poss. Time Save", "Time Save"];
        visual.TwoRows = settings.Display2Rows;
        visual.Value = visual.DetailFormatter.Format(
            PossibleTimeSaveCalculator.GetDisplayedValue(
                _state,
                comparison,
                settings.TotalTimeSave));
        visual.LabelBrush = Brush(
            settings.OverrideTextColor ? settings.TextColor : _layout.Settings.TextColor);
        visual.ValueBrush = Brush(
            settings.OverrideTimeColor ? settings.TimeColor : _layout.Settings.TextColor);
    }

    private void UpdateText(ComponentVisual visual, TextComponentSettings settings)
    {
        if (_state is null || _layout is null)
        {
            return;
        }

        string value = settings.CustomVariable
            ? _state.Run.Metadata.CustomVariableValue(settings.Text2) ?? "-"
            : settings.Text2;
        bool single = string.IsNullOrEmpty(settings.Text1) || string.IsNullOrEmpty(value);
        visual.Label = single
            ? (!string.IsNullOrEmpty(settings.Text1) ? settings.Text1 : value)
            : settings.Text1;
        visual.Value = single ? "" : value;
        visual.CenterLabel = single;
        visual.TwoRows = settings.Display2Rows;
        // TextComponent's value is drawn in the text font, not the times font.
        visual.ValueUsesTextFont = true;
        visual.LabelBrush = Brush(
            settings.OverrideTextColor ? settings.TextColor : _layout.Settings.TextColor);
        visual.ValueBrush = Brush(
            settings.OverrideTimeColor ? settings.TimeColor : _layout.Settings.TextColor);
    }

    /// <summary>
    /// What an information component computed in Core (InfoComponentLogic) goes on the visual:
    /// InfoTimeComponent / InfoTextComponent's name, shorter names, value and colors.
    /// </summary>
    private void ShowInfo(ComponentVisual visual, IInfoComponentSettings settings, InfoComponentView view)
    {
        visual.Label = view.Name;
        visual.AlternateLabels = view.AlternateNames;
        visual.Value = view.Value;
        visual.LabelBrush = Brush(view.NameColor);
        visual.ValueBrush = Brush(view.ValueColor);
        visual.TwoRows = settings.Display2Rows;
        visual.ValueUsesTextFont = view.ValueInTextFont;
    }

    /// <summary>The Contextual Slideshow's clock (TimeStamp.Now: monotonic); tests set their own.</summary>
    internal static Func<TimeSpan> SlideshowClock { get; set; } = () => SlideshowStopwatch.Elapsed;

    private static readonly System.Diagnostics.Stopwatch SlideshowStopwatch = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// ContextualSlideshowComponent.Update: each of its components updates (as one of the
    /// layout's would, with its default settings); one whose texts or colors changed counts as
    /// invalidated, and the slideshow picks the one to show.
    /// </summary>
    private void UpdateSlideshow(ComponentVisual visual)
    {
        if (_state is null || _layout is null || visual.SlideshowParts is not { } parts || visual.Slideshow is null)
        {
            return;
        }

        var changed = new bool[parts.Count];
        for (int i = 0; i < parts.Count; i++)
        {
            ComponentVisual part = parts[i];
            switch (part.Settings)
            {
                case PossibleTimeSaveComponentSettings possible:
                    UpdatePossibleTimeSave(part, possible);
                    break;
                case PreviousSegmentComponentSettings previous:
                    UpdatePreviousSegment(part, previous);
                    break;
                case RunPredictionComponentSettings prediction:
                    ShowInfo(part, prediction, InfoComponentLogic.RunPrediction(_state, prediction, _layout.Settings));
                    break;
            }

            // The component's Cache: its name, value and their colors.
            string key = string.Join('\u001f', part.Label, part.Value, Key(part.LabelBrush), Key(part.ValueBrush), string.Join('\u001e', part.AlternateLabels));
            changed[i] = key != visual.SlideshowKeys![i];
            visual.SlideshowKeys[i] = key;
        }

        visual.Slideshow.Update(changed, SlideshowClock());

        static string Key(IBrush brush) => brush is ISolidColorBrush solid ? solid.Color.ToString() : brush.GetHashCode().ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The Contextual Slideshow's shown component as "Name=Value" (tests), or null.</summary>
    internal string? SlideshowShown()
    {
        ComponentVisual? visual = _components.FirstOrDefault(c => c.Slideshow is not null);
        return visual?.Slideshow?.Shown is int shown && visual.SlideshowParts is { } parts
            ? parts[shown].Label + "=" + parts[shown].Value
            : null;
    }

    /// <summary>speedrun.com for the World Record component (tests put an offline one here).</summary>
    internal static SpeedrunComClient SpeedrunCom { get; set; } = SpeedrunComClient.Default;

    /// <summary>
    /// WorldRecordComponent.Update: the lookup (in the background, kept on the layout component)
    /// and what it shows; Centered Text is one centered line.
    /// </summary>
    private void UpdateWorldRecord(ComponentVisual visual, WorldRecordComponentSettings settings)
    {
        if (_state is null || _layout is null || visual.Source is null)
        {
            return;
        }

        WorldRecordFetcher fetcher = WorldRecordFetcher.For(visual.Source, SpeedrunCom);
        fetcher.Update(_state.Run, settings);
        WorldRecordView view = WorldRecordLogic.View(
            _state,
            settings,
            _layout.Settings,
            fetcher.Result,
            fetcher.IsLoading,
            _layout.Mode == LayoutMode.Horizontal);
        ShowInfo(visual, settings, view.Info);
        visual.CenterLabel = view.Centered;
    }

    /// <summary>CounterComponent.Update: the counter text and the count.</summary>
    private void UpdateCounter(ComponentVisual visual, CounterComponentSettings settings)
    {
        if (_layout is null || visual.Source is null)
        {
            return;
        }

        visual.Label = settings.CounterText;
        visual.Value = CounterLogic.For(visual.Source, settings).Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        visual.LabelBrush = Brush(settings.OverrideTextColor ? settings.CounterTextColor : _layout.Settings.TextColor);
        visual.ValueBrush = Brush(settings.OverrideTextColor ? settings.CounterValueColor : _layout.Settings.TextColor);
    }

    /// <summary>
    /// CounterComponent.DrawGeneral: the background, then the counter text from x 5 up to the
    /// width of "1000" before the right edge, and the count right-aligned 5 from the edge, both
    /// vertically centered in the Text font.
    /// </summary>
    private void DrawCounter(DrawingContext context, Rect bounds, ComponentVisual visual, CounterComponentSettings settings)
    {
        FillConfiguredBackground(
            context,
            bounds,
            settings.BackgroundGradient,
            settings.BackgroundColor,
            settings.BackgroundColor2,
            transparentFallback: true);
        double fourCharWidth = MakeText("1000", TextFontSize, visual.ValueBrush, TextFontWeight, TextFontFamily, TextFontStyle).Width;
        DrawTextInRect(
            context,
            visual.Label,
            new Rect(bounds.X + 5, bounds.Y, Math.Max(0, bounds.Width - fourCharWidth - 5), bounds.Height),
            TextFontSize,
            visual.LabelBrush,
            TextFontWeight,
            alignCenter: false,
            fontFamily: TextFontFamily,
            style: TextFontStyle);
        DrawTextInRect(
            context,
            visual.Value,
            new Rect(bounds.X + 5, bounds.Y, Math.Max(0, bounds.Width - 10), bounds.Height),
            TextFontSize,
            visual.ValueBrush,
            TextFontWeight,
            alignCenter: false,
            alignRight: true,
            fontFamily: TextFontFamily,
            style: TextFontStyle);
    }

    /// <summary>Device pixels per layout unit (the window's scale times the screen's).</summary>
    private double PixelsPerUnit => (_snapshot?.Scale ?? 1) * (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);

    /// <summary>
    /// A line of this thickness in layout units, rounded to whole device pixels (at least one),
    /// as LineComponent users do: Math.Max((int)(size * scale + 0.5f), 1) / scale.
    /// </summary>
    private double WholePixels(double size)
    {
        double ppu = PixelsPerUnit;
        return ppu > 0 ? Math.Max((int)(size * ppu + 0.5), 1) / ppu : size;
    }

    /// <summary>HotkeyIndicator.DrawVertical / DrawHorizontal: the bar across the slot, centered.</summary>
    private void DrawHotkeyIndicator(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        HotkeyIndicatorComponentSettings settings)
    {
        if (_layout?.Mode == LayoutMode.Horizontal)
        {
            double width = WholePixels(settings.IndicatorWidth);
            context.FillRectangle(visual.ValueBrush, new Rect(bounds.X + (bounds.Width - width) / 2, bounds.Y, width, bounds.Height));
        }
        else
        {
            double height = WholePixels(settings.IndicatorHeight);
            context.FillRectangle(visual.ValueBrush, new Rect(bounds.X, bounds.Y + (bounds.Height - height) / 2, bounds.Width, height));
        }
    }

    /// <summary>
    /// GraphCompositeComponent: a Separators-color line (one device pixel or more, at the inner
    /// edge of its 1 unit slot), the graph (GraphComponent.DrawGeneral, flipped with Flip Graph),
    /// and another line.
    /// </summary>
    private void DrawGraph(DrawingContext context, Rect bounds, ComponentVisual visual, GraphComponentSettings settings)
    {
        if (_state is null || visual.Graph is null)
        {
            return;
        }

        bool horizontal = _layout?.Mode == LayoutMode.Horizontal;
        double line = WholePixels(1);
        IBrush lines = Brush(settings.GraphLinesColor);
        Rect area;
        if (horizontal)
        {
            context.FillRectangle(lines, new Rect(bounds.X + 1 - line, bounds.Y, line, bounds.Height));
            context.FillRectangle(lines, new Rect(bounds.Right - 1, bounds.Y, line, bounds.Height));
            area = new Rect(bounds.X + 1, bounds.Y, Math.Max(0, bounds.Width - 2), bounds.Height);
        }
        else
        {
            context.FillRectangle(lines, new Rect(bounds.X, bounds.Y + 1 - line, bounds.Width, line));
            context.FillRectangle(lines, new Rect(bounds.X, bounds.Bottom - 1, bounds.Width, line));
            area = new Rect(bounds.X, bounds.Y + 1, bounds.Width, Math.Max(0, bounds.Height - 2));
        }

        GraphPicture picture = GraphLogic.Draw(_state, settings, visual.Graph, (float)area.Width, (float)area.Height);
        Point Map((float X, float Y) p) => new(
            area.X + p.X,
            area.Y + (picture.Flip ? area.Height - p.Y : p.Y));
        static bool Finite(IReadOnlyList<(float X, float Y)> points) =>
            points.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y));
        IBrush shadow = Brush(picture.ShadowsColor);
        using (context.PushClip(area))
        {
            foreach (GraphOp op in picture.Ops)
            {
                if (!Finite(op.Points))
                {
                    continue;
                }

                IBrush brush = Brush(op.Color);
                switch (op.Kind)
                {
                    case GraphOpKind.Rectangle:
                        Point a = Map(op.Points[0]);
                        Point b = Map(op.Points[1]);
                        context.FillRectangle(brush, new Rect(
                            Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)));
                        break;
                    case GraphOpKind.Gridline:
                        context.DrawLine(new Pen(brush, 2), Map(op.Points[0]), Map(op.Points[1]));
                        break;
                    case GraphOpKind.Polygon when op.Points.Count > 2:
                        var geometry = new StreamGeometry();
                        using (StreamGeometryContext figure = geometry.Open())
                        {
                            figure.BeginFigure(Map(op.Points[0]), isFilled: true);
                            for (int i = 1; i < op.Points.Count; i++)
                            {
                                figure.LineTo(Map(op.Points[i]));
                            }

                            figure.EndFigure(isClosed: true);
                        }

                        context.DrawGeometry(brush, null, geometry);
                        break;
                    case GraphOpKind.Line:
                    {
                        // DrawLineShadowed: three shadow lines 1 right and 1-3 down, then the line.
                        Point from = Map(op.Points[0]);
                        Point to = Map(op.Points[1]);
                        var shadowPen = new Pen(shadow, 1.75, lineCap: PenLineCap.Round);
                        for (int k = 1; k <= 3; k++)
                        {
                            context.DrawLine(shadowPen, new Point(from.X + 1, from.Y + k), new Point(to.X + 1, to.Y + k));
                        }

                        context.DrawLine(new Pen(brush, 1.75, lineCap: PenLineCap.Round), from, to);
                        break;
                    }

                    case GraphOpKind.Dot:
                    {
                        // DrawEllipseShadowed: FillEllipse(x - 2.5, y - 2.5, 5, 5) and its shadows.
                        Point center = Map(op.Points[0]);
                        for (int k = 1; k <= 3; k++)
                        {
                            context.DrawEllipse(shadow, null, new Point(center.X + 1, center.Y + k), 2.5, 2.5);
                        }

                        context.DrawEllipse(brush, null, center, 2.5, 2.5);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Title.DrawGeneral: everything in the layout's Text font (Windows' Title font follows it);
    /// the game icon as high as the component (less 4), the game name at the top and the category
    /// at the bottom (either one alone in the middle), the attempt count at the bottom right, the
    /// names centered between the icon and the count when Text Alignment asks for it.
    /// </summary>
    private void DrawTitle(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        TitleComponentSettings settings)
    {
        FillConfiguredBackground(
            context,
            bounds,
            settings.BackgroundGradient,
            settings.BackgroundColor,
            settings.BackgroundColor2,
            transparentFallback: true);
        double width = bounds.Width;
        double height = bounds.Height;
        bool showIcon = visual.Icon is not null;
        if (visual.Icon is { } icon && icon.Size.Width > 0 && icon.Size.Height > 0)
        {
            // DrawGameIcon: height - 4 square, the icon's shape kept, 7 from the left and 2 from the top.
            double box = Math.Max(1, height - 4);
            double drawWidth = box;
            double drawHeight = box;
            if (icon.Size.Width > icon.Size.Height)
            {
                drawHeight *= icon.Size.Height / icon.Size.Width;
            }
            else
            {
                drawWidth *= icon.Size.Width / icon.Size.Height;
            }

            DrawImageFill(
                context,
                icon,
                new Rect(bounds.X + 7 + ((box - drawWidth) / 2), bounds.Y + 2 + ((box - drawHeight) / 2), drawWidth, drawHeight));
        }

        // DrawAttemptCount: right-aligned 5 from the right, at the bottom of a 40 high label that
        // ends at the component's bottom.
        double countWidth = 0;
        if (!string.IsNullOrEmpty(visual.Value))
        {
            countWidth = MakeText(visual.Value, TextFontSize, visual.ValueBrush, TextFontWeight, TextFontFamily, TextFontStyle).Width;
            DrawTextInRect(
                context,
                visual.Value,
                new Rect(bounds.X, bounds.Bottom - 40, Math.Max(0, width - 5), 40),
                TextFontSize,
                visual.ValueBrush,
                TextFontWeight,
                alignCenter: false,
                alignRight: true,
                TextFontFamily,
                verticalAlignment: VerticalAlignment.Bottom,
                style: TextFontStyle);
        }

        // CalculatePadding: 5 at both ends, the icon's room at the start, the count's room after
        // the category (after the game name when there is no category), in a vertical layout.
        double start = 5 + (showIcon ? height + 3 : 0);
        double titleEnd = 5;
        double categoryEnd = 5;
        if (_layout?.Mode != LayoutMode.Horizontal && !string.IsNullOrEmpty(visual.Value))
        {
            if (string.IsNullOrEmpty(visual.SecondaryLabel))
            {
                titleEnd += countWidth;
            }
            else
            {
                categoryEnd += countWidth;
            }
        }

        bool center = settings.TextAlignment == 2 || (settings.TextAlignment == 0 && !showIcon);
        DrawTitleLine(
            context,
            bounds,
            visual.Label,
            visual.LabelBrush,
            start,
            titleEnd,
            center,
            string.IsNullOrEmpty(visual.SecondaryLabel) ? VerticalAlignment.Center : VerticalAlignment.Top);
        DrawTitleLine(
            context,
            bounds,
            visual.SecondaryLabel,
            visual.SecondaryLabelBrush,
            start,
            categoryEnd,
            center,
            string.IsNullOrEmpty(visual.Label) ? VerticalAlignment.Center : VerticalAlignment.Bottom);
    }

    /// <summary>
    /// DrawGameName / DrawCategoryName: one name in the Text font over the component's height,
    /// left-aligned after the start padding or centered (calculateCenteredPositionAndWidth: in the
    /// middle of the component, pushed away from the paddings, at the start when it does not fit).
    /// </summary>
    private void DrawTitleLine(
        DrawingContext context,
        Rect bounds,
        string text,
        IBrush brush,
        double startPadding,
        double endPadding,
        bool center,
        VerticalAlignment verticalAlignment)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        double total = bounds.Width;
        double position = startPadding;
        if (center)
        {
            double textWidth = MakeText(text, TextFontSize, brush, TextFontWeight, TextFontFamily, TextFontStyle).Width;
            if (startPadding + textWidth + endPadding <= total)
            {
                position = Math.Max((total - textWidth) / 2, startPadding);
                if (position + textWidth > total - endPadding)
                {
                    position = total - endPadding - textWidth;
                }
            }
        }

        DrawTextInRect(
            context,
            text,
            new Rect(bounds.X + position, bounds.Y, Math.Max(0, total - endPadding - position), bounds.Height),
            TextFontSize,
            brush,
            TextFontWeight,
            alignCenter: false,
            fontFamily: TextFontFamily,
            verticalAlignment: verticalAlignment,
            style: TextFontStyle);
    }

    private void DrawSplits(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        SplitsComponentSettings settings)
    {
        // SplitsComponent.DrawBackground: Plain / Vertical / Horizontal paint the whole component
        // (nothing when both colors are transparent); Alternating is painted row by row.
        if (!IsAlternating(settings.BackgroundGradient)
            && (settings.BackgroundColor.A > 0
                || (TimerPaintLogic.NormalizeGradientType(settings.BackgroundGradient) != "Plain" && settings.BackgroundColor2.A > 0)))
        {
            context.FillRectangle(GradientBrush(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2), bounds);
        }

        List<SplitRowVisual> rows = visual.Rows.Where(r => r.IsVisible).ToList();
        if (rows.Count == 0 || _layout is null)
        {
            return;
        }

        if (_layout.Mode == LayoutMode.Horizontal)
        {
            double separatorTotal = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                separatorTotal += SeparatorWidth(settings, rows.Count, i);
            }

            double rowWidth = Math.Max(1, (bounds.Width - separatorTotal) / rows.Count);
            double x = bounds.X;
            for (int i = 0; i < rows.Count; i++)
            {
                DrawHorizontalSplitRow(
                    context,
                    new Rect(x, bounds.Y, rowWidth, bounds.Height),
                    visual,
                    rows[i],
                    settings);
                x += rowWidth;
                double separator = SeparatorWidth(settings, rows.Count, i);
                if (separator > 0)
                {
                    context.FillRectangle(
                        Brush(separator > 1
                            ? _layout.Settings.SeparatorsColor
                            : _layout.Settings.ThinSeparatorsColor),
                        new Rect(x, bounds.Y, separator, bounds.Height));
                    x += separator;
                }
            }

            return;
        }

        double y = bounds.Y;
        if (settings.ShowColumnLabels)
        {
            // LabelsComponent.DrawGeneral: Alternating gives the labels row Background Color; the
            // row is as high as a split row (VerticalHeight 25 + Split Height).
            double labelsHeight = LayoutMetrics.RowHeightFor(settings.SplitHeight);
            if (AlternateBrush(settings, second: false) is { } labelsBackground)
            {
                context.FillRectangle(labelsBackground, new Rect(bounds.X, y, bounds.Width, labelsHeight));
            }

            DrawColumnLabels(context, new Rect(bounds.X, y, bounds.Width, labelsHeight), visual, settings);
            y += labelsHeight;
        }

        double rowHeight = LayoutMetrics.RowHeightFor(settings.SplitHeight);
        for (int i = 0; i < rows.Count; i++)
        {
            Rect rowBounds = new(bounds.X, y, bounds.Width, rowHeight);
            DrawVerticalSplitRow(context, rowBounds, visual, rows[i], settings);
            y += rowHeight;
            double separator = SeparatorWidth(settings, rows.Count, i);
            if (separator > 0)
            {
                context.FillRectangle(
                    Brush(separator > 1
                        ? _layout.Settings.SeparatorsColor
                        : _layout.Settings.ThinSeparatorsColor),
                    new Rect(bounds.X, y, bounds.Width, separator));
                y += separator;
            }
        }
    }

    /// <summary>
    /// LiveSplit.Subsplits SplitsComponent.DrawVertical / DrawHorizontal: the background, then
    /// the Total Splits rows with their separators. The separator before the last row is a
    /// Separator (2 high) only with Always Show Last Split and Show Separator Before Last Split;
    /// what it draws there is SplitsComponent.Update's DisplayedSize (none, thin or thick).
    /// </summary>
    private void DrawSubsplits(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        SubsplitsComponentSettings subsplits)
    {
        SplitsComponentSettings settings = subsplits.AsSplitsSettings();
        if (!IsAlternating(settings.BackgroundGradient)
            && (settings.BackgroundColor.A > 0
                || (TimerPaintLogic.NormalizeGradientType(settings.BackgroundGradient) != "Plain" && settings.BackgroundColor2.A > 0)))
        {
            context.FillRectangle(GradientBrush(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2), bounds);
        }

        List<SplitRowVisual> rows = visual.Rows.Where(r => r.IsVisible).ToList();
        if (rows.Count == 0 || _layout is null)
        {
            return;
        }

        bool separatorSlot = subsplits.AlwaysShowLastSplit && subsplits.SeparatorLastSplit && rows.Count > 1;
        double SlotSize(int afterRow) =>
            separatorSlot && afterRow == rows.Count - 2 ? 2 : settings.ShowThinSeparators ? 1 : 0;
        bool horizontal = _layout.Mode == LayoutMode.Horizontal;

        double position;
        double rowExtent;
        if (horizontal)
        {
            double separatorTotal = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                separatorTotal += SlotSize(i);
            }

            rowExtent = Math.Max(1, (bounds.Width - separatorTotal) / rows.Count);
            position = bounds.X;
        }
        else
        {
            position = bounds.Y;
            if (settings.ShowColumnLabels)
            {
                double labelsHeight = LayoutMetrics.RowHeightFor(settings.SplitHeight);
                if (AlternateBrush(settings, second: false) is { } labelsBackground)
                {
                    context.FillRectangle(labelsBackground, new Rect(bounds.X, position, bounds.Width, labelsHeight));
                }

                DrawColumnLabels(context, new Rect(bounds.X, position, bounds.Width, labelsHeight), visual, settings);
                position += labelsHeight;
            }

            rowExtent = LayoutMetrics.RowHeightFor(settings.SplitHeight);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (horizontal)
            {
                DrawHorizontalSplitRow(context, new Rect(position, bounds.Y, rowExtent, bounds.Height), visual, rows[i], settings);
            }
            else
            {
                DrawVerticalSplitRow(context, new Rect(bounds.X, position, bounds.Width, rowExtent), visual, rows[i], settings);
            }

            position += rowExtent;
            double slot = SlotSize(i);
            if (slot <= 0)
            {
                continue;
            }

            double line = slot;
            double offset = 0;
            ArgbColor color = _layout.Settings.ThinSeparatorsColor;
            if (separatorSlot && i == rows.Count - 2)
            {
                // SeparatorComponent: DisplayedSize of its 2; a thin line sits at the top of the
                // slot, or at the bottom while locked to the current last split.
                SubsplitsFrame? frame = visual.SubsplitsFrame;
                line = Math.Min(2, frame?.LastSeparatorSize ?? 2);
                color = frame?.LastSeparatorUsesSeparatorColor ?? true
                    ? _layout.Settings.SeparatorsColor
                    : _layout.Settings.ThinSeparatorsColor;
                offset = line >= 2 ? 0 : visual.LastSeparatorLockToBottom ? 2 - line : 0;
            }

            if (line > 0)
            {
                context.FillRectangle(
                    Brush(color),
                    horizontal
                        ? new Rect(position + offset, bounds.Y, line, bounds.Height)
                        : new Rect(bounds.X, position + offset, bounds.Width, line));
            }

            position += slot;
        }
    }

    /// <summary>SplitComponent.DrawGeneral: a scrolled-to split gets a white 1 px outline.</summary>
    private static void DrawHighlight(DrawingContext context, Rect bounds, SplitRowVisual row)
    {
        if (row.IsHighlight)
        {
            context.DrawRectangle(
                null,
                new Pen(Brushes.White, 1),
                new Rect(bounds.X + 0.5, bounds.Y + 0.5, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1)));
        }
    }

    /// <summary>
    /// LiveSplit.Subsplits SplitComponent.DrawHeader: the Alternating color and the header
    /// background over the row, the section icon, the section name, and at the right the total
    /// section time (TimeLabel) with the section timer (DeltaLabel) left of it. The two time
    /// slots are as wide as the widest times their formatters write (24:00:00 and 9:00:00).
    /// In horizontal mode the name is at the top and the times at the bottom.
    /// </summary>
    private void DrawSubsplitsHeaderRow(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        SplitRowVisual row,
        SplitsComponentSettings settings,
        bool horizontal)
    {
        if (_layout is null || visual.Settings is not SubsplitsComponentSettings subsplits)
        {
            return;
        }

        if (row.AlternateBrush is not null)
        {
            context.FillRectangle(row.AlternateBrush, bounds);
        }

        if (row.HeaderBrush is not null)
        {
            context.FillRectangle(row.HeaderBrush, bounds);
        }

        double left = bounds.X + LayoutMetrics.PadLeft;
        if (row.ReserveIcon ?? false)
        {
            double iconSize = LayoutMetrics.IconDrawSize(settings.IconSize, horizontal ? Math.Min(bounds.Height, LayoutMetrics.RowHeightFor(settings.SplitHeight)) : bounds.Height);
            if (row.Icon is not null)
            {
                double iconY = horizontal ? bounds.Y : bounds.Y + (bounds.Height - iconSize) / 2;
                DrawSplitIcon(context, row, settings, new Rect(left, iconY, iconSize, iconSize));
            }

            left += iconSize + 4;
        }

        // HeaderMeasureTimeLabel: "24:00:00" in the header's accuracy, measured as the monospaced
        // label it is (digits as "0", whole pixels per character), as the split rows' time column
        // is, so the section timer ends where the rows' deltas end.
        double timeSlot = MonospacedWidth(SplitColumnLogic.TimeFormatter(subsplits.HeaderAccuracy).Format(TimeSpan.FromHours(24)));
        double right = bounds.Right - LayoutMetrics.SplitsPadRight;
        double band = horizontal ? Math.Max(12, bounds.Height / 2) : bounds.Height;
        double timesTop = horizontal ? bounds.Bottom - band : bounds.Y;
        double nameRight = right;
        if (DrawTextInRect(
            context,
            row.HeaderTime,
            new Rect(right - timeSlot - 20, timesTop, timeSlot + 20, band),
            TimesFontSize,
            row.HeaderTimeBrush,
            TimesFontWeight,
            alignCenter: false,
            alignRight: true,
            TimesFontFamily,
            style: TimesFontStyle) is { } timeLeft)
        {
            nameRight = timeLeft;
        }

        double sectionRight = right - timeSlot - 5;
        if (!string.IsNullOrEmpty(row.SectionTimer))
        {
            FormattedText probe = MakeText(row.SectionTimer, TimesFontSize, Brushes.White, TimesFontWeight, TimesFontFamily, TimesFontStyle);
            double x = sectionRight - probe.Width;
            double y = PlaceVertically(new Rect(0, timesTop, 0, band), probe.Height, VerticalAlignment.Center);
            IBrush brush = row.SectionTimerBrush;
            if (row.SectionTimerGradient is { } gradient)
            {
                // From the label's top over the Timer font's GDI cell (ascent + descent), in the
                // text's own coordinates.
                (double ascent, double descent) = visual.MainDigits.WindowsCellMetricsPerEm();
                double cell = (ascent + descent) * Math.Max(1, _layout.Settings.TimerFontSizePx);
                brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, timesTop - y, RelativeUnit.Absolute),
                    EndPoint = new RelativePoint(0, timesTop + cell - y, RelativeUnit.Absolute),
                    GradientStops =
                    [
                        new GradientStop(gradient.Top, 0),
                        new GradientStop(gradient.Bottom, 1),
                    ],
                };
            }

            using (context.PushClip(new Rect(bounds.X, timesTop, Math.Max(0, sectionRight - bounds.X), band)))
            {
                context.DrawText(MakeText(row.SectionTimer, TimesFontSize, brush, TimesFontWeight, TimesFontFamily, TimesFontStyle), new Point(x, y));
            }

            nameRight = x;
        }

        double nameTop = bounds.Y;
        double nameHeight = horizontal ? band : bounds.Height;
        double nameLimit = horizontal ? bounds.Right - LayoutMetrics.SplitsPadRight : nameRight;
        double nameWidth = Math.Max(0, nameLimit - left - 4);
        DrawTextInRect(
            context,
            ChooseAlternateText(row.Name, row.AlternateNames, nameWidth, TextFontSize, TextFontWeight, TextFontFamily, TextFontStyle),
            new Rect(left, nameTop, nameWidth, nameHeight),
            TextFontSize,
            row.NameBrush,
            TextFontWeight,
            alignCenter: false,
            fontFamily: TextFontFamily,
            style: TextFontStyle);
    }

    private void DrawVerticalSplitRow(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        SplitRowVisual row,
        SplitsComponentSettings settings)
    {
        if (_layout is null)
        {
            return;
        }

        if (row.Kind == SubsplitsRowKind.Header)
        {
            DrawSubsplitsHeaderRow(context, bounds, visual, row, settings, horizontal: false);
            return;
        }

        // SplitComponent.DrawGeneral: the Alternating color, Subsplit Color, then the current
        // split's colors while the timer runs.
        DrawRowBackground(context, bounds, row, settings);
        if (row.Kind == SubsplitsRowKind.Blank)
        {
            return;
        }

        DrawHighlight(context, bounds, row);

        // SplitComponent.DrawGeneral: columns right to left, each its own width; the name runs up
        // to the leftmost time actually drawn (nameX), so empty columns give it their room.
        // Each column ends 5 left of the next one's start (curX -= width + 5), its label 20
        // wider than the column so a longer time still shows (label.Width = width + 20).
        double textRight = bounds.Right - LayoutMetrics.SplitsPadRight;
        double nameRight = textRight;
        for (int i = row.Columns.Count - 1; i >= 0; i--)
        {
            SplitCellVisual cell = row.Columns[i];
            double? textLeft = DrawTextInRect(
                context,
                cell.Text,
                new Rect(textRight - cell.Width - 20, bounds.Y, cell.Width + 20, bounds.Height),
                TimesFontSize,
                cell.Brush,
                TimesFontWeight,
                alignCenter: false,
                alignRight: true,
                TimesFontFamily,
                style: TimesFontStyle);
            if (textLeft is { } drawnLeft)
            {
                nameRight = drawnLeft;
            }

            textRight -= cell.Width + 5;
        }

        double left = bounds.X + LayoutMetrics.PadLeft + Math.Max(0, row.IndentLevel) * 20;
        if (row.ReserveIcon ?? visual.ReserveSplitIcons)
        {
            double iconSize = LayoutMetrics.IconDrawSize(settings.IconSize, bounds.Height);
            if (row.Icon is not null)
            {
                DrawSplitIcon(context, row, settings, new Rect(left, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize));
            }

            left += iconSize + 4;
        }

        double nameWidth = Math.Max(0, nameRight - left - 4);
        DrawTextInRect(
            context,
            ChooseAlternateText(row.Name, row.AlternateNames, nameWidth, TextFontSize, TextFontWeight, TextFontFamily, TextFontStyle),
            new Rect(left, bounds.Y, nameWidth, bounds.Height),
            TextFontSize,
            row.NameBrush,
            TextFontWeight,
            alignCenter: false,
            fontFamily: TextFontFamily,
            style: TextFontStyle);
    }

    /// <summary>
    /// SimpleLabel.CalculateAlternateText: while the text is wider than <paramref name="width"/>,
    /// try the alternates from longest to shortest; what is left over is cut with an ellipsis.
    /// </summary>
    private static string ChooseAlternateText(
        string text,
        IReadOnlyList<string> alternates,
        double width,
        double fontSize,
        FontWeight weight,
        FontFamily family,
        FontStyle style = FontStyle.Normal)
    {
        if (alternates.Count == 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        string actual = text;
        double actualWidth = MakeText(text, fontSize, Brushes.White, weight, family, style).Width;
        foreach (string candidate in alternates.OrderByDescending(a => a.Length))
        {
            if (width >= actualWidth)
            {
                break;
            }

            actual = candidate;
            actualWidth = MakeText(candidate, fontSize, Brushes.White, weight, family, style).Width;
        }

        return actual;
    }

    private void DrawHorizontalSplitRow(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        SplitRowVisual row,
        SplitsComponentSettings settings)
    {
        if (_layout is null)
        {
            return;
        }

        if (row.Kind == SubsplitsRowKind.Header)
        {
            DrawSubsplitsHeaderRow(context, bounds, visual, row, settings, horizontal: true);
            return;
        }

        // SplitComponent.DrawGeneral: the Alternating color, Subsplit Color, then the current
        // split's colors while the timer runs.
        DrawRowBackground(context, bounds, row, settings);
        if (row.Kind == SubsplitsRowKind.Blank)
        {
            return;
        }

        DrawHighlight(context, bounds, row);

        double x = bounds.X + LayoutMetrics.PadLeft + Math.Max(0, row.IndentLevel) * 20;
        if (row.ReserveIcon ?? visual.ReserveSplitIcons)
        {
            double iconSize = Math.Min(
                LayoutMetrics.IconDrawSize(settings.IconSize, bounds.Height),
                Math.Max(8, bounds.Width * 0.3));
            if (row.Icon is not null)
            {
                DrawSplitIcon(context, row, settings, new Rect(x, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize));
            }

            x += iconSize + 4;
        }

        double textHeight = Math.Max(12, bounds.Height / Math.Max(2, row.Columns.Count + 1));
        DrawTextInRect(
            context,
            ChooseAlternateText(row.Name, row.AlternateNames, Math.Max(0, bounds.Right - x - 4), Math.Min(TextFontSize, textHeight), TextFontWeight, TextFontFamily, TextFontStyle),
            new Rect(x, bounds.Y, Math.Max(0, bounds.Right - x - 4), textHeight),
            Math.Min(TextFontSize, textHeight),
            row.NameBrush,
            TextFontWeight,
            alignCenter: false,
            fontFamily: TextFontFamily,
            style: TextFontStyle);
        for (int i = 0; i < row.Columns.Count; i++)
        {
            DrawTextInRect(
                context,
                row.Columns[i].Text,
                new Rect(
                    x,
                    bounds.Y + textHeight * (i + 1),
                    Math.Max(0, bounds.Right - x - 4),
                    textHeight),
                Math.Min(TimesFontSize, textHeight),
                row.Columns[i].Brush,
                TimesFontWeight,
                alignCenter: false,
                alignRight: true,
                TimesFontFamily,
                style: TimesFontStyle);
        }
    }

    /// <summary>LabelsComponent: the column names in the layout's Text font, right-aligned over their columns.</summary>
    private void DrawColumnLabels(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        SplitsComponentSettings settings)
    {
        IReadOnlyList<ColumnData> columns = settings.Columns.Count > 0
            ? settings.Columns
            : ColumnData.CreateDefault();
        double textRight = bounds.Right - LayoutMetrics.SplitsPadRight;
        for (int i = columns.Count - 1; i >= 0; i--)
        {
            double width = i < visual.ColumnWidths.Length ? visual.ColumnWidths[i] : SplitsColumnRenderer.ColumnWidth(columns[i]);
            DrawTextInRect(
                context,
                columns[i].Name,
                new Rect(textRight - width, bounds.Y, width, bounds.Height),
                TextFontSize,
                Brush(settings.LabelsColor),
                TextFontWeight,
                alignCenter: false,
                alignRight: true,
                TextFontFamily,
                style: TextFontStyle);
            textRight -= width + 5;
        }
    }

    /// <summary>
    /// SplitsComponent.CalculateColumnWidths (Splits and Subsplits, 93a11b85): each column as wide
    /// as its longest possible text in the Times font. Times: "24:00:00" in the split time format,
    /// or one character more than the longest time in the run (at least 9:00:00); deltas: "+9:00:00"
    /// in the delta format, or one character more than the longest delta (at least 59:00). Text is
    /// measured as SimpleLabel's monospaced labels do: every digit as wide as "0", each character
    /// rounded to whole pixels.
    /// </summary>
    private double[] WindowsColumnWidths(
        IReadOnlyList<ColumnData> columns,
        GeneralTimeFormatter splitFormatter,
        GeneralTimeFormatter deltaFormatter)
    {
        var longestTime = new TimeSpan(9, 0, 0);
        var longestDelta = new TimeSpan(0, 0, 59, 0);
        Run? run = _state?.Run;
        if (run is not null)
        {
            foreach (Segment segment in run.Segments)
            {
                if (segment.SplitTime.RealTime is TimeSpan split && longestTime < split)
                {
                    longestTime = split;
                }

                foreach (SplitTime comparison in segment.Comparisons.Values)
                {
                    if (comparison.RealTime is TimeSpan time && longestTime < time)
                    {
                        longestTime = time;
                    }

                    if (segment.SplitTime.RealTime - comparison.RealTime is TimeSpan delta)
                    {
                        TimeSpan size = delta.Duration();
                        if (longestDelta < size)
                        {
                            longestDelta = size;
                        }
                    }
                }
            }
        }

        string measureTime = splitFormatter.Format(new TimeSpan(24, 0, 0));
        string measureDelta = deltaFormatter.Format(new TimeSpan(0, 9, 0, 0));
        double timeLabel = MonospacedWidth(measureTime);
        double deltaLabel = MonospacedWidth(measureDelta);
        double charWidth = measureTime.Length > 0 ? timeLabel / measureTime.Length : MonospacedWidth("W");
        double timeWidth = Math.Max(timeLabel, charWidth * (splitFormatter.Format(longestTime).Length + 1));
        double deltaWidth = Math.Max(deltaLabel, charWidth * (deltaFormatter.Format(longestDelta).Length + 1));
        var widths = new double[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            widths[i] = columns[i].Type switch
            {
                ColumnType.DeltaorSplitTime or ColumnType.SegmentDeltaorSegmentTime => Math.Max(deltaWidth, timeWidth),
                ColumnType.Delta or ColumnType.SegmentDelta => deltaWidth,
                ColumnType.SplitTime or ColumnType.SegmentTime => timeWidth,
                _ => MonospacedWidth("W") * (LongestCustomVariable(run, columns[i].Name) + 1),
            };
        }

        return widths;
    }

    private static int LongestCustomVariable(Run? run, string name)
    {
        if (run is null)
        {
            return 0;
        }

        int longest = run.Metadata.CustomVariableValue(name)?.Length ?? 0;
        foreach (Segment segment in run.Segments)
        {
            if (segment.CustomVariableValues.TryGetValue(name, out string? value) && !string.IsNullOrEmpty(value))
            {
                longest = Math.Max(longest, value.Length);
            }
        }

        return longest;
    }

    /// <summary>SimpleLabel.MeasureActualWidth in the Times font: digits as "0", whole pixels per character.</summary>
    private double MonospacedWidth(string text) =>
        MonospacedWidth(text, TimesFontSize, TimesFontWeight, TimesFontFamily, TimesFontStyle);

    /// <summary>SimpleLabel.MeasureActualWidth: digits as wide as "0", each character rounded to whole pixels.</summary>
    internal static double MonospacedWidth(string text, double size, FontWeight weight, FontFamily family, FontStyle style)
    {
        double total = 0;
        double? digit = null;
        foreach (char c in text)
        {
            if (char.IsDigit(c))
            {
                digit ??= Math.Round(MakeText("0", size, Brushes.White, weight, family, style).Width, MidpointRounding.AwayFromZero);
                total += digit.Value;
            }
            else
            {
                total += Math.Round(MakeText(c.ToString(), size, Brushes.White, weight, family, style).Width, MidpointRounding.AwayFromZero);
            }
        }

        return total;
    }

    private static void DrawRowBackground(DrawingContext context, Rect bounds, SplitRowVisual row, SplitsComponentSettings settings)
    {
        if (row.AlternateBrush is not null)
        {
            context.FillRectangle(row.AlternateBrush, bounds);
        }

        if (row.SubsplitBrush is not null)
        {
            context.FillRectangle(row.SubsplitBrush, bounds);
        }

        if (row.IsCurrent)
        {
            context.FillRectangle(SplitsColumnRenderer.CurrentSplitBrush(settings), bounds);
        }
    }

    private static bool IsAlternating(string? gradient) =>
        string.Equals(TimerPaintLogic.NormalizeGradientType(gradient), "Alternating", StringComparison.OrdinalIgnoreCase);

    /// <summary>A row's Alternating fill: Background Color 2 or Background Color; null when not Alternating or transparent.</summary>
    private static IBrush? AlternateBrush(SplitsComponentSettings settings, bool second)
    {
        if (!IsAlternating(settings.BackgroundGradient))
        {
            return null;
        }

        ArgbColor color = second ? settings.BackgroundColor2 : settings.BackgroundColor;
        return color.A > 0 ? Brush(color) : null;
    }

    private static double SeparatorWidth(
        SplitsComponentSettings settings,
        int rowCount,
        int rowIndex)
    {
        bool beforeLast = settings.SeparatorLastSplit
            && rowCount > 1
            && rowIndex == rowCount - 2;
        if (beforeLast)
        {
            return 2;
        }

        return settings.ShowThinSeparators ? 1 : 0;
    }

    private void DrawTimer(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        TimerComponentSettings settings)
    {
        // Timer.DrawBackground: the Background colors and gradient type (the delta-color types
        // tint from the timer color); nothing is painted when both are transparent. Show Gradient
        // is about the digits (DrawUnscaled), not the background.
        var (background1, background2, _) = TimerPaintLogic.ResolveBackgroundColors(
            settings.BackgroundGradient,
            visual.TimerColor,
            settings.BackgroundColor,
            settings.BackgroundColor2);
        if (background1.A > 0
            || (TimerPaintLogic.NormalizeGradientType(settings.BackgroundGradient) != "Plain" && background2.A > 0))
        {
            context.FillRectangle(GradientBrush(settings.BackgroundGradient, background1, background2), bounds);
        }

        // Timer.DrawGeneral, the same drawing as the Detailed Timer's two timers.
        DrawTimerDigits(context, bounds, visual.MainDigits, settings.DecimalsSize, settings.CenterTimer);
    }

    private void DrawDetailedTimer(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        DetailedTimerComponentSettings settings)
    {
        if (_state is null || _layout is null)
        {
            return;
        }

        ArgbColor timerColor = settings.OverrideTimerColors
            ? settings.TimerColor
            : LiveSplitStateHelper.GetTimerColor(_state, _layout.Settings);
        var (background1, background2, _) = TimerPaintLogic.ResolveBackgroundColors(
            settings.BackgroundGradient,
            timerColor,
            settings.BackgroundColor,
            settings.BackgroundColor2);
        if (TimerPaintLogic.UsesDeltaColor(settings.BackgroundGradient)
            && background1.A == 0
            && background2.A == 0)
        {
            background1 = background2 = ArgbColor.FromArgb(255, 22, 22, 22);
        }

        FillConfiguredBackground(
            context,
            bounds,
            settings.BackgroundGradient,
            background1,
            background2);

        // DetailedTimer.DrawGeneral / DrawVertical / DrawHorizontal (93a11b85), in component
        // coordinates: x from the component's left edge, no padding in vertical mode.
        double width = bounds.Width;
        double height = bounds.Height;
        double ratio = Math.Clamp(settings.SegmentTimerSizeRatio, 0, 100) / 100.0;
        // Segment Timer Size is the segment timer's share of the component height; the main
        // timer gets the rest, so it shrinks as the segment timer grows (as on Windows).
        double mainHeight = height * (1 - ratio);
        double segmentHeight = height * ratio;

        // The icon: at most IconSize and width - 14, aspect kept, centered in the whole height.
        // It does not move the timers (they use the full width); only labels and name start after it.
        int iconWidth = 0;
        if (visual.Icon is { } icon && icon.Size.Width > 0 && icon.Size.Height > 0)
        {
            double drawSize = Math.Max(0, Math.Min(settings.IconSize, width - 14));
            double drawWidth = drawSize;
            double drawHeight = drawSize;
            if (icon.Size.Width > icon.Size.Height)
            {
                drawHeight *= icon.Size.Height / icon.Size.Width;
            }
            else
            {
                drawWidth *= icon.Size.Width / icon.Size.Height;
            }

            context.DrawImage(
                icon,
                new Rect(
                    bounds.X + 7 + ((drawSize - drawWidth) / 2),
                    bounds.Y + ((height - drawSize) / 2) + ((drawSize - drawHeight) / 2),
                    drawWidth,
                    drawHeight));
            iconWidth = (int)(drawSize + 7.5);
        }

        // InternalComponent (a Timer) in the top band, SegmentTimer in the band below it. Windows
        // draws them after DrawGeneral (labels, times, split name), so they are laid out here and
        // painted last.
        Rect mainBox = new(bounds.X, bounds.Y, width, mainHeight);
        Rect segmentBox = new(bounds.X, bounds.Y + mainHeight, width, segmentHeight);
        TimerDigitsLayout mainTimer = LayoutTimerDigits(mainBox, visual.MainDigits, settings.DecimalsSize, centerTimer: false);
        TimerDigitsLayout segmentTimer = LayoutTimerDigits(segmentBox, visual.SegmentDigits, settings.SegmentTimerDecimalsSize, centerTimer: false);
        double mainTimerWidth = mainTimer.Width;

        LastDetailedTimerText = _state.CurrentSplitIndex >= 0
            ? DrawDetailedTimerComparisons(context, bounds, visual, settings, mainHeight, segmentHeight, ratio, iconWidth, segmentTimer)
            : null;

        if (_state.CurrentSplitIndex >= 0 && !string.IsNullOrEmpty(visual.Label))
        {
            // SplitName: the main band, up to the main timer's drawn text.
            DrawTextInRect(
                context,
                visual.Label,
                new Rect(bounds.X + iconWidth + 5, bounds.Y, Math.Max(0, width - mainTimerWidth - iconWidth - 5), mainHeight),
                settings.SplitNameFontSizePx,
                Brush(settings.SplitNameColor),
                settings.SplitNameFontBold ? FontWeight.Bold : FontWeight.Normal,
                alignCenter: false,
                fontFamily: ResolveUiFont(settings.SplitNameFontFamily),
                style: settings.SplitNameFontItalic ? FontStyle.Italic : FontStyle.Normal);
        }

        PaintTimerDigits(context, mainTimer);
        PaintTimerDigits(context, segmentTimer);
    }

    /// <summary>
    /// The comparison lines of DetailedTimer.DrawGeneral (93a11b85): label and time for
    /// Comparison 1 and (unless hidden) Comparison 2, each in its half of the segment band (the
    /// first one in all of it when the second is hidden), in the Segment Labels / Segment Times
    /// fonts at their own size. The labels run from the icon to the segment timer (width -
    /// SegmentTimer.ActualWidth, cut with an ellipsis), the times in one column after the wider
    /// label + 10 up to the same edge, cut with "..." when they do not fit (SimpleLabel.CutOff,
    /// monospaced). One change from Windows (Basten 17 Oct): the last line sits on the segment
    /// timer's baseline instead of the middle of its half; both lines move together.
    /// </summary>
    private static DetailedTimerText DrawDetailedTimerComparisons(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        DetailedTimerComponentSettings settings,
        double mainHeight,
        double segmentHeight,
        double ratio,
        int iconWidth,
        TimerDigitsLayout segmentTimer)
    {
        bool hideSecond = visual.HideSecondComparison;
        double lineHeight = segmentHeight / 2;
        double firstHeight = hideSecond ? segmentHeight : lineHeight;
        double firstY = bounds.Y + mainHeight;
        double secondY = bounds.Y + (bounds.Height * (1 - (ratio / 2)));
        double slotRight = bounds.Right - segmentTimer.Width;
        double labelX = bounds.X + 5 + iconWidth;
        double labelWidth = Math.Max(0, slotRight - labelX);
        double labelSize = settings.SegmentLabelsFontSizePx;
        FontWeight labelWeight = settings.SegmentLabelsFontBold ? FontWeight.Bold : FontWeight.Normal;
        FontFamily labelFamily = ResolveUiFont(settings.SegmentLabelsFontFamily);
        FontStyle labelStyle = settings.SegmentLabelsFontItalic ? FontStyle.Italic : FontStyle.Normal;
        IBrush labelBrush = Brush(settings.SegmentLabelsColor);
        bool showFirst = !string.IsNullOrEmpty(visual.SecondaryLabel);
        bool showSecond = !hideSecond && !string.IsNullOrEmpty(visual.TertiaryLabel);

        // Both times start in one column: offset = the wider label (whole text) + 10.
        double offset = Math.Max(
            showFirst ? MakeText(visual.SecondaryLabel, labelSize, labelBrush, labelWeight, labelFamily, labelStyle).Width : 0,
            showSecond ? MakeText(visual.TertiaryLabel, labelSize, labelBrush, labelWeight, labelFamily, labelStyle).Width : 0)
            + 10;
        double timesX = bounds.X + offset + iconWidth;
        double timesWidth = Math.Max(0, slotRight - timesX);
        double timesSize = settings.SegmentTimesFontSizePx;
        FontWeight timesWeight = settings.SegmentTimesFontBold ? FontWeight.Bold : FontWeight.Normal;
        FontFamily timesFamily = ResolveUiFont(settings.SegmentTimesFontFamily);
        FontStyle timesStyle = settings.SegmentTimesFontItalic ? FontStyle.Italic : FontStyle.Normal;
        IBrush timesBrush = Brush(settings.SegmentTimesColor);
        string firstValue = showFirst ? CutOff(visual.SecondaryValue, timesWidth, timesSize, timesWeight, timesFamily, timesStyle) : "";
        string secondValue = showSecond ? CutOff(visual.TertiaryValue, timesWidth, timesSize, timesWeight, timesFamily, timesStyle) : "";

        // Basten 17 Oct: the last line's label and time sit on the segment timer's baseline (both
        // lines move together, keeping their spacing), not centered in their half-slot.
        string? lastLabel = showSecond ? visual.TertiaryLabel : showFirst ? visual.SecondaryLabel : null;
        string? lastValue = showSecond ? visual.TertiaryValue : showFirst ? visual.SecondaryValue : null;
        double lastY = showSecond ? secondY : firstY;
        double lastHeight = showSecond ? lineHeight : firstHeight;
        double BaselineShift(string? text, double size, FontWeight weight, FontFamily family, FontStyle style)
        {
            if (string.IsNullOrEmpty(text) || segmentTimer.Baseline is not { } timerBaseline)
            {
                return 0;
            }

            FormattedText measured = MakeText(text, size, labelBrush, weight, family, style);
            return timerBaseline - (lastY + ((lastHeight - measured.Height) / 2) + measured.Baseline);
        }

        double labelShift = BaselineShift(lastLabel, labelSize, labelWeight, labelFamily, labelStyle);
        double timesShift = BaselineShift(lastValue, timesSize, timesWeight, timesFamily, timesStyle);

        if (showFirst)
        {
            DrawTextInRect(context, visual.SecondaryLabel, new Rect(labelX, firstY + labelShift, labelWidth, firstHeight),
                labelSize, labelBrush, labelWeight, alignCenter: false, fontFamily: labelFamily, style: labelStyle);
        }

        if (showSecond)
        {
            DrawTextInRect(context, visual.TertiaryLabel, new Rect(labelX, secondY + labelShift, labelWidth, lineHeight),
                labelSize, labelBrush, labelWeight, alignCenter: false, fontFamily: labelFamily, style: labelStyle);
        }

        if (showFirst)
        {
            DrawTextInRect(context, firstValue, new Rect(timesX, firstY + timesShift, timesWidth, firstHeight),
                timesSize, timesBrush, timesWeight, alignCenter: false, fontFamily: timesFamily, ellipsis: false, style: timesStyle);
        }

        if (showSecond)
        {
            DrawTextInRect(context, secondValue, new Rect(timesX, secondY + timesShift, timesWidth, lineHeight),
                timesSize, timesBrush, timesWeight, alignCenter: false, fontFamily: timesFamily, ellipsis: false, style: timesStyle);
        }

        double TextHeight(string? text, double size, FontWeight weight, FontFamily family, FontStyle style) =>
            string.IsNullOrEmpty(text) ? 0 : MakeText(text, size, timesBrush, weight, family, style).Height;
        double TextWidth(string? text, double size, FontWeight weight, FontFamily family, FontStyle style) =>
            string.IsNullOrEmpty(text) ? 0 : MakeText(text, size, timesBrush, weight, family, style).Width;
        return new DetailedTimerText(
            labelSize,
            timesSize,
            Math.Max(
                showFirst ? TextHeight(visual.SecondaryLabel, labelSize, labelWeight, labelFamily, labelStyle) : 0,
                showSecond ? TextHeight(visual.TertiaryLabel, labelSize, labelWeight, labelFamily, labelStyle) : 0),
            Math.Max(
                showFirst ? TextHeight(visual.SecondaryValue, timesSize, timesWeight, timesFamily, timesStyle) : 0,
                showSecond ? TextHeight(visual.TertiaryValue, timesSize, timesWeight, timesFamily, timesStyle) : 0),
            showSecond ? lineHeight : firstHeight,
            timesX - bounds.X + Math.Max(
                TextWidth(firstValue, timesSize, timesWeight, timesFamily, timesStyle),
                TextWidth(secondValue, timesSize, timesWeight, timesFamily, timesStyle)),
            slotRight - bounds.X,
            segmentTimer.Left - bounds.X,
            firstValue,
            secondValue,
            lastValue is null ? null : lastY + timesShift + ((lastHeight - TextHeight(lastValue, timesSize, timesWeight, timesFamily, timesStyle)) / 2)
                + MakeText(lastValue, timesSize, timesBrush, timesWeight, timesFamily, timesStyle).Baseline,
            segmentTimer.Baseline);
    }

    /// <summary>
    /// What the Detailed Timer's comparison lines were drawn with: the label and time sizes, the
    /// tallest label / time line, the half-slot's height, in component coordinates the times'
    /// right edge, where the slots end (width - SegmentTimer.ActualWidth) and where the segment
    /// timer's digits start, the times as drawn, and the last time's / segment timer's baselines.
    /// </summary>
    internal sealed record DetailedTimerText(
        double LabelSize,
        double TimesSize,
        double LabelLineHeight,
        double TimesLineHeight,
        double SlotHeight,
        double TimesRight,
        double SlotRight,
        double SegmentTimerLeft,
        string FirstTime,
        string SecondTime,
        double? LastTimeBaseline,
        double? SegmentTimerBaseline);

    /// <summary>The last Detailed Timer's comparison lines (null: none drawn, the timer has not started).</summary>
    internal DetailedTimerText? LastDetailedTimerText { get; private set; }

    /// <summary>
    /// SimpleLabel.CutOff for a monospaced label: the text when it is narrower than
    /// <paramref name="width"/>; otherwise characters come off the end, with "..." added, until it
    /// is; nothing when even "..." does not fit.
    /// </summary>
    internal static string CutOff(string? text, double width, double size, FontWeight weight, FontFamily family, FontStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        double actualWidth = MonospacedWidth(text, size, weight, family, style);
        if (actualWidth < width)
        {
            return text;
        }

        string cut = text;
        while (actualWidth >= width && cut.Length > 0)
        {
            cut = cut[..^1];
            actualWidth = MonospacedWidth(cut + "...", size, weight, family, style);
        }

        return actualWidth >= width ? "" : cut + "...";
    }

    /// <summary>
    /// Timer.DrawGeneral for one timer box (see <see cref="TimerDrawGeometry"/>): the digits laid
    /// out at the layout's timer font size in the 45-high "88:88:88" box, scaled uniformly to fit
    /// <paramref name="box"/>, right-aligned (or centered) and centered vertically. Returns the
    /// drawn text's width (Timer.ActualWidth), which the labels beside it stop at.
    /// </summary>
    private double DrawTimerDigits(
        DrawingContext context,
        Rect box,
        TimerDigitsView digits,
        double decimalsSize,
        bool centerTimer)
    {
        TimerDigitsLayout layout = LayoutTimerDigits(box, digits, decimalsSize, centerTimer);
        PaintTimerDigits(context, layout);
        return layout.Width;
    }

    /// <summary>
    /// Where <see cref="DrawTimerDigits"/> puts the digits (it sets the view's font sizes): the
    /// drawn text's width and left edge, and the point the text is painted at.
    /// </summary>
    private TimerDigitsLayout LayoutTimerDigits(
        Rect box,
        TimerDigitsView digits,
        double decimalsSize,
        bool centerTimer)
    {
        if (box.Width <= 0 || box.Height <= 0)
        {
            return new TimerDigitsLayout(digits, 0, box.Right, null, null);
        }

        double fontSize = _layout?.Settings.TimerFontSizePx > 0 ? _layout.Settings.TimerFontSizePx : 43.75;
        double decimalsFontSize = TimerDrawGeometry.DecimalsFontSize(fontSize, decimalsSize);
        double unscaledWidth = TimerDrawGeometry.UnscaledWidth(
            digits.StripWidth(TimerDigitsView.WidthMeasureSample, "", fontSize, decimalsFontSize),
            digits.StripWidth("", digits.SmallText ?? "", fontSize, decimalsFontSize));
        double scale = TimerDrawGeometry.Scale(box.Width, box.Height, unscaledWidth);
        digits.BigFontSize = fontSize * scale;
        digits.SmallFontSize = decimalsFontSize * scale;
        Size size = digits.MeasureContent();
        double right = box.X + TimerDrawGeometry.TextRight(box.Width, unscaledWidth, scale, centerTimer);
        (double ascent, double descent) = digits.WindowsCellMetricsPerEm();
        double baseline = TimerDrawGeometry.BaselineY(box.Y, box.Height, digits.BigFontSize, ascent, descent);
        return new TimerDigitsLayout(
            digits,
            size.Width,
            right - size.Width,
            new Point(right - size.Width, baseline - digits.BigBaselineOffset),
            baseline);
    }

    private void PaintTimerDigits(DrawingContext context, TimerDigitsLayout layout)
    {
        if (layout.Origin is { } origin)
        {
            PaintTimerDigits(context, layout.Digits, origin);
        }
    }

    /// <summary>A laid-out timer: its digits, drawn width, left edge and paint origin (null: nothing to draw).</summary>
    private readonly record struct TimerDigitsLayout(TimerDigitsView Digits, double Width, double Left, Point? Origin, double? Baseline);

    /// <summary>
    /// InfoTextComponent.DrawVertical / DrawTwoRows (93a11b85) for Previous Segment, Sum of Best,
    /// Possible Time Save and Text. One row: the value at the right, the name up to it (falling
    /// back to its shorter forms, then an ellipsis). Display 2 Rows, and always in horizontal
    /// mode: the name at the top, the value at the bottom right.
    /// </summary>
    private void DrawInfo(
        DrawingContext context,
        Rect bounds,
        ComponentVisual visual,
        string gradient,
        ArgbColor background1,
        ArgbColor background2)
    {
        FillConfiguredBackground(
            context,
            bounds,
            gradient,
            background1,
            background2,
            transparentFallback: true);
        double inset = LayoutMetrics.PadLeft;
        bool twoRows = visual.TwoRows || _layout?.Mode == LayoutMode.Horizontal;
        if (visual.CenterLabel || string.IsNullOrEmpty(visual.Value))
        {
            // TextComponent with one text empty: both labels centered. World Record's Centered
            // Text takes the longest of its texts that fits.
            double centeredWidth = Math.Max(0, bounds.Width - inset * 2);
            DrawTextInRect(
                context,
                ChooseAlternateText(visual.Label, visual.AlternateLabels, centeredWidth, TextFontSize, TextFontWeight, TextFontFamily, TextFontStyle),
                new Rect(bounds.X + inset, bounds.Y, Math.Max(0, bounds.Width - inset * 2), bounds.Height),
                TextFontSize,
                visual.LabelBrush,
                TextFontWeight,
                alignCenter: true,
                fontFamily: TextFontFamily,
            style: TextFontStyle);
            return;
        }

        double valueSize = visual.ValueUsesTextFont ? TextFontSize : TimesFontSize;
        FontWeight valueWeight = visual.ValueUsesTextFont ? TextFontWeight : TimesFontWeight;
        FontFamily valueFamily = visual.ValueUsesTextFont ? TextFontFamily : TimesFontFamily;
        FontStyle valueStyle = visual.ValueUsesTextFont ? TextFontStyle : TimesFontStyle;
        double valueRight = bounds.Right - LayoutMetrics.SplitsPadRight;
        double nameWidth = twoRows
            ? Math.Max(0, bounds.Width - inset * 2)
            : Math.Max(0, valueRight - MakeText(visual.Value, valueSize, visual.ValueBrush, valueWeight, valueFamily, valueStyle).Width - 4 - (bounds.X + inset));
        DrawTextInRect(
            context,
            ChooseAlternateText(visual.Label, visual.AlternateLabels, nameWidth, TextFontSize, TextFontWeight, TextFontFamily, TextFontStyle),
            new Rect(bounds.X + inset, bounds.Y, nameWidth, bounds.Height),
            TextFontSize,
            visual.LabelBrush,
            TextFontWeight,
            alignCenter: false,
            fontFamily: TextFontFamily,
            verticalAlignment: twoRows ? VerticalAlignment.Top : VerticalAlignment.Center,
            style: TextFontStyle);
        DrawTextInRect(
            context,
            visual.Value,
            new Rect(bounds.X + inset, bounds.Y, Math.Max(0, valueRight - (bounds.X + inset)), bounds.Height),
            valueSize,
            visual.ValueBrush,
            valueWeight,
            alignCenter: false,
            alignRight: true,
            valueFamily,
            verticalAlignment: twoRows ? VerticalAlignment.Bottom : VerticalAlignment.Center,
            style: valueStyle);
    }

    private void PaintTimerDigits(
        DrawingContext context,
        TimerDigitsView digits,
        Point origin)
    {
        if (_layout is null)
        {
            return;
        }

        IBrush? foreground = digits.DigitBrush;
        if (_layout.Settings.DropShadows)
        {
            digits.DigitBrush = Brush(_layout.Settings.ShadowsColor);
            digits.PaintAt(context, new Point(origin.X + 1, origin.Y + 1));
        }

        ArgbColor outline = _layout.Settings.TextOutlineColor;
        if (outline.A > 0)
        {
            digits.DigitBrush = Brush(outline);
            digits.PaintAt(context, new Point(origin.X - 1, origin.Y));
            digits.PaintAt(context, new Point(origin.X + 1, origin.Y));
            digits.PaintAt(context, new Point(origin.X, origin.Y - 1));
            digits.PaintAt(context, new Point(origin.X, origin.Y + 1));
        }

        digits.DigitBrush = foreground;
        digits.PaintAt(context, origin, withGradient: true);
    }

    private void FillConfiguredBackground(
        DrawingContext context,
        Rect bounds,
        string gradient,
        ArgbColor first,
        ArgbColor second,
        bool transparentFallback = false)
    {
        if (transparentFallback
            && first.A == 0
            && (TimerPaintLogic.NormalizeGradientType(gradient).Equals(
                    "Plain",
                    StringComparison.OrdinalIgnoreCase)
                || second.A == 0))
        {
            return;
        }

        context.FillRectangle(GradientBrush(gradient, first, second), bounds);
    }

    private double TextFontSize =>
        Math.Clamp(_layout?.Settings.TextFontSizePx > 0
            ? _layout.Settings.TextFontSizePx
            : 16, 1, 200);

    private FontWeight TextFontWeight =>
        _layout?.Settings.TextFontBold == true ? FontWeight.Bold : FontWeight.Normal;

    private FontFamily TextFontFamily =>
        new(ResolveUiFont(_layout?.Settings.TextFontFamily));

    private FontStyle TextFontStyle =>
        _layout?.Settings.TextFontItalic == true ? FontStyle.Italic : FontStyle.Normal;

    private FontStyle TimesFontStyle =>
        _layout?.Settings.TimesFontItalic == true ? FontStyle.Italic : FontStyle.Normal;

    private double TimesFontSize =>
        Math.Clamp(_layout?.Settings.TimesFontSizePx > 0
            ? _layout.Settings.TimesFontSizePx
            : 16, 1, 200);

    private FontWeight TimesFontWeight =>
        _layout?.Settings.TimesFontBold == true ? FontWeight.Bold : FontWeight.Normal;

    private FontFamily TimesFontFamily =>
        new(ResolveUiFont(_layout?.Settings.TimesFontFamily));

    /// <summary>
    /// Draws one line of text in <paramref name="bounds"/> and returns the left edge of what was
    /// drawn (null when nothing was). Text that does not fit is cut with an ellipsis, as Windows
    /// SimpleLabel does (StringTrimming.EllipsisCharacter, NoWrap); right-aligned times are drawn
    /// whole and only clipped, as Windows draws monospaced times character by character.
    /// </summary>
    private static double? DrawTextInRect(
        DrawingContext context,
        string text,
        Rect bounds,
        double fontSize,
        IBrush brush,
        FontWeight weight,
        bool alignCenter,
        bool alignRight = false,
        FontFamily? fontFamily = null,
        bool? ellipsis = null,
        VerticalAlignment verticalAlignment = VerticalAlignment.Center,
        FontStyle style = FontStyle.Normal)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        FormattedText formatted = MakeText(text, fontSize, brush, weight, fontFamily, style);
        if ((ellipsis ?? !alignRight) && formatted.Width > bounds.Width)
        {
            using TextLayout trimmed = new(
                text,
                new Typeface(fontFamily ?? new FontFamily(AppFonts.LayoutTextFamily), style, weight),
                fontSize,
                brush,
                textWrapping: TextWrapping.NoWrap,
                textTrimming: TextTrimming.CharacterEllipsis,
                maxWidth: bounds.Width,
                maxLines: 1);
            double trimmedY = PlaceVertically(bounds, trimmed.Height, verticalAlignment);
            using (context.PushClip(bounds))
            {
                trimmed.Draw(context, new Point(bounds.X, trimmedY));
            }

            return bounds.X;
        }

        double x = alignCenter
            ? bounds.X + (bounds.Width - formatted.Width) / 2
            : alignRight
                ? bounds.Right - formatted.Width
                : bounds.X;
        double y = PlaceVertically(bounds, formatted.Height, verticalAlignment);
        using (context.PushClip(bounds))
        {
            context.DrawText(formatted, new Point(x, y));
        }

        return x;
    }

    /// <summary>StringFormat.LineAlignment: Near puts the line at the top, Far at the bottom.</summary>
    private static double PlaceVertically(Rect bounds, double lineHeight, VerticalAlignment alignment) =>
        alignment switch
        {
            VerticalAlignment.Top => bounds.Y,
            VerticalAlignment.Bottom => bounds.Bottom - lineHeight,
            _ => bounds.Y + ((bounds.Height - lineHeight) / 2),
        };

    private static FormattedText MakeText(
        string text,
        double fontSize,
        IBrush brush,
        FontWeight weight,
        FontFamily? fontFamily = null,
        FontStyle style = FontStyle.Normal) =>
        new(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily ?? new FontFamily(AppFonts.LayoutTextFamily), style, weight),
            fontSize,
            brush);

    private static IBrush GradientBrush(string type, ArgbColor first, ArgbColor second)
    {
        string normalized = TimerPaintLogic.NormalizeGradientType(type);
        if (normalized.Equals("Plain", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("PlainWithDeltaColor", StringComparison.OrdinalIgnoreCase))
        {
            return Brush(first);
        }

        bool horizontal = TimerPaintLogic.IsHorizontalGradient(normalized);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = horizontal
                ? new RelativePoint(1, 0, RelativeUnit.Relative)
                : new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(ToColor(first), 0),
                new GradientStop(ToColor(second), 1),
            ],
        };
    }

    /// <summary>
    /// Timer.DrawUnscaled: the digits in the timer color, or with Show Gradient from a lighter,
    /// paler top to a darker bottom (the brushes depend on the drawn size, so the digits view
    /// makes them when it paints).
    /// </summary>
    private static void SetDigitColor(TimerDigitsView digits, ArgbColor color, bool showGradient)
    {
        digits.DigitBrush = Brush(color);
        if (showGradient)
        {
            var (top, bottom) = TimerPaintLogic.DigitGradientColors(color);
            digits.DigitGradient = (ToColor(top), ToColor(bottom));
        }
        else
        {
            digits.DigitGradient = null;
        }
    }

    /// <summary>
    /// SplitComponent.DrawGeneral's icon: the whole icon fitted in its box (aspect kept), and with
    /// Icon Shadows on, IconShadow's blurred shadow in a box 5 / 4 as big, 0.7 px up and left.
    /// </summary>
    private void DrawSplitIcon(DrawingContext context, SplitRowVisual row, SplitsComponentSettings settings, Rect box)
    {
        if (row.Icon is not { } icon || icon.Size.Width <= 0 || icon.Size.Height <= 0 || _layout is null)
        {
            return;
        }

        double ratio = icon.Size.Width > icon.Size.Height
            ? icon.Size.Height / icon.Size.Width
            : icon.Size.Width / icon.Size.Height;
        bool wide = icon.Size.Width > icon.Size.Height;
        double drawWidth = wide ? box.Width : box.Width * ratio;
        double drawHeight = wide ? box.Height * ratio : box.Height;
        if (settings.IconShadows && _icons.GetShadow(row.IconXml, _layout.Settings.ShadowsColor) is { } shadow)
        {
            double shadowBox = box.Width * 5 / 4;
            double shadowWidth = wide ? shadowBox : shadowBox * ratio;
            double shadowHeight = wide ? shadowBox * ratio : shadowBox;
            context.DrawImage(
                shadow,
                new Rect(
                    box.X + ((shadowBox - shadowWidth) / 2) - 0.7,
                    box.Y + ((shadowBox - shadowHeight) / 2) - 0.7,
                    shadowWidth,
                    shadowHeight));
        }

        context.DrawImage(
            icon,
            new Rect(box.X + ((box.Width - drawWidth) / 2), box.Y + ((box.Height - drawHeight) / 2), drawWidth, drawHeight));
    }

    private static void DrawImageFill(DrawingContext context, Bitmap bitmap, Rect destination)
    {
        Size size = bitmap.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        double sourceAspect = size.Width / size.Height;
        double destinationAspect = destination.Width / destination.Height;
        Rect source;
        if (sourceAspect > destinationAspect)
        {
            double width = size.Height * destinationAspect;
            source = new Rect((size.Width - width) / 2, 0, width, size.Height);
        }
        else
        {
            double height = size.Width / destinationAspect;
            source = new Rect(0, (size.Height - height) / 2, size.Width, height);
        }

        context.DrawImage(bitmap, source, destination);
    }

    private TimingMethod ResolveTimingMethod(string setting) =>
        TimerComponentLogic.ResolveTimingMethod(setting, _state?.CurrentTimingMethod ?? TimingMethod.RealTime);

    /// <summary>Timer.Update: a time is split at the decimal point; no time shows a dash.</summary>
    private static void AssignTimerValue(TimerDigitsView digits, GeneralTimeFormatter formatter, TimeSpan? value)
    {
        if (value is null)
        {
            digits.BigText = "";
            digits.SmallText = TimeFormatConstants.Dash;
            return;
        }

        AssignTimerText(digits, formatter.Format(value));
    }

    private static void AssignTimerText(TimerDigitsView digits, string formatted)
    {
        int dot = formatted.IndexOf('.');
        if (dot < 0)
        {
            digits.BigText = formatted;
            digits.SmallText = "";
            return;
        }

        digits.BigText = formatted[..dot];
        digits.SmallText = formatted[dot..];
    }

    private static string ResolveUiFont(string? family) => AppFonts.ForTextFont(family);

    private static FontFamily ResolveTimerFont(string? family) => new(AppFonts.ForTimerFont(family));

    private static Rect ToRect(TimerLayoutRect rect) =>
        new(rect.X, rect.Y, rect.Width, rect.Height);

    private static IBrush Brush(ArgbColor color) => new SolidColorBrush(ToColor(color));

    private static Color ToColor(ArgbColor color) =>
        Color.FromArgb(color.A, color.R, color.G, color.B);

    private readonly record struct RowBinding(
        int Index,
        string? DisplayName = null,
        int IndentLevel = 0);

    private sealed class ComponentVisual
    {
        private ComponentVisual(string path, object settings)
        {
            Path = path;
            Settings = settings;
            MainDigits = NewDigits();
            SegmentDigits = NewDigits();
        }

        public string Path { get; }
        public object Settings { get; }

        /// <summary>Graph: GraphComponent.Calculate's deltas for this frame.</summary>
        public GraphData? Graph { get; set; }

        /// <summary>The layout component this draws (a Counter keeps its count on it).</summary>
        public LayoutComponent? Source { get; private init; }

        private Run? _playtimeRun;
        private int _playtimeAttempts = -1;
        private TimeSpan _playtimeHistory;

        /// <summary>
        /// Total Playtime's finished attempts, worked out again only when the run or its number of
        /// attempts changes (TotalPlaytimeComponent.Update's LastRun / LastAttemptCount).
        /// </summary>
        public TimeSpan AttemptHistoryPlaytime(Run run)
        {
            if (!ReferenceEquals(run, _playtimeRun) || run.AttemptHistory.Count != _playtimeAttempts)
            {
                _playtimeHistory = InfoComponentLogic.AttemptHistoryPlaytime(run);
                _playtimeRun = run;
                _playtimeAttempts = run.AttemptHistory.Count;
            }

            return _playtimeHistory;
        }

        /// <summary>Subsplits: the component's row choice and scroll position.</summary>
        public SubsplitsLogic? Subsplits { get; private set; }

        /// <summary>Subsplits as component DLLs see it (LiveSplitState.Layout).</summary>
        public SubsplitsHostView? HostSubsplits { get; set; }

        /// <summary>Splits: the component's scroll position.</summary>
        public SplitsLogic? Splits { get; set; }

        /// <summary>Splits / Subsplits: the columns' widths (SplitsComponent.CalculateColumnWidths).</summary>
        public double[] ColumnWidths { get; set; } = [];
        public SubsplitsFrame? SubsplitsFrame { get; set; }

        /// <summary>Subsplits: SeparatorComponent.LockToBottom of the separator before the last row.</summary>
        public bool LastSeparatorLockToBottom { get; set; }

        /// <summary>Takes over a Splits / Subsplits component's scroll position and separator state.</summary>
        /// <summary>Contextual Slideshow: its components, which one it shows, and what each showed.</summary>
        public IReadOnlyList<ComponentVisual>? SlideshowParts { get; private set; }

        public ContextualSlideshowLogic? Slideshow { get; private set; }

        public string?[]? SlideshowKeys { get; private set; }

        public void KeepListState(ComponentVisual previous)
        {
            // The slideshow goes on where it was (the component object is kept).
            if (previous.Slideshow is not null && Slideshow is not null)
            {
                SlideshowParts = previous.SlideshowParts;
                Slideshow = previous.Slideshow;
                SlideshowKeys = previous.SlideshowKeys;
            }

            if (previous.Subsplits is not null && Subsplits is not null)
            {
                Subsplits = previous.Subsplits;
                LastSeparatorLockToBottom = previous.LastSeparatorLockToBottom;
            }

            if (previous.Splits is not null && Splits is not null)
            {
                Splits = previous.Splits;
            }
        }
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string SecondaryLabel { get; set; } = "";
        public string SecondaryValue { get; set; } = "";
        public string TertiaryLabel { get; set; } = "";
        public string TertiaryValue { get; set; } = "";
        public bool CenterLabel { get; set; }
        public bool ReserveSplitIcons { get; set; }
        public bool HideSecondComparison { get; set; }
        public ArgbColor TimerColor { get; set; }
        public IReadOnlyList<string> AlternateLabels { get; set; } = [];
        public bool TwoRows { get; set; }
        public bool ValueUsesTextFont { get; set; }
        public IBrush LabelBrush { get; set; } = Brushes.White;
        public IBrush SecondaryLabelBrush { get; set; } = Brushes.White;
        public IBrush ValueBrush { get; set; } = Brushes.White;
        public Bitmap? Icon { get; set; }
        public TimerDigitsView MainDigits { get; }
        public TimerDigitsView SegmentDigits { get; }
        public List<SplitRowVisual> Rows { get; } = [];
        public GeneralTimeFormatter TimerFormatter { get; } = NewTimerFormatter();
        public GeneralTimeFormatter SegmentFormatter { get; } = NewTimerFormatter();
        public GeneralTimeFormatter DetailFormatter { get; } = NewDetailFormatter();
        public GeneralTimeFormatter SplitFormatter { get; } = NewSplitFormatter();
        public GeneralTimeFormatter DeltaFormatter { get; } = NewDeltaFormatter();

        public void EnsureRows(int count)
        {
            while (Rows.Count < count)
            {
                Rows.Add(new SplitRowVisual());
            }
        }

        public static ComponentVisual Create(LayoutComponent component)
        {
            object settings = component.Path switch
            {
                StandardLayoutFactory.TitlePath =>
                    TitleComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.SplitsPath =>
                    SplitsComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.SubsplitsPath =>
                    SubsplitsComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.TimerPath =>
                    TimerComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.DetailedTimerPath =>
                    DetailedTimerComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.PreviousSegmentPath =>
                    PreviousSegmentComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.SumOfBestPath =>
                    SumOfBestComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.PossibleTimeSavePath =>
                    PossibleTimeSaveComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.TextPath =>
                    TextComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.BlankSpacePath =>
                    BlankSpaceComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.RunPredictionPath =>
                    RunPredictionComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.DeltaPath =>
                    DeltaComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.ComparisonTimePath =>
                    ComparisonTimeComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.CurrentComparisonPath =>
                    CurrentComparisonComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.TotalPlaytimePath =>
                    TotalPlaytimeComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.CounterPath =>
                    CounterComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.GraphPath =>
                    GraphComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.HotkeyIndicatorPath =>
                    HotkeyIndicatorComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.ManualGameTimePath =>
                    ManualGameTimeComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.WorldRecordPath =>
                    WorldRecordComponentSettings.FromInnerXml(component.SettingsInnerXml),
                StandardLayoutFactory.ContextualSlideshowPath =>
                    ContextualSlideshowComponentSettings.FromInnerXml(component.SettingsInnerXml),
                _ => new object(),
            };
            var visual = new ComponentVisual(component.Path, settings)
            {
                Source = component,
                Splits = settings is SplitsComponentSettings ? new SplitsLogic() : null,
            };
            visual.Subsplits = settings is SubsplitsComponentSettings ? new SubsplitsLogic() : null;
            if (settings is ContextualSlideshowComponentSettings)
            {
                // ContextualSlideshowComponent's constructor: its components with their defaults.
                visual.SlideshowParts = ContextualSlideshowComponentSettings.ComponentPaths
                    .Select(path => Create(StandardLayoutFactory.CreateBuiltin(path)))
                    .ToList();
                visual.Slideshow = new ContextualSlideshowLogic(visual.SlideshowParts.Count, SlideshowClock());
                visual.SlideshowKeys = new string?[visual.SlideshowParts.Count];
            }

            return visual;
        }

        private static TimerDigitsView NewDigits() => new()
        {
            DigitFontFamily = new FontFamily(AppFonts.TimerFamily),
            DigitFontWeight = FontWeight.Bold,
        };

        private static GeneralTimeFormatter NewTimerFormatter() => new()
        {
            Accuracy = TimeAccuracy.Hundredths,
            DigitsFormat = DigitsFormat.SingleDigitSeconds,
            NullFormat = NullFormat.ZeroWithAccuracy,
        };

        private static GeneralTimeFormatter NewDetailFormatter() => new()
        {
            Accuracy = TimeAccuracy.Hundredths,
            DigitsFormat = DigitsFormat.SingleDigitSeconds,
            NullFormat = NullFormat.Dash,
        };

        private static GeneralTimeFormatter NewSplitFormatter() => new()
        {
            Accuracy = TimeAccuracy.Seconds,
            DigitsFormat = DigitsFormat.SingleDigitMinutes,
            NullFormat = NullFormat.Dash,
            DropDecimals = true,
        };

        private static GeneralTimeFormatter NewDeltaFormatter() => new()
        {
            Accuracy = TimeAccuracy.Tenths,
            DigitsFormat = DigitsFormat.SingleDigitSeconds,
            NullFormat = NullFormat.Dash,
            DropDecimals = true,
        };
    }

    private sealed class SplitRowVisual
    {
        public bool IsVisible { get; set; }

        /// <summary>Subsplits rows can be a section header or blank; Splits rows are always splits.</summary>
        public SubsplitsRowKind Kind { get; set; } = SubsplitsRowKind.Split;
        public int SegmentIndex { get; set; }
        public int IndentLevel { get; set; }
        public bool IsCurrent { get; set; }

        /// <summary>Subsplits: the split scrolled to (SplitsSettings.HilightSplit).</summary>
        public bool IsHighlight { get; set; }

        /// <summary>Subsplits: this row's icon slot (SplitComponent.DisplayIcon); null uses the component's.</summary>
        public bool? ReserveIcon { get; set; }

        /// <summary>Subsplits header: Header Background Color (2) and gradient.</summary>
        public IBrush? HeaderBrush { get; set; }
        public string HeaderTime { get; set; } = "";
        public IBrush HeaderTimeBrush { get; set; } = Brushes.White;
        public string SectionTimer { get; set; } = "";
        public IBrush SectionTimerBrush { get; set; } = Brushes.White;
        public (Color Top, Color Bottom)? SectionTimerGradient { get; set; }

        /// <summary>Alternating background: the row's Background Color (2), or null when it is transparent.</summary>
        public IBrush? AlternateBrush { get; set; }

        /// <summary>Subsplits' Subsplit Color (Override Layout Settings) on a subsplit's row.</summary>
        public IBrush? SubsplitBrush { get; set; }

        public string Name { get; set; } = "";
        public IBrush NameBrush { get; set; } = Brushes.White;
        public IReadOnlyList<string> AlternateNames { get; set; } = [];
        public string? AlternatesFor { get; set; }
        public Bitmap? Icon { get; set; }
        public string? IconXml { get; set; }
        public List<SplitCellVisual> Columns { get; } = [];

        public void EnsureColumns(int count)
        {
            while (Columns.Count < count)
            {
                Columns.Add(new SplitCellVisual());
            }

            if (Columns.Count > count)
            {
                Columns.RemoveRange(count, Columns.Count - count);
            }
        }
    }

    private sealed class SplitCellVisual
    {
        public string Text { get; set; } = "";
        public IBrush Brush { get; set; } = Brushes.White;
        public double Width { get; set; }
    }

    private sealed class IconBitmapCache : IDisposable
    {
        private readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.Ordinal);

        public Bitmap? Get(string? iconXml)
        {
            if (string.IsNullOrWhiteSpace(iconXml))
            {
                return null;
            }

            if (_cache.TryGetValue(iconXml, out Bitmap? cached))
            {
                return cached;
            }

            Bitmap? bitmap = IconBitmaps.TryLoad(iconXml);
            _cache.Add(iconXml, bitmap);
            return bitmap;
        }

        private readonly Dictionary<(string Icon, ArgbColor Color), Bitmap?> _shadows = [];

        /// <summary>SplitsComponent.Prepare: one shadow per icon and shadow color.</summary>
        public Bitmap? GetShadow(string? iconXml, ArgbColor color)
        {
            if (string.IsNullOrWhiteSpace(iconXml) || Get(iconXml) is not { } icon)
            {
                return null;
            }

            if (_shadows.TryGetValue((iconXml, color), out Bitmap? cached))
            {
                return cached;
            }

            Bitmap? shadow = null;
            try
            {
                using Bitmap scaled = icon.CreateScaledBitmap(
                    new PixelSize(IconShadow.SourceSize, IconShadow.SourceSize),
                    BitmapInterpolationMode.HighQuality);
                var pixels = new byte[IconShadow.SourceSize * IconShadow.SourceSize * 4];
                GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    scaled.CopyPixels(
                        new PixelRect(0, 0, IconShadow.SourceSize, IconShadow.SourceSize),
                        handle.AddrOfPinnedObject(),
                        pixels.Length,
                        IconShadow.SourceSize * 4);
                }
                finally
                {
                    handle.Free();
                }

                var alpha = new byte[IconShadow.SourceSize * IconShadow.SourceSize];
                for (int i = 0; i < alpha.Length; i++)
                {
                    alpha[i] = pixels[(i * 4) + 3];
                }

                byte[] bgra = IconShadow.Generate(alpha, color);
                var bitmap = new WriteableBitmap(
                    new PixelSize(IconShadow.Size, IconShadow.Size),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Unpremul);
                using (ILockedFramebuffer buffer = bitmap.Lock())
                {
                    for (int y = 0; y < IconShadow.Size; y++)
                    {
                        Marshal.Copy(bgra, y * IconShadow.Size * 4, buffer.Address + (y * buffer.RowBytes), IconShadow.Size * 4);
                    }
                }

                shadow = bitmap;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Icon shadow failed: " + ex.Message);
            }

            _shadows.Add((iconXml, color), shadow);
            return shadow;
        }

        public void Dispose()
        {
            foreach (Bitmap bitmap in _cache.Values.OfType<Bitmap>().Concat(_shadows.Values.OfType<Bitmap>()))
            {
                bitmap.Dispose();
            }

            _cache.Clear();
            _shadows.Clear();
        }
    }
}
