using System.Reflection;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Server;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// Global hotkeys (TimerForm.hook_KeyOrButtonPressed over CompositeHook) and the LiveSplit Server
/// (CommandServer's WebSocket server and the Control menu's Start / Stop WebSocket Server).
/// </summary>
public partial class MainWindow : ICommandHost
{
    private IGlobalKeyboardHook? _globalKeys;
    private CommandServer? _server;
    private CommandProcessor? _commands;
    private volatile HashSet<string> _consumedChords = [];

    /// <summary>The global keyboard hook is running (key presses in other apps are seen).</summary>
    internal bool GlobalHotkeysListening => _globalKeys?.IsActive == true;

    internal CommandServer? ServerForTests => _server;

    internal AppSettings SettingsForTests => _appSettings;

    /// <summary>Tests: whether the timer window has the keyboard (headless windows stay active).</summary>
    internal bool? TimerHasKeyboardForTests { get; set; }

    /// <summary>False keeps the real keyboard hook off (tests: no permission prompts).</summary>
    internal static bool UseGlobalKeyboardHook { get; set; } = true;

    /// <summary>Listening to the controllers while the hotkey profile allows gamepads.</summary>
    private IDisposable? _gamepadLease;

    /// <summary>False keeps the atomic clock (NTP) sync off (tests: no network).</summary>
    internal static bool SyncAtomicClock { get; set; } = true;

    internal CommandProcessor? CommandsForTests => _commands;

    private void InitIntegrations()
    {
        if (UseGlobalKeyboardHook && !AppCli.Headless
            && GlobalKeyboardHooks.Create() is { } hook)
        {
            _globalKeys = hook;
            _globalKeys.KeyPressed += chord => Dispatcher.UIThread.Post(() => OnGlobalKey(chord));
            _globalKeys.ShouldConsume = chord => _consumedChords.Contains(chord);
        }

        // CompositeHook: gamepad buttons arrive like keys (KeyOrButtonPressed).
        Gamepads.Current.ButtonPressed += OnGamepadButton;
        RefreshHotkeyHook();

        _commands = new CommandProcessor(_session, this);
        _server = new CommandServer(message => Dispatcher.UIThread.InvokeAsync(() => _commands.ProcessAsync(message)));

        // TimerForm init: the server the settings ask for.
        ServerStateType startup = _appSettings.ServerStartup switch
        {
            ServerStartupType.TCP => ServerStateType.TCP,
            ServerStartupType.Websocket => ServerStateType.Websocket,
            ServerStartupType.PreviousState => _appSettings.ServerState,
            _ => ServerStateType.Off,
        };
        if (startup != ServerStateType.Off)
        {
            StartServer(startup, showError: false);
        }
    }

    private void OnGamepadButton(string button) => Dispatcher.UIThread.Post(() => OnGlobalKey(button));

    private void DisposeIntegrations()
    {
        Gamepads.Current.ButtonPressed -= OnGamepadButton;
        _gamepadLease?.Dispose();
        _gamepadLease = null;
        _globalKeys?.Dispose();
        _server?.Dispose();
    }

    /// <summary>
    /// RegisterHotkeys: the hook runs while global hotkeys are on, a Toggle Global Hotkeys key is
    /// set or a Counter has global hotkeys; Deactivate For Other Programs keeps the timer's keys
    /// from the app that has the keyboard.
    /// </summary>
    internal void RefreshHotkeyHook()
    {
        HotkeyProfileStub p = _appSettings.HotkeyProfile;
        bool consume = p.GlobalHotkeysEnabled && p.DeactivateHotkeysForOtherPrograms;
        _consumedChords = consume
            ? [.. new[] { p.SplitKey, p.ResetKey, p.SkipKey, p.UndoKey, p.PauseKey, p.SwitchComparisonPrevious, p.SwitchComparisonNext, p.ToggleGlobalHotkeys }
                .Where(k => !string.IsNullOrEmpty(k))]
            : [];
        // RegisterHotkeys: hook.AllowGamepads = the profile's Allow Gamepads as Hotkeys.
        bool gamepads = p.AllowGamepadsAsHotkeys;
        if (gamepads && _gamepadLease is null)
        {
            _gamepadLease = Gamepads.Current.Acquire();
        }
        else if (!gamepads && _gamepadLease is not null)
        {
            _gamepadLease.Dispose();
            _gamepadLease = null;
        }

        bool needed = p.GlobalHotkeysEnabled || !string.IsNullOrEmpty(p.ToggleGlobalHotkeys) || CounterLogic.HasGlobalHotkeys(_layout);
        // macOS: just turned on without the permission, LiveSplit asks for it (MainWindow.HotkeyPermission.cs).
        OnHotkeyHookNeeded(needed);
        if (_globalKeys is null)
        {
            return;
        }

        if (needed)
        {
            _globalKeys.Start(consume);
        }
        else
        {
            _globalKeys.Stop();
        }
    }

