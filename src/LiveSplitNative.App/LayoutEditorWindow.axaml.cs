using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

public partial class LayoutEditorWindow : Window
{
    private readonly Layout _layout;
    private readonly INativeContextMenu _nativeContextMenu = NativeContextMenuFactory.Current;

    public LayoutEditorWindow() : this(StandardLayoutFactory.Create())
    {
    }

    public LayoutEditorWindow(Layout layout)
    {
        InitializeComponent();
        _layout = layout.Clone();
        VerticalRadio.IsChecked = _layout.Mode == LayoutMode.Vertical;
        HorizontalRadio.IsChecked = _layout.Mode == LayoutMode.Horizontal;
        VerticalRadio.IsCheckedChanged += (_, _) => NotifyPreview();
        HorizontalRadio.IsCheckedChanged += (_, _) => NotifyPreview();
        RefreshList();
    }

    public Layout? Result { get; private set; }

    /// <summary>Push working layout to the live timer (host SettingsEditSession).</summary>
    public Action<Layout>? LivePreview { get; set; }

    /// <summary>
    /// LayoutEditorDialog.LayoutResized: a component was added or removed (before the preview
    /// of it), so the host drops the other orientation's saved size.
    /// </summary>
    public Action? LayoutResized { get; set; }

    public Layout WorkingLayout => _layout;

    private void NotifyPreview()
    {
        _layout.Mode = HorizontalRadio.IsChecked == true ? LayoutMode.Horizontal : LayoutMode.Vertical;
        LivePreview?.Invoke(_layout);
    }

    /// <summary>Select a component row for screenshot / selection-chrome proof.</summary>
    public void SelectComponentRow(int index)
    {
        if (index < 0 || index >= _layout.Components.Count)
        {
            return;
        }

        ComponentsList.SelectedIndex = index;
    }

    private void RefreshList()
    {
        int selected = ComponentsList.SelectedIndex;
        ComponentsList.ItemsSource = null;
        ComponentsList.Items.Clear();
        // Name-only rows — decorative component-type icons belong on the Add menu only
        // (Windows Layout Editor list is text; game/segment icons stay on the timer surface).
        foreach (LayoutComponent component in _layout.Components)
        {
            ComponentsList.Items.Add(new TextBlock
            {
                Text = component.DisplayName,
                FontSize = AppFonts.ControlFontSize,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });
        }

        if (selected >= 0 && selected < _layout.Components.Count)
        {
            ComponentsList.SelectedIndex = selected;
        }

        FitListHeight();
    }

    /// <summary>
    /// Content-aware list height: grow with item count, scroll past MaxHeight, never
    /// leave a huge empty stadium under a short layout.
    /// </summary>
    private void FitListHeight()
    {
        const double rowPitch = 28;
        const double minList = 84;
        const double maxList = 420;
        // Exactly the rows — border host sizes to this so no empty stadium under a short layout.
        double content = Math.Max(1, _layout.Components.Count) * rowPitch;
        double h = Math.Clamp(content, minList, maxList);
        ComponentsList.Height = h;
        ComponentsList.MinHeight = h;
        ComponentsList.MaxHeight = maxList;
        ComponentsList.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        if (ComponentsList.Parent is Border host)
        {
            host.Height = h;
            host.MinHeight = h;
            host.MaxHeight = maxList;
            host.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        }
    }

    private async void OnComponentSettings(object? sender, RoutedEventArgs e) =>
        await EditSelectedComponentSettingsAsync();

    private async void OnComponentDoubleTapped(object? sender, TappedEventArgs e) =>
        await EditSelectedComponentSettingsAsync();

    private async Task EditSelectedComponentSettingsAsync()
    {
        int index = ComponentsList.SelectedIndex;
        // lbxComponents_MouseDoubleClick: only a component with settings opens them.
        if (index < 0 || index >= _layout.Components.Count || !_layout.Components[index].HasSettings)
        {
            return;
        }

        LayoutComponent original = _layout.Components[index].Clone();
        var dialog = new ComponentSettingsWindow(_layout.Components[index], _layout.Mode)
        {
            LivePreview = component =>
            {
                _layout.Components[index] = component;
                NotifyPreview();
            },
        };
        LayoutComponent? result = await OwnedDialog.ShowAsync<LayoutComponent?>(this, dialog);
        if (result is null)
        {
            _layout.Components[index] = original;
            NotifyPreview();
            return;
        }

        _layout.Components[index] = result;
        _layout.HasChanged = true;
        RefreshList();
        NotifyPreview();
    }

