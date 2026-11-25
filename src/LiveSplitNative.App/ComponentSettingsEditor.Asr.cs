using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// LiveSplit.AutoSplittingRuntime ComponentSettings: Script Path with Browse... (a WebAssembly
/// module), and the Auto Splitter Settings group the auto splitter declares (BuildTree): check
/// boxes, "—  " titles that indent what follows by 20 per heading level, choices as a label and a
/// list, and file selections as buttons (lists and buttons the group's width). A change goes
/// straight to the auto splitter's settings map.
/// </summary>
public sealed partial class ComponentSettingsEditor
{
    private const string ScriptExtension = ".wasm";

    private void BuildAutoSplittingRuntimeFields()
    {
        AutoSplittingRuntimeSettings settings = AutoSplittingRuntimeSettings.FromInnerXml(_component.SettingsInnerXml);
        AsrComponentRuntime holder = AutoSplittingRuntimeController.For(_component);

        var pathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var label = new TextBlock { Text = "Script Path:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
        var path = new TextBox { Text = settings.ScriptPath, VerticalAlignment = VerticalAlignment.Center };
        Button browse = MakeDialogButton("Browse...");
        browse.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(path, 1);
        Grid.SetColumn(browse, 2);
        pathRow.Children.Add(label);
        pathRow.Children.Add(path);
        pathRow.Children.Add(browse);
        Rows(_fields).Add(pathRow);

        SettingsCard group = BeginGroup("Auto Splitter Settings");
        // settingsTable: a fixed 431 high in the group (the page is always the same size), scrolling.
        var rows = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 5, 0) };
        group.Children.Add(new ScrollViewer
        {
            Height = 431,
            Content = rows,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        });
        string shown = "";
        int shownVersion = -1;

        // BuildTree: the widgets as they are now (rebuilt when the auto splitter changes them).
        void Rebuild()
        {
            AsrRuntime? runtime = holder.Runtime;
            IReadOnlyList<AsrWidget> widgets = runtime?.GetWidgets() ?? [];
            string signature = string.Join("\n", widgets.Select(w => $"{w.Kind}|{w.Key}|{w.Description}|{w.HeadingLevel}|{w.Bool}|{w.ChoiceIndex}|{w.Choices.Count}"));
            if (signature == shown && shownVersion == holder.SettingsVersion)
            {
                return;
            }

            shown = signature;
            shownVersion = holder.SettingsVersion;
            rows.Children.Clear();
            double margin = 3;
            foreach (AsrWidget widget in widgets)
            {
                switch (widget.Kind)
                {
                    case "bool":
                    {
                        var check = new CheckBox { Content = widget.Description, IsChecked = widget.Bool, Margin = new Thickness(margin, 0, 0, 0) };
                        SetAsrTip(check, widget.Tooltip);
                        string key = widget.Key;
                        check.IsCheckedChanged += (_, _) =>
                        {
                            holder.Runtime?.SetBool(key, check.IsChecked == true);
                            shown = "";
                            NotifyPreview();
                        };
                        rows.Children.Add(check);
                        break;
                    }

                    case "title":
                    {
                        margin = 20 * widget.HeadingLevel;
                        var title = new TextBlock { Text = "—  " + widget.Description, Margin = new Thickness(margin, 3, 0, 0) };
                        SetAsrTip(title, widget.Tooltip);
                        rows.Children.Add(title);
                        margin += 20;
                        break;
                    }

                    case "choice":
                    {
                        var choiceLabel = new TextBlock { Text = widget.Description, Margin = new Thickness(margin, 0, 0, 0) };
                        SetAsrTip(choiceLabel, widget.Tooltip);
                        rows.Children.Add(choiceLabel);
                        var combo = new ComboBox
                        {
                            ItemsSource = widget.Choices.Select(c => c.Description).ToList(),
                            SelectedIndex = widget.ChoiceIndex,
                            Margin = new Thickness(margin, 0, 0, 5),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                        };
                        SetAsrTip(combo, widget.Tooltip);
                        string key = widget.Key;
                        IReadOnlyList<(string Key, string Description)> choices = widget.Choices;
                        combo.SelectionChanged += (_, _) =>
                        {
                            if (combo.SelectedIndex >= 0 && combo.SelectedIndex < choices.Count)
                            {
                                holder.Runtime?.SetString(key, choices[combo.SelectedIndex].Key);
                                shown = "";
                                NotifyPreview();
                            }
                        };
                        rows.Children.Add(combo);
                        break;
                    }

                    case "file-select":
                    {
                        Button button = MakeDialogButton(widget.Description);
                        button.Margin = new Thickness(margin, 0, 0, 0);
                        button.HorizontalAlignment = HorizontalAlignment.Stretch;
                        SetAsrTip(button, widget.Tooltip);
                        string key = widget.Key;
                        string filter = widget.FileFilter;
                        button.Click += async (_, _) =>
                        {
                            string? chosen = await PickFileAsync("", FileTypes(filter), CurrentFile(key));
                            if (chosen is not null)
                            {
                                holder.Runtime?.SetString(key, AsrRuntime.PathToWasi(chosen));
                                shown = "";
                                NotifyPreview();
                            }
                        };
                        rows.Children.Add(button);
                        break;
                    }
                }
            }
        }

