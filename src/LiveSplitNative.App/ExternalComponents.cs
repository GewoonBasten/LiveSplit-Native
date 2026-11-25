using Avalonia.Controls;
using Avalonia.Media;
using LiveSplitNative.ComponentHost.Compat;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;
using DrawingColor = System.Drawing.Color;
using DrawingSizeF = System.Drawing.SizeF;

namespace LiveSplitNative.App;

/// <summary>
/// Windows LiveSplit component DLLs (ComponentManager): the ones in the Components folder are
/// loaded unchanged through the look-alike assemblies, offered in the Layout Editor's Add menu by
/// their category, drawn in the layout and their settings pages shown. A layout component whose
/// Path names one of their DLLs gets its component on <see cref="LayoutComponent.RuntimeState"/>
/// (<see cref="HostedLayoutComponent"/>).
/// </summary>
internal static class ExternalComponents
{
    private static readonly Dictionary<string, IHostedComponentFactory> Factories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<(string Path, string Error)> FailuresList = [];
    private static CompatComponentLoader? _loader;
    private static ICompatRuntime? _runtime;
    private static IHostTimerSource? _source;
    private static IHostedTimer? _timer;
    private static bool _loaded;

    /// <summary>LiveSplit Native's Components folder (next to settings.cfg), as Windows' Components\.</summary>
    public static string UserComponentsDirectory =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(AppCli.SettingsPath ?? XmlAppSettingsSerializer.DefaultPath)) ?? AppContext.BaseDirectory, "Components");

    /// <summary>The loaded factories, by DLL file name.</summary>
    public static IReadOnlyCollection<IHostedComponentFactory> All
    {
        get
        {
            EnsureLoaded();
            return Factories.Values;
        }
    }

    public static IReadOnlyList<(string Path, string Error)> Failures => FailuresList;

    /// <summary>The main window's timer, which the components see as LiveSplitState.</summary>
    public static IHostTimerSource? Source
    {
        get => _source;
        set
        {
            if (!ReferenceEquals(_source, value))
            {
                _timer?.Dispose();
                _timer = null;
                _source = value;
            }
        }
    }

    /// <summary>
    /// The Components folder's DLLs are left off this session (the last one did not end
    /// normally: <see cref="ComponentCrashGuard"/>); <see cref="TurnOnUserComponents"/> loads them.
    /// </summary>
    public static bool UserComponentsOff { get; set; }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        ConfigureText();
        if (!UserComponentsOff)
        {
            Load(UserComponentsDirectory);
        }

        Load(Path.Combine(AppContext.BaseDirectory, "Components"));
    }

    /// <summary>The Components folder's DLLs after all (they were left off at start).</summary>
    public static void TurnOnUserComponents()
    {
        if (!UserComponentsOff)
        {
            return;
        }

        UserComponentsOff = false;
        if (_loaded)
        {
            Load(UserComponentsDirectory);
        }
    }

    /// <summary>Loads the component DLLs of a folder (tests and the app's own folder).</summary>
    public static void Load(string directory)
    {
        ConfigureText();
        if (!Directory.Exists(directory) || !Directory.EnumerateFiles(directory, "*.dll").Any())
        {
            return;
        }

        string compat = CompatComponentLoader.DefaultCompatDirectory;
        if (!CompatComponentLoader.LookAlikesPresent(compat))
        {
            FailuresList.Add((directory, "The look-alike assemblies are missing next to the app (compat/)."));
            return;
        }

        try
        {
            _loader ??= new CompatComponentLoader(compat);
            _runtime ??= _loader.Runtime;
            ComponentLoadReport report = _loader.LoadDirectory(directory);
            foreach (IHostedComponentFactory factory in report.Factories)
            {
                Factories.TryAdd(factory.FileName, factory);
            }

            FailuresList.AddRange(report.Failures);
        }
        catch (Exception ex)
        {
            FailuresList.Add((directory, ex.GetBaseException().Message));
        }
    }

    public static bool IsExternal(string path)
    {
        EnsureLoaded();
        return Factories.ContainsKey(path);
    }

    /// <summary>The menu name of a component DLL's path (its ComponentName).</summary>
    public static string? ComponentName(string path) =>
        IsExternal(path) ? Factories[path].ComponentName : null;

    public static void Attach(Layout layout)
    {
        foreach (LayoutComponent component in layout.Components)
        {
            Attach(component);
        }
    }

    /// <summary>
    /// Gives a layout component of a DLL its component (created by the factory, with the layout's
    /// settings), or brings an existing one's settings back to the layout's when they differ (the
    /// layout was changed, or a settings page was cancelled).
    /// </summary>
    public static void Attach(LayoutComponent component)
    {
        if (component.RuntimeState is HostedLayoutComponent hosted)
        {
            hosted.Sync(component);
            return;
        }

        if (!IsExternal(component.Path) || Timer() is not { } timer)
        {
            return;
        }

        IHostedComponent created;
        try
        {
            created = Factories[component.Path].Create(timer);
        }
        catch (Exception ex)
        {
            FailuresList.Add((component.Path, ex.GetBaseException().Message));
            return;
        }

        var state = new HostedLayoutComponent(created);
        component.RuntimeState = state;
        if (string.IsNullOrEmpty(component.SettingsInnerXml))
        {
            // A new component: the layout keeps its settings (LayoutSaver writes GetSettings).
            component.SettingsInnerXml = created.GetSettingsInnerXml();
        }

        state.Sync(component);
    }

    private static IHostedTimer? Timer()
    {
        if (_runtime is null || _source is null)
        {
            return null;
        }

        return _timer ??= _runtime.CreateTimer(_source);
    }

    private static bool _textConfigured;

    /// <summary>The components' and settings pages' text is measured as the app draws it.</summary>
    private static void ConfigureText()
    {
        if (_textConfigured)
        {
            return;
        }

        _textConfigured = true;
        HostDialogs.Current = AppHostDialogs.Instance;
        HostText.DialogFont = new HostFont(AppFonts.ChromeFamily, (float)AppFonts.ControlFontSize, true, false, false, false, false);
        // Settings pages in the settings look (Basten, 27 Oct sketch): input controls as tall as
        // ours (26), roomier check box lines, group boxes as cards under their heading (the card
        // starts 6 below the caption, its content 10 inside it) with the app's gap between them,
        // and the label columns of a page's groups lined up. A table's rows are as tall as the
        // app's own (36, their controls centred) and its label column as wide (212), so a page
        // made of label tables looks like the app's own pages (Basten 29 Oct).
        HostText.Spacing = new HostSpacing(
            ControlHeight: 26,
            CheckHeight: 22,
            GroupPaddingX: 14,
            GroupPaddingTop: 16,
            // The card's bottom edge is drawn from its rows (HostedSettingsView.GroupView), so
            // the group keeps no room of its own below them.
            GroupPaddingBottom: 0,
            // Measured on screen: with the heading above the card this gives the app's 22 from
            // a card to the next heading.
            GroupGap: 10,
            AlignLabelColumns: true,
            RowHeight: 36,
            LabelColumn: 212,
            // Room above and below a row's controls (a list or a paragraph gets air), and lists
            // with rows as roomy as the app's tables.
            RowPadding: 8,
            ListRowHeight: 26,
            ListHeaderHeight: 28);
        HostText.Measure = (text, font) =>
        {
            FormattedText formatted = AvaloniaHostCanvas.Format(text, font, Brushes.Black);
            return new DrawingSizeF((float)formatted.WidthIncludingTrailingWhitespace, (float)formatted.Height);
        };
        HostText.CellMetrics = AvaloniaHostCanvas.CellMetrics;
    }
}

