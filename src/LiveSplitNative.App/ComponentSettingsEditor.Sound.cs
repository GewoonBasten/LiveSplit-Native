using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// SoundSettings (93a11b85): two tabs. Sound Files: Drag and Drop Mode, then per situation the
/// files (a read-only box listing them, which takes dropped files), Browse... and Clear. Volume
/// Mixer: Output Device, General Volume, then per situation its volume (0-100).
/// </summary>
public sealed partial class ComponentSettingsEditor
{
    private const string SoundPathSeparator = ", ";
    private const double SoundLabelWidth = 190;

    /// <summary>The Output Device list (the tests set their own).</summary>
    internal static Func<IReadOnlyList<string>> SoundOutputDevices { get; set; } =
        () => OperatingSystem.IsLinux()
            ? ["Default"]
            : MacOsSoundPlayer.OutputDevices().Select(d => d.Name).ToList();

    private void BuildSoundFields()
    {
        SoundComponentSettings settings = SoundComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        var files = new StackPanel { Spacing = 2 };
        var mixer = new StackPanel { Spacing = 2 };
        var tabs = new TabControl
        {
            Classes = { "segmentedTabs" },
            Items =
            {
                new TabItem { Header = new TextBlock { Text = "Sound Files" }, Content = new SettingsCard { Children = { files } } },
                new TabItem { Header = new TextBlock { Text = "Volume Mixer" }, Content = new SettingsCard { Children = { mixer } } },
            },
        };
        _fields.Children.Add(tabs);

        // Drag and Drop Mode: Clear and Add Files, or Append Files.
        string group = Guid.NewGuid().ToString("N");
        var clearAdd = new RadioButton { Content = "Clear and Add Files", GroupName = group, IsChecked = settings.IsClearAddDragDrop };
        var append = new RadioButton { Content = "Append Files", GroupName = group, IsChecked = !settings.IsClearAddDragDrop };
        foreach (RadioButton radio in new[] { clearAdd, append })
        {
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked == true)
                {
                    NotifyPreview();
                }
            };
        }

        files.Children.Add(SoundRow("Drag and Drop Mode:", new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { clearAdd, append },
        }));

        var paths = new Dictionary<SoundEventType, List<string>>();
        foreach (SoundEventType type in SoundEventTypes.All)
        {
            paths[type] = [.. settings.Sounds[type].FilePaths];
            files.Children.Add(SoundFilesRow(type, paths[type], () => clearAdd.IsChecked == true));
        }

        // Output Device and General Volume, then every situation's volume.
        IReadOnlyList<string> devices = SoundOutputDevices();
        var device = new ComboBox
        {
            ItemsSource = devices,
            SelectedIndex = settings.OutputDevice >= 0 && settings.OutputDevice < devices.Count ? settings.OutputDevice : -1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 26,
            Classes = { "settingsCombo" },
        };
        device.SelectionChanged += (_, _) => NotifyPreview();
        mixer.Children.Add(SoundRow("Output Device:", device));
        Slider general = VolumeSlider(settings.GeneralVolume);
        mixer.Children.Add(SoundRow("General Volume:", general));
        var volumes = new Dictionary<SoundEventType, Slider>();
        foreach (SoundEventType type in SoundEventTypes.All)
        {
            volumes[type] = VolumeSlider(settings.Sounds[type].Volume);
            mixer.Children.Add(SoundRow(type.Label(), volumes[type]));
        }

        _applyMore = () =>
        {
            settings.IsClearAddDragDrop = clearAdd.IsChecked == true;
            if (device.SelectedIndex >= 0)
            {
                settings.OutputDevice = device.SelectedIndex;
            }

            settings.GeneralVolume = (int)Math.Round(general.Value);
            foreach (SoundEventType type in SoundEventTypes.All)
            {
                settings.Sounds[type].FilePaths = [.. paths[type]];
                settings.Sounds[type].Volume = (int)Math.Round(volumes[type].Value);
            }

            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>One 29-high row of either tab: the label, then the control(s).</summary>
    private static Grid SoundRow(string label, Control control)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{SoundLabelWidth},*"),
            MinHeight = 29,
        };
        row.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        });
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    /// <summary>
    /// SoundFileSettings: the files joined with ", " in a read-only box (dropped files clear and
    /// add, or append, by the Drag and Drop Mode; focused, it lists them one per line below it),
    /// Browse... (several files at once) and Clear.
    /// </summary>
    private Grid SoundFilesRow(SoundEventType type, List<string> paths, Func<bool> clearOnDrop)
    {
        var box = new TextBox
        {
            IsReadOnly = true,
            Text = string.Join(SoundPathSeparator, paths),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        string? Lines() => paths.Count == 0 ? null : string.Join("\n", paths);
        void Show()
        {
            box.Text = string.Join(SoundPathSeparator, paths);
            if (box.IsFocused)
            {
                ToolTip.SetTip(box, Lines());
            }

            NotifyPreview();
        }

        void Set(IEnumerable<string> chosen, bool replace)
        {
            List<string> next = replace ? [.. chosen] : [.. paths.Concat(chosen).Distinct()];
            paths.Clear();
            paths.AddRange(next);
            Show();
        }

        _soundFiles[type] = (chosen, replace) => Set(chosen, replace ?? clearOnDrop());
        // txtFilePath_Enter / _Leave: the paths show below the box only while it has focus.
        ToolTip.SetPlacement(box, PlacementMode.Bottom);
        box.GotFocus += (_, _) =>
        {
            ToolTip.SetTip(box, Lines());
            ToolTip.SetIsOpen(box, paths.Count > 0);
        };
        box.LostFocus += (_, _) =>
        {
            ToolTip.SetIsOpen(box, false);
            ToolTip.SetTip(box, null);
        };
        DragDrop.SetAllowDrop(box, true);
        box.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        box.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            List<string> dropped = (e.Data.GetFiles() ?? [])
                .Select(file => file.TryGetLocalPath())
                .OfType<string>()
                .ToList();
            if (dropped.Count > 0)
            {
                Set(dropped, clearOnDrop());
            }

            e.Handled = true;
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        Button browse = MakeDialogButton("Browse...");
        browse.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            {
                return;
            }

            IReadOnlyList<IStorageFile> chosen = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = true,
                FileTypeFilter =
                [
                    new FilePickerFileType("Audio Files") { Patterns = ["*.mp3", "*.wav", "*.aiff", "*.wma"] },
                    new FilePickerFileType("All Files") { Patterns = ["*.*"] },
                ],
            });
            List<string> picked = chosen.Select(file => file.TryGetLocalPath()).OfType<string>().ToList();
            if (picked.Count > 0)
            {
                Set(picked, replace: true);
            }
        };
        Button clear = MakeDialogButton("Clear");
        clear.Click += (_, _) => Set([], replace: true);
        foreach (Button button in new[] { browse, clear })
        {
            button.Margin = new Thickness(6, 0, 0, 0);
            button.VerticalAlignment = VerticalAlignment.Center;
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        row.Children.Add(box);
        Grid.SetColumn(browse, 1);
        Grid.SetColumn(clear, 2);
        row.Children.Add(browse);
        row.Children.Add(clear);
        return SoundRow(type.Label(), row);
    }

    /// <summary>A volume trackbar, 0-100; its value shows as a tooltip while it moves.</summary>
    private Slider VolumeSlider(int value)
    {
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(value, 0, 100),
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        SliderChrome.Compact(slider);
        ToolTip.SetTip(slider, slider.Value.ToString("0"));
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                ToolTip.SetTip(slider, slider.Value.ToString("0"));
                NotifyPreview();
            }
        };
        return slider;
    }

    /// <summary>Each situation's file setter (replace, or the drag and drop mode's choice), for the tests.</summary>
    private readonly Dictionary<SoundEventType, Action<IEnumerable<string>, bool?>> _soundFiles = [];

    /// <summary>For the tests: files picked with Browse... (replace: true) or dropped (replace: null).</summary>
    internal void SetSoundFilesForTests(SoundEventType type, bool? replace, params string[] files) =>
        _soundFiles[type](files, replace);
}
