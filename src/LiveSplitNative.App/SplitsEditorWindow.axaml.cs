using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Imaging;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.App;

public sealed class SegmentEditRow : INotifyPropertyChanged
{
    private string _name = "";
    private string _bestSegmentRealText = "";
    private string _bestSegmentGameText = "";
    private string _iconXml = "";

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public string BestSegmentRealText
    {
        get => _bestSegmentRealText;
        set { _bestSegmentRealText = value; OnPropertyChanged(); }
    }

    public string BestSegmentGameText
    {
        get => _bestSegmentGameText;
        set { _bestSegmentGameText = value; OnPropertyChanged(); }
    }

    public string? IconXml
    {
        get => string.IsNullOrEmpty(_iconXml) ? null : _iconXml;
        set
        {
            _iconXml = value ?? "";
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(IconSource));
        }
    }

    public bool HasIcon => !string.IsNullOrEmpty(_iconXml);

    public Bitmap? IconSource => IconBitmaps.TryLoad(_iconXml, decodeSize: 40);

    /// <summary>Index-parallel to SplitsEditorWindow comparison names (Real Time).</summary>
    public ObservableCollection<string> RealComparisonTexts { get; } = [];

    /// <summary>Index-parallel to SplitsEditorWindow comparison names (Game Time).</summary>
    public ObservableCollection<string> GameComparisonTexts { get; } = [];

    /// <summary>Preserved segment history (not shown; committed on OK).</summary>
    public SortedDictionary<int, SplitTime> SegmentHistory { get; } = [];

    public List<string> UnknownOuterXml { get; } = [];

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SplitsEditorWindow : Window
{
    private static readonly GeneralTimeFormatter EditorFormatter = new()
    {
        Accuracy = TimeAccuracy.Hundredths,
        DigitsFormat = DigitsFormat.SingleDigitSeconds,
        NullFormat = NullFormat.Dash,
    };

    private readonly ObservableCollection<SegmentEditRow> _rows = [];
    private readonly List<string> _comparisons = [];
    private readonly List<Attempt> _attemptHistory = [];
    private readonly List<string> _unknownRoot = [];
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _customVariables = new(StringComparer.Ordinal);
    private readonly bool _allowChangingSegments;
    private string? _gameIconXml;
    private string? _autoSplitterSettingsXml;
    private string? _version;
    private string _layoutPath = "";

    public SplitsEditorWindow() : this(Run.CreateStandard(), TimingMethod.RealTime)
    {
    }

    /// <param name="allowChangingSegments">
    /// Windows <c>RunEditorDialog.AllowChangingSegments</c>: true only when timer is
    /// <see cref="TimerPhase.NotRunning"/>. While Running/Paused/Ended, structure buttons stay off.
    /// </param>
    public SplitsEditorWindow(Run run, TimingMethod method, bool allowChangingSegments = true)
    {
        InitializeComponent();
        _allowChangingSegments = allowChangingSegments;
        _ = method; // both RT and GT are edited on their tabs; method kept for API compat
        _version = run.Version;
        _layoutPath = run.LayoutPath;
        _gameIconXml = run.GameIconXml;
        _autoSplitterSettingsXml = run.AutoSplitterSettingsXml;
        _unknownRoot.AddRange(run.UnknownRootOuterXml);
        foreach ((string k, string v) in run.Metadata.VariableValueNames)
        {
            _variables[k] = v;
        }

        foreach ((string k, string v) in run.Metadata.PermanentCustomVariables)
        {
            _customVariables[k] = v;
        }

        _attemptHistory.AddRange(run.AttemptHistory);
        _comparisons.AddRange(run.CustomComparisons);
        if (!_comparisons.Contains(Run.PersonalBestComparisonName))
        {
            _comparisons.Insert(0, Run.PersonalBestComparisonName);
        }

        GameNameBox.Text = run.GameName;
        CategoryBox.Text = run.CategoryName;
        OffsetBox.Text = FormatOffset(run.Offset);
        AttemptsBox.Text = run.AttemptCount.ToString(CultureInfo.InvariantCulture);
        InitializeMetadata(run);
        RefreshGameIconImage();

        foreach (Segment segment in run.Segments)
        {
            _rows.Add(CreateRowFromSegment(segment));
        }

        if (_rows.Count == 0)
        {
            _rows.Add(CreateEmptyRow());
        }

        ConfigureGrid(SegmentsGrid);
        ConfigureGrid(GameTimeSegmentsGrid);
        // Columns before ItemsSource — Avalonia DataGrid skips row generation when the
        // source is attached with an empty column set, then columns are added later.
        RebuildGridColumns();
        RefreshAttemptList();
        SegmentsGrid.AttachedToVisualTree += OnSegmentsGridAttachedToVisualTree;
        GameTimeSegmentsGrid.AttachedToVisualTree += OnGameTimeGridAttachedToVisualTree;
        if (SegmentsGrid.GetVisualRoot() is not null)
        {
            AttachSegmentsSource(SegmentsGrid);
        }

        if (GameTimeSegmentsGrid.GetVisualRoot() is not null)
        {
            AttachSegmentsSource(GameTimeSegmentsGrid);
        }

        Opened += OnEditorOpened;
        SegmentsGrid.AddHandler(InputElement.KeyDownEvent, OnSegmentsGridKeyDown, RoutingStrategies.Tunnel);
        GameTimeSegmentsGrid.AddHandler(InputElement.KeyDownEvent, OnGameTimeGridKeyDown, RoutingStrategies.Tunnel);
        SegmentsGrid.DoubleTapped += OnSegmentsGridDoubleTapped;
        GameTimeSegmentsGrid.DoubleTapped += OnSegmentsGridDoubleTapped;
        SegmentsGrid.SelectionChanged += (_, _) => UpdateMoveButtonEnabled();
        GameTimeSegmentsGrid.SelectionChanged += (_, _) => UpdateMoveButtonEnabled();
        GameIconBorder.DoubleTapped += async (_, _) => await SetGameIconFromFileAsync();
        SetUpGameAndCategorySuggestions();
        SetUpMetadata();
        SetUpGameIconMenu();
        ApplyAllowChangingSegments();
    }

    private void ConfigureGrid(DataGrid grid)
    {
        grid.RowHeight = 30;
        grid.SelectionMode = DataGridSelectionMode.Extended;
        grid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
    }

    private SegmentEditRow CreateRowFromSegment(Segment segment)
    {
        var row = new SegmentEditRow
        {
            Name = segment.Name,
            BestSegmentRealText = FormatOptional(segment.BestSegmentTime.RealTime),
            BestSegmentGameText = FormatOptional(segment.BestSegmentTime.GameTime),
            IconXml = segment.IconXml,
        };
        foreach (string comparison in _comparisons)
        {
            segment.Comparisons.TryGetValue(comparison, out SplitTime time);
            row.RealComparisonTexts.Add(FormatOptional(time.RealTime));
            row.GameComparisonTexts.Add(FormatOptional(time.GameTime));
        }

        foreach ((int id, SplitTime hist) in segment.SegmentHistory)
        {
            row.SegmentHistory[id] = hist;
        }

        row.UnknownOuterXml.AddRange(segment.UnknownOuterXml);
        return row;
    }

    private void ApplyAllowChangingSegments()
    {
        bool can = _allowChangingSegments;
        BtnInsert.IsEnabled = can;
        BtnAdd.IsEnabled = can;
        BtnAddComparison.IsEnabled = can;
        BtnImportComparison.IsEnabled = can;
        // RunEditorDialog: btnOther.Enabled = AllowChangingSegments (and not in history mode).
        BtnOther.IsEnabled = can;
        if (!can)
        {
            BtnRemove.IsEnabled = false;
            BtnMoveUp.IsEnabled = false;
            BtnMoveDown.IsEnabled = false;
            return;
        }

        BtnRemove.IsEnabled = _rows.Count > 1;
        UpdateMoveButtonEnabled();
    }

    private void UpdateMoveButtonEnabled()
    {
        if (!_allowChangingSegments)
        {
            BtnMoveUp.IsEnabled = false;
            BtnMoveDown.IsEnabled = false;
            return;
        }

        List<int> selected = GetSelectedRowIndices();
        if (selected.Count == 0)
        {
            BtnMoveUp.IsEnabled = false;
            BtnMoveDown.IsEnabled = false;
            return;
        }

        BtnMoveUp.IsEnabled = selected.Min() > 0;
        BtnMoveDown.IsEnabled = selected.Max() < _rows.Count - 1;
    }

    private void OnEditorOpened(object? sender, EventArgs e)
    {
        Opened -= OnEditorOpened;
        InitializeUseLayout();
        EditorTabs.SelectedIndex = 0;
        Dispatcher.UIThread.Post(() =>
        {
            AttachSegmentsSource(SegmentsGrid);
            AttachSegmentsSource(GameTimeSegmentsGrid);
        }, DispatcherPriority.Loaded);
    }

    private void OnSegmentsGridAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) =>
        AttachSegmentsSource(SegmentsGrid);

    private void OnGameTimeGridAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) =>
        AttachSegmentsSource(GameTimeSegmentsGrid);

    public int SegmentRowCount => _rows.Count;

    private void AttachSegmentsSource(DataGrid grid)
    {
        if (grid.Columns.Count == 0)
        {
            RebuildGridColumns();
        }

        double rowPitch = grid.RowHeight > 0 ? grid.RowHeight : 30;
        // The viewport stays bounded by the tab. Row count belongs to the internal
        // scroll extent; using it as MinHeight makes long runs grow behind the footer.
        grid.MinHeight = Math.Max(88, 28 + 2 * rowPitch);
        grid.MaxHeight = double.PositiveInfinity;
        grid.Height = double.NaN;
        grid.VerticalAlignment = VerticalAlignment.Stretch;
        grid.HorizontalAlignment = HorizontalAlignment.Stretch;
        grid.ItemsSource = null;
        grid.ItemsSource = _rows;
    }

    public Run? Result { get; private set; }

    /// <summary>
    /// Runtime regression assertion used by the 43-segment viewport proof.
    /// Scrolls to the true final item, verifies that its realized row is entirely
    /// inside the DataGrid, and verifies that the grid ends above the fixed footer.
    /// </summary>
    public bool IsFinalRowReachableForProof(out string detail)
    {
        DataGrid grid = ActiveGrid;
        SegmentEditRow final = _rows[^1];
        grid.ScrollIntoView(final, null);
        grid.UpdateLayout();
        UpdateLayout();

        DataGridRow? row = grid.GetVisualDescendants()
            .OfType<DataGridRow>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, final));
        // The DataGrid scrolls with its own vertical ScrollBar, not a ScrollViewer.
        ScrollBar? scroll = grid.GetVisualDescendants()
            .OfType<ScrollBar>()
            .FirstOrDefault(candidate => candidate.Orientation == Orientation.Vertical);
        Point? gridOrigin = grid.TranslatePoint(default, this);
        Point? rowOrigin = row?.TranslatePoint(default, this);
        Point? footerOrigin = FooterPanel.TranslatePoint(default, this);

        double gridBottom = (gridOrigin?.Y ?? double.NaN) + grid.Bounds.Height;
        double rowBottom = (rowOrigin?.Y ?? double.NaN) + (row?.Bounds.Height ?? double.NaN);
        double footerTop = footerOrigin?.Y ?? double.NaN;
        bool hasScrollableExtent = scroll is not null && scroll.Maximum > 1;
        bool finalFullyVisible = row is not null
            && rowOrigin is not null
            && rowOrigin.Value.Y >= (gridOrigin?.Y ?? 0) - 0.5
            && rowBottom <= gridBottom + 0.5;
        bool footerClear = double.IsFinite(gridBottom)
            && double.IsFinite(footerTop)
            && gridBottom <= footerTop - 1;

        double iconColumnWidth = grid.Columns.Count > 0 ? grid.Columns[0].ActualWidth : 0;
        detail =
            $"rows={_rows.Count}; window={Bounds.Width:0}x{Bounds.Height:0}; iconColumn={iconColumnWidth:0}; " +
            $"scroll={scroll?.Value:0.0}/{scroll?.Maximum:0.0} viewport={scroll?.ViewportSize:0.0}; " +
            $"rowBottom={rowBottom:0.0}; gridBottom={gridBottom:0.0}; footerTop={footerTop:0.0}";
        return _rows.Count == 43 && hasScrollableExtent && finalFullyVisible && footerClear
            && iconColumnWidth >= 49.5;
    }

    private void SetStatus(string? text)
    {
        StatusLabel.Text = text ?? "";
        StatusLabel.IsVisible = !string.IsNullOrEmpty(text);
    }

    public void SelectSegmentRow(int index) => SelectSegmentRows(index);

    public void SelectSegmentRows(params int[] indices)
    {
        SelectOnGrid(SegmentsGrid, indices);
        SelectOnGrid(GameTimeSegmentsGrid, indices);
    }

    private void SelectOnGrid(DataGrid grid, int[] indices)
    {
        grid.SelectedItems.Clear();
        foreach (int index in indices.Distinct().OrderBy(i => i))
        {
            if (index < 0 || index >= _rows.Count)
            {
                continue;
            }

            grid.SelectedItems.Add(_rows[index]);
            grid.ScrollIntoView(_rows[index], null);
        }

        if (indices.Length > 0 && indices[0] >= 0 && indices[0] < _rows.Count)
        {
            grid.SelectedIndex = indices[0];
        }
    }

    /// <summary>
    /// Prefer full headers over ellipsis. Fixed pixel widths + horizontal scroll
    /// (not Star on time cols) so many comparisons do not crush "Personal Best".
    /// Restores pre-5bb9f61 sizing; that tip clamped to 72–110px and truncated.
    /// </summary>
    private static double EstimateTimeColumnWidth(string header)
    {
        // SemiBold ~12pt + DataGrid header padding; content min for "0:00:00.00".
        double byHeader = 24 + header.Length * 8.5;
        const double contentMin = 96;
        return Math.Clamp(Math.Max(byHeader, contentMin), 130, 220);
    }

    private void RebuildGridColumns()
    {
        RebuildOneGridColumns(SegmentsGrid, TimingMethod.RealTime);
        RebuildOneGridColumns(GameTimeSegmentsGrid, TimingMethod.GameTime);
    }

    private void RebuildOneGridColumns(DataGrid grid, TimingMethod method)
    {
        grid.ItemsSource = null;
        grid.Columns.Clear();
        // Windows RunEditor: DataGridViewImageColumn "Icon", Width 50, ImageLayout.Zoom (the whole
        // icon, aspect kept). Here the icon sits in a square well as tall as the row (Basten,
        // 12 Oct: square looks cleaner with imported icons), so it never depends on the row.
        double iconWell = Math.Max(12, grid.RowHeight - 4);
        var iconColumn = new DataGridTemplateColumn
        {
            Header = "Icon",
            Width = new DataGridLength(50),
            // A DataGrid short of width shrinks every column to its MinWidth (20 by default),
            // which squeezed the icons; the icon column keeps Windows' 50.
            MinWidth = 50,
            IsReadOnly = true,
            CellTemplate = new FuncDataTemplate<SegmentEditRow>((row, _) =>
            {
                var img = new Image
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Source = row.IconSource,
                };
                img.Bind(Image.SourceProperty, new Binding(nameof(SegmentEditRow.IconSource)));
                var border = new Border
                {
                    Width = iconWell,
                    Height = iconWell,
                    Padding = new Thickness(1),
                    BorderBrush = AppThemeController.GetDialogBrush("LsDialogBorderLightBrush"),
                    BorderThickness = new Thickness(1),
                    Background = AppThemeController.GetDialogBrush("LsDialogIconWellBackground"),
                    ClipToBounds = true,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = img,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                ToolTip.SetTip(border, "Double-click to set segment icon");
                return border;
            }, supportsRecycling: true),
        };
        // No cell padding here: the well is sized to the row, not to what padding leaves.
        iconColumn.CellStyleClasses.Add("iconCell");
        grid.Columns.Add(iconColumn);
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Segment Name",
            Binding = new Binding(nameof(SegmentEditRow.Name)),
            // Star takes leftover only; MinWidth keeps names readable when scrolling.
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 200,
        });

        string comparisonPath = method == TimingMethod.RealTime
            ? nameof(SegmentEditRow.RealComparisonTexts)
            : nameof(SegmentEditRow.GameComparisonTexts);
        for (int i = 0; i < _comparisons.Count; i++)
        {
            double w = EstimateTimeColumnWidth(_comparisons[i]);
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = _comparisons[i],
                Binding = new Binding($"{comparisonPath}[{i}]")
                {
                    Mode = BindingMode.TwoWay,
                },
                Width = new DataGridLength(w, DataGridLengthUnitType.Pixel),
                MinWidth = 130,
            });
        }

        double bestSegW = EstimateTimeColumnWidth("Best Segment");
        string bestPath = method == TimingMethod.RealTime
            ? nameof(SegmentEditRow.BestSegmentRealText)
            : nameof(SegmentEditRow.BestSegmentGameText);
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "Best Segment",
            Binding = new Binding(bestPath),
            Width = new DataGridLength(bestSegW, DataGridLengthUnitType.Pixel),
            MinWidth = 130,
        });

        if (grid.GetVisualRoot() is not null)
        {
            AttachSegmentsSource(grid);
        }
    }

    private void RefreshAttemptList()
    {
        AttemptHistoryList.ItemsSource = null;
        AttemptHistoryList.ItemsSource = _attemptHistory
            .Select(a => $"#{a.Index}  RT={FormatOptional(a.Time.RealTime)}  GT={FormatOptional(a.Time.GameTime)}")
            .ToList();
    }

    private void RefreshGameIconImage()
    {
        GameIconImage.Source = IconBitmaps.TryLoad(_gameIconXml, decodeSize: 128);
    }

    private async Task SetGameIconFromFileAsync()
    {
        string? xml = await PickIconXmlAsync("Set Game Icon...");
        if (xml is null)
        {
            return;
        }

        _gameIconXml = xml;
        RefreshGameIconImage();
    }

    private async void OnSegmentsGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Control source)
        {
            return;
        }

        bool onIcon = source is Image || (source is Border b && b.Child is Image);
        if (!onIcon)
        {
            return;
        }

        List<int> indices = GetSelectedRowIndices();
        if (indices.Count == 0 && ActiveGrid.SelectedIndex >= 0)
        {
            indices.Add(ActiveGrid.SelectedIndex);
        }

        if (indices.Count == 0)
        {
            return;
        }

        string tip = indices.Count == 1 && indices[0] < _rows.Count
            ? $"Set Icon for {_rows[indices[0]].Name}..."
            : "Set Icon...";
        string? xml = await PickIconXmlAsync(tip);
        if (xml is null)
        {
            return;
        }

        foreach (int index in indices)
        {
            _rows[index].IconXml = xml;
        }
    }

    private async Task<string?> PickIconXmlAsync(string title)
    {
        IStorageProvider? storage = StorageProvider;
        if (storage is null)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.ico"],
                },
            ],
        });
        if (files.Count == 0)
        {
            return null;
        }

        await using Stream stream = await files[0].OpenReadAsync();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return IconBlobDecoder.ToPortableIconXml(ms.ToArray());
    }

    private SegmentEditRow CreateEmptyRow()
    {
        var row = new SegmentEditRow();
        foreach (string _ in _comparisons)
        {
            row.RealComparisonTexts.Add("");
            row.GameComparisonTexts.Add("");
        }

        return row;
    }

    private DataGrid ActiveGrid =>
        EditorTabs.SelectedIndex == 1 ? GameTimeSegmentsGrid : SegmentsGrid;

    private void OnInsert(object? sender, RoutedEventArgs e)
    {
        if (!_allowChangingSegments)
        {
            return;
        }

        int index = Math.Max(0, GetSelectedRowIndices().DefaultIfEmpty(0).Min());
        _rows.Insert(index, CreateEmptyRow());
        SelectSegmentRows(index);
        ApplyAllowChangingSegments();
        AttachSegmentsSource(SegmentsGrid);
        AttachSegmentsSource(GameTimeSegmentsGrid);
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        if (!_allowChangingSegments)
        {
            return;
        }

        _rows.Add(CreateEmptyRow());
        SelectSegmentRows(_rows.Count - 1);
        ApplyAllowChangingSegments();
        AttachSegmentsSource(SegmentsGrid);
        AttachSegmentsSource(GameTimeSegmentsGrid);
    }

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if (!_allowChangingSegments)
        {
            return;
        }

        List<int> selected = GetSelectedRowIndices();
        if (selected.Count == 0)
        {
            return;
        }

        if (_rows.Count - selected.Count < 1)
        {
            SetStatus("A run must keep at least one segment.");
            return;
        }

        foreach (int index in selected.OrderByDescending(i => i))
        {
            _rows.RemoveAt(index);
        }

        SetStatus("");
        if (_rows.Count > 0)
        {
            SelectSegmentRows(Math.Min(selected[0], _rows.Count - 1));
        }

        ApplyAllowChangingSegments();
        AttachSegmentsSource(SegmentsGrid);
        AttachSegmentsSource(GameTimeSegmentsGrid);
    }

    private void OnMoveUp(object? sender, RoutedEventArgs e) => MoveSelected(-1);

    private void OnMoveDown(object? sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        if (!_allowChangingSegments)
        {
            return;
        }

        List<int> selected = GetSelectedRowIndices();
        if (selected.Count == 0)
        {
            return;
        }

        int first = selected.Min();
        int last = selected.Max();
        bool contiguous = selected.Count == last - first + 1;
        if (!contiguous)
        {
            int index = ActiveGrid.SelectedIndex;
            int target = index + offset;
            if (index < 0 || target < 0 || target >= _rows.Count)
            {
                return;
            }

            SegmentEditRow row = _rows[index];
            _rows.RemoveAt(index);
            _rows.Insert(target, row);
            SelectSegmentRows(target);
            return;
        }

        if (offset < 0 && first == 0)
        {
            return;
        }

        if (offset > 0 && last == _rows.Count - 1)
        {
            return;
        }

        List<SegmentEditRow> block = selected.OrderBy(i => i).Select(i => _rows[i]).ToList();
        foreach (int index in selected.OrderByDescending(i => i))
        {
            _rows.RemoveAt(index);
        }

        int insertAt = first + offset;
        for (int i = 0; i < block.Count; i++)
        {
            _rows.Insert(insertAt + i, block[i]);
        }

        SelectSegmentRows(Enumerable.Range(insertAt, block.Count).ToArray());
    }

    private void OnClearIcon(object? sender, RoutedEventArgs e)
    {
        List<int> indices = GetSelectedRowIndices();
        if (indices.Count == 0)
        {
            _gameIconXml = null;
            RefreshGameIconImage();
            return;
        }

        foreach (int index in indices)
        {
            _rows[index].IconXml = null;
        }
    }

    private List<int> GetSelectedRowIndices()
    {
        var indices = new List<int>();
        DataGrid grid = ActiveGrid;
        foreach (object? item in grid.SelectedItems)
        {
            if (item is SegmentEditRow row)
            {
                int i = _rows.IndexOf(row);
                if (i >= 0)
                {
                    indices.Add(i);
                }
            }
        }

        if (indices.Count == 0 && grid.SelectedIndex >= 0)
        {
            indices.Add(grid.SelectedIndex);
        }

        return indices.Distinct().OrderBy(i => i).ToList();
    }

    private async void OnSegmentsGridKeyDown(object? sender, KeyEventArgs e) =>
        await HandleGridKeyDownAsync(SegmentsGrid, TimingMethod.RealTime, e);

    private async void OnGameTimeGridKeyDown(object? sender, KeyEventArgs e) =>
        await HandleGridKeyDownAsync(GameTimeSegmentsGrid, TimingMethod.GameTime, e);

    private async Task HandleGridKeyDownAsync(DataGrid grid, TimingMethod method, KeyEventArgs e)
    {
        bool cmdOrCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
            || e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (cmdOrCtrl && e.Key == Key.V)
        {
            e.Handled = true;
            await PasteClipboardIntoGridAsync(grid, method);
            return;
        }

        if (cmdOrCtrl && e.Key == Key.C)
        {
            e.Handled = true;
            await CopySelectedToClipboardAsync(grid, method);
            return;
        }

        if (e.Key is Key.Delete or Key.Back)
        {
            if (grid.IsReadOnly || TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
            {
                return;
            }

            e.Handled = true;
            ClearSelectedCellValues(grid, method);
        }
    }

    private async Task PasteClipboardIntoGridAsync(DataGrid grid, TimingMethod method)
    {
        try
        {
            string? text = await GetClipboardTextAsync();
            if (string.IsNullOrEmpty(text) || _rows.Count == 0)
            {
                return;
            }

            string[] rowsInClipboard = text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .TrimEnd('\n')
                .Split('\n');
            int startRow = Math.Max(0, GetSelectedRowIndices().DefaultIfEmpty(0).Min());
            // DisplayIndex includes Icon (0). Logical editable columns start at Name (1).
            int startCol = Math.Max(1, grid.CurrentColumn?.DisplayIndex ?? 1);
            int colCount = grid.Columns.Count;

            for (int i = 0; i < rowsInClipboard.Length && startRow + i < _rows.Count; i++)
            {
                string[] values = rowsInClipboard[i].Split('\t');
                for (int j = 0; j < values.Length && startCol + j < colCount; j++)
                {
                    SetCellText(startRow + i, startCol + j, values[j], method);
                }
            }

            SetStatus("");
        }
        catch (Exception ex)
        {
            SetStatus("Paste failed: " + ex.Message);
        }
    }

    private async Task CopySelectedToClipboardAsync(DataGrid grid, TimingMethod method)
    {
        List<int> rows = GetSelectedRowIndices();
        if (rows.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        int colCount = grid.Columns.Count;
        for (int r = 0; r < rows.Count; r++)
        {
            if (r > 0)
            {
                sb.Append('\n');
            }

            // Skip icon column in TSV (Windows-style text paste of name/times).
            for (int c = 1; c < colCount; c++)
            {
                if (c > 1)
                {
                    sb.Append('\t');
                }

                sb.Append(GetCellText(rows[r], c, method));
            }
        }

        if (Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(sb.ToString());
        }
    }

    private void ClearSelectedCellValues(DataGrid grid, TimingMethod method)
    {
        int col = grid.CurrentColumn?.DisplayIndex ?? -1;
        foreach (int row in GetSelectedRowIndices())
        {
            if (col == 0)
            {
                continue; // icon column
            }

            if (col >= 0)
            {
                SetCellText(row, col, "", method);
            }
            else
            {
                for (int c = 1; c < grid.Columns.Count; c++)
                {
                    SetCellText(row, c, "", method);
                }
            }
        }
    }

    /// <summary>DisplayIndex: 0=Icon, 1=Name, 2..=comparisons, last=Best Segment.</summary>
    private string GetCellText(int rowIndex, int columnIndex, TimingMethod method)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count || columnIndex < 0)
        {
            return "";
        }

        SegmentEditRow row = _rows[rowIndex];
        if (columnIndex <= 0)
        {
            return "";
        }

        if (columnIndex == 1)
        {
            return row.Name;
        }

        int bestCol = 2 + _comparisons.Count;
        if (columnIndex == bestCol)
        {
            return method == TimingMethod.RealTime ? row.BestSegmentRealText : row.BestSegmentGameText;
        }

        int cmp = columnIndex - 2;
        ObservableCollection<string> texts = method == TimingMethod.RealTime
            ? row.RealComparisonTexts
            : row.GameComparisonTexts;
        if (cmp >= 0 && cmp < texts.Count)
        {
            return texts[cmp];
        }

        return "";
    }

    private void SetCellText(int rowIndex, int columnIndex, string value, TimingMethod method)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count || columnIndex <= 0)
        {
            return;
        }

        SegmentEditRow row = _rows[rowIndex];
        if (columnIndex == 1)
        {
            row.Name = value;
            return;
        }

        int bestCol = 2 + _comparisons.Count;
        if (columnIndex == bestCol)
        {
            if (method == TimingMethod.RealTime)
            {
                row.BestSegmentRealText = value.Trim();
            }
            else
            {
                row.BestSegmentGameText = value.Trim();
            }

            return;
        }

        int cmp = columnIndex - 2;
        ObservableCollection<string> texts = method == TimingMethod.RealTime
            ? row.RealComparisonTexts
            : row.GameComparisonTexts;
        if (cmp >= 0 && cmp < texts.Count)
        {
            texts[cmp] = value.Trim();
        }
    }

    private async Task<string?> GetClipboardTextAsync()
    {
        if (Clipboard is null)
        {
            return null;
        }

        return await Clipboard.GetTextAsync();
    }

    /// <summary>
    /// Headless paste proof: TSV into Name / comparisons / Best Segment
    /// (logical columns starting at Name = display index 1).
    /// </summary>
    public void PasteTsvForProof(string tsv, int startRow = 0, int startCol = 0)
    {
        // startCol is logical (0=Name) for backward-compat with older proofs.
        int displayCol = startCol + 1;
        string[] rowsInClipboard = tsv.Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n')
            .Split('\n');
        int colCount = SegmentsGrid.Columns.Count;
        for (int i = 0; i < rowsInClipboard.Length && startRow + i < _rows.Count; i++)
        {
            string[] values = rowsInClipboard[i].Split('\t');
            for (int j = 0; j < values.Length && displayCol + j < colCount; j++)
            {
                SetCellText(startRow + i, displayCol + j, values[j], TimingMethod.RealTime);
            }
        }
    }

    private async void OnAddComparison(object? sender, RoutedEventArgs e)
    {
        if (!_allowChangingSegments)
        {
            return;
        }

        string? name = await PromptTextAsync("New Comparison", "Comparison Name:");
        if (name is null)
        {
            return;
        }

        if (name.StartsWith("[Race]", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("A Comparison name cannot start with [Race].");
            return;
        }

        if (_comparisons.Contains(name, StringComparer.Ordinal))
        {
            SetStatus("A Comparison with this name already exists.");
            return;
        }

        _comparisons.Add(name);
        foreach (SegmentEditRow row in _rows)
        {
            row.RealComparisonTexts.Add("");
            row.GameComparisonTexts.Add("");
        }

        RebuildGridColumns();
        SetStatus("");
    }

    /// <summary>RunEditorDialog.btnImportComparison_Click: its menu (ImportComparisonMenu) at the button.</summary>
    private void OnImportComparison(object? sender, RoutedEventArgs e)
    {
        if (!_allowChangingSegments)
        {
            return;
        }

        PixelPoint screen = BtnImportComparison.PointToScreen(new Point(BtnImportComparison.Bounds.Width, BtnImportComparison.Bounds.Height / 2));
        Platform.NativeContextMenuFactory.Current.Show(this, screen, UiLocalizer.Apply(ImportComparisonMenuModel()));
    }

    /// <summary>ImportComparisonMenu: From File..., From URL....</summary>
    internal Platform.NativeMenuModel ImportComparisonMenuModel() => new()
    {
        Items =
        [
            new Platform.NativeMenuAction { Header = "From File...", OnClickAsync = ImportComparisonFromFileAsync },
            new Platform.NativeMenuAction { Header = "From URL...", OnClickAsync = ImportComparisonFromUrlAsync },
        ],
    };

    /// <summary>FileRunImporter.ImportAsComparison: a splits file, then the comparison's name.</summary>
    private async Task ImportComparisonFromFileAsync()
    {
        try
        {
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Comparison From File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("LiveSplit Splits") { Patterns = ["*.lss"] },
                ],
            });
            if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            {
                return;
            }

            Run imported = XmlRunSerializer.LoadFile(path);
            string defaultName = Path.GetFileNameWithoutExtension(path);
            string? name = await PromptTextAsync("Enter Comparison Name", "Name:", defaultName);
            if (name is null)
            {
                return;
            }

            AddImportedComparison(imported, name);
        }
        catch (Exception ex)
        {
            SetStatus("Import failed: " + ex.Message);
        }
    }

    /// <summary>
    /// URLRunImporter.ImportAsComparison: the comparison's name and the splits' address, then the
    /// splits downloaded ("couldn't be downloaded" / "not recognized as a splits file" otherwise).
    /// </summary>
    internal async Task ImportComparisonFromUrlAsync()
    {
        string[]? answers = await DialogChrome.PromptFieldsAsync(this, "Import Comparison from URL", [("Name:", ""), ("URL:", "")], 320);
        if (answers is null)
        {
            return;
        }

        byte[] data;
        try
        {
            data = await MainWindow.DownloadFromUrl(new Uri(answers[1].Trim()), CancellationToken.None);
        }
        catch (Exception)
        {
            await ShowMessageAsync(UiLocalizer.T("Error"), UiLocalizer.T("The splits file couldn't be downloaded."));
            return;
        }

        Run imported;
        try
        {
            imported = XmlRunSerializer.Load(new MemoryStream(data));
        }
        catch (Exception)
        {
            await ShowMessageAsync(UiLocalizer.T("Error"), UiLocalizer.T("The selected file was not recognized as a splits file."));
            return;
        }

        try
        {
            AddImportedComparison(imported, answers[0]);
        }
        catch (Exception ex)
        {
            SetStatus("Import failed: " + ex.Message);
        }
    }

    /// <summary>Run.AddComparisonWithNameInput: the imported run's PB as a comparison column.</summary>
    private void AddImportedComparison(Run imported, string name)
    {
        Run scratch = BuildRun(preserveHistory: true);
        if (!scratch.TryImportComparison(imported, name, out string? error))
        {
            SetStatus(error ?? "Import failed.");
            return;
        }

        if (!_comparisons.Contains(name, StringComparer.Ordinal))
        {
            _comparisons.Add(name);
            foreach (SegmentEditRow row in _rows)
            {
                row.RealComparisonTexts.Add("");
                row.GameComparisonTexts.Add("");
            }
        }

        int cmpIndex = _comparisons.IndexOf(name);
        for (int i = 0; i < _rows.Count && i < scratch.Count; i++)
        {
            scratch[i].Comparisons.TryGetValue(name, out SplitTime time);
            while (_rows[i].RealComparisonTexts.Count <= cmpIndex)
            {
                _rows[i].RealComparisonTexts.Add("");
                _rows[i].GameComparisonTexts.Add("");
            }

            _rows[i].RealComparisonTexts[cmpIndex] = FormatOptional(time.RealTime);
            _rows[i].GameComparisonTexts[cmpIndex] = FormatOptional(time.GameTime);
        }

        RebuildGridColumns();
        SetStatus("");
    }

    /// <summary>The recent layouts (Settings.RecentLayouts, oldest first), for Use Layout.</summary>
    public IReadOnlyList<string> RecentLayouts { get; set; } = [];

    /// <summary>
    /// TimerForm.OpenLayoutFromFile / LoadDefaultLayout ("?default") for Use Layout: true when
    /// the layout opened. Without it the choice is only kept with the splits.
    /// </summary>
    public Func<string, Task<bool>>? ApplyLayoutPath { get; set; }

    /// <summary>A Use Layout choice: its name in the list and its path ("?default": the default layout).</summary>
    internal sealed record LayoutChoice(string Name, string Path)
    {
        public override string ToString() => Name;
    }

    private bool _settingLayoutChoice;

    /// <summary>The layout the splits use ("" for none), as OK will save it.</summary>
    internal string LayoutPathForTests => _layoutPath;

    /// <summary>The splits as OK would give them back (tests).</summary>
    internal Run BuildRunForTests() => BuildRun(preserveHistory: true);

    /// <summary>
    /// RunEditorDialog.InitializeUseLayoutUI: Default Layout and the recent layouts (newest first);
    /// the splits' own layout selected and Use Layout ticked when they have one.
    /// </summary>
    internal void InitializeUseLayout()
    {
        _settingLayoutChoice = true;
        try
        {
            var choices = new List<LayoutChoice> { new(UiLocalizer.T("Default Layout"), "?default") };
            choices.AddRange(RecentLayouts.Reverse()
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .Select(path => new LayoutChoice(Path.GetFileNameWithoutExtension(path), path)));
            LayoutToUseBox.ItemsSource = new ObservableCollection<LayoutChoice>(choices);
            if (string.IsNullOrEmpty(_layoutPath))
            {
                UseLayoutBox.IsChecked = false;
                LayoutSelectPanel.IsEnabled = false;
                return;
            }

            SelectLayoutChoice(_layoutPath);
            UseLayoutBox.IsChecked = true;
            LayoutSelectPanel.IsEnabled = true;
        }
        finally
        {
            _settingLayoutChoice = false;
        }
    }

    /// <summary>ApplyLayoutPathToCbx: the path's entry selected, added after Default Layout when missing.</summary>
    private void SelectLayoutChoice(string layoutPath)
    {
        var choices = (ObservableCollection<LayoutChoice>)LayoutToUseBox.ItemsSource!;
        LayoutChoice? selected = choices.FirstOrDefault(c => c.Path == layoutPath);
        if (selected is null)
        {
            selected = new LayoutChoice(Path.GetFileNameWithoutExtension(layoutPath), layoutPath);
            choices.Insert(1, selected);
        }

        bool was = _settingLayoutChoice;
        _settingLayoutChoice = true;
        LayoutToUseBox.SelectedItem = selected;
        _settingLayoutChoice = was;
    }

    /// <summary>SaveAndApplyLayoutPath: the layout opens on the timer, and the splits keep it.</summary>
    private async Task<bool> UseLayoutAsync(string layoutPath)
    {
        if (_layoutPath == layoutPath)
        {
            return true;
        }

        bool success = ApplyLayoutPath is null || await ApplyLayoutPath(layoutPath);
        if (success)
        {
            _layoutPath = layoutPath;
        }

        return success;
    }

    private async void OnLayoutToUseChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_settingLayoutChoice || LayoutToUseBox.SelectedItem is not LayoutChoice choice)
        {
            return;
        }

        await UseLayoutAsync(choice.Path);
    }

    /// <summary>btnBrowseLayout_Click: a layout file, opened and kept when it opens.</summary>
    private async void OnBrowseLayout(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiLocalizer.T("Open Layout"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(UiLocalizer.T("LiveSplit Layout")) { Patterns = ["*.lsl"] },
                new FilePickerFileType(UiLocalizer.T("All files")) { Patterns = ["*"] },
            ],
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path && await UseLayoutAsync(path))
        {
            SelectLayoutChoice(path);
        }
    }

    /// <summary>chkbxUseLayout_CheckedChanged: the choice is kept (or dropped) without opening anything.</summary>
    private void OnUseLayoutChanged(object? sender, RoutedEventArgs e)
    {
        if (_settingLayoutChoice)
        {
            return;
        }

        bool use = UseLayoutBox.IsChecked == true;
        LayoutSelectPanel.IsEnabled = use;
        _layoutPath = use && LayoutToUseBox.SelectedItem is LayoutChoice choice ? choice.Path : "";
    }

    /// <summary>Use Layout chooses a layout as picking it in the list does (tests).</summary>
    internal Task<bool> UseLayoutForTestsAsync(string layoutPath)
    {
        UseLayoutBox.IsChecked = true;
        return UseLayoutAsync(layoutPath).ContinueWith(t =>
        {
            if (t.Result)
            {
                SelectLayoutChoice(layoutPath);
            }

            return t.Result;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>RunEditorDialog.btnOther_Click: its menu (OtherMenu) at the button.</summary>
    private void OnOther(object? sender, RoutedEventArgs e)
    {
        PixelPoint screen = BtnOther.PointToScreen(new Point(BtnOther.Bounds.Width, BtnOther.Bounds.Height / 2));
        Platform.NativeContextMenuFactory.Current.Show(this, screen, UiLocalizer.Apply(OtherMenuModel()));
    }

    /// <summary>OtherMenu: Clear History, Clear Times, Clean Sum of Best.</summary>
    internal Platform.NativeMenuModel OtherMenuModel() => new()
    {
        Items =
        [
            new Platform.NativeMenuAction { Header = "Clear History", OnClickAsync = ClearHistoryAsync },
            new Platform.NativeMenuAction { Header = "Clear Times", OnClickAsync = ClearTimesAsync },
            new Platform.NativeMenuAction { Header = "Clean Sum of Best", OnClickAsync = CleanSumOfBestAsync },
        ],
    };

    /// <summary>The answers to Clean Sum of Best's questions for the tests (true yes, false no, null cancel).</summary>
    internal Func<string, bool?>? CleanAnswerForTests { get; set; }

    /// <summary>The messages shown (title, text), for the tests; set to skip showing them.</summary>
    internal List<(string Title, string Text)>? MessagesForTests { get; set; }

    private async Task ShowMessageAsync(string title, string text)
    {
        if (MessagesForTests is { } messages)
        {
            messages.Add((title, text));
            return;
        }

        var ok = new Button { Content = "OK", IsDefault = true };
        Window dialog = DialogChrome.Message(title, text, 420, ok);
        ok.Click += (_, _) => dialog.Close();
        await OwnedDialog.ShowAsync(this, dialog);
    }

    /// <summary>
    /// A Yes / No / Cancel question that waits for its answer, as Windows' MessageBox does
    /// inside SumOfBest.Clean's callback (the cleaning goes on with each answer).
    /// </summary>
    private bool? AskYesNoCancel(string title, string text)
    {
        if (CleanAnswerForTests is { } answerFor)
        {
            return answerFor(text);
        }

        bool? answer = null;
        var yes = new Button { Content = "Yes", IsDefault = true };
        var no = new Button { Content = "No" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        Window dialog = DialogChrome.Message(title, text, 460, cancel, no, yes);
        yes.Click += (_, _) => { answer = true; dialog.Close(); };
        no.Click += (_, _) => { answer = false; dialog.Close(); };
        cancel.Click += (_, _) => { answer = null; dialog.Close(); };
        Task shown = OwnedDialog.ShowAsync(this, dialog);
        if (!shown.IsCompleted)
        {
            var frame = new DispatcherFrame();
            shown.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.PushFrame(frame);
        }

        return answer;
    }

    /// <summary>
    /// cleanSumOfBestToolStripMenuItem_Click: SumOfBest.Clean over the edited splits, asking about
    /// each time (the same text is asked once; Cancel answers No to the rest), then "No times to
    /// clean" when there was nothing to ask.
    /// </summary>
    private async Task CleanSumOfBestAsync()
    {
        Run scratch;
        try
        {
            scratch = BuildRun(preserveHistory: true);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
            return;
        }

        bool alwaysCancel = false;
        bool prompted = false;
        var pastResponses = new Dictionary<string, bool>(StringComparer.Ordinal);
        SumOfBest.Clean(scratch, parameters =>
        {
            prompted = true;
            if (alwaysCancel)
            {
                return false;
            }

            string key = CleanSumOfBestText.Description(parameters);
            if (pastResponses.TryGetValue(key, out bool known))
            {
                return known;
            }

            bool? answer = AskYesNoCancel(CleanSumOfBestText.QuestionTitle, CleanSumOfBestText.Question(parameters));
            if (answer is bool yesOrNo)
            {
                pastResponses[key] = yesOrNo;
                return yesOrNo;
            }

            alwaysCancel = true;
            return false;
        });

        // The times removed leave the editor's segment history too (applied on OK).
        for (int i = 0; i < _rows.Count && i < scratch.Count; i++)
        {
            foreach (int id in _rows[i].SegmentHistory.Keys.Where(id => !scratch[i].SegmentHistory.ContainsKey(id)).ToList())
            {
                _rows[i].SegmentHistory.Remove(id);
            }
        }

        if (!prompted)
        {
            await ShowMessageAsync(CleanSumOfBestText.NothingTitle, CleanSumOfBestText.NothingText);
        }
    }

    /// <summary>clearHistoryToolStripMenuItem_Click: Run.ClearHistory, then "History cleared!".</summary>
    private async Task ClearHistoryAsync()
    {
        _attemptHistory.Clear();
        foreach (SegmentEditRow row in _rows)
        {
            row.SegmentHistory.Clear();
        }

        RefreshAttemptList();
        await ShowMessageAsync(UiLocalizer.T("History cleared"), UiLocalizer.T("History cleared!"));
    }

    /// <summary>clearTimesToolStripMenuItem_Click: Run.ClearTimes (attempts 0), then "Times cleared!".</summary>
    private async Task ClearTimesAsync()
    {
        ClearTimes();
        await ShowMessageAsync(UiLocalizer.T("Times cleared"), UiLocalizer.T("Times cleared!"));
    }

    private void ClearTimes()
    {
        _attemptHistory.Clear();
        AttemptsBox.Text = "0";
        ClearRunId();
        _comparisons.Clear();
        _comparisons.Add(Run.PersonalBestComparisonName);
        foreach (SegmentEditRow row in _rows)
        {
            row.BestSegmentRealText = "";
            row.BestSegmentGameText = "";
            row.RealComparisonTexts.Clear();
            row.GameComparisonTexts.Clear();
            row.RealComparisonTexts.Add("");
            row.GameComparisonTexts.Add("");
            row.SegmentHistory.Clear();
        }

        RebuildGridColumns();
        RefreshAttemptList();
    }

    private void OnDeleteAttempt(object? sender, RoutedEventArgs e)
    {
        int index = AttemptHistoryList.SelectedIndex;
        if (index < 0 || index >= _attemptHistory.Count)
        {
            return;
        }

        _attemptHistory.RemoveAt(index);
        RefreshAttemptList();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        try
        {
            Result = BuildRun(preserveHistory: true);
            Close(Result);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private Run BuildRun(bool preserveHistory)
    {
        if (!int.TryParse(AttemptsBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int attempts)
            || attempts < 0)
        {
            throw new FormatException("Attempts must be a non-negative integer.");
        }

        TimeSpan offset = string.IsNullOrWhiteSpace(OffsetBox.Text)
            ? TimeSpan.Zero
            : TimeSpanParser.Parse(OffsetBox.Text!);

        var draft = new RunEditorDraft
        {
            GameName = GameNameBox.Text?.Trim() ?? "",
            CategoryName = CategoryBox.Text?.Trim() ?? "",
            Offset = offset,
            AttemptCount = attempts,
            Version = _version,
            LayoutPath = _layoutPath,
            GameIconXml = _gameIconXml,
            AutoSplitterSettingsXml = _autoSplitterSettingsXml,
            RunId = CurrentRunId,
            PlatformName = _platformName,
            RegionName = _regionName,
            UsesEmulator = _usesEmulator,
        };
        foreach ((string k, string v) in _variables)
        {
            draft.VariableValueNames[k] = v;
        }

        foreach ((string k, string v) in _customVariables)
        {
            draft.PermanentCustomVariables[k] = v;
        }

        foreach (string unknown in _unknownRoot)
        {
            draft.UnknownRootOuterXml.Add(unknown);
        }

        draft.Comparisons.AddRange(_comparisons);
        if (preserveHistory)
        {
            draft.AttemptHistory.AddRange(_attemptHistory);
        }

        foreach (SegmentEditRow row in _rows)
        {
            var seg = new RunEditorSegmentDraft
            {
                Name = row.Name ?? "",
                IconXml = row.IconXml,
            };
            seg.SetBestSegmentTime(TimingMethod.RealTime, TimeSpanParser.ParseNullable(row.BestSegmentRealText));
            seg.SetBestSegmentTime(TimingMethod.GameTime, TimeSpanParser.ParseNullable(row.BestSegmentGameText));
            for (int i = 0; i < _comparisons.Count; i++)
            {
                string cmp = _comparisons[i];
                string rt = i < row.RealComparisonTexts.Count ? row.RealComparisonTexts[i] : "";
                string gt = i < row.GameComparisonTexts.Count ? row.GameComparisonTexts[i] : "";
                seg.SetComparisonTime(cmp, TimingMethod.RealTime, TimeSpanParser.ParseNullable(rt));
                seg.SetComparisonTime(cmp, TimingMethod.GameTime, TimeSpanParser.ParseNullable(gt));
            }

            foreach ((int id, SplitTime time) in row.SegmentHistory)
            {
                seg.SegmentHistory[id] = time;
            }

            seg.UnknownOuterXml.AddRange(row.UnknownOuterXml);
            draft.Segments.Add(seg);
        }

        return draft.ToRun(preserveAttemptHistory: preserveHistory);
    }

    private async Task<string?> PromptTextAsync(string title, string label, string initial = "")
    {
        var box = new TextBox { Text = initial, Width = 280 };
        var ok = DialogChrome.Style(new Button { Content = "OK", IsDefault = true });
        var cancel = DialogChrome.Style(new Button { Content = "Cancel", IsCancel = true });
        string? result = null;
        var dialog = DialogChrome.ConfigureLightDialog(new Window
        {
            Title = title,
            Width = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            SizeToContent = SizeToContent.Height,
        });
        ok.Click += (_, _) =>
        {
            result = box.Text?.Trim();
            if (string.IsNullOrEmpty(result))
            {
                result = null;
            }

            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = label },
                box,
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { ok, cancel },
                },
            },
        };
        await OwnedDialog.ShowAsync(this, dialog);
        return result;
    }

    private static string FormatOptional(TimeSpan? value) =>
        value.HasValue ? EditorFormatter.Format(value) : "";

    private static string FormatOffset(TimeSpan offset) =>
        offset == TimeSpan.Zero ? "0.00" : EditorFormatter.Format(offset);
}