/// <summary>A component DLL's component on a layout component, with what the layout needs of it.</summary>
internal sealed class HostedLayoutComponent : IExternalComponent
{
    private string? _appliedXml;
    private string? _appliedOwn;

    public HostedLayoutComponent(IHostedComponent component) => Component = component;

    public IHostedComponent Component { get; }

    public string ComponentName => Component.ComponentName;

    public double VerticalHeight => Component.VerticalHeight;

    public double HorizontalWidth => Component.HorizontalWidth;

    public double PaddingTop => Component.PaddingTop;

    public double PaddingBottom => Component.PaddingBottom;

    public double PaddingLeft => Component.PaddingLeft;

    public double PaddingRight => Component.PaddingRight;

    /// <summary>The sizes the layout's current snapshot was measured with.</summary>
    public double MeasuredVerticalHeight { get; set; }

    public double MeasuredHorizontalWidth { get; set; }

    /// <summary>The component takes the layout's settings when they changed, or when its own did since.</summary>
    public void Sync(LayoutComponent layoutComponent)
    {
        string? xml = layoutComponent.SettingsInnerXml;
        if (string.IsNullOrEmpty(xml))
        {
            return;
        }

        if (xml != _appliedXml || Component.GetSettingsInnerXml() != _appliedOwn)
        {
            Component.SetSettingsInnerXml(xml);
            _appliedXml = xml;
            _appliedOwn = Component.GetSettingsInnerXml();
        }
    }

    /// <summary>The settings the component has now (after its settings page changed them).</summary>
    public string ReadSettings()
    {
        string xml = Component.GetSettingsInnerXml();
        _appliedXml = xml;
        _appliedOwn = xml;
        return xml;
    }
}

/// <summary>The app's dialogs for settings pages (SettingsHelper.ColorButtonClick).</summary>
internal sealed class AppHostDialogs : IHostDialogs
{
    public static AppHostDialogs Instance { get; } = new();

    public async Task<DrawingColor?> PickColorAsync(DrawingColor current, object? owner, Action<DrawingColor>? live)
    {
        Window? window = HostedSettingsView.WindowOf(owner as IHostControl);
        ArgbColor? picked = await ColorPickerDialog.PickAsync(
            window,
            new ArgbColor(current.A, current.R, current.G, current.B),
            color => live?.Invoke(DrawingColor.FromArgb(color.A, color.R, color.G, color.B)));
        return picked is { } c ? DrawingColor.FromArgb(c.A, c.R, c.G, c.B) : null;
    }

    /// <summary>A modal dialog for a component: the dispatcher keeps running until it is closed.</summary>
    public DrawingColor? PickColorModal(DrawingColor current, object? owner, Action<DrawingColor>? live)
    {
        Task<DrawingColor?> task = PickColorAsync(current, owner, live);
        if (!task.IsCompleted)
        {
            var frame = new Avalonia.Threading.DispatcherFrame();
            task.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
            Avalonia.Threading.Dispatcher.UIThread.PushFrame(frame);
        }

        return task.Status == TaskStatus.RanToCompletion ? task.Result : null;
    }
}
