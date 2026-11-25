using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// AutoSplitIntegrationComponentSettings (Basten's Pilgrim build): AutoSplit Path and Settings
/// Path with Browse..., Start AutoSplit / Kill AutoSplit with the running version, and Pausing.
/// Browsing to an autosplitter starts it; browsing to a settings file sends it.
/// </summary>
public sealed partial class ComponentSettingsEditor
{
    /// <summary>Whether the timer is running, for Pausing (set by the main window; tests leave it).</summary>
    internal static Func<TimerPhase> TimerPhaseNow { get; set; } = () => TimerPhase.NotRunning;

    private void BuildAutoSplitIntegrationFields()
    {
        AutoSplitIntegrationSettings settings = AutoSplitIntegrationSettings.FromInnerXml(_component.SettingsInnerXml);
        AutoSplitIntegrationRuntime runtime = AutoSplitIntegrationController.RuntimeFor(_component);

        // The settings look: one card, a row per Windows line, the label column shared with
        // every other page.
        Controls rows = Rows(_fields);

        // A path box with its Browse... button on the right.
        Grid PathRow(TextBox box, Button browse)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            box.VerticalAlignment = VerticalAlignment.Center;
            browse.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(browse, 1);
            row.Children.Add(box);
            row.Children.Add(browse);
            return row;
        }

        TextBox autoSplitPath = new() { Text = settings.AutoSplitPath };
        Button browseAutoSplit = MakeDialogButton("Browse...");
        rows.Add(Labeled("AutoSplit Path:", PathRow(autoSplitPath, browseAutoSplit)));
        TextBox settingsPath = new() { Text = settings.SettingsPath };
        Button browseSettings = MakeDialogButton("Browse...");
        rows.Add(Labeled("Settings Path:", PathRow(settingsPath, browseSettings)));

        Button start = MakeDialogButton("Start AutoSplit", minWidth: 100);
        Button kill = MakeDialogButton("Kill AutoSplit", minWidth: 100);
        // The version under the buttons: next to them it does not fit the label column's row.
        var version = new TextBlock { Margin = new Thickness(1, 0, 0, 0) };
        rows.Add(Labeled("AutoSplit:", new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 2, 0, 2),
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { start, kill } },
                version,
            },
        }));
        CheckBox gameTimePausing = new()
        {
            Content = "Pause Game Time only (this will disallow manual unpausing)",
            IsChecked = settings.GameTimePausing,
            // Only while the timer is not running (OnStart / OnReset).
            IsEnabled = TimerPhaseNow() == TimerPhase.NotRunning,
        };
        rows.Add(Labeled("Pausing:", gameTimePausing));
        gameTimePausing.IsCheckedChanged += (_, _) => NotifyPreview();

        AutoSplitIntegrationSettings Current() => new()
        {
            AutoSplitPath = autoSplitPath.Text ?? "",
            SettingsPath = settingsPath.Text ?? "",
            GameTimePausing = gameTimePausing.IsChecked == true,
        };

        // AutoSplitProcess.IsRunning / Version: the buttons and the version follow the autosplitter.
        void ShowRuntime()
        {
            start.IsEnabled = !runtime.IsRunning && AutoSplitIntegrationRuntime.Program(autoSplitPath.Text ?? "") is not null;
            kill.IsEnabled = runtime.IsRunning;
            version.Text = "AutoSplit version: " + runtime.Version;
        }

        runtime.Changed += ShowRuntime;
        DetachedFromVisualTree += (_, _) => runtime.Changed -= ShowRuntime;
        ShowRuntime();

        foreach (TextBox box in new[] { autoSplitPath, settingsPath })
        {
            box.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    ShowRuntime();
                    NotifyPreview();
                }
            };
        }

        async Task<string?> BrowseAsync(string title, FilePickerFileType[] types, string current)
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            {
                return null;
            }

            IStorageFolder? folder = null;
            string? directory = Path.GetDirectoryName(current);
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

        browseAutoSplit.Click += async (_, _) =>
        {
            // AutoSplit.exe on Windows; on a Mac the app (Pilgrim Autosplitter.app) or a program.
            string? path = await BrowseAsync(
                "AutoSplit Path",
                [
                    new FilePickerFileType("Applications") { Patterns = ["*.app"], AppleUniformTypeIdentifiers = ["com.apple.application-bundle"] },
                    new FilePickerFileType("All files") { Patterns = ["*"] },
                ],
                autoSplitPath.Text ?? "");
            if (path is not null)
            {
                autoSplitPath.Text = path;
                runtime.Start(Current());
            }
        };
        browseSettings.Click += async (_, _) =>
        {
            string? path = await BrowseAsync(
                "Settings Path",
                [
                    new FilePickerFileType("AutoSplit Settings (*.pkl; *.toml)") { Patterns = ["*.pkl", "*.toml"] },
                    new FilePickerFileType("All files") { Patterns = ["*"] },
                ],
                settingsPath.Text ?? "");
            if (path is not null)
            {
                settingsPath.Text = path;
                runtime.LoadSettings(Current());
            }
        };
        start.Click += (_, _) => runtime.Start(Current());
        kill.Click += (_, _) => runtime.Kill();

        _applyMore = () =>
        {
            AutoSplitIntegrationSettings current = Current();
            settings.AutoSplitPath = current.AutoSplitPath;
            settings.SettingsPath = current.SettingsPath;
            settings.GameTimePausing = current.GameTimePausing;
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }
}