    /// <summary>
    /// Windows LayoutEditorDialog menuAddComponents: category → submenu of factories,
    /// then separator + "Download More...". Shown via <see cref="INativeContextMenu"/> (NSMenu on macOS).
    /// </summary>
    public NativeMenuModel BuildAddComponentMenuModel(Action<string> onAddPath)
    {
        var items = new List<NativeMenuEntry>();
        foreach (StandardLayoutFactory.ComponentCategory category in StandardLayoutFactory.AddMenuCategories)
        {
            IEnumerable<StandardLayoutFactory.BuiltinComponentInfo> catalog = StandardLayoutFactory.BuiltinCatalog
                .Where(c => c.Category == category)
                .OrderBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase);

            // The built-in components and the Components folder's DLLs of this category, by name.
            var entries = catalog.Select(info => (Name: info.DisplayName, info.Path)).ToList();
            entries.AddRange(ExternalComponents.All
                .Where(f => f.Category - 1 == (int)category)
                .Select(f => (Name: f.ComponentName, Path: f.FileName)));
            var children = new List<NativeMenuEntry>();
            foreach ((string name, string path) in entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            {
                children.Add(new NativeMenuAction
                {
                    Header = name,
                    OnClick = () => onAddPath(path),
                });
            }

            items.Add(new NativeMenuAction
            {
                Header = category.ToString(),
                Children = children,
            });
        }

        items.Add(new NativeMenuSeparator());
        items.Add(new NativeMenuAction
        {
            Header = "Download More...",
            IsEnabled = false,
        });
        return new NativeMenuModel { Items = items };
    }

