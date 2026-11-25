using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;
using System.Collections.ObjectModel;
using System.Globalization;

namespace LiveSplitNative.App;

/// <summary>
/// Avalonia port of LiveSplit.View SettingsDialog. Upstream is a single scrolling form;
/// we expose Hotkeys / General / Racing / Server as tabs with matching labels.
/// Layout chrome (Always on Top, etc.) stays in <see cref="LayoutSettingsWindow"/>.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly LayoutSettings _layoutSettings;
    private readonly AppSettings _appSettings;
    private readonly string _settingsPath;
    private readonly TimingMethod _timingMethod;
    private HotkeyProfileStub _hotkeys;

    /// <summary>Copies of the hotkey profiles (Settings OK keeps them), and the one shown.</summary>
    private readonly Dictionary<string, HotkeyProfileStub> _profiles = new(StringComparer.Ordinal);
    private string _selectedProfile;

    /// <summary>Profiles renamed here (old, new): the recent splits follow on OK, as on Windows.</summary>
    private readonly List<(string From, string To)> _renames = [];
    private bool _showingProfile;
    private Dictionary<string, bool> _generatorStates;
    private int _hcpHistorySize;
    private int _hcpNBestRuns;
    private List<RaceProviderSettings> _raceProviders;

    /// <summary>
    /// Preview Appearance without writing settings.cfg. Host rolls back via SettingsEditSession.
    /// </summary>
    public Action<AppAppearanceMode>? LiveAppearancePreview { get; set; }

    /// <summary>
    /// Windows SettingsDialog.SumOfBestModeChanged: toggling Simple Sum of Best regenerates the
    /// comparisons at once. The host previews it and rolls it back on Cancel.
    /// </summary>
    public Action<bool>? LiveSimpleSumOfBestPreview { get; set; }

    public SettingsWindow()
        : this(LayoutSettings.CreateStandard(), TimingMethod.RealTime, new AppSettings())
    {
    }

    public SettingsWindow(LayoutSettings layoutSettings, TimingMethod timingMethod, AppSettings appSettings, string? settingsPath = null)
    {
        InitializeComponent();
        DialogChrome.ConfigureDialog(this);
        // Grow to Hotkeys page height — fixed Height=420 + Auto,* TabControl clipped Delay.
        DialogChrome.PreferFitToContentHeight(this);
        _layoutSettings = layoutSettings.Clone();
        _appSettings = appSettings;
        _settingsPath = settingsPath ?? XmlAppSettingsSerializer.DefaultPath;
        _timingMethod = timingMethod;
        foreach ((string name, HotkeyProfileStub profile) in appSettings.HotkeyProfiles)
        {
            _profiles[name] = profile.Clone();
        }

        // SettingsDialog(hotkeyProfile): the profile in use is shown.
        _selectedProfile = _profiles.ContainsKey(appSettings.CurrentHotkeyProfile)
            ? appSettings.CurrentHotkeyProfile
            : _profiles.Keys.First();
        _hotkeys = _profiles[_selectedProfile];
        ShowProfile(_selectedProfile);
        HotkeyProfileBox.ItemsSource = new System.Collections.ObjectModel.ObservableCollection<string>(_profiles.Keys);
        HotkeyProfileBox.SelectedItem = _selectedProfile;
        HotkeyProfileBox.SelectionChanged += OnHotkeyProfileChanged;
        RefreshRemoveProfileButton();

        WarnOnResetBox.IsChecked = _appSettings.WarnOnReset;
        SimpleSobBox.IsChecked = _appSettings.SimpleSumOfBest;
        SimpleSobBox.IsCheckedChanged += (_, _) => LiveSimpleSumOfBestPreview?.Invoke(SimpleSobBox.IsChecked == true);
        _generatorStates = new Dictionary<string, bool>(_appSettings.ComparisonGeneratorStates, StringComparer.Ordinal);
        _hcpHistorySize = _appSettings.HcpHistorySize;
        _hcpNBestRuns = _appSettings.HcpNBestRuns;
        _raceProviders = CloneRaceProviders(_appSettings.RaceProviders);
        RefreshRateBox.Value = _appSettings.RefreshRate;
        AppearanceBox.SelectedIndex = _appSettings.Appearance switch
        {
            AppAppearanceMode.Light => 1,
            AppAppearanceMode.Dark => 2,
            _ => 0,
        };

        RaceViewerBox.SelectedIndex = Math.Max(0, Array.IndexOf(AppSettings.RaceViewers, _appSettings.RaceViewer));
        RefreshLogOutButton();
        ServerPortBox.Text = _appSettings.ServerPort.ToString(CultureInfo.InvariantCulture);
        ServerStartupBox.SelectedIndex = (int)_appSettings.ServerStartup;

        SettingsTabs.SelectionChanged += (_, _) => RefitHeight();
        Opened += (_, _) => RefitHeight();
    }

    private void OnAppearanceChanged(object? sender, SelectionChangedEventArgs e)
    {
        AppAppearanceMode mode = AppearanceBox.SelectedIndex switch
        {
            1 => AppAppearanceMode.Light,
            2 => AppAppearanceMode.Dark,
            _ => AppAppearanceMode.System,
        };
        // Live preview only — persist settings.cfg on OK via host session.
        if (LiveAppearancePreview is not null)
        {
            LiveAppearancePreview(mode);
        }
        else
        {
            _appSettings.Appearance = mode;
            AppThemeController.Apply(mode);
        }
    }

    private void RefitHeight()
    {
        // After bindings / tab switch, remeasure so SizeToContent includes full Hotkeys page.
        SizeToContent = SizeToContent.Manual;
        UpdateLayout();
        // Force content desired height into the window when Auto measure under-reports
        // (seen when tab strip chrome changes); still prefer SizeToContent afterward.
        if (SettingsTabs.SelectedItem is TabItem { Content: Control page })
        {
            double availW = Bounds.Width > 1 ? Bounds.Width - 48 : Width - 48;
            page.Measure(new Avalonia.Size(Math.Max(200, availW), double.PositiveInfinity));
            double pageH = page.DesiredSize.Height;
            // The window around the page: 20 + tabs 28 + 22 above it, 20 + the footer's 56 below.
            double target = pageH + 146;
            if (target > Height + 1)
            {
                Height = Math.Min(MaxHeight > 0 ? MaxHeight : target, target);
            }
        }

        SizeToContent = SizeToContent.Height;
        UpdateLayout();
    }

    /// <summary>
    /// Layout settings are passed through unchanged (window chrome is Layout Settings).
    /// Timing method is passed through (set via Compare Against on Windows).
    /// </summary>
    public (LayoutSettings Settings, TimingMethod Method, HotkeyProfileStub Hotkeys)? Result { get; private set; }

    public void SelectTab(int index)
    {
        int count = SettingsTabs.ItemCount;
        if (index >= 0 && index < count)
        {
            SettingsTabs.SelectedIndex = index;
        }
    }

    private void BuildHotkeyBindingRows()
    {
        HotkeyBindingsCard.Children.Clear();
        AddBindingRow(0, "Start / Split:", () => _hotkeys.SplitKey, v => _hotkeys.SplitKey = v);
        AddBindingRow(1, "Reset:", () => _hotkeys.ResetKey, v => _hotkeys.ResetKey = v);
        AddBindingRow(2, "Undo Split:", () => _hotkeys.UndoKey, v => _hotkeys.UndoKey = v);
        AddBindingRow(3, "Skip Split:", () => _hotkeys.SkipKey, v => _hotkeys.SkipKey = v);
        AddBindingRow(4, "Pause:", () => _hotkeys.PauseKey, v => _hotkeys.PauseKey = v);
        AddBindingRow(5, "Switch Comparison (Previous):", () => _hotkeys.SwitchComparisonPrevious, v => _hotkeys.SwitchComparisonPrevious = v);
        AddBindingRow(6, "Switch Comparison (Next):", () => _hotkeys.SwitchComparisonNext, v => _hotkeys.SwitchComparisonNext = v);
        AddBindingRow(7, "Toggle Global Hotkeys:", () => _hotkeys.ToggleGlobalHotkeys, v => _hotkeys.ToggleGlobalHotkeys = v);
    }

    private void AddBindingRow(int row, string label, Func<string> get, Action<string> set)
    {
        var box = new TextBox
        {
            Text = DisplayKey(get()),
            IsReadOnly = true,
            Watermark = "Click, then press a key",
            Height = 26,
            [Grid.ColumnProperty] = 0,
        };
        // dialogCompact — not Button.dialog (MinWidth=75 clipped Clear in the old 56px column).
        var clear = new Button
        {
            Content = "Clear",
            Width = 60,
            MinWidth = 60,
            Height = 26,
            MinHeight = 26,
            [Grid.ColumnProperty] = 1,
            Margin = new Avalonia.Thickness(8, 0, 0, 0),
        };
        clear.Classes.Add("dialogCompact");

        // SetHotkeyHandlers: with Allow Gamepads as Hotkeys, a controller button sets the field too.
        IDisposable? gamepadLease = null;
        void OnButton(string button) => Dispatcher.UIThread.Post(() =>
        {
            if (gamepadLease is null)
            {
                return;
            }

            set(button);
            box.Text = button;
            GlobalHotkeysBox.Focus();
        });
        void StopGamepads()
        {
            if (gamepadLease is not null)
            {
                Gamepads.Current.ButtonPressed -= OnButton;
                gamepadLease.Dispose();
                gamepadLease = null;
            }
        }

        box.GotFocus += (_, _) =>
        {
            box.Text = "Press a key…";
            if (AllowGamepadsBox.IsChecked == true && gamepadLease is null)
            {
                Gamepads.Current.ButtonPressed += OnButton;
                gamepadLease = Gamepads.Current.Acquire();
            }
        };
        Closed += (_, _) => StopGamepads();
        box.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape)
            {
                set("");
                box.Text = DisplayKey("");
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Tab or Key.None)
            {
                return;
            }

            string name = FormatKey(e);
            set(name);
            box.Text = name;
            e.Handled = true;
        };
        box.LostFocus += (_, _) =>
        {
            StopGamepads();
            box.Text = DisplayKey(get());
        };
        clear.Click += (_, _) =>
        {
            set("");
            box.Text = DisplayKey("");
        };

        // A row of the Hotkeys card: the action, the key's field, Clear.
        var field = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        field.Children.Add(box);
        field.Children.Add(clear);
        HotkeyBindingsCard.Children.Add(new SettingsField { Label = label, Field = field, Tag = row });
    }

    private void OnGlobalHotkeysChanged(object? sender, RoutedEventArgs e) => UpdateDeactivateEnabled();

    // Windows SettingsDialog.btnChooseComparisons_Click: edits copies; Settings OK commits them.
    private async void OnChooseComparisons(object? sender, RoutedEventArgs e)
    {
        var dialog = new ChooseComparisonsWindow(_generatorStates, _hcpHistorySize, _hcpNBestRuns);
        ChooseComparisonsResult? result = await OwnedDialog.ShowAsync<ChooseComparisonsResult?>(this, dialog);
        if (result is null)
        {
            return;
        }

        _generatorStates = new Dictionary<string, bool>(result.GeneratorStates, StringComparer.Ordinal);
        _hcpHistorySize = result.HcpHistorySize;
        _hcpNBestRuns = result.HcpNBestRuns;
    }

    // Windows SettingsDialog.btnChooseRaceProvider_Click: the dialog edits copies; its OK keeps
    // them, and Settings OK commits them.
    private async void OnManageRaceProviders(object? sender, RoutedEventArgs e)
    {
        List<RaceProviderSettings> newSettings = CloneRaceProviders(_raceProviders);
        var dialog = new RaceProvidersWindow(newSettings);
        if (await OwnedDialog.ShowAsync<bool>(this, dialog))
        {
            _raceProviders = newSettings;
        }
    }

    private static List<RaceProviderSettings> CloneRaceProviders(IEnumerable<RaceProviderSettings> providers) =>
        [.. providers.Select(p => p.Clone())];

    private void UpdateDeactivateEnabled()
    {
        bool global = GlobalHotkeysBox.IsChecked == true;
        DeactivateForOtherProgramsBox.IsEnabled = global;
        if (!global)
        {
            DeactivateForOtherProgramsBox.IsChecked = false;
        }
    }

    /// <summary>The check boxes and delay into the shown profile (before another is shown, and on OK).</summary>
    private void StoreProfileOptions()
    {
        _hotkeys.GlobalHotkeysEnabled = GlobalHotkeysBox.IsChecked == true;
        _hotkeys.DeactivateHotkeysForOtherPrograms =
            _hotkeys.GlobalHotkeysEnabled && DeactivateForOtherProgramsBox.IsChecked == true;
        _hotkeys.DoubleTapPrevention = DoubleTapPreventionBox.IsChecked == true;
        _hotkeys.AllowGamepadsAsHotkeys = AllowGamepadsBox.IsChecked == true;
        _hotkeys.HotkeyDelay = (float)(HotkeyDelayBox.Value ?? 0);
    }

    /// <summary>SettingsDialog.UpdateDisplayedHotkeyValues: the profile's keys and options.</summary>
    private void ShowProfile(string name)
    {
        _showingProfile = true;
        try
        {
            _selectedProfile = name;
            _hotkeys = _profiles[name];
            BuildHotkeyBindingRows();
            GlobalHotkeysBox.IsChecked = _hotkeys.GlobalHotkeysEnabled;
            DeactivateForOtherProgramsBox.IsChecked = _hotkeys.DeactivateHotkeysForOtherPrograms;
            DoubleTapPreventionBox.IsChecked = _hotkeys.DoubleTapPrevention;
            AllowGamepadsBox.IsChecked = _hotkeys.AllowGamepadsAsHotkeys;
            HotkeyDelayBox.Value = (decimal)_hotkeys.HotkeyDelay;
            UpdateDeactivateEnabled();
        }
        finally
        {
            _showingProfile = false;
        }
    }

    /// <summary>The profile shown now (tests).</summary>
    internal string SelectedHotkeyProfile => _selectedProfile;

    /// <summary>The profiles as OK would keep them (tests).</summary>
    internal IReadOnlyDictionary<string, HotkeyProfileStub> HotkeyProfilesForTests => _profiles;

    /// <summary>The names asked for (New / Rename), for the tests; null answers Cancel.</summary>
    internal Queue<string?>? ProfileNameAnswersForTests { get; set; }

    private void OnHotkeyProfileChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_showingProfile || HotkeyProfileBox.SelectedItem is not string name || name == _selectedProfile || !_profiles.ContainsKey(name))
        {
            return;
        }

        StoreProfileOptions();
        ShowProfile(name);
    }

    private ObservableCollection<string> ProfileNames => (ObservableCollection<string>)HotkeyProfileBox.ItemsSource!;

    /// <summary>RefreshRemoveButton: a profile can be removed while there is another.</summary>
    private void RefreshRemoveProfileButton() => RemoveProfileButton.IsEnabled = _profiles.Count > 1;

    private void SelectProfile(string name)
    {
        ShowProfile(name);
        _showingProfile = true;
        HotkeyProfileBox.SelectedItem = name;
        _showingProfile = false;
    }

    /// <summary>The name for a new or renamed profile (InputBox.Show); null when cancelled.</summary>
    private async Task<string?> AskProfileNameAsync(string title, string initial)
    {
        if (ProfileNameAnswersForTests is { } answers)
        {
            return answers.Count > 0 ? answers.Dequeue() : null;
        }

        return await DialogChrome.PromptAsync(this, title, "Hotkey Profile Name:", initial);
    }

    /// <summary>"A Hotkey Profile with this name already exists." Retry / Cancel: true to retry.</summary>
    private async Task<bool> AskRetryNameAsync()
    {
        if (ProfileNameAnswersForTests is not null)
        {
            return true;
        }

        bool retry = false;
        var again = new Button { Content = "Retry", IsDefault = true };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        Window dialog = DialogChrome.Message(
            UiLocalizer.T("Hotkey Profile Already Exists"),
            UiLocalizer.T("A Hotkey Profile with this name already exists."),
            360,
            cancel,
            again);
        again.Click += (_, _) => { retry = true; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();
        await OwnedDialog.ShowAsync(this, dialog);
        return retry;
    }

    /// <summary>btnNewProfile_Click: a copy of the shown profile under a new name.</summary>
    private async void OnNewProfile(object? sender, RoutedEventArgs e) => await NewProfileAsync();

    internal async Task NewProfileAsync()
    {
        while (await AskProfileNameAsync("New Hotkey Profile", "") is { } name)
        {
            if (!_profiles.ContainsKey(name))
            {
                StoreProfileOptions();
                _profiles.Add(name, _hotkeys.Clone());
                ProfileNames.Add(name);
                SelectProfile(name);
                RefreshRemoveProfileButton();
                return;
            }

            if (!await AskRetryNameAsync())
            {
                return;
            }
        }
    }

    /// <summary>btnRenameProfile_Click: the shown profile under a new name; recent splits using it follow.</summary>
    private async void OnRenameProfile(object? sender, RoutedEventArgs e) => await RenameProfileAsync();

    internal async Task RenameProfileAsync()
    {
        while (await AskProfileNameAsync("Rename Hotkey Profile", _selectedProfile) is { } name)
        {
            if (!_profiles.ContainsKey(name))
            {
                StoreProfileOptions();
                string old = _selectedProfile;
                HotkeyProfileStub profile = _profiles[old];
                _profiles.Remove(old);
                _profiles[name] = profile;
                _renames.Add((old, name));
                ProfileNames.Remove(old);
                ProfileNames.Add(name);
                SelectProfile(name);
                return;
            }

            if (name == _selectedProfile || !await AskRetryNameAsync())
            {
                return;
            }
        }
    }

    /// <summary>btnRemoveProfile_Click: the shown profile goes (never the last one); the last one left is shown.</summary>
    private void OnRemoveProfile(object? sender, RoutedEventArgs e) => RemoveProfile();

    internal void RemoveProfile()
    {
        if (_profiles.Count <= 1)
        {
            return;
        }

        string removed = _selectedProfile;
        _profiles.Remove(removed);
        ProfileNames.Remove(removed);
        SelectProfile(_profiles.Keys.Last());
        RefreshRemoveProfileButton();
    }

    /// <summary>WebCredentials' names (LiveSplit's own, so the same Keychain items Windows would use).</summary>
    internal static readonly string[] CredentialNames =
    [
        "LiveSplit_TwitchAccessToken",
        "LiveSplit_Bluesky",
        SpeedrunComSubmitter.CredentialName,
        "LiveSplit_SpeedRunsLiveIRC",
        RacetimeAuthenticator.AccessTokenName,
        RacetimeAuthenticator.RefreshTokenName,
    ];

    private static IEnumerable<ICredentialStore> CredentialStores =>
        new[] { SpeedrunComSharing.Credentials, RacetimeController.Credentials }.Distinct();

    /// <summary>RefreshLogOutButton: WebCredentials.AnyCredentialsExist.</summary>
    private void RefreshLogOutButton() =>
        LogOutButton.IsEnabled = CredentialStores.Any(store => CredentialNames.Any(name => store.Read(name) is not null));

    /// <summary>
    /// btnLogOut_Click: speedrun.com forgets the key and every saved login is deleted (at once,
    /// not on OK, as on Windows).
    /// </summary>
    internal void OnLogOut(object? sender, RoutedEventArgs e)
    {
        SpeedrunComSharing.Submitter.ClearAccessToken();
        foreach (ICredentialStore store in CredentialStores)
        {
            foreach (string name in CredentialNames)
            {
                store.Write(name, null);
            }
        }

        RefreshLogOutButton();
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        StoreProfileOptions();

        _appSettings.WarnOnReset = WarnOnResetBox.IsChecked == true;
        _appSettings.SimpleSumOfBest = SimpleSobBox.IsChecked == true;
        // Windows SettingsDialog.RefreshRate clamps to 20–300 Hz.
        _appSettings.RefreshRate = Math.Clamp(
            (int)(RefreshRateBox.Value ?? 40),
            AppSettings.MinRefreshRate,
            AppSettings.MaxRefreshRate);
        _appSettings.Appearance = AppearanceBox.SelectedIndex switch
        {
            1 => AppAppearanceMode.Light,
            2 => AppAppearanceMode.Dark,
            _ => AppAppearanceMode.System,
        };
        if (int.TryParse(ServerPortBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)
            && port is >= 1 and <= 65535)
        {
            _appSettings.ServerPort = port;
        }

        // cbxServerStartup: Don't start / TCP / WebSocket / Restore Previous State.
        if (Enum.IsDefined(typeof(ServerStartupType), ServerStartupBox.SelectedIndex))
        {
            _appSettings.ServerStartup = (ServerStartupType)ServerStartupBox.SelectedIndex;
        }

        // The profiles as edited, the shown one in use (TimerForm: CurrentHotkeyProfile =
        // editor.SelectedHotkeyProfile), and the recent splits of renamed profiles following.
        _appSettings.HotkeyProfiles.Clear();
        foreach ((string name, HotkeyProfileStub profile) in _profiles)
        {
            _appSettings.HotkeyProfiles[name] = profile;
        }

        _appSettings.CurrentHotkeyProfile = _selectedProfile;
        foreach ((string from, string to) in _renames)
        {
            foreach (RecentSplitsFile file in _appSettings.RecentSplits.Where(f => f.LastHotkeyProfile == from))
            {
                file.LastHotkeyProfile = to;
            }
        }

        _appSettings.SetComparisonGeneratorStates(_generatorStates);
        _appSettings.HcpHistorySize = _hcpHistorySize;
        _appSettings.HcpNBestRuns = _hcpNBestRuns;
        _appSettings.RaceProviders = _raceProviders;
        _appSettings.RaceViewer = AppSettings.RaceViewers[Math.Max(0, RaceViewerBox.SelectedIndex)];
        AppThemeController.Apply(_appSettings.Appearance);

        Result = (_layoutSettings, _timingMethod, _hotkeys);
        Close(Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    private static string DisplayKey(string key) =>
        string.IsNullOrEmpty(key) ? "" : key;

    private static string FormatKey(KeyEventArgs e)
    {
        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Meta");
        }

        parts.Add(e.Key.ToString());
        return string.Join("+", parts);
    }

}