    /// <summary>
    /// hook_KeyOrButtonPressed: the timer's keys work while the timer window has the keyboard, or
    /// anywhere with global hotkeys on, never while "Update Times?" or a dialog is open; Toggle
    /// Global Hotkeys always works. Counters: their own Global Hotkeys setting.
    /// </summary>
    internal void OnGlobalKey(string chord)
    {
        bool focused = TimerHasKeyboardForTests ?? IsActive;
        if (CounterLogic.HandleHotkey(_layout, chord, focused))
        {
            TimerSurface.UpdateValues();
        }

        HotkeyProfileStub p = _appSettings.HotkeyProfile;
        HotkeyResolution resolved = HotkeyCommands.Resolve(p, chord);
        if (resolved.Command is { } command && (focused || p.GlobalHotkeysEnabled) && !_resetMessageShown && _ownedDialogDepth == 0)
        {
            ExecuteTimerCommand(command, TimeSpan.FromSeconds(Math.Max(0, p.HotkeyDelay)));
        }

        if (resolved.ToggleGlobalHotkeys)
        {
            p.GlobalHotkeysEnabled = !p.GlobalHotkeysEnabled;
            RefreshHotkeyHook();
            TimerSurface.UpdateValues();
        }
    }

    /// <summary>
    /// UpdateServerMenuItems: each item reads Start, or Stop while its server runs, and is off
    /// while the other server runs.
    /// </summary>
    private NativeMenuAction ServerMenuItem(ServerStateType type)
    {
        ServerStateType state = _server?.ServerState ?? ServerStateType.Off;
        string name = type == ServerStateType.TCP ? "TCP Server" : "WebSocket Server";
        return NativeItem(
            (state == type ? "Stop " : "Start ") + name,
            () => ToggleServer(type),
            enabled: _server is not null && (state == ServerStateType.Off || state == type));
    }

    /// <summary>TCPServerMenuItem_Click / WebSocketMenuItem_Click.</summary>
    private void ToggleServer(ServerStateType type)
    {
        if (_server is null)
        {
            return;
        }

        if (_server.ServerState == ServerStateType.Off)
        {
            StartServer(type, showError: true);
        }
        else if (_server.ServerState == type)
        {
            if (type == ServerStateType.TCP)
            {
                _server.StopTcp();
            }
            else
            {
                _server.StopWs();
            }

            _appSettings.ServerState = _server.ServerState;
            PersistAppSettingsQuiet();
        }
    }

    private void StartServer(ServerStateType type, bool showError)
    {
        string name = type == ServerStateType.TCP ? "TCP Server" : "WebSocket Server";
        try
        {
            if (type == ServerStateType.TCP)
            {
                _server!.StartTcp(_appSettings.ServerPort);
            }
            else
            {
                _server!.StartWs(_appSettings.ServerPort);
            }
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            Console.Error.WriteLine(name + ": " + ex.Message);
            if (showError)
            {
                _ = ShowErrorAsync(name, $"The server could not start on port {_appSettings.ServerPort}:\n{ex.Message}");
            }
        }

        _appSettings.ServerState = _server!.ServerState;
        PersistAppSettingsQuiet();
    }

    // ICommandHost: what the server's commands need from the window.
    HotkeyProfileStub ICommandHost.HotkeyProfile => _appSettings.HotkeyProfile;

    string ICommandHost.HotkeyProfileName => _appSettings.CurrentHotkeyProfile;

    string? ICommandHost.LayoutPath => _layoutPath;

    string? ICommandHost.SplitsPath => _splitsPath;

    string ICommandHost.Version =>
        typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(MainWindow).Assembly.GetName().Version?.ToString()
        ?? "Unknown Version";

    ServerStateType ICommandHost.ServerState => _server?.ServerState ?? ServerStateType.Off;

    async Task<bool> ICommandHost.SaveLayoutAsync(string? path)
    {
        if (path is not null)
        {
            _layoutPath = path;
        }

        return _layoutPath is not null && await SaveLayoutAsync(saveAs: false);
    }

    async Task<bool> ICommandHost.SaveSplitsAsync(string? path)
    {
        if (path is not null)
        {
            _splitsPath = path;
        }

        return _splitsPath is not null && await SaveSplitsAsync(saveAs: false, promptPersonalBest: false);
    }

    async Task<bool> ICommandHost.OpenLayoutAsync(string path)
    {
        await OpenLayoutFromPathAsync(path);
        return string.Equals(_layoutPath, path, StringComparison.Ordinal);
    }

    Task<bool> ICommandHost.OpenSplitsAsync(string path) => OpenRunFromFileAsync(path);

    Task<byte[]?> ICommandHost.ScreenshotAsync()
    {
        try
        {
            var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(Bounds.Width)), Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
            using var bitmap = new RenderTargetBitmap(size);
            bitmap.Render(this);
            using var png = new MemoryStream();
            bitmap.Save(png);
            return Task.FromResult<byte[]?>(png.ToArray());
        }
        catch (Exception)
        {
            return Task.FromResult<byte[]?>(null);
        }
    }

    void ICommandHost.HotkeysChanged()
    {
        RefreshHotkeyHook();
        TimerSurface.UpdateValues();
    }
}
