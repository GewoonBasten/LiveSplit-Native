using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

public partial class ComponentSettingsWindow : Window
{
    private readonly ComponentSettingsEditor _editor;

    /// <summary>
    /// Windows-like dialog cap: content scrolls inside; OK/Cancel stay docked.
    /// Prefer ~720px or 85% of working area — not a near-fullscreen window.
    /// </summary>
    private const double PreferredMaxContentDialogHeight = 720;

    public ComponentSettingsWindow() : this(new LayoutComponent(StandardLayoutFactory.TitlePath))
    {
    }

    public ComponentSettingsWindow(
        LayoutComponent component,
        LayoutMode layoutMode = LayoutMode.Vertical)
    {
        InitializeComponent();
        DialogChrome.ConfigureLightDialog(this);
        ApplyScreenCappedMaxHeight();
        _editor = new ComponentSettingsEditor(component, layoutMode);
        _editor.SettingsChanged = () => LivePreview?.Invoke(_editor.Apply());
        Title = $"Component Settings — {_editor.TabHeader}";
        Host.Content = _editor;
        // A component DLL's page has its own (WinForms) width: the dialog fits it, as Windows'
        // ComponentSettingsDialog fits its control.
        if (_editor.HostedPageWidth > 0)
        {
            Width = Math.Max(Width, _editor.HostedPageWidth + ComponentSettingsEditor.PagePadding.Left + ComponentSettingsEditor.PagePadding.Right);
        }

        Opened += (_, _) => RefitHeight();
    }

    public LayoutComponent? Result { get; private set; }

    /// <summary>Live preview of the working component (parent nests into layout session).</summary>
    public Action<LayoutComponent>? LivePreview { get; set; }

    private void ApplyScreenCappedMaxHeight()
    {
        double screen = MenuChrome.ResolveScreenMaxHeightDipPublic(this);
        // Leave chrome for title bar + docked OK/Cancel; scroll fields inside.
        MaxHeight = Math.Min(PreferredMaxContentDialogHeight, Math.Max(280, screen * 0.85));
        SizeToContent = SizeToContent.Manual;
    }

    /// <summary>
    /// Size the window to content up to MaxHeight. When taller, enable field scrolling
    /// and keep the OK/Cancel dock fully visible (Windows-like content scroll).
    /// </summary>
    public void RefitHeight()
    {
        ApplyScreenCappedMaxHeight();
        SizeToContent = SizeToContent.Manual;
        UpdateLayout();
        double availW = Bounds.Width > 1 ? Bounds.Width : Width;
        double contentH = _editor.MeasureContentHeight(availW);
        // The footer (OK / Cancel) as it measures; the page has its own padding. (Height is the
        // client area: the title bar is not part of it.)
        Footer.Measure(new Avalonia.Size(availW, double.PositiveInfinity));
        double chrome = Footer.DesiredSize.Height > 1 ? Footer.DesiredSize.Height : 56;
        double target = contentH + chrome;
        double cap = MaxHeight > 0 ? MaxHeight : target;
        Height = Math.Min(cap, Math.Max(MinHeight, target));
        bool needsScroll = target > Height + 1;
        HostScroll.VerticalScrollBarVisibility = needsScroll
            ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            : Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
        HostScroll.Padding = new Avalonia.Thickness(0, 0, 0, needsScroll ? 4 : 0);
        UpdateLayout();
    }

    /// <summary>
    /// Render the full settings field list at its measured height (not screen-capped
    /// window Bounds). Used for inventory proofs where the form exceeds the display.
    /// </summary>
    public void SaveFullContentPng(string path)
    {
        const double width = 600;
        double fieldsH = _editor.MeasureContentHeight(width);
        // Detach from Host before parenting into the off-screen root.
        Host.Content = null;

        const double footer = 58;
        double totalH = fieldsH + footer;
        int pw = (int)Math.Ceiling(width);
        int ph = (int)Math.Ceiling(totalH);
        var root = new DockPanel
        {
            Width = width,
            Height = totalH,
            Background = AppThemeController.GetDialogBrush("LsDialogWindowBackground"),
        };
        var buttons = new Border
        {
            Classes = { "dialogFooter" },
            Child = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = 10,
                Children =
                {
                    new Button { Content = "Cancel", Classes = { "dialog" }, MinWidth = 75 },
                    new Button { Content = "OK", Classes = { "dialog", "primary" }, MinWidth = 75 },
                },
            },
        };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(_editor);
        root.Measure(new Size(width, totalH));
        root.Arrange(new Rect(0, 0, width, totalH));

        using var bitmap = new RenderTargetBitmap(new PixelSize(pw, ph), new Vector(96, 96));
        bitmap.Render(root);
        bitmap.Save(path);

        root.Children.Remove(_editor);
        Host.Content = _editor;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Result = _editor.Apply();
        Close(Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
