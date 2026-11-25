using Avalonia.Controls;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Options;

namespace LiveSplitNative.App;

/// <summary>
/// macOS only (Windows needs no permission for global hotkeys): global hotkeys need the Input
/// Monitoring permission. While it is missing, LiveSplit asks at start (until Don't ask again)
/// and when global hotkeys are turned on. Open System Settings goes to the list where LiveSplit
/// is turned on; the first time macOS's own question comes first, which puts LiveSplit in that
/// list and has its own Open System Settings.
/// </summary>
public partial class MainWindow
{
    private bool _askingForHotkeyPermission;
    private bool _askedWhenHotkeysTurnedOn;
    private bool? _hotkeyHookNeeded;

    /// <summary>Tests: the permission (instead of macOS's); set, the question is asked in tests too.</summary>
    internal static KeyListenPermission? HotkeyPermissionForTests { get; set; }

    /// <summary>Tests: the answer to the question, without the dialog.</summary>
    internal static (bool Open, bool DontAskAgain)? HotkeyPermissionAnswerForTests { get; set; }

    /// <summary>Tests: what Open System Settings did ("ask macOS" or "open settings"), instead of doing it.</summary>
    internal static List<string>? HotkeyPermissionActionsForTests { get; set; }

    /// <summary>Tests: how often the question was asked.</summary>
    internal int HotkeyPermissionQuestionsForTests { get; private set; }

    private bool CanAskForHotkeyPermission =>
        HotkeyPermissionForTests is not null
        || (MacGlobalKeyboardHook.IsSupported && UseGlobalKeyboardHook && !AppCli.Headless);

    private static KeyListenPermission CurrentHotkeyPermission =>
        HotkeyPermissionForTests
        ?? (MacGlobalKeyboardHook.CanListen ? KeyListenPermission.Granted : MacGlobalKeyboardHook.ListenPermission);

    /// <summary>Whether to ask: the permission is missing, and at start only until Don't ask again.</summary>
    internal static bool ShouldAskForHotkeyPermission(bool atStart, KeyListenPermission permission, AppSettings settings) =>
        permission != KeyListenPermission.Granted && (!atStart || settings.AskForHotkeyPermission);

    /// <summary>The question at start, or when global hotkeys were just turned on (then once a session).</summary>
    internal async Task AskForHotkeyPermissionAsync(bool atStart)
    {
        if (!CanAskForHotkeyPermission || _askingForHotkeyPermission)
        {
            return;
        }

        KeyListenPermission permission = CurrentHotkeyPermission;
        if (!ShouldAskForHotkeyPermission(atStart, permission, _appSettings))
        {
            return;
        }

        _askingForHotkeyPermission = true;
        try
        {
            HotkeyPermissionQuestionsForTests++;
            Console.WriteLine($"Global hotkeys: asking for the Input Monitoring permission ({permission}{(atStart ? ", at start" : "")}).");
            (Window dialog, Button notNow, Button openSettings, CheckBox? dontAsk) =
                HotkeyPermissionDialog(atStart, _appSettings.HotkeyProfile.GlobalHotkeysEnabled);
            bool open = false;
            notNow.Click += (_, _) => dialog.Close();
            openSettings.Click += (_, _) =>
            {
                open = true;
                dialog.Close();
            };
            bool dontAskAgain;
            if (HotkeyPermissionAnswerForTests is { } answer)
            {
                open = answer.Open;
                dontAskAgain = answer.DontAskAgain;
            }
            else
            {
                await ShowOwnedDialogAsync(dialog);
                dontAskAgain = dontAsk?.IsChecked == true;
            }

            if (atStart && dontAskAgain)
            {
                _appSettings.AskForHotkeyPermission = false;
                PersistAppSettingsQuiet();
            }

            if (open)
            {
                OpenHotkeyPermission(permission);
            }
        }
        finally
        {
            _askingForHotkeyPermission = false;
        }
    }

    /// <summary>Never asked: macOS's own question (it lists LiveSplit). Otherwise: the list in System Settings.</summary>
    private static void OpenHotkeyPermission(KeyListenPermission permission)
    {
        bool askMacOs = permission == KeyListenPermission.NotAsked;
        if (HotkeyPermissionActionsForTests is { } actions)
        {
            actions.Add(askMacOs ? "ask macOS" : "open settings");
            return;
        }

        if (askMacOs)
        {
            MacGlobalKeyboardHook.RequestListenPermission();
        }
        else
        {
            MacGlobalKeyboardHook.OpenInputMonitoringSettings();
        }
    }

    /// <summary>
    /// RefreshHotkeyHook: the hook was just turned on (not at start, where the question at start
    /// covers it), so the permission is asked for once a session, Don't ask again or not.
    /// </summary>
    private void OnHotkeyHookNeeded(bool needed)
    {
        bool? before = _hotkeyHookNeeded;
        _hotkeyHookNeeded = needed;
        if (before == false && needed && !_askedWhenHotkeysTurnedOn && CurrentHotkeyPermission != KeyListenPermission.Granted && CanAskForHotkeyPermission)
        {
            _askedWhenHotkeysTurnedOn = true;
            _ = AskForHotkeyPermissionAsync(atStart: false);
        }
    }

    /// <summary>The question: why, where to turn LiveSplit on, Not Now and Open System Settings (Don't ask again at start).</summary>
    internal static (Window Dialog, Button NotNow, Button OpenSettings, CheckBox? DontAsk) HotkeyPermissionDialog(bool atStart, bool globalHotkeysOn)
    {
        string message =
            "Global hotkeys let you split while your game is in front. For that, macOS needs to let "
            + "LiveSplit see your key presses (Input Monitoring)."
            + Environment.NewLine + Environment.NewLine
            + "Click Open System Settings and turn on LiveSplit there. The hotkeys start working a few seconds later."
            + (globalHotkeysOn ? "" : " To use them, also turn on Global Hotkeys in LiveSplit's Settings.");
        var notNow = new Button { Content = "Not Now" };
        var openSettings = new Button { Content = "Open System Settings", IsDefault = true };
        if (!atStart)
        {
            return (DialogChrome.Message("Global Hotkeys", message, 440, notNow, openSettings), notNow, openSettings, null);
        }

        var dontAsk = new CheckBox { Content = "Don't ask again", FontSize = DialogChrome.ControlFontSize };
        return (DialogChrome.MessageWith("Global Hotkeys", message, 440, dontAsk, notNow, openSettings), notNow, openSettings, dontAsk);
    }
}
