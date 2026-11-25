using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace LiveSplitNative.App;

/// <summary>
/// WinForms-style folder <see cref="TabControl"/> with horizontal strip overflow arrows.
/// Arrows live in reserved left/right chrome (fixed columns) — never overlay tab faces.
/// Tabs are clipped to the center strip only; scroll via <see cref="TranslateTransform"/>.
/// </summary>
public sealed class DialogTabControl : TabControl
{
    private const double ChromeWidth = 28;

    /// <summary>
    /// The page gets the height left under the strip (a star row) instead of its own height
    /// (an Auto row, which measures it with unlimited height). For fixed-size dialogs whose page
    /// scrolls itself, such as the Splits Editor's grids: measured with unlimited height, a
    /// DataGrid takes the whole window's height as its viewport, so its last rows sat below the
    /// visible page and could not be scrolled to.
    /// </summary>
    public static readonly StyledProperty<bool> FillsHeightProperty =
        AvaloniaProperty.Register<DialogTabControl, bool>(nameof(FillsHeight));

    public bool FillsHeight
    {
        get => GetValue(FillsHeightProperty);
        set => SetValue(FillsHeightProperty, value);
    }

    private ItemsPresenter? _itemsPresenter;
    private Border? _stripClip;
    private Border? _leftChrome;
    private Border? _rightChrome;
    private Button? _scrollLeft;
    private Button? _scrollRight;
    private TranslateTransform? _stripTransform;
    private double _offsetX;
    private bool _chromeReserved;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_scrollLeft is not null)
        {
            _scrollLeft.Click -= OnScrollLeft;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.Click -= OnScrollRight;
        }

        base.OnApplyTemplate(e);

        if (FillsHeight
            && e.NameScope.Find<Grid>("PART_LayoutRoot") is { RowDefinitions.Count: 2 } root
            && e.NameScope.Find<ContentPresenter>("PART_SelectedContentHost") is { } page)
        {
            root.RowDefinitions[1].Height = GridLength.Star;
            page.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            page.VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        }

        _itemsPresenter = e.NameScope.Find<ItemsPresenter>("PART_ItemsPresenter");
        _stripClip = e.NameScope.Find<Border>("PART_TabStripClip");
        _leftChrome = e.NameScope.Find<Border>("PART_LeftChrome");
        _rightChrome = e.NameScope.Find<Border>("PART_RightChrome");
        _scrollLeft = e.NameScope.Find<Button>("PART_ScrollLeft");
        _scrollRight = e.NameScope.Find<Button>("PART_ScrollRight");

        if (_itemsPresenter is not null)
        {
            _stripTransform = new TranslateTransform();
            _itemsPresenter.RenderTransform = _stripTransform;
        }

        if (_scrollLeft is not null)
        {
            _scrollLeft.Click += OnScrollLeft;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.Click += OnScrollRight;
        }

        // Start collapsed; UpdateOverflowButtons reserves chrome when needed.
        SetChromeReserved(false);

        SizeChanged += (_, _) => UpdateOverflowButtons();
        SelectionChanged += (_, _) => Dispatcher.UIThread.Post(EnsureSelectedTabVisible, DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(UpdateOverflowButtons, DispatcherPriority.Loaded);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty || change.Property == ItemsSourceProperty)
        {
            UpdateOverflowButtons();
        }
    }

    private void OnScrollLeft(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        double step = Math.Max(48, (_stripClip?.Bounds.Width ?? 120) * 0.4);
        _offsetX = Math.Max(0, _offsetX - step);
        ApplyOffset();
        UpdateOverflowButtons();
    }

    private void OnScrollRight(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        double step = Math.Max(48, (_stripClip?.Bounds.Width ?? 120) * 0.4);
        double max = Math.Max(0, StripContentWidth() - (_stripClip?.Bounds.Width ?? 0));
        _offsetX = Math.Min(max, _offsetX + step);
        ApplyOffset();
        UpdateOverflowButtons();
    }

    private void ApplyOffset()
    {
        if (_stripTransform is not null)
        {
            _stripTransform.X = -_offsetX;
        }
    }

    private double StripContentWidth()
    {
        if (_itemsPresenter is null)
        {
            return 0;
        }

        _itemsPresenter.Measure(Size.Infinity);
        return _itemsPresenter.DesiredSize.Width;
    }

    private void SetChromeReserved(bool reserve)
    {
        _chromeReserved = reserve;
        double w = reserve ? ChromeWidth : 0;
        if (_leftChrome is not null)
        {
            _leftChrome.Width = w;
            _leftChrome.IsVisible = reserve;
        }

        if (_rightChrome is not null)
        {
            _rightChrome.Width = w;
            _rightChrome.IsVisible = reserve;
        }

        // Gap between chrome and tab faces so ▶ never sits on clipped label pixels.
        if (_stripClip is not null)
        {
            _stripClip.Margin = reserve ? new Thickness(6, 0, 6, 0) : default;
        }
    }

    /// <summary>Force overflow chrome + clip layout (screenshot / after adding many tabs).</summary>
    public void RefreshOverflowChrome()
    {
        UpdateOverflowButtons();
        InvalidateArrange();
        UpdateLayout();
        UpdateOverflowButtons();
    }

    private void EnsureSelectedTabVisible()
    {
        UpdateOverflowButtons();
        if (_stripClip is null || SelectedIndex < 0 || ContainerFromIndex(SelectedIndex) is not Control tab)
        {
            return;
        }

        tab.UpdateLayout();
        double tabLeft = tab.Bounds.Left;
        double tabRight = tab.Bounds.Right;
        double viewLeft = _offsetX;
        double viewRight = viewLeft + _stripClip.Bounds.Width;
        if (tabLeft < viewLeft)
        {
            _offsetX = Math.Max(0, tabLeft - 4);
        }
        else if (tabRight > viewRight)
        {
            _offsetX = Math.Max(0, tabRight - _stripClip.Bounds.Width + 4);
        }

        ApplyOffset();
        UpdateOverflowButtons();
    }

    private void UpdateOverflowButtons()
    {
        if (_stripClip is null || _itemsPresenter is null)
        {
            return;
        }

        // Total strip width available (this control's arranged width).
        double totalW = Bounds.Width > 1 ? Bounds.Width : DesiredSize.Width;
        if (totalW < 1)
        {
            return;
        }

        double contentW = StripContentWidth();
        // Need chrome if tabs don't fit in the full width (or in the shrunk center).
        bool need = contentW > totalW - 1;
        if (!need && _chromeReserved)
        {
            // Already reserved: still need if content exceeds center.
            need = contentW > Math.Max(0, totalW - ChromeWidth * 2) - 1;
        }

        if (need != _chromeReserved)
        {
            SetChromeReserved(need);
            // Remeasure so center clip shrinks before we compare overflow.
            InvalidateArrange();
            UpdateLayout();
        }

        _stripClip.UpdateLayout();
        double viewW = _stripClip.Bounds.Width;
        double overflow = Math.Max(0, contentW - viewW);
        _offsetX = Math.Clamp(_offsetX, 0, overflow);
        ApplyOffset();

        if (_scrollLeft is not null)
        {
            _scrollLeft.IsVisible = need;
            _scrollLeft.IsEnabled = need && _offsetX > 0.5;
        }

        if (_scrollRight is not null)
        {
            _scrollRight.IsVisible = need;
            _scrollRight.IsEnabled = need && _offsetX < overflow - 0.5;
        }
    }
}