    /// <summary>Proof host: hierarchy strip (Timer/List children expanded). Not OS chrome.</summary>
    public static Control CreateAddComponentMenuProofVisual()
    {
        var host = new LayoutEditorWindow();
        NativeMenuModel model = host.BuildAddComponentMenuModel(_ => { });
        var strip = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            MinWidth = 160,
        };
        foreach (NativeMenuEntry entry in model.Items)
        {
            if (entry is NativeMenuSeparator)
            {
                strip.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(4, 3),
                    Background = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0)),
                });
                continue;
            }

            var top = (NativeMenuAction)entry;
            bool hasKids = top.Children is { Count: > 0 };
            strip.Children.Add(new MenuItem
            {
                Header = hasKids ? $"{top.Header}  ›" : top.Header,
                FontSize = 13,
                MinHeight = 22,
                Padding = new Thickness(8, 2, 16, 2),
                IsEnabled = top.IsEnabled,
                Foreground = AppThemeController.GetDialogBrush("LsDialogForeground"),
            });
            if (hasKids && (string.Equals(top.Header, "Timer", StringComparison.Ordinal)
                || string.Equals(top.Header, "List", StringComparison.Ordinal)))
            {
                foreach (NativeMenuEntry child in top.Children!)
                {
                    if (child is not NativeMenuAction mi)
                    {
                        continue;
                    }

                    var row = new MenuItem
                    {
                        Header = "    " + mi.Header,
                        FontSize = 13,
                        MinHeight = 22,
                        Padding = new Thickness(8, 2, 16, 2),
                        IsEnabled = mi.IsEnabled,
                        Foreground = mi.IsEnabled
                            ? AppThemeController.GetDialogBrush("LsDialogForeground")
                            : AppThemeController.GetDialogBrush("LsDialogMutedForeground"),
                    };
                    row.Icon = mi.Header switch
                    {
                        "Detailed Timer" => ComponentMenuIcons.DetailedTimerIcon(),
                        "Timer" => ComponentMenuIcons.TimerIcon(),
                        "Subsplits" => ComponentMenuIcons.SubsplitsIcon(),
                        "Splits" => ComponentMenuIcons.SplitsIcon(),
                        _ => null,
                    };
                    strip.Children.Add(row);
                }
            }
        }

        return new Border
        {
            Background = AppThemeController.GetDialogBrush("LsDialogMenuBackground"),
            BorderBrush = AppThemeController.GetDialogBrush("LsDialogBorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(0),
            Child = strip,
        };
    }

    private void OnAdd(object? sender, RoutedEventArgs e)
    {
        NativeMenuModel model = UiLocalizer.Apply(BuildAddComponentMenuModel(AddComponent));
        // Windows shows the menu beside btnAdd (+).
        PixelPoint screen = BtnAdd.PointToScreen(new Point(BtnAdd.Bounds.Width, BtnAdd.Bounds.Height / 2));
        _nativeContextMenu.Show(this, screen, model);
    }

    /// <summary>An Add menu item: the new component goes at the end and is selected.</summary>
    internal void AddComponent(string path)
    {
        LayoutComponent added = StandardLayoutFactory.CreateBuiltin(path);
        // A component DLL gets its component (ComponentManager: factory.Create(CurrentState)).
        ExternalComponents.Attach(added);
        InheritBackground(added, _layout.Components);
        _layout.Components.Add(added);
        _layout.HasChanged = true;
        LayoutResized?.Invoke();
        RefreshList();
        ComponentsList.SelectedIndex = _layout.Components.Count - 1;
        NotifyPreview();
        NotifyPreview();
    }

    /// <summary>The background settings a new component takes from the rows above it.</summary>
    private static readonly string[] BackgroundSettings = ["BackgroundColor", "BackgroundColor2", "BackgroundGradient"];

    /// <summary>
    /// LiveSplitNative (Basten 28 Oct, "connected and applied the same as rows above"): a new
    /// component takes the background of the nearest component above it that has one, so it
    /// joins the rows instead of standing out (Windows gives a new component its own default,
    /// often transparent). Only a component with background settings takes them; it can still be changed.
    /// </summary>
    internal static void InheritBackground(LayoutComponent added, IReadOnlyList<LayoutComponent> above)
    {
        var hosted = added.RuntimeState as HostedLayoutComponent;
        string ownXml = hosted?.Component.GetSettingsInnerXml() ?? added.SettingsInnerXml ?? "";
        System.Xml.XmlElement? own = SettingsRoot(ownXml);
        if (own is null || !BackgroundSettings.Any(name => own[name] is not null))
        {
            return;
        }

        System.Xml.XmlElement? source = above.Reverse()
            .Select(c => SettingsRoot(c.RuntimeState is HostedLayoutComponent h ? h.Component.GetSettingsInnerXml() : c.SettingsInnerXml ?? ""))
            // A row with a plain or gradient background (not the Splits' alternating rows).
            .FirstOrDefault(xml => xml?["BackgroundColor"] is not null
                && (xml["BackgroundGradient"]?.InnerText is null or "Plain" or "Vertical" or "Horizontal"));
        if (source is null)
        {
            return;
        }

        // A component with a background setting reads all three (a missing one is its default).
        foreach (string name in BackgroundSettings)
        {
            if (source[name] is { } value)
            {
                System.Xml.XmlElement target = own[name] ?? (System.Xml.XmlElement)own.AppendChild(own.OwnerDocument.CreateElement(name))!;
                target.InnerText = value.InnerText;
            }
        }

        added.SettingsInnerXml = own.InnerXml;
        if (hosted is not null)
        {
            // Sync applies the layout's settings to the component.
            ExternalComponents.Attach(added);
        }
    }

    private static System.Xml.XmlElement? SettingsRoot(string innerXml)
    {
        if (string.IsNullOrWhiteSpace(innerXml))
        {
            return null;
        }

        try
        {
            var document = new System.Xml.XmlDocument();
            document.LoadXml("<Settings>" + innerXml + "</Settings>");
            return document.DocumentElement;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>What a double-click on the selected component does, for the app tests.</summary>
    internal Task EditSelectedComponentSettingsForTestsAsync() => EditSelectedComponentSettingsAsync();

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        int index = ComponentsList.SelectedIndex;
        if (index < 0 || index >= _layout.Components.Count)
        {
            return;
        }

        _layout.Components.RemoveAt(index);
        _layout.HasChanged = true;
        LayoutResized?.Invoke();
        RefreshList();
        NotifyPreview();
    }

    private void OnMoveUp(object? sender, RoutedEventArgs e) => MoveSelected(-1);

    private void OnMoveDown(object? sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        int index = ComponentsList.SelectedIndex;
        int target = index + offset;
        if (index < 0 || target < 0 || target >= _layout.Components.Count)
        {
            return;
        }

        LayoutComponent item = _layout.Components[index];
        _layout.Components.RemoveAt(index);
        _layout.Components.Insert(target, item);
        _layout.HasChanged = true;
        RefreshList();
        ComponentsList.SelectedIndex = target;
        NotifyPreview();
    }

    private async void OnLayoutSettings(object? sender, RoutedEventArgs e)
    {
        Layout before = _layout.Clone();
        var dialog = new LayoutSettingsWindow(_layout)
        {
            LiveLayoutPreview = working =>
            {
                _layout.Settings.Assign(working.Settings);
                _layout.Components.Clear();
                foreach (LayoutComponent c in working.Components)
                {
                    _layout.Components.Add(c.Clone());
                }

                NotifyPreview();
            },
        };
        Layout? result = await OwnedDialog.ShowAsync<Layout?>(this, dialog);
        if (result is null)
        {
            _layout.Assign(before);
            RefreshList();
            NotifyPreview();
            return;
        }

        _layout.Settings.Assign(result.Settings);
        _layout.Components.Clear();
        foreach (LayoutComponent c in result.Components)
        {
            _layout.Components.Add(c.Clone());
        }

        _layout.HasChanged = true;
        RefreshList();
        NotifyPreview();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _layout.Mode = HorizontalRadio.IsChecked == true ? LayoutMode.Horizontal : LayoutMode.Vertical;
        Result = _layout;
        Close(Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