        // The file a file-select setting holds now (wasi_to_path), to start the dialog there.
        string CurrentFile(string key)
        {
            KeyValuePair<string, AsrSettingValue>? stored = holder.Runtime?.GetSettings().FirstOrDefault(p => p.Key == key);
            return stored?.Value is { Kind: AsrValueKind.String } value ? AsrRuntime.WasiToPath(value.String) : "";
        }

        void LoadScript(string scriptPath)
        {
            AutoSplittingRuntimeSettings current = AutoSplittingRuntimeSettings.FromInnerXml(_component.SettingsInnerXml);
            current.ScriptPath = scriptPath;
            // A new script starts with its own defaults (ReloadRuntime(null)).
            current.CustomSettings = null;
            AutoSplittingRuntimeController.Current?.Apply(holder, current);
            if (AutoSplittingRuntimeController.Current is null)
            {
                holder.Load(scriptPath, null, NullAsrTimer.Instance);
            }

            Rebuild();
            NotifyPreview();
        }

        // The path is saved as it is typed; the auto splitter loads when the field is left.
        path.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                NotifyPreview();
            }
        };
        path.LostFocus += (_, _) =>
        {
            if ((path.Text ?? "") != holder.ScriptPath)
            {
                LoadScript(path.Text ?? "");
            }
        };
        browse.Click += async (_, _) =>
        {
            string? chosen = await PickFileAsync(
                "Script Path",
                [
                    new FilePickerFileType($"WebAssembly module (*{ScriptExtension})") { Patterns = ["*" + ScriptExtension] },
                    new FilePickerFileType("All Files (*.*)") { Patterns = ["*"] },
                ],
                path.Text ?? "");
            if (chosen is not null)
            {
                path.Text = chosen;
                LoadScript(chosen);
            }
        };

        // ASRComponent rebuilds the tree on every update when the settings changed.
        var watch = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        watch.Tick += (_, _) => Rebuild();
        AttachedToVisualTree += (_, _) => watch.Start();
        DetachedFromVisualTree += (_, _) => watch.Stop();
        Rebuild();

        _applyMore = () =>
        {
            AutoSplittingRuntimeSettings current = AutoSplittingRuntimeSettings.FromInnerXml(_component.SettingsInnerXml);
            current.ScriptPath = path.Text ?? "";
            if (holder.Runtime is { } runtime && holder.ScriptPath == current.ScriptPath)
            {
                current.CustomSettings = runtime.GetSettings();
            }

            _component.SettingsInnerXml = current.ToInnerXml();
        };
    }

    // toolTip.SetToolTip with no tooltip shows none.
    private static void SetAsrTip(Control control, string? tooltip) =>
        ToolTip.SetTip(control, string.IsNullOrEmpty(tooltip) ? null : tooltip);

    /// <summary>A WinForms filter ("Description|*.a;*.b|…") as file types for the picker.</summary>
    internal static IReadOnlyList<FilePickerFileType> FileTypes(string filter)
    {
        var types = new List<FilePickerFileType>();
        string[] parts = filter.Split('|');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            string[] patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            types.Add(new FilePickerFileType(parts[i])
            {
                Patterns = patterns.Select(p => p == "*.*" ? "*" : p).ToList(),
            });
        }

        return types;
    }

    private async Task<string?> PickFileAsync(string title, IReadOnlyList<FilePickerFileType> types, string current)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return null;
        }

        IStorageFolder? folder = null;
        string? directory = string.IsNullOrEmpty(current) ? null : Path.GetDirectoryName(current);
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
        {
            folder = await storage.TryGetFolderFromPathAsync(directory);
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = types,
            SuggestedStartLocation = folder,
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }
}

/// <summary>An auto splitter's timer when no window runs one (the settings page alone, in tests).</summary>
internal sealed class NullAsrTimer : IAsrTimer
{
    public static readonly NullAsrTimer Instance = new();

    public int State => 0;

    public int CurrentSplitIndex => -1;

    public int SegmentSplitted(int index) => -1;

    public void Start()
    {
    }

    public void Split()
    {
    }

    public void SkipSplit()
    {
    }

    public void UndoSplit()
    {
    }

    public void Reset()
    {
    }

    public void SetGameTime(TimeSpan time)
    {
    }

    public void PauseGameTime()
    {
    }

    public void ResumeGameTime()
    {
    }

    public void SetCustomVariable(string name, string value)
    {
    }

    public void Log(string message) => Console.WriteLine("[Auto Splitting Runtime] " + message);
}
