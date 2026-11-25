using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// Snapshot / live-preview / commit / rollback for settings dialogs.
/// Preview mutates the live timer surface; disk writes happen only on <see cref="Commit"/>.
/// Nested color-picker preview nests into the same session via parent <c>LivePreview</c> callbacks.
/// </summary>
public sealed class SettingsEditSession
{
    private readonly Layout? _liveLayout;
    private readonly Layout? _layoutSnapshot;
    private readonly AppSettings? _liveApp;
    private readonly AppSettingsSnapshot? _appSnapshot;
    private readonly Action _refresh;
    private bool _committed;

    private SettingsEditSession(
        Layout? liveLayout,
        Layout? layoutSnapshot,
        AppSettings? liveApp,
        AppSettingsSnapshot? appSnapshot,
        Action refresh)
    {
        _liveLayout = liveLayout;
        _layoutSnapshot = layoutSnapshot;
        _liveApp = liveApp;
        _appSnapshot = appSnapshot;
        _refresh = refresh;
    }

    public static SettingsEditSession ForLayout(Layout live, Action refresh) =>
        new(live, live.Clone(), liveApp: null, appSnapshot: null, refresh);

    public static SettingsEditSession ForAppSettings(AppSettings live, Action refresh) =>
        new(liveLayout: null, layoutSnapshot: null, live, AppSettingsSnapshot.Capture(live), refresh);

    /// <summary>Push a working layout copy onto the live timer (no disk I/O).</summary>
    public void PreviewLayout(Layout working)
    {
        if (_liveLayout is null)
        {
            return;
        }

        // The dialogs never edit the window's position or size, and their copy of the layout was
        // taken when they opened: the live layout keeps the window's own geometry (as Windows,
        // where the dialog edits the form's layout and the form keeps its size).
        AssignKeepingGeometry(_liveLayout, working);
        _refresh();
    }

    private static void AssignKeepingGeometry(Layout live, Layout source)
    {
        int vx = live.VerticalWidth;
        int vy = live.VerticalHeight;
        int hx = live.HorizontalWidth;
        int hy = live.HorizontalHeight;
        int x = live.X;
        int y = live.Y;
        live.Assign(source);
        live.VerticalWidth = vx;
        live.VerticalHeight = vy;
        live.HorizontalWidth = hx;
        live.HorizontalHeight = hy;
        live.X = x;
        live.Y = y;
    }

    /// <summary>Preview Appearance (and related chrome) without writing settings.cfg.</summary>
    public void PreviewAppearance(AppAppearanceMode mode)
    {
        if (_liveApp is null)
        {
            return;
        }

        _liveApp.Appearance = mode;
        AppThemeController.Apply(mode);
    }

    public void Commit()
    {
        _committed = true;
        if (_liveLayout is not null)
        {
            _liveLayout.HasChanged = true;
        }
    }

    public void RollbackIfUncommitted()
    {
        if (_committed)
        {
            return;
        }

        if (_liveLayout is not null && _layoutSnapshot is not null)
        {
            // Keep user-initiated window geometry; settings Cancel must not undo drag-move/resize.
            // A size the edit dropped (editor_LayoutResized) comes back, as Windows' Cancel
            // restores the layout copy taken when the editor opened.
            Layout live = _liveLayout;
            Layout before = _layoutSnapshot;
            bool restoreVertical = live.VerticalWidth <= 0 || live.VerticalHeight <= 0;
            bool restoreHorizontal = live.HorizontalWidth <= 0 || live.HorizontalHeight <= 0;
            AssignKeepingGeometry(live, before);
            if (restoreVertical)
            {
                live.VerticalWidth = before.VerticalWidth;
                live.VerticalHeight = before.VerticalHeight;
            }

            if (restoreHorizontal)
            {
                live.HorizontalWidth = before.HorizontalWidth;
                live.HorizontalHeight = before.HorizontalHeight;
            }
        }

        if (_liveApp is not null && _appSnapshot is not null)
        {
            _appSnapshot.Restore(_liveApp);
            AppThemeController.Apply(_liveApp.Appearance);
        }

        _refresh();
    }

    private sealed class AppSettingsSnapshot
    {
        private readonly AppAppearanceMode _appearance;
        private readonly bool _warnOnReset;
        private readonly bool _simpleSumOfBest;
        private readonly int _refreshRate;
        private readonly int _serverPort;
        private readonly HotkeyProfileStub _hotkeys;

        private AppSettingsSnapshot(AppSettings s)
        {
            _appearance = s.Appearance;
            _warnOnReset = s.WarnOnReset;
            _simpleSumOfBest = s.SimpleSumOfBest;
            _refreshRate = s.RefreshRate;
            _serverPort = s.ServerPort;
            _hotkeys = CloneHotkeys(s.HotkeyProfile);
        }

        public static AppSettingsSnapshot Capture(AppSettings s) => new(s);

        public void Restore(AppSettings live)
        {
            live.Appearance = _appearance;
            live.WarnOnReset = _warnOnReset;
            live.SimpleSumOfBest = _simpleSumOfBest;
            live.RefreshRate = _refreshRate;
            live.ServerPort = _serverPort;
            live.HotkeyProfile = CloneHotkeys(_hotkeys);
        }

        private static HotkeyProfileStub CloneHotkeys(HotkeyProfileStub p) => new()
        {
            SplitKey = p.SplitKey,
            ResetKey = p.ResetKey,
            SkipKey = p.SkipKey,
            UndoKey = p.UndoKey,
            PauseKey = p.PauseKey,
            ToggleGlobalHotkeys = p.ToggleGlobalHotkeys,
            SwitchComparisonPrevious = p.SwitchComparisonPrevious,
            SwitchComparisonNext = p.SwitchComparisonNext,
            GlobalHotkeysEnabled = p.GlobalHotkeysEnabled,
            DeactivateHotkeysForOtherPrograms = p.DeactivateHotkeysForOtherPrograms,
            AllowGamepadsAsHotkeys = p.AllowGamepadsAsHotkeys,
            DoubleTapPrevention = p.DoubleTapPrevention,
            HotkeyDelay = p.HotkeyDelay,
        };
    }
}
