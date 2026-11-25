using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.ComponentHost;
using LiveSplitNative.Core.Localization;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

public partial class MainWindow : Window
{
    private readonly INativeContextMenu _nativeContextMenu = NativeContextMenuFactory.Current;
    private readonly INativeFileDialogs _fileDialogs = NativeFileDialogs.Current;
    private readonly INativeWindowFrame _windowFrame = NativeWindowFrameFactory.Current;
    /// <summary>F1: the one owner of timer state; inputs call it, its Changed event redraws.</summary>
    private readonly TimerSession _session;
    private readonly TimerModel _model;
    private readonly ManualGameTimeController _manualGameTime;
    /// <summary>The layout's Image background at the window's size (TimerForm's bakedBackground).</summary>
    private readonly BackgroundImageBaker _backgroundImage = new();
    /// <summary>The therun.gg components' collectors (CollectorComponent's events and Dispose).</summary>
    private readonly TheRunController _theRun;
    /// <summary>The AutoSplit Integration components' autosplitters.</summary>
    private readonly AutoSplitIntegrationController _autoSplit;
    private readonly RacetimeController _racetime;
    private readonly AutoSplittingRuntimeController _asr;
    private readonly LiveSplitState _state;
    private readonly AppSettings _appSettings;
    private readonly string _settingsPath;
    private readonly DispatcherTimer _uiTimer;
    private readonly ComponentAssemblyProbe _probe = new();

    private Layout _layout;
    private string? _splitsPath;
    private string? _layoutPath;
    private string? _sessionRestoreMessage;
    /// <summary>Set once the user has answered the save prompts, so the next Close goes through.</summary>
    private bool _closeConfirmed;
    /// <summary>The quit prompts are showing; a second Close waits for them.</summary>
    private bool _confirmingClose;
    /// <summary>"Update Times?" is showing (Windows ResetMessageShown): hotkeys and Reset are ignored.</summary>
    private bool _resetMessageShown;
    /// <summary>
    /// Windows InTimerOnlyMode: no splits and a Timer-only layout. Resets keep nothing, and the
    /// Save Splits? and Update Times? questions are skipped.
    /// </summary>
    private bool _inTimerOnlyMode;
    private bool _geometryInteractionActive;
    private BorderlessInteractionSession? _interactionSession;
    /// <summary>A right press on the timer; its release opens the context menu.</summary>
    private bool _contextMenuPressPending;
    /// <summary>F2: the move or resize in progress, in screen points (one AppKit frame per event).</summary>
    private ScreenFrameDrag? _frameDrag;
    private IPointer? _capturedPointer;
    private double _interactionScaling = 1;
    private double _naturalContentSize = 1;
    private bool _applyingGeometry;
    private int _ownedDialogDepth;
    private bool? _mousePassThroughApplied;
    /// <summary>Windows TimerForm.OldSize — last OverallSize used for FixSize.</summary>
    private double _oldOverallSize;
    private LayoutMode? _appliedChromeMode;
    private bool _timerSurfaceConfigured;
    private bool _showTitle = true;
    private bool _showSplits = true;
    private bool _showSubsplits;
    private bool _showTimer = true;
    private bool _showDetailedTimer;
    private bool _showPreviousSegment = true;
    private bool _showSumOfBest;
    private bool _showBlankSpace;
    private TitleComponentSettings _titleSettings = new();
    private SplitsComponentSettings _splitsSettings = new();
    private SubsplitsComponentSettings _subsplitsSettings = new();

    /// <summary>Wheel movement not yet scrolled (a trackpad's small steps add up to one).</summary>
    private double _wheelSteps;
    private TimerComponentSettings _timerSettings = new();
    private DetailedTimerComponentSettings _detailedTimerSettings = new();
    private PreviousSegmentComponentSettings _previousSettings = new();
    private SumOfBestComponentSettings _sumOfBestSettings = new();
    private BlankSpaceComponentSettings _blankSettings = new();
    private PossibleTimeSaveComponentSettings _ptsSettings = new();
    private bool _showPossibleTimeSave;
    private readonly List<TextComponentSettings> _textSettings = [];
    private bool _showText;
    public MainWindow()
    {
        InitializeComponent();

        _settingsPath = AppCli.SettingsPath ?? XmlAppSettingsSerializer.DefaultPath;
        _appSettings = XmlAppSettingsSerializer.LoadFile(_settingsPath);
        // XMLSettingsFactory.LoadDrift: the clock is corrected by the drift measured before (from
        // now on, before any time is taken), and the atomic clock is read in the background.
        TimeStamp.Clock.LoadDrift(_appSettings.TimerDrift);
        if (SyncAtomicClock && !AppCli.Headless)
        {
            TimeStamp.StartAtomicClockSync();
        }

        // TimerForm.LoadSettings: the UI language comes from the settings (or the system).
        LanguageResolver.SetCurrentLanguageSetting(_appSettings.UILanguage);
        AppThemeController.Apply(_appSettings.Appearance);
        // Timer window stays visually on its layout surface (not Appearance-driven).
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;

        // Windows: without splits the run is a blank standard run (one empty segment).
        Run run = Run.CreateStandard();

        _state = new LiveSplitState(run);
        // The session regenerates comparisons on reset (Windows TimerForm.CurrentState_OnReset).
        _session = new TimerSession(_state, () => _appSettings.ToComparisonGeneratorSettings(), AvaloniaSessionDispatcher.Instance);
        _model = _session.Model;
        _session.Changed += OnSessionChanged;
        // Windows CurrentState_OnReset: in timer-only mode every reset starts a new blank run
        // (with the same start offset), so nothing is recorded.
        _model.OnReset += (_, _) =>
        {
            if (_inTimerOnlyMode)
            {
                Run blank = Run.CreateStandard();
                blank.Offset = _state.Run.Offset;
                _session.SetRun(blank, TimerPhase.NotRunning, -1);
            }
        };
        // Splits and Subsplits: the mouse wheel scrolls the lists (TimerModel.OnScrollUp /
        // OnScrollDown); start, split, skip, undo and reset bring them back to the current split.
        // Component DLLs (ComponentManager): they see this timer as their LiveSplitState.
        ExternalComponents.Source = new ExternalTimerSource(this);
        TimerSurface.ExternalComponentResized += () =>
        {
            // Posted by the surface, so it can arrive after the window closed (PlatformImpl null).
            if (_timerSurfaceConfigured && PlatformImpl is not null)
            {
                ApplyLayoutChange();
            }
        };
        _model.OnScrollUp += (_, _) => TimerSurface.ScrollUp();
        _model.OnScrollDown += (_, _) => TimerSurface.ScrollDown();
        _model.OnStart += (_, _) => TimerSurface.ResetScroll();
        _model.OnSplit += (_, _) => TimerSurface.ResetScroll();
        _model.OnSkipSplit += (_, _) => TimerSurface.ResetScroll();
        _model.OnUndoSplit += (_, _) => TimerSurface.ResetScroll();
        _model.OnReset += (_, _) => TimerSurface.ResetScroll();
        // Manual Game Time (a component with no drawing): its window and the game time it sets.
        _manualGameTime = new ManualGameTimeController(this, _state, () => _layout, () => _session.Execute(TimerCommand.StartOrSplit));
        _model.OnStart += (_, _) => _manualGameTime.OnStart();
        _model.OnReset += (_, _) => _manualGameTime.OnReset();
        _model.OnUndoSplit += (_, _) => _manualGameTime.OnUndoSplit();
        // Sound Effects (a component with no drawing): a sound for each timer event.
        var sounds = new SoundEffectsController(_state, () => _layout);
        _model.OnStart += (_, _) => sounds.OnStart();
        _model.OnSplit += (_, _) => sounds.OnSplit();
        _model.OnSkipSplit += (_, _) => sounds.OnSkipSplit();
        _model.OnUndoSplit += (_, _) => sounds.OnUndoSplit();
        _model.OnPause += (_, _) => sounds.OnPause();
        _model.OnResume += (_, _) => sounds.OnResume();
        _model.OnReset += (_, previousPhase) => sounds.OnReset(previousPhase);
        // therun.gg (a component with no drawing): the live run and the splits, sent to therun.gg.
        _theRun = new TheRunController(_state, () => _layout);
        _model.OnStart += (_, _) => _theRun.OnSplit();
        _model.OnSplit += (_, _) => _theRun.OnSplit();
        _model.OnSkipSplit += (_, _) => _theRun.OnSplit();
        _model.OnUndoSplit += (_, _) => _theRun.OnSplit();
        _model.OnUndoAllPauses += (_, _) => _theRun.OnSplit();
        _model.OnPause += (_, _) => _theRun.OnPause();
        _model.OnResume += (_, _) => _theRun.OnResume();
        _model.OnReset += (_, _) => _theRun.OnReset();
        // AutoSplit Integration (Pilgrim Autosplitter): the autosplitter and the timer drive each other.
        _autoSplit = new AutoSplitIntegrationController(_session, () => _layout);
        _model.OnStart += (_, _) => _autoSplit.OnStart();
        _model.OnSplit += (_, _) => _autoSplit.OnSplit();
        _model.OnReset += (_, _) => _autoSplit.OnReset();
        _model.OnSkipSplit += (_, _) => _autoSplit.OnSkipSplit();
        _model.OnUndoSplit += (_, _) => _autoSplit.OnUndoSplit();
        // Auto Splitting Runtime (WebAssembly auto splitters): they drive the timer through the session.
        _asr = new AutoSplittingRuntimeController(_session, () => _layout);
        // racetime.gg (a race provider): its races in the menu, read once at start.
        _racetime = new RacetimeController(_session, () => _appSettings, () => _layout);
        _racetime.Refresh();
        ComponentSettingsEditor.TimerPhaseNow = () => _state.CurrentPhase;
        // Component settings list this run's comparisons, as upstream settings controls do.
        ComponentSettingsEditor.RunComparisons = () => _state.Run.Comparisons.ToList();
        _layout = StandardLayoutFactory.Create();
        RestoreLastSessionFiles();
        // Windows TimerForm init: SwitchComparisonGenerators(), then SwitchComparison(LastComparison)
        // (a comparison the restored run lacks falls back to Personal Best).
        _session.SwitchComparisonGenerators(_appSettings.ComparisonGeneratorStates);
        _session.SwitchComparison(_appSettings.LastComparison);
        // Global hotkeys and the LiveSplit Server (MainWindow.Integrations.cs).
        InitIntegrations();

        _ = _probe.ProbeDirectory(Path.Combine(AppContext.BaseDirectory, "Components"));

        RebuildLayoutVisibility();
        ApplyLayoutWindowChrome();
        SyncSplitsColumnsFromRun();
        RestoreWindowGeometry();
        ApplyLayoutChange();

        _uiTimer = new DispatcherTimer { Interval = AppSettings.RefreshInterval(_appSettings.RefreshRate) };
        _uiTimer.Tick += (_, _) => UpdateTimerVisuals();
        _uiTimer.Start();

        KeyDown += OnKeyDown;
        // Window-coordinate input is independent of the scaled timer paint surface.
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        Resized += OnWindowResized;
        Opened += OnOpened;
        Closing += OnClosing;
        ScalingChanged += (_, _) =>
        {
            GeometryTrail.Add($"scaling {RenderScaling:0.##} client={ClientSize}");
            ApplyWindowBackground();
        };
        PositionChanged += (_, _) =>
        {
            if (!_applyingGeometry)
            {
                CaptureGeometryToModel();
            }
        };
        Console.WriteLine(
            $"Context menu backend: {_nativeContextMenu.BackendName} (systemNative={_nativeContextMenu.IsSystemNative}); " +
            $"windowFrame={_windowFrame.BackendName}");
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Windows TimerForm_FormClosing: changed splits, then a changed layout, are not saved
        // silently; ask first (Yes saves, No discards, Cancel keeps the timer open).
        if (!_closeConfirmed && (_state.Run.HasChanged || _layout.HasChanged))
        {
            e.Cancel = true;
            _ = ConfirmCloseAsync();
            return;
        }

        try
        {
            _windowFrame.SetMousePassThrough(this, false);
            _mousePassThroughApplied = false;
            TimerSurface.DisposeResources();
            _appSettings.LastComparison = _state.CurrentComparison;
            // The components are disposed with the form: therun.gg uploads the splits then.
            _theRun.DisposeAll();
            _autoSplit.DisposeAll();
            _asr.DisposeAll();
            DisposeIntegrations();
            // Windows TimerForm_FormClosing: the open splits are remembered with their timing method.
            AddCurrentSplitsToRecent(_state.CurrentTimingMethod);
            _appSettings.TimerDrift = TimeStamp.Clock.NewDrift;
            XmlAppSettingsSerializer.SaveFile(_appSettings, _settingsPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to persist settings.cfg: " + ex.Message);
        }
    }

    private async Task ConfirmCloseAsync()
    {
        if (_confirmingClose)
        {
            return;
        }

        _confirmingClose = true;
        try
        {
            if (!await WarnUserAboutSplitsSaveAsync() || !await WarnUserAboutLayoutSaveAsync(canCancel: true))
            {
                return;
            }
        }
        finally
        {
            _confirmingClose = false;
        }

        _closeConfirmed = true;
        Close();
    }

    /// <summary>
    /// Windows TimerForm.WarnUserAboutSplitsSave: changed splits are offered for saving (Yes saves,
    /// No drops the changes, Cancel stops whatever asked). When it is safe to go on the timer is
    /// reset, as Windows does before quitting or switching splits. Timer-only mode only resets.
    /// </summary>
    private async Task<bool> WarnUserAboutSplitsSaveAsync()
    {
        if (_inTimerOnlyMode)
        {
            _session.Execute(TimerCommand.Reset);
            return true;
        }

        bool safeToContinue = true;
        if (_state.Run.HasChanged)
        {
            QuestionAnswer answer = await ShowQuestionAsync(
                "Save Splits?",
                "Your splits have been updated but not yet saved.\nDo you want to save your splits now?",
                canCancel: true);
            if (answer == QuestionAnswer.Yes)
            {
                safeToContinue = await SaveSplitsAsync(saveAs: false, promptPersonalBest: false);
            }
            else if (answer == QuestionAnswer.Cancel)
            {
                return false;
            }
        }

        if (safeToContinue)
        {
            _session.Execute(TimerCommand.Reset);
        }

        return safeToContinue;
    }

    /// <summary>
    /// Windows TimerForm.Reset (Reset menu item and hotkey): with Warn On Reset on and a gold or a
    /// new PB in the attempt, "Update Times?" asks first. Yes resets and keeps the times, No resets
    /// without them, Cancel leaves the timer as it is.
    /// </summary>
    internal async Task ResetAsync()
    {
        if (_resetMessageShown)
        {
            return;
        }

        QuestionAnswer result = QuestionAnswer.Yes;
        if (_appSettings.WarnOnReset && !_inTimerOnlyMode)
        {
            _resetMessageShown = true;
            try
            {
                if (SplitsPrompts.ShouldWarnBeforeReset(_state))
                {
                    result = await ShowQuestionAsync(
                        "Update Times?",
                        "You have beaten some of your best times.\nDo you want to update them?",
                        canCancel: true);
                }
            }
            finally
            {
                _resetMessageShown = false;
            }
        }

        if (result == QuestionAnswer.Yes)
        {
            _session.Execute(TimerCommand.Reset);
        }
        else if (result == QuestionAnswer.No)
        {
            _session.Execute(TimerCommand.ResetWithoutSaving);
        }
    }

    /// <summary>Menu, hotkeys and demo keys: Reset goes through <see cref="ResetAsync"/>, the rest straight to the session.</summary>
    private void ExecuteTimerCommand(TimerCommand command, TimeSpan hotkeyDelay = default)
    {
        if (command == TimerCommand.Reset)
        {
            _ = ResetAsync();
            return;
        }

        _session.ExecuteFromHotkey(command, hotkeyDelay);
    }

    /// <summary>
    /// Windows TimerForm.WarnUserAboutLayoutSave: when the layout changed since it was opened or
    /// saved, ask whether to save it. Returns false when the user cancels (or cancels Save As).
    /// </summary>
    private async Task<bool> WarnUserAboutLayoutSaveAsync(bool canCancel)
    {
        if (!_layout.HasChanged)
        {
            return true;
        }

        QuestionAnswer answer = await ShowQuestionAsync(
            "Save Layout?",
            "Your layout has been updated but not yet saved.\nDo you want to save your layout now?",
            canCancel);
        return answer switch
        {
            QuestionAnswer.Yes => await SaveLayoutAsync(saveAs: false),
            QuestionAnswer.No => true,
            _ => false,
        };
    }

    /// <summary>
    /// Reload Splits column settings from layout XML. Does <b>not</b> synthesize one column
    /// per available comparison — Windows default is +/- + Time (Current Comparison);
    /// multi-columns only via the Splits Columns editor. Existing layout XML is preserved as-is.
    /// </summary>
    private void SyncSplitsColumnsFromRun()
    {
        LayoutComponent? splits = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.SplitsPath);
        if (splits is null)
        {
            return;
        }

        // FromInnerXml already materializes default +/- + Time when Columns missing.
        // Never write CustomComparisons back into layout Columns.
        _splitsSettings = SplitsComponentSettings.FromInnerXml(splits.SettingsInnerXml);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _diagPressCount++;
        PointerPoint point = e.GetCurrentPoint(this);
        Point local = e.GetPosition(this);
        double width = Bounds.Width > 1 ? Bounds.Width : Width;
        double height = Bounds.Height > 1 ? Bounds.Height : Height;
        BorderlessPointerAction action = BorderlessInteractionSession.RoutePointerPress(
            point.Properties.IsLeftButtonPressed,
            point.Properties.IsRightButtonPressed,
            local.X,
            local.Y,
            width,
            height,
            _layout.Settings.AllowMoving,
            _layout.Settings.AllowResizing);
        if (action == BorderlessPointerAction.ContextMenu)
        {
            // Windows opens the context menu when the right button comes up (WM_CONTEXTMENU),
            // and so do we: the NSMenu tracking loop must never run while a button is down, or
            // it swallows that button's mouse-up and Avalonia keeps it "pressed" for good, so
            // later left presses never arrive as presses (11 Oct 2026: no move or resize after
            // opening Edit Layout / Layout Settings from this menu).
            e.Handled = true;
            _contextMenuPressPending = true;
            return;
        }

        if (action == BorderlessPointerAction.None)
        {
            return;
        }

        try
        {
            PixelPoint screen = this.PointToScreen(local);
            _geometryInteractionActive = true;
            e.Pointer.Capture(this);
            _capturedPointer = e.Pointer;
            BorderlessResizeEdge? hitEdge = action == BorderlessPointerAction.Resize
                ? TimerWindowHitTest.HitResizeEdge(local.X, local.Y, width, height)
                : null;
            if (TryStartFrameDrag(hitEdge))
            {
                e.Handled = true;
                StandardCursorType? dragCursor = hitEdge is null ? StandardCursorType.SizeAll : TimerWindowHitTest.CursorForEdge(hitEdge);
                if (dragCursor is { } dc)
                {
                    Cursor = new Cursor(dc);
                }

                return;
            }

            if (action == BorderlessPointerAction.Resize
                && hitEdge is { } edge)
            {
                e.Handled = true;
                double scaling = Math.Max(0.1, RenderScaling);
                _interactionScaling = scaling;
                _interactionSession = BorderlessInteractionSession.StartResize(
                    edge,
                    Position.X / scaling,
                    Position.Y / scaling,
                    width,
                    height,
                    screen.X / scaling,
                    screen.Y / scaling);
                StandardCursorType? cursor = TimerWindowHitTest.CursorForEdge(edge);
                if (cursor is { } c)
                {
                    Cursor = new Cursor(c);
                }

                return;
            }

            e.Handled = true;
            _interactionSession = BorderlessInteractionSession.StartMove(
                Position.X,
                Position.Y,
                screen.X,
                screen.Y);
            Cursor = new Cursor(StandardCursorType.SizeAll);
        }
        catch (Exception ex)
        {
            ClearGeometryInteraction();
            Console.Error.WriteLine("Window move/resize failed: " + ex.Message);
        }
    }

    /// <summary>
    /// F2: starts a move (<paramref name="edge"/> null) or resize in screen points when the
    /// platform can set a whole frame at once (macOS on Apple silicon). False means use the
    /// Avalonia path, as in headless tests, on Intel Macs, or with --legacy-window-drag.
    /// </summary>
    private bool TryStartFrameDrag(BorderlessResizeEdge? edge)
    {
        if (AppCli.LegacyWindowDrag
            || !_windowFrame.SupportsScreenFrames
            || !_windowFrame.TryGetScreenFrame(this, out ScreenFrame frame)
            || !_windowFrame.TryGetMouseLocation(out double mouseX, out double mouseY))
        {
            return false;
        }

        _frameDrag = edge is { } resizeEdge
            ? ScreenFrameDrag.StartResize(resizeEdge, frame, mouseX, mouseY)
            : ScreenFrameDrag.StartMove(frame, mouseX, mouseY);
        return true;
    }

    /// <summary>
    /// One pointer event of an F2 drag: the frame for the mouse position right now, set in one
    /// AppKit call; then the layout follows the new size and keeps the new geometry in memory.
    /// </summary>
    private void ContinueFrameDrag(ScreenFrameDrag drag)
    {
        if (!_windowFrame.TryGetMouseLocation(out double mouseX, out double mouseY))
        {
            return;
        }

        ScreenFrame frame = drag.FrameFor(
            mouseX,
            mouseY,
            MinWidth,
            MinHeight,
            drag.IsMove ? null : _windowFrame.VisibleTop(this));
        if (!drag.IsMove)
        {
            // TimerForm_SizeChanged -> MaintainMinimumSize, applied to the frame before it is set,
            // so the window never shows a size the rules would take back.
            (double width, double height) = MaintainedSize(frame.Width, frame.Height);
            frame = drag.Resized(frame, Math.Min(width, MaxWidth), Math.Min(height, MaxHeight));
        }

        _applyingGeometry = true;
        try
        {
            _windowFrame.SetScreenFrame(this, frame);
        }
        finally
        {
            _applyingGeometry = false;
        }

        if (!drag.IsMove)
        {
            UpdateLayoutSnapshot(frame.Width, frame.Height);
        }

        CaptureGeometryToModel(frame.Width, frame.Height);
    }

    /// <summary>
    /// TimerForm_MouseWheel: a wheel step up scrolls the split lists up, down scrolls them down.
    /// A mouse wheel step (1.0 or more) is one split, as on Windows. The first movement of a new
    /// scroll (after <see cref="WheelPause"/> without any) is one split however small it is
    /// (Basten 2 Nov: a small turn of the wheel, which macOS reports as a fraction of a step,
    /// should move a split). A trackpad or Magic Mouse then sends many small steps; every
    /// <see cref="TrackpadStep"/> of them scrolls one more split (Basten 17 Oct: a whole 1.0 per
    /// split needed too long a swipe).
    /// </summary>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        double delta = e.Delta.Y;
        if (delta == 0)
        {
            return;
        }

        e.Handled = true;
        TimeSpan now = WheelClock();
        bool newScroll = now - _lastWheel >= WheelPause;
        _lastWheel = now;
        if (Math.Abs(delta) >= 1 || newScroll)
        {
            _wheelSteps = 0;
            _session.Scroll(delta > 0);
            return;
        }

        if (Math.Sign(delta) != Math.Sign(_wheelSteps))
        {
            _wheelSteps = 0;
        }

        _wheelSteps += delta;
        if (Math.Abs(_wheelSteps) >= TrackpadStep - 1e-9)
        {
            bool up = _wheelSteps > 0;
            _wheelSteps -= up ? TrackpadStep : -TrackpadStep;
            _session.Scroll(up);
        }
    }

    /// <summary>How much small (trackpad) wheel movement scrolls one split.</summary>
    internal const double TrackpadStep = 0.3;

    /// <summary>A wheel quiet this long has stopped: its next movement starts a new scroll.</summary>
    internal static readonly TimeSpan WheelPause = TimeSpan.FromMilliseconds(250);

    /// <summary>The time wheel movements are told apart by (tests set their own).</summary>
    internal static Func<TimeSpan> WheelClock { get; set; } = () => TimeSpan.FromTicks(Environment.TickCount64 * TimeSpan.TicksPerMillisecond);

    private TimeSpan _lastWheel = TimeSpan.MinValue / 2;

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_frameDrag is { } frameDrag)
        {
            ContinueFrameDrag(frameDrag);
            return;
        }

        if (_interactionSession is { Action: BorderlessPointerAction.Resize } resize)
        {
            Point local = e.GetPosition(this);
            PixelPoint screen = this.PointToScreen(local);
            double scaling = _interactionScaling;
            BorderlessWindowRect rect = resize.ResizeTo(
                screen.X / scaling,
                screen.Y / scaling,
                MinWidth,
                MinHeight);
            (double maintainedWidth, double maintainedHeight) = MaintainedSize(rect.Width, rect.Height);
            rect = WindowSizeRules.Resize(rect, resize.Edge, maintainedWidth, maintainedHeight);
            _applyingGeometry = true;
            try
            {
                Width = rect.Width;
                Height = rect.Height;
                Position = new PixelPoint(
                    (int)Math.Round(rect.X * scaling),
                    (int)Math.Round(rect.Y * scaling));
            }
            finally
            {
                _applyingGeometry = false;
            }

            UpdateLayoutSnapshot();
            CaptureGeometryToModel();
            return;
        }

        if (_interactionSession is { Action: BorderlessPointerAction.Move } move)
        {
            Point local = e.GetPosition(this);
            PixelPoint screen = this.PointToScreen(local);
            BorderlessWindowRect rect = move.MoveTo(screen.X, screen.Y);
            _applyingGeometry = true;
            try
            {
                Position = new PixelPoint(
                    (int)Math.Round(rect.X),
                    (int)Math.Round(rect.Y));
            }
            finally
            {
                _applyingGeometry = false;
            }

            CaptureGeometryToModel();
            return;
        }

        if (!_layout.Settings.AllowResizing || e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        Point hover = e.GetPosition(this);
        double bw = Bounds.Width > 1 ? Bounds.Width : Width;
        double bh = Bounds.Height > 1 ? Bounds.Height : Height;
        BorderlessResizeEdge? hoverEdge = TimerWindowHitTest.HitResizeEdge(hover.X, hover.Y, bw, bh);
        StandardCursorType? hoverCursor = TimerWindowHitTest.CursorForEdge(hoverEdge);
        Cursor = hoverCursor is { } hc ? new Cursor(hc) : Cursor.Default;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Right && _contextMenuPressPending)
        {
            _contextMenuPressPending = false;
            e.Handled = true;
            PixelPoint screen = this.PointToScreen(e.GetPosition(this));
            // After this release has been fully handled (capture released, button up), never
            // from inside the input dispatch.
            Dispatcher.UIThread.Post(() => ShowTimerContextMenu(screen), DispatcherPriority.Input);
            return;
        }

        if (!_geometryInteractionActive)
        {
            return;
        }

        ClearGeometryInteraction();
        CaptureGeometryToModel();
        UpdateLayoutSnapshot();
        MaintainMinimumSize();
    }

    private void ClearGeometryInteraction()
    {
        try
        {
            _capturedPointer?.Capture(null);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Pointer capture release failed: " + ex.Message);
        }

        _capturedPointer = null;
        _interactionSession = null;
        _frameDrag = null;
        _interactionScaling = 1;
        _geometryInteractionActive = false;
        Cursor = Cursor.Default;
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        GeometryTrail.Add($"resized {e.ClientSize.Width:0.#}x{e.ClientSize.Height:0.#} reason={e.Reason} applying={_applyingGeometry} scaling={RenderScaling:0.##}");
        // TimerForm_SizeChanged -> CreateBakedBackground: the image follows the window's size.
        if (string.Equals(_layout.Settings.BackgroundType, "Image", StringComparison.OrdinalIgnoreCase))
        {
            ApplyWindowBackground();
        }

        if (_applyingGeometry)
        {
            return;
        }

        UpdateLayoutSnapshot();
        CaptureGeometryToModel();
        MaintainMinimumSize();
    }

    /// <summary>TimerForm.RightClickMenu — OS-native on macOS (NSMenu), not Avalonia chrome.</summary>
    private void ShowTimerContextMenu(PixelPoint screenPoint)
    {
        _diagMenusShown++;
        // RightClickMenu_Opening: UiLocalizer.Apply(this) translates the menu's items.
        NativeMenuModel model = UiLocalizer.Apply(BuildNativeContextMenuModel());
        _nativeContextMenu.Show(this, screenPoint, model);
    }

    private static NativeMenuAction NativeItem(string header, Action action, bool enabled = true, bool @checked = false) =>
        new()
        {
            Header = header,
            IsEnabled = enabled,
            IsChecked = @checked,
            OnClick = action,
        };

    private static NativeMenuAction NativeItem(string header, Func<Task> action, bool enabled = true, bool @checked = false) =>
        new()
        {
            Header = header,
            IsEnabled = enabled,
            IsChecked = @checked,
            OnClickAsync = action,
        };

    private static NativeMenuAction NativeSubmenu(string header, IEnumerable<NativeMenuEntry> children) =>
        new()
        {
            Header = header,
            Children = children.ToList(),
        };

    private void ApplyLayoutWindowChrome(bool syncSizeFromLayout = false)
    {
        // The dialogs sit above the timer by window level (OwnedDialog), so the timer keeps
        // Always on Top while they are open, live preview included.
        Topmost = _layout.Settings.AlwaysOnTop;
        CanResize = _layout.Settings.AllowResizing;
        Opacity = Math.Clamp(_layout.Settings.Opacity, 0.15, 1.0);
        SizeToContent = SizeToContent.Manual;

        bool modeChanged = _appliedChromeMode is not null && _appliedChromeMode != _layout.Mode;
        bool applySize = syncSizeFromLayout || _appliedChromeMode != _layout.Mode;
        if (modeChanged)
        {
            _oldOverallSize = 0;
        }

        if (applySize)
        {
            _appliedChromeMode = _layout.Mode;
            if (!_applyingGeometry)
            {
                RecalculateNaturalContentSize();
                if (modeChanged)
                {
                    // editor_OrientationSwitched: the orientation being left keeps the window's
                    // size; the new one gets Windows' first size if it has none yet.
                    WindowSizeRules.SwitchOrientation(
                        _layout,
                        (int)Math.Round(CurrentWindowWidth()),
                        (int)Math.Round(CurrentWindowHeight()),
                        _naturalContentSize);
                }

                bool horizontal = _layout.Mode == LayoutMode.Horizontal;
                double width = horizontal
                    ? (_layout.HorizontalWidth > 0 ? _layout.HorizontalWidth : (int)(_naturalContentSize + 0.5))
                    : (_layout.VerticalWidth > 0 ? _layout.VerticalWidth : 300);
                double height = horizontal
                    ? (_layout.HorizontalHeight > 0 ? _layout.HorizontalHeight : 45)
                    : (_layout.VerticalHeight > 0 ? _layout.VerticalHeight : (int)(_naturalContentSize + 0.5));
                // SetLayout: the window takes that size (never more than the screens, as
                // WinForms' maximum tracking size) and keeps its top-left corner.
                ApplySizeLimits();
                width = Math.Clamp(width, BorderlessWindowGeometry.WindowsMinFloor, MaxWidth);
                height = Math.Clamp(height, BorderlessWindowGeometry.WindowsMinFloor, MaxHeight);
                GeometryTrail.Add($"chrome {_layout.Mode} switch={modeChanged} size={width}x{height}");
                _applyingGeometry = true;
                try
                {
                    Width = width;
                    Height = height;
                    if (modeChanged)
                    {
                        PlaceOnScreenAfterSwitch(width, height);
                    }
                }
                finally
                {
                    _applyingGeometry = false;
                }

                if (modeChanged)
                {
                    CaptureGeometryToModel(width, height);
                }
            }
        }

        ApplyWindowBackground();
        RebuildLayoutSnapshot(preserveScale: !modeChanged && !applySize);
    }

    private double CurrentWindowWidth() => double.IsFinite(Width) && Width > 1 ? Width : Bounds.Width;

    private double CurrentWindowHeight() => double.IsFinite(Height) && Height > 1 ? Height : Bounds.Height;

    /// <summary>Every screen's bounds and working area, in screen units (y down), and the scale from window units.</summary>
    private (List<BorderlessWindowRect> Bounds, List<BorderlessWindowRect> WorkingAreas, double Scaling) ScreenRects()
    {
        var bounds = new List<BorderlessWindowRect>();
        var areas = new List<BorderlessWindowRect>();
        IReadOnlyList<Screen> all = Screens?.All ?? [];
        foreach (Screen screen in all)
        {
            bounds.Add(new BorderlessWindowRect(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height));
            areas.Add(new BorderlessWindowRect(
                screen.WorkingArea.X,
                screen.WorkingArea.Y,
                screen.WorkingArea.Width,
                screen.WorkingArea.Height));
        }

        double scaling = Screens?.ScreenFromWindow(this)?.Scaling ?? all.FirstOrDefault()?.Scaling ?? 1;
        return (bounds, areas, Math.Max(0.1, scaling));
    }

    /// <summary>
    /// The window's size limits: FixSize's MinimumSize (set elsewhere) and, as WinForms' maximum
    /// tracking size, never more than all screens together. A minimum never exceeds the maximum.
    /// </summary>
    private void ApplySizeLimits(double? minWidth = null, double? minHeight = null)
    {
        (List<BorderlessWindowRect> screens, _, double scaling) = ScreenRects();
        (double maxWidth, double maxHeight) = WindowSizeRules.MaximumSize(screens);
        maxWidth /= scaling;
        maxHeight /= scaling;
        MaxWidth = maxWidth;
        MaxHeight = maxHeight;
        MinWidth = Math.Min(minWidth ?? MinWidth, maxWidth);
        MinHeight = Math.Min(minHeight ?? MinHeight, maxHeight);
    }

    /// <summary>
    /// After an orientation switch: the window keeps its top-left corner as on Windows, but is
    /// moved inside its screen's working area when it would stick out (a macOS addition; see
    /// <see cref="WindowSizeRules.FitInWorkingArea"/>).
    /// </summary>
    private void PlaceOnScreenAfterSwitch(double width, double height)
    {
        (List<BorderlessWindowRect> screens, List<BorderlessWindowRect> areas, double scaling) = ScreenRects();
        if (screens.Count == 0)
        {
            return;
        }

        var bounds = new BorderlessWindowRect(Position.X, Position.Y, width * scaling, height * scaling);
        (double x, double y) = WindowSizeRules.FitInWorkingArea(bounds, screens, areas);
        var position = new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
        if (position != Position)
        {
            GeometryTrail.Add($"switch moved {Position} -> {position}");
            Position = position;
        }
    }

    private void ApplyWindowBackground()
    {
        // TimerForm.DrawBackground: the image replaces the colors (baked over the form's black,
        // cropped to the window's shape around its center, never stretched); otherwise the
        // colors, a solid color or a gradient.
        var c1 = _layout.Settings.BackgroundColor;
        var c2 = _layout.Settings.BackgroundColor2;
        byte a1 = c1.A == 0 ? (byte)255 : c1.A;
        var first = ArgbColor.FromArgb(a1, c1.R, c1.G, c1.B);
        var second = c2.A == 0 ? first : c2;
        string type = _layout.Settings.ColorLayerType;
        bool gradient = string.Equals(type, "VerticalGradient", StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, "HorizontalGradient", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(_layout.Settings.BackgroundType, "Image", StringComparison.OrdinalIgnoreCase))
        {
            double scaling = Math.Max(0.1, RenderScaling);
            var pixels = new PixelSize(
                (int)Math.Round(ClientSize.Width * scaling),
                (int)Math.Round(ClientSize.Height * scaling));
            Bitmap? image = _backgroundImage.Bake(
                _layout.Settings.BackgroundImage,
                _layout.Settings.ImageOpacity,
                _layout.Settings.ImageBlur,
                pixels,
                scaling);
            Background = image is null ? Brushes.Black : new ImageBrush(image) { Stretch = Stretch.Fill };
            return;
        }

        Background = gradient
            ? GradientBrush(type.Contains("Horizontal", StringComparison.OrdinalIgnoreCase) ? "Horizontal" : "Vertical", first, second)
            : new SolidColorBrush(Color.FromArgb(a1, c1.R, c1.G, c1.B));
    }

    private void RebuildLayoutVisibility()
    {
        _timerSurfaceConfigured = false;
        LayoutComponent? title = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.TitlePath);
        LayoutComponent? splits = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.SplitsPath);
        LayoutComponent? subsplits = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.SubsplitsPath);
        LayoutComponent? timer = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.TimerPath);
        LayoutComponent? detailedTimer = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.DetailedTimerPath);
        LayoutComponent? previous = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.PreviousSegmentPath);
        LayoutComponent? sob = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.SumOfBestPath);
        LayoutComponent? pts = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.PossibleTimeSavePath);
        LayoutComponent? blank = _layout.Components.FirstOrDefault(c => c.Path == StandardLayoutFactory.BlankSpacePath);
        List<LayoutComponent> textComps = _layout.Components
            .Where(c => c.Path == StandardLayoutFactory.TextPath)
            .ToList();

        _showTitle = title is not null;
        _showSplits = splits is not null;
        _showSubsplits = subsplits is not null;
        _showTimer = timer is not null;
        _showDetailedTimer = detailedTimer is not null;
        _showPreviousSegment = previous is not null;
        _showSumOfBest = sob is not null;
        _showPossibleTimeSave = pts is not null;
        _showBlankSpace = blank is not null;
        _showText = textComps.Count > 0;
        _titleSettings = TitleComponentSettings.FromInnerXml(title?.SettingsInnerXml);
        _splitsSettings = SplitsComponentSettings.FromInnerXml(splits?.SettingsInnerXml);
        _subsplitsSettings = SubsplitsComponentSettings.FromInnerXml(subsplits?.SettingsInnerXml);
        _timerSettings = TimerComponentSettings.FromInnerXml(timer?.SettingsInnerXml);
        _detailedTimerSettings = DetailedTimerComponentSettings.FromInnerXml(detailedTimer?.SettingsInnerXml);
        _previousSettings = PreviousSegmentComponentSettings.FromInnerXml(previous?.SettingsInnerXml);
        _sumOfBestSettings = SumOfBestComponentSettings.FromInnerXml(sob?.SettingsInnerXml);
        _ptsSettings = PossibleTimeSaveComponentSettings.FromInnerXml(pts?.SettingsInnerXml);
        _blankSettings = BlankSpaceComponentSettings.FromInnerXml(blank?.SettingsInnerXml);
        _textSettings.Clear();
        foreach (LayoutComponent tc in textComps)
        {
            _textSettings.Add(TextComponentSettings.FromInnerXml(tc.SettingsInnerXml));
        }

    }

    private static IBrush GradientBrush(string type, ArgbColor c1, ArgbColor c2)
    {
        // Accept both UI labels ("Plain With Delta Color") and storage tokens ("PlainWithDeltaColor").
        // Delta-color variants for Detailed Timer go through ApplyDetailedTimerBackground (live timer color).
        string normalized = TimerPaintLogic.NormalizeGradientType(type);
        if (normalized.Equals("Plain", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("PlainWithDeltaColor", StringComparison.OrdinalIgnoreCase))
        {
            return ToBrush(c1);
        }

        bool horizontal = TimerPaintLogic.IsHorizontalGradient(normalized);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = horizontal
                ? new RelativePoint(1, 0, RelativeUnit.Relative)
                : new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(c1.A, c1.R, c1.G, c1.B), 0),
                new GradientStop(Color.FromArgb(c2.A, c2.R, c2.G, c2.B), 1),
            ],
        };
    }

    /// <summary>
    /// The layout (or which components show) changed: recompute its size, which may resize the
    /// window as Windows FixSize does, then redraw. Timer commands never come here; they reach
    /// the view through <see cref="OnSessionChanged"/>.
    /// </summary>
    private void ApplyLayoutChange()
    {
        RebuildLayoutSnapshot(preserveScale: true);
        TimerSurface.UpdateValues();
        MaintainMinimumSize();
    }

    /// <summary>
    /// Timer-session events. Timer and comparison changes only redraw values; only a different or
    /// edited run recomputes the layout size (its segment count may differ).
    /// </summary>
    private void OnSessionChanged(object? sender, SessionChangedEventArgs e)
    {
        if (!_timerSurfaceConfigured)
        {
            return;
        }

        if (e.Change.HasFlag(SessionChange.Run))
        {
            SyncSplitsColumnsFromRun();
            RebuildLayoutSnapshot(preserveScale: true);
        }

        TimerSurface.UpdateValues();
        if (e.Change.HasFlag(SessionChange.Run))
        {
            // A different run can need more room (icons, columns). Timer commands never resize
            // the window (the F1 gate), so they do not come here.
            MaintainMinimumSize();
        }

        ApplyMousePassThrough();
    }

    /// <summary>The session, for the window-property gate test.</summary>
    internal TimerSession Session => _session;

    /// <summary>The Subsplits rows as text, for the Subsplits tests.</summary>
    internal string SubsplitsRowsForTests => TimerSurface.DescribeSubsplitsRows();

    /// <summary>What the Detailed Timer's comparison lines were last drawn with, for its tests.</summary>
    internal TimerSurfaceControl.DetailedTimerText? DetailedTimerTextForTests => TimerSurface.LastDetailedTimerText;

    /// <summary>The current layout, for the layout-saving tests.</summary>
    internal Layout CurrentLayout => _layout;

    /// <summary>The live settings.cfg values, for the splits-saving tests.</summary>
    internal AppSettings CurrentSettings => _appSettings;

    private async void OnOpened(object? sender, EventArgs e)
    {
        UpdateLayoutSnapshot();
        _mousePassThroughApplied = null;
        ApplyMousePassThrough();

        if (!string.IsNullOrWhiteSpace(_sessionRestoreMessage))
        {
            string msg = _sessionRestoreMessage;
            _sessionRestoreMessage = null;
            await ShowErrorAsync("Last session", msg);
        }

        CheckForUpdatesAtStart();

        if (ExternalComponents.UserComponentsOff && ComponentCrashGuard.SkippedAfterCrash.Count > 0)
        {
            await AskAboutComponentsAfterCrashAsync(ComponentCrashGuard.SkippedAfterCrash);
        }

        // macOS: the permission global hotkeys need, while it is missing (until Don't ask again).
        await AskForHotkeyPermissionAsync(atStart: true);
    }

    /// <summary>
    /// Load last .lsl / .lss paths from settings.cfg. Missing or malformed → message + safe
    /// fallback (default layout / seeded run). Never writes .lss/.lsl during restore.
    /// </summary>
    private void RestoreLastSessionFiles()
    {
        var warnings = new List<string>();

        // Windows TimerForm init: the last splits first (with the timing method they were last
        // used with); without them the run stays the blank one.
        bool splitsLoaded = false;
        string splitsPath = _appSettings.LastSplitsPath;
        if (!string.IsNullOrWhiteSpace(splitsPath))
        {
            if (!File.Exists(splitsPath))
            {
                // Start without splits next time too (an empty entry, as Windows' Close Splits).
                warnings.Add($"Last splits not found:\n{splitsPath}");
                _appSettings.AddToRecentSplits("", null, TimingMethod.RealTime, _appSettings.CurrentHotkeyProfile);
                PersistAppSettingsQuiet();
            }
            else
            {
                try
                {
                    UpdateStateFromSplitsPath(splitsPath);
                    Run loaded = XmlRunSerializer.LoadFile(splitsPath);
                    _session.SetRun(loaded, TimerPhase.NotRunning, -1);
                    _splitsPath = splitsPath;
                    splitsLoaded = true;
                    Title = "LiveSplit — " + Path.GetFileName(splitsPath);
                }
                catch (Exception ex)
                {
                    // Keep the blank run; do not write over the broken file.
                    warnings.Add($"Could not open last splits:\n{splitsPath}\n{ex.Message}");
                    _appSettings.AddToRecentSplits("", null, TimingMethod.RealTime, _appSettings.CurrentHotkeyProfile);
                    PersistAppSettingsQuiet();
                }
            }
        }

        // Then the last layout; with none, a Timer-only layout when there are no splits (Windows
        // timer-only mode), else the default layout. A layout that fails to open is the default.
        string layoutPath = _appSettings.LastLayoutPath;
        _layoutPath = null;
        if (string.IsNullOrWhiteSpace(layoutPath))
        {
            _layout = !splitsLoaded ? StandardLayoutFactory.CreateTimerOnly() : StandardLayoutFactory.Create();
        }
        else
        {
            try
            {
                _layout = XmlLayoutSerializer.LoadFile(layoutPath);
                _layoutPath = layoutPath;
            }
            catch (Exception ex)
            {
                _layout = StandardLayoutFactory.Create();
                warnings.Add(File.Exists(layoutPath)
                    ? $"Could not open last layout:\n{layoutPath}\n{ex.Message}"
                    : $"Last layout not found:\n{layoutPath}");
                // As Windows: the entry stays in the recent list until Edit History removes it.
            }
        }

        _inTimerOnlyMode = !splitsLoaded;
        SetInTimerOnlyMode();

        if (warnings.Count > 0)
        {
            _sessionRestoreMessage = string.Join("\n\n", warnings);
        }
    }

    /// <summary>Windows SetInTimerOnlyMode: timer-only mode lasts only while the layout is a single Timer.</summary>
    private void SetInTimerOnlyMode()
    {
        if (!StandardLayoutFactory.IsTimerOnly(_layout))
        {
            _inTimerOnlyMode = false;
        }
    }

    /// <summary>
    /// Windows TimerForm.SetLayout: the window takes the layout's components, size and position
    /// (kept on a screen), and timer-only mode ends unless the layout is a single Timer.
    /// </summary>
    private void SetLayout(Layout layout, string? path)
    {
        _layout = layout;
        _layoutPath = path;
        _theRun.LayoutChanged();
        _autoSplit.LayoutChanged();
        _asr.LayoutChanged();
        RefreshHotkeyHook();
        _oldOverallSize = 0;
        RebuildLayoutVisibility();
        ApplyLayoutWindowChrome(syncSizeFromLayout: true);
        SyncSplitsColumnsFromRun();
        RestoreWindowGeometry();
        ApplyLayoutChange();
        SetInTimerOnlyMode();
    }

    /// <summary>
    /// Windows WarnAndRemoveTimerOnly (splits opened or edited): leaving timer-only mode brings
    /// back the last layout used, or the default one, at the window's current position.
    /// </summary>
    private async Task<bool> WarnAndRemoveTimerOnlyAsync(bool canCancel)
    {
        if (!_inTimerOnlyMode)
        {
            return true;
        }

        if (!await WarnUserAboutLayoutSaveAsync(canCancel))
        {
            return false;
        }

        _inTimerOnlyMode = false;
        string? lastLayoutPath = _appSettings.RecentLayouts.LastOrDefault(x => !string.IsNullOrEmpty(x));
        Layout layout;
        string? path = null;
        try
        {
            if (lastLayoutPath is not null)
            {
                layout = XmlLayoutSerializer.LoadFile(lastLayoutPath);
                path = lastLayoutPath;
                RememberAndPersistLayout(lastLayoutPath);
            }
            else
            {
                layout = StandardLayoutFactory.Create();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Could not open the last layout: " + ex.Message);
            layout = StandardLayoutFactory.Create();
            path = null;
        }

        layout.X = Position.X;
        layout.Y = Position.Y;
        SetLayout(layout, path);
        return true;
    }

    private void PersistAppSettingsQuiet()
    {
        try
        {
            // AddDriftToSettings: the drift measured this session, for the next one.
            _appSettings.TimerDrift = TimeStamp.Clock.NewDrift;
            XmlAppSettingsSerializer.SaveFile(_appSettings, _settingsPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to persist settings.cfg: " + ex.Message);
        }
    }

    /// <summary>
    /// Windows TimerForm.SetLayout: the window takes the layout's size and position (Basten,
    /// 11 Oct 2026: exactly like Windows, nothing about the window is kept in settings.cfg).
    /// Unusable sizes fall back to defaults, and the window is kept on a screen.
    /// </summary>
    private void RestoreWindowGeometry()
    {
        _applyingGeometry = true;
        try
        {
            RecalculateNaturalContentSize();
            bool vertical = _layout.Mode == LayoutMode.Vertical;
            int fallbackW = 286;
            int fallbackH = vertical ? (int)Math.Ceiling(_naturalContentSize) : 140;
            // Windows FixSize: min = max(OverallSize/5, 25) — short windows scale down uniformly.
            int minW = vertical
                ? BorderlessWindowGeometry.WindowsMinFloor
                : (int)BorderlessWindowGeometry.WindowsMinExtent(_naturalContentSize);
            int minH = vertical
                ? (int)BorderlessWindowGeometry.WindowsMinExtent(_naturalContentSize)
                : BorderlessWindowGeometry.WindowsMinFloor;
            ApplySizeLimits(minW, minH);
            (int width, int height) = BorderlessWindowGeometry.SanitizeSavedSize(
                vertical ? _layout.VerticalWidth : _layout.HorizontalWidth,
                vertical ? _layout.VerticalHeight : _layout.HorizontalHeight,
                fallbackW,
                fallbackH,
                minW,
                minH);

            Width = Math.Min(width, MaxWidth);
            Height = Math.Min(height, MaxHeight);
            _oldOverallSize = _naturalContentSize;
            UpdateLayoutSnapshot();

            (PixelRect workArea, double workAreaScaling) = ResolveRestoreWorkArea();
            int windowPixelWidth = (int)Math.Round(Width * workAreaScaling);
            int windowPixelHeight = (int)Math.Round(Height * workAreaScaling);
            int maxX = Math.Max(workArea.X, workArea.X + workArea.Width - windowPixelWidth);
            int maxY = Math.Max(workArea.Y, workArea.Y + workArea.Height - windowPixelHeight);
            Position = new PixelPoint(Math.Clamp(_layout.X, workArea.X, maxX), Math.Clamp(_layout.Y, workArea.Y, maxY));
            CaptureGeometryToModel();
        }
        finally
        {
            _applyingGeometry = false;
        }
    }

    private (PixelRect WorkArea, double Scaling) ResolveRestoreWorkArea()
    {
        Screen? screen = Screens?.ScreenFromPoint(new PixelPoint(_layout.X, _layout.Y))
            ?? Screens?.Primary
            ?? Screens?.All.FirstOrDefault();
        return screen is null
            ? (new PixelRect(0, 0, 1440, 900), 1)
            : (screen.WorkingArea, Math.Max(0.1, screen.Scaling));
    }

    /// <summary>
    /// Windows TimerForm_SizeChanged / SaveLayout: the layout holds the window's current size and
    /// position. This only updates the layout in memory; it reaches disk when the layout is saved.
    /// </summary>
    private void CaptureGeometryToModel(double? frameWidth = null, double? frameHeight = null)
    {
        // An F2 drag passes the frame it just set, which is the size even before Avalonia's
        // Width / Height catch up with the native resize.
        int w = (int)Math.Round(
            frameWidth ?? (double.IsFinite(Width) && Width > 1 ? Width : Bounds.Width));
        int h = (int)Math.Round(
            frameHeight ?? (double.IsFinite(Height) && Height > 1 ? Height : Bounds.Height));
        if (w < 1 || h < 1) return;
        _layout.X = Position.X;
        _layout.Y = Position.Y;
        if (_layout.Mode == LayoutMode.Vertical)
        {
            _layout.VerticalWidth = w;
            _layout.VerticalHeight = h;
        }
        else
        {
            _layout.HorizontalWidth = w;
            _layout.HorizontalHeight = h;
        }
    }

    private void RecalculateNaturalContentSize()
    {
        IReadOnlyList<TimerComponentMetric> metrics =
            ComponentMetrics();
        _naturalContentSize = TimerLayoutSnapshot.CalculateOverallSize(
            _layout.Mode,
            metrics);
    }

    private void RebuildLayoutSnapshot(bool preserveScale)
    {
        double previousOverall = _oldOverallSize;
        IReadOnlyList<TimerComponentMetric> metrics =
            ComponentMetrics();
        _naturalContentSize = TimerLayoutSnapshot.CalculateOverallSize(
            _layout.Mode,
            metrics);
        bool vertical = _layout.Mode == LayoutMode.Vertical;
        double windowWidth = double.IsFinite(Width) && Width > 1 ? Width : Bounds.Width;
        double windowHeight = double.IsFinite(Height) && Height > 1 ? Height : Bounds.Height;
        if (windowWidth < 1 || windowHeight < 1)
        {
            return;
        }

        if (preserveScale
            && previousOverall > 1
            && !_applyingGeometry
            && _interactionSession is null
            && _frameDrag is null
            && Math.Abs(previousOverall - _naturalContentSize) > 0.5)
        {
            _applyingGeometry = true;
            try
            {
                if (vertical)
                {
                    Height = Math.Min(
                        MaxHeight,
                        BorderlessWindowGeometry.FixSizePrimaryExtent(
                            previousOverall,
                            _naturalContentSize,
                            windowHeight));
                    windowHeight = Height;
                }
                else
                {
                    Width = Math.Min(
                        MaxWidth,
                        BorderlessWindowGeometry.FixSizePrimaryExtent(
                            previousOverall,
                            _naturalContentSize,
                            windowWidth));
                    windowWidth = Width;
                }

                GeometryTrail.Add($"fixsize {previousOverall:0.#}->{_naturalContentSize:0.#} size={windowWidth:0.#}x{windowHeight:0.#}");
            }
            finally
            {
                _applyingGeometry = false;
            }
        }

        _oldOverallSize = _naturalContentSize;
        double minPrimary = BorderlessWindowGeometry.WindowsMinExtent(_naturalContentSize);
        ApplySizeLimits(
            vertical ? BorderlessWindowGeometry.WindowsMinFloor : minPrimary,
            vertical ? minPrimary : BorderlessWindowGeometry.WindowsMinFloor);

        TimerLayoutSnapshot snapshot = TimerLayoutSnapshot.Create(
            _layout.Mode,
            windowWidth,
            windowHeight,
            metrics);
        if (!_timerSurfaceConfigured)
        {
            TimerSurface.Configure(_layout, _state, _appSettings, snapshot);
            _timerSurfaceConfigured = true;
        }
        else
        {
            TimerSurface.SetSnapshot(snapshot);
        }
    }

    /// <summary>
    /// TimerForm.MaintainMinimumSize for a window size: the size at which the components still
    /// get their minimum width (vertical; height in horizontal mode) at the scale that size gives
    /// them, and never below FixSize's MinimumSize. One rule for every way the size changes.
    /// </summary>
    private (double Width, double Height) MaintainedSize(double width, double height)
    {
        (double minimumWidth, double minimumHeight) = TimerSurface.MinimumContentSize();
        return WindowSizeRules.MaintainMinimumSize(
            _layout.Mode,
            width,
            height,
            _naturalContentSize,
            minimumWidth,
            minimumHeight);
    }

    /// <summary>
    /// MaintainMinimumSize outside a drag: after a layout change, a timer event or a resize from
    /// elsewhere. The window keeps its top-left corner, as WinForms does when Height is set.
    /// A drag applies the same rule to each frame it sets instead.
    /// </summary>
    private void MaintainMinimumSize()
    {
        if (_interactionSession is not null || _frameDrag is not null || !_timerSurfaceConfigured)
        {
            return;
        }

        double width = double.IsFinite(Width) && Width > 1 ? Width : Bounds.Width;
        double height = double.IsFinite(Height) && Height > 1 ? Height : Bounds.Height;
        if (width < 1 || height < 1)
        {
            return;
        }

        (double maintainedWidth, double maintainedHeight) = MaintainedSize(width, height);
        if (Math.Abs(maintainedWidth - width) < 0.5 && Math.Abs(maintainedHeight - height) < 0.5)
        {
            return;
        }

        maintainedWidth = Math.Min(maintainedWidth, MaxWidth);
        maintainedHeight = Math.Min(maintainedHeight, MaxHeight);
        GeometryTrail.Add($"maintain {width:0.#}x{height:0.#} -> {maintainedWidth:0.#}x{maintainedHeight:0.#}");
        _applyingGeometry = true;
        try
        {
            Width = maintainedWidth;
            Height = maintainedHeight;
        }
        finally
        {
            _applyingGeometry = false;
        }

        UpdateLayoutSnapshot();
        CaptureGeometryToModel();
    }

    /// <summary>
    /// Redraws the layout for the window's size, or for <paramref name="frameWidth"/> x
    /// <paramref name="frameHeight"/> when an F2 drag has just set a frame that Width / Height
    /// have not caught up with yet.
    /// </summary>
    private void UpdateLayoutSnapshot(double? frameWidth = null, double? frameHeight = null)
    {
        IReadOnlyList<TimerComponentMetric> metrics =
            ComponentMetrics();
        _naturalContentSize = TimerLayoutSnapshot.CalculateOverallSize(
            _layout.Mode,
            metrics);
        double windowWidth = frameWidth ?? (double.IsFinite(Width) && Width > 1 ? Width : Bounds.Width);
        double windowHeight = frameHeight ?? (double.IsFinite(Height) && Height > 1 ? Height : Bounds.Height);
        if (windowWidth < 1 || windowHeight < 1)
        {
            return;
        }

        TimerLayoutSnapshot snapshot = TimerLayoutSnapshot.Create(
            _layout.Mode,
            windowWidth,
            windowHeight,
            metrics);
        if (!_timerSurfaceConfigured)
        {
            TimerSurface.Configure(_layout, _state, _appSettings, snapshot);
            _timerSurfaceConfigured = true;
        }
        else
        {
            TimerSurface.SetSnapshot(snapshot);
        }
    }

    /// <summary>Windows AddSplitsFileToLRU: the file goes to the end of the recent splits with this run's names.</summary>
    private void AddSplitsFileToRecent(string path, Run run, TimingMethod lastTimingMethod)
    {
        _appSettings.AddToRecentSplits(path, run, lastTimingMethod, _appSettings.CurrentHotkeyProfile);
        PersistAppSettingsQuiet();
    }

    /// <summary>Windows AddCurrentSplitsToLRU: the open splits, if they came from a recent file, move to the end.</summary>
    private void AddCurrentSplitsToRecent(TimingMethod lastTimingMethod)
    {
        if (_splitsPath is { } path && _appSettings.FindRecentSplits(path) is not null)
        {
            AddSplitsFileToRecent(path, _state.Run, lastTimingMethod);
        }
    }

    /// <summary>Windows UpdateStateFromSplitsPath: a recent file reopens with the timing method it was last used with.</summary>
    private void UpdateStateFromSplitsPath(string path)
    {
        if (_appSettings.FindRecentSplits(path) is not { } recent)
        {
            return;
        }

        if (Enum.TryParse(recent.LastTimingMethod, out TimingMethod method))
        {
            _session.SetTimingMethod(method);
        }

        // TimerForm.UpdateStateFromSplitsPath: the splits' last hotkey profile, when it still exists.
        if (_appSettings.HotkeyProfiles.ContainsKey(recent.LastHotkeyProfile)
            && recent.LastHotkeyProfile != _appSettings.CurrentHotkeyProfile)
        {
            _appSettings.CurrentHotkeyProfile = recent.LastHotkeyProfile;
            RefreshHotkeyHook();
        }
    }

    private void RememberAndPersistLayout(string path)
    {
        _appSettings.AddToRecentLayouts(path);
        PersistAppSettingsQuiet();
    }

    /// <summary>The right-click menu as it would open now (tests).</summary>
    internal NativeMenuModel ContextMenuModelForTests() => BuildNativeContextMenuModel();

    /// <summary>Hide: the whole app out of sight (macOS), or the window minimized elsewhere.</summary>
    private void HideApp()
    {
        if (!MacOsAppActivation.Hide())
        {
            WindowState = WindowState.Minimized;
        }
    }

    internal RacetimeController Racetime => _racetime;

    /// <summary>TimerForm.RightClickMenu items (Designer order + RebuildControlMenu children).</summary>
    private NativeMenuModel BuildNativeContextMenuModel()
    {
        var items = new List<NativeMenuEntry>
        {
            NativeItem("Edit Splits...", async () => await EditSplitsAsync()),
            NativeSubmenu("Open Splits", BuildOpenSplitsMenuItems()),
            NativeItem(
                "Save Splits",
                async () => await SaveSplitsAsync(saveAs: false, promptPersonalBest: true),
                enabled: _splitsPath is not null || _state.Run.HasChanged || _state.Run.Count > 0),
            NativeItem("Save Splits As...", async () => await SaveSplitsAsync(saveAs: true, promptPersonalBest: true)),
            NativeItem("Close Splits", async () => await CloseSplitsAsync()),
            new NativeMenuSeparator(),
        };

        // Timer items come from TimerControlMenu so menu and hotkeys send the same session commands.
        var controlItems = new List<NativeMenuEntry>();
        foreach (ControlMenuItem item in TimerControlMenu.Build(_state))
        {
            TimerCommand command = item.Command;
            controlItems.Add(NativeItem(item.Label, () => ExecuteTimerCommand(command), enabled: item.IsEnabled));
        }

        items.Add(NativeSubmenu("Control", [
            .. controlItems,
            new NativeMenuSeparator(),
            NativeItem(
                "Global Hotkeys",
                () =>
                {
                    _appSettings.HotkeyProfile.GlobalHotkeysEnabled = !_appSettings.HotkeyProfile.GlobalHotkeysEnabled;
                    RefreshHotkeyHook();
                },
                @checked: _appSettings.HotkeyProfile.GlobalHotkeysEnabled),
            new NativeMenuSeparator(),
            ServerMenuItem(ServerStateType.TCP),
            ServerMenuItem(ServerStateType.Websocket),
            // RebuildControlMenu: each component's ContextMenuControls after a separator.
            .. _autoSplit.MenuSections().SelectMany(section => (IEnumerable<NativeMenuEntry>)[
                new NativeMenuSeparator(),
                .. section.Select(item => (NativeMenuEntry)NativeItem(item.Label, item.Action)),
            ]),
        ]));

        // Windows TimerForm.RebuildComparisonsMenu: custom comparisons, a separator when the run has
        // generators, the generated comparisons, then a separator before the timing methods.
        var comparisonItems = new List<NativeMenuEntry>();
        foreach (string comparison in _state.Run.CustomComparisons)
        {
            comparisonItems.Add(ComparisonMenuItem(comparison));
        }

        if (_state.Run.ComparisonGenerators.Count > 0)
        {
            comparisonItems.Add(new NativeMenuSeparator());
        }

        foreach (IComparisonGenerator generator in _state.Run.ComparisonGenerators)
        {
            comparisonItems.Add(ComparisonMenuItem(generator.Name));
        }

        comparisonItems.Add(new NativeMenuSeparator());
        comparisonItems.Add(NativeItem("Real Time", () =>
        {
            _session.SetTimingMethod(TimingMethod.RealTime);
        }, @checked: _state.CurrentTimingMethod == TimingMethod.RealTime));
        comparisonItems.Add(NativeItem("Game Time", () =>
        {
            _session.SetTimingMethod(TimingMethod.GameTime);
        }, @checked: _state.CurrentTimingMethod == TimingMethod.GameTime));
        items.Add(NativeSubmenu("Compare Against", comparisonItems));
        items.Add(new NativeMenuSeparator());
        items.Add(NativeItem("Share...", async () => await ShareAsync()));
        // UpdateRaceProviderIntegration: each enabled race provider's "<name> Races" after Share....
        if (_racetime.MenuItem() is { } racetime)
        {
            items.Add(racetime);
        }

        items.Add(new NativeMenuSeparator());
        items.Add(NativeItem("Edit Layout...", async () => await EditLayoutAsync()));
        items.Add(NativeSubmenu("Open Layout", BuildOpenLayoutMenuItems()));
        items.Add(NativeItem("Save Layout", async () => await SaveLayoutAsync(saveAs: false)));
        items.Add(NativeItem("Save Layout As...", async () => await SaveLayoutAsync(saveAs: true)));
        items.Add(new NativeMenuSeparator());
        items.Add(NativeItem("Settings", async () => await OpenSettingsAsync()));
        // InitializeLanguageMenu: Language before the separator above About.
        items.Add(LanguageMenu());
        items.Add(new NativeMenuSeparator());
        items.Add(NativeItem("About", async () => { await ShowAboutAsync(); }));
        items.Add(NativeItem("Check for Updates...", async () => await CheckForUpdatesAsync(userAsked: true)));
        // Basten 2 Nov: out of sight while it keeps timing and listening to the global hotkeys;
        // the Dock icon brings it back (macOS's Hide; elsewhere the window is minimized).
        items.Add(NativeItem("Hide", HideApp));
        items.Add(NativeItem("Exit", () =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(0);
            }
            else
            {
                Close();
            }
        }));
        return new NativeMenuModel { Items = items };
    }


    private void UpdateTimerVisuals()
    {
        TimerSurface.UpdateValues();
        ApplyMousePassThrough();
    }

    private void ApplyMousePassThrough()
    {
        bool enabled = _layout.Settings.MousePassThroughWhileRunning
            && _state.CurrentPhase == TimerPhase.Running;
        if (_mousePassThroughApplied == enabled)
        {
            return;
        }

        try
        {
            _windowFrame.SetMousePassThrough(this, enabled);
            _mousePassThroughApplied = enabled;
        }
        catch (Exception ex)
        {
            _mousePassThroughApplied = null;
            Console.Error.WriteLine("Mouse pass-through update failed: " + ex.Message);
        }
    }

    private static IBrush ToBrush(ArgbColor c) =>
        new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Never steal typing / Cmd+Z / Cmd+A from settings numerics or other text fields.
        if (IsTextEditingFocus())
        {
            return;
        }

        // Windows hook_KeyOrButtonPressed ignores every hotkey while "Update Times?" is up.
        if (_resetMessageShown)
        {
            return;
        }

        // While the global keyboard hook runs it sees these keys too: the timer's and the Counters'
        // keys are handled there (OnGlobalKey), once.
        bool hooked = GlobalHotkeysListening;

        // CounterComponent has its own hook: its keys count whether or not they are timer hotkeys too.
        if (!hooked && CounterLogic.HandleHotkey(_layout, FormatHotkeyChord(e)))
        {
            TimerSurface.UpdateValues();
            e.Handled = true;
        }

        if (hooked && !HotkeyCommands.Resolve(_appSettings.HotkeyProfile, FormatHotkeyChord(e)).IsEmpty)
        {
            e.Handled = true;
            return;
        }

        if (TryHandleBoundHotkey(e) || e.Handled)
        {
            return;
        }

        // Fallback demo keys only when the corresponding profile binding is empty.
        HotkeyProfileStub p = _appSettings.HotkeyProfile;
        TimerCommand? fallback =
            e.Key == Key.Space && string.IsNullOrEmpty(p.SplitKey) ? TimerCommand.StartOrSplit
            : e.Key == Key.R && string.IsNullOrEmpty(p.ResetKey) ? TimerCommand.Reset
            : e.Key == Key.P && string.IsNullOrEmpty(p.PauseKey) ? TimerCommand.Pause
            : e.Key == Key.U && string.IsNullOrEmpty(p.UndoKey) ? TimerCommand.UndoSplit
            : e.Key == Key.K && string.IsNullOrEmpty(p.SkipKey) ? TimerCommand.SkipSplit
            : null;
        if (fallback is { } command)
        {
            ExecuteTimerCommand(command);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Persisted hotkey profile bindings, resolved as Windows does (HotkeyCommands) and sent to the
    /// session; Split and Pause honour the Hotkey Delay.
    /// </summary>
    private bool TryHandleBoundHotkey(KeyEventArgs e)
    {
        HotkeyProfileStub p = _appSettings.HotkeyProfile;
        HotkeyResolution resolved = HotkeyCommands.Resolve(p, FormatHotkeyChord(e));
        if (resolved.IsEmpty)
        {
            return false;
        }

        if (resolved.Command is { } command)
        {
            ExecuteTimerCommand(command, TimeSpan.FromSeconds(Math.Max(0, p.HotkeyDelay)));
        }

        if (resolved.ToggleGlobalHotkeys)
        {
            p.GlobalHotkeysEnabled = !p.GlobalHotkeysEnabled;
            RefreshHotkeyHook();
        }

        e.Handled = true;
        return true;
    }

    /// <summary>
    /// True when keyboard focus is in an editable text surface (NumericUpDown field, TextBox, etc.).
    /// Timer hotkeys (U = Undo Split) must not fire while the user is editing a spinner.
    /// </summary>
    private bool IsTextEditingFocus()
    {
        object? focused = FocusManager?.GetFocusedElement();
        return focused is TextBox { IsReadOnly: false }
            || focused is NumericUpDown { IsReadOnly: false };
    }

    private NativeMenuEntry ComparisonMenuItem(string name) =>
        NativeItem(name, () => _session.SwitchComparison(name),
            @checked: string.Equals(_state.CurrentComparison, name, StringComparison.Ordinal));

    private static string FormatHotkeyChord(KeyEventArgs e) => HotkeyChord.Format(e.Key, e.KeyModifiers);

    private async Task EditSplitsAsync()
    {
        // Windows TimerForm.EditSplits: dialog always opens; AllowChangingSegments only when NotRunning.
        bool allowChangingSegments = _state.CurrentPhase == TimerPhase.NotRunning;
        TimerPhase phaseBefore = _state.CurrentPhase;
        int splitIndexBefore = _state.CurrentSplitIndex;

        var dialog = new SplitsEditorWindow(_state.Run, _state.CurrentTimingMethod, allowChangingSegments)
        {
            RecentLayouts = _appSettings.RecentLayouts.ToList(),
            ApplyLayoutPath = UseLayoutFromSplitsEditorAsync,
        };
        Run? result = await ShowOwnedDialogAsync<Run?>(dialog);
        if (result is null)
        {
            return;
        }

        // Keep the run going (Windows does not reset on OK). Segment count is unchanged
        // when AllowChangingSegments is false.
        int splitIndex = phaseBefore switch
        {
            TimerPhase.NotRunning => -1,
            TimerPhase.Ended => result.Count,
            _ => Math.Clamp(splitIndexBefore, 0, Math.Max(0, result.Count - 1)),
        };

        // Windows editor_RunEdited regenerates; SetRun also carries the generators to the edited copy.
        _session.SetRun(result, phaseBefore, splitIndex);
        // editor_RunEdited also leaves timer-only mode: edited splits get a full layout.
        await WarnAndRemoveTimerOnlyAsync(canCancel: false);
    }

    private async Task OpenSplitsAsync()
    {
        try
        {
            IReadOnlyList<IStorageFile> files = await _fileDialogs.OpenFilesAsync(this, new FilePickerOpenOptions
            {
                Title = "Open Splits",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("LiveSplit Splits") { Patterns = ["*.lss"] },
                ],
            });
            if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            {
                return;
            }

            await OpenRunFromFileAsync(path);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Open Splits failed", ex.Message);
        }
    }

    /// <summary>
    /// Windows TimerForm.OpenRunFromFile (Open Splits from a file or the recent list): offer to
    /// save changed splits, bring back the file's last timing method, remember the splits being
    /// left and then these, switch to the run and open the layout it asks for (Use Layout).
    /// </summary>
    internal async Task<bool> OpenRunFromFileAsync(string path)
    {
        if (!await WarnUserAboutSplitsSaveAsync() || !await WarnAndRemoveTimerOnlyAsync(canCancel: true))
        {
            return false;
        }

        TimingMethod previousTimingMethod = _state.CurrentTimingMethod;
        Run loaded;
        try
        {
            UpdateStateFromSplitsPath(path);
            loaded = XmlRunSerializer.LoadFile(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Open splits failed: " + ex);
            await ShowErrorAsync("Error", "The selected file was not recognized as a splits file.");
            return false;
        }

        AddCurrentSplitsToRecent(previousTimingMethod);
        AddSplitsFileToRecent(path, loaded, _state.CurrentTimingMethod);
        _session.SetRun(loaded, TimerPhase.NotRunning, -1);
        _splitsPath = path;
        Title = "LiveSplit — " + Path.GetFileName(path);
        await OpenRunLayoutAsync(loaded);
        return true;
    }

    /// <summary>
    /// The end of Windows TimerForm.SetRun: splits with Use Layout set open that layout
    /// ("?default" is the default layout), after the usual Save Layout? question.
    /// </summary>
    private async Task OpenRunLayoutAsync(Run run)
    {
        if (string.IsNullOrEmpty(run.LayoutPath))
        {
            return;
        }

        if (run.LayoutPath == "?default")
        {
            await LoadDefaultLayoutAsync();
        }
        else if (_layoutPath != run.LayoutPath && await WarnUserAboutLayoutSaveAsync(canCancel: true))
        {
            await OpenLayoutFromPathAsync(run.LayoutPath);
        }
    }

    /// <summary>
    /// The Splits Editor's Use Layout (RunEditorDialog.SaveAndApplyLayoutPath): the default layout
    /// or a layout file opens on the timer, after Save Layout?; true when it did.
    /// </summary>
    private async Task<bool> UseLayoutFromSplitsEditorAsync(string layoutPath)
    {
        if (layoutPath == "?default")
        {
            return await LoadDefaultLayoutAsync();
        }

        if (string.Equals(_layoutPath, layoutPath, StringComparison.Ordinal))
        {
            return true;
        }

        return await WarnUserAboutLayoutSaveAsync(canCancel: true) && await OpenLayoutFromPathAsync(layoutPath);
    }

    /// <summary>
    /// Windows TimerForm.LoadDefaultLayout: after Save Layout?, the standard layout at the window's
    /// current position; the recent layouts end with "" so the next start uses it too.
    /// </summary>
    private async Task<bool> LoadDefaultLayoutAsync()
    {
        if (!await WarnUserAboutLayoutSaveAsync(canCancel: true))
        {
            return false;
        }

        Layout layout = StandardLayoutFactory.Create();
        layout.X = Position.X;
        layout.Y = Position.Y;
        SetLayout(layout, null);
        _appSettings.AddToRecentLayouts("");
        PersistAppSettingsQuiet();
        return true;
    }

    /// <summary>
    /// Windows TimerForm.SaveSplits / SaveSplitsAs. Saving while the attempt is still going (or
    /// ended slower than the PB) asks "Save as Personal Best?" when <paramref name="promptPersonalBest"/>:
    /// Yes ends the attempt as the new PB, No saves the attempt as a reset would record it while the
    /// timer keeps running, Cancel saves nothing. Returns false when nothing was saved.
    /// </summary>
    internal async Task<bool> SaveSplitsAsync(bool saveAs, bool promptPersonalBest)
    {
        string? path = _splitsPath;
        if (saveAs || path is null)
        {
            IStorageFile? file;
            try
            {
                file = await _fileDialogs.SaveFileAsync(this, new FilePickerSaveOptions
                {
                    Title = "Save Splits As",
                    DefaultExtension = "lss",
                    // Windows SaveSplitsAs: Run.GetExtendedFileName() ("Game - Category (variables)").
                    SuggestedFileName = string.IsNullOrWhiteSpace(_state.Run.GetExtendedFileName())
                        ? "splits.lss"
                        : _state.Run.GetExtendedFileName() + ".lss",
                    FileTypeChoices =
                    [
                        new FilePickerFileType("LiveSplit Splits") { Patterns = ["*.lss"] },
                    ],
                });
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Save Failed", ex.Message);
                return false;
            }

            path = file?.TryGetLocalPath();
            if (path is null)
            {
                return false;
            }

            if (!path.EndsWith(".lss", StringComparison.Ordinal))
            {
                await ShowErrorAsync("Save Failed", "Cannot save splits with a file type that is not .lss");
                return false;
            }

            // Windows sets Run.FilePath here, before the save itself.
            _splitsPath = path;
        }

        _state.Run.FixSplits();

        QuestionAnswer answer = QuestionAnswer.No;
        if (promptPersonalBest && SplitsPrompts.ShouldOfferSaveAsPersonalBest(_state))
        {
            answer = await ShowQuestionAsync(
                "Save as Personal Best?",
                "This run did not beat your current splits. Would you like to save this run as a Personal Best?",
                canCancel: true);
            if (answer == QuestionAnswer.Yes)
            {
                _session.ResetAndSetAttemptAsPersonalBest();
            }
            else if (answer == QuestionAnswer.Cancel)
            {
                return false;
            }
        }

        Run toSave = answer == QuestionAnswer.No ? SplitsPrompts.ResetCopyForSaving(_state) : _state.Run;
        try
        {
            XmlRunSerializer.SaveFile(toSave, path);
            _state.Run.HasChanged = false;
            AddSplitsFileToRecent(path, toSave, _state.CurrentTimingMethod);
            Title = "LiveSplit — " + Path.GetFileName(path);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Save splits failed: " + ex);
            await ShowErrorAsync("Save Failed", "Splits could not be saved!");
            return false;
        }
    }

    /// <summary>
    /// Windows TimerForm.CloseSplits: changed splits (and a layout about to be replaced) are
    /// offered for saving, then the timer goes to timer-only mode with a blank run and a
    /// Timer-only layout that keeps the layout settings, position and orientation.
    /// </summary>
    internal async Task CloseSplitsAsync()
    {
        bool needToChangeLayout = !StandardLayoutFactory.IsTimerOnly(_layout);
        if (!await WarnUserAboutSplitsSaveAsync())
        {
            return;
        }

        if (needToChangeLayout && !await WarnUserAboutLayoutSaveAsync(canCancel: true))
        {
            return;
        }

        AddCurrentSplitsToRecent(_state.CurrentTimingMethod);
        _session.SetRun(Run.CreateStandard(), TimerPhase.NotRunning, -1);
        _appSettings.AddToRecentSplits("", null, TimingMethod.RealTime, _appSettings.CurrentHotkeyProfile);
        _splitsPath = null;
        Title = "LiveSplit";
        _inTimerOnlyMode = true;
        if (needToChangeLayout)
        {
            Layout timerOnly = StandardLayoutFactory.CreateTimerOnly();
            timerOnly.Settings = _layout.Settings;
            timerOnly.X = Position.X;
            timerOnly.Y = Position.Y;
            timerOnly.Mode = _layout.Mode;
            SetLayout(timerOnly, null);
            _appSettings.AddToRecentLayouts("");
        }

        PersistAppSettingsQuiet();
    }

    private async Task EditLayoutAsync()
    {
        var session = SettingsEditSession.ForLayout(_layout, () =>
        {
            RebuildLayoutVisibility();
            ApplyLayoutWindowChrome();
            SyncSplitsColumnsFromRun();
            ApplyLayoutChange();
        });
        var dialog = new LayoutEditorWindow(_layout)
        {
            LivePreview = working => session.PreviewLayout(working),
            // editor_LayoutResized: the other orientation's size no longer fits the components.
            LayoutResized = () => WindowSizeRules.InvalidateOtherOrientation(_layout),
        };
        Layout? result = await ShowOwnedDialogAsync<Layout?>(dialog);
        if (result is null)
        {
            session.RollbackIfUncommitted();
            // The auto splitters take back the settings the layout had.
            _asr.LayoutChanged();
            return;
        }

        session.PreviewLayout(result);
        session.Commit();
        _theRun.LayoutChanged();
        _autoSplit.LayoutChanged();
        _asr.LayoutChanged();
        RefreshHotkeyHook();
        // Windows editor_LayoutResized: a layout that is no longer a single Timer ends timer-only mode.
        SetInTimerOnlyMode();
    }

    private async Task OpenSettingsAsync()
    {
        var session = SettingsEditSession.ForAppSettings(_appSettings, refresh: () => { });
        bool simpleSumOfBestBefore = _appSettings.SimpleSumOfBest;
        var dialog = new SettingsWindow(_layout.Settings, _state.CurrentTimingMethod, _appSettings, _settingsPath)
        {
            LiveAppearancePreview = mode => session.PreviewAppearance(mode),
            // Windows editor_SumOfBestModeChanged: regenerate while the dialog is open.
            LiveSimpleSumOfBestPreview = simple =>
            {
                _appSettings.SimpleSumOfBest = simple;
                _session.RegenerateComparisons();
            },
        };
        (LayoutSettings Settings, TimingMethod Method, HotkeyProfileStub Hotkeys)? result =
            await ShowOwnedDialogAsync<(LayoutSettings, TimingMethod, HotkeyProfileStub)?>(dialog);
        if (result is null)
        {
            // Windows Cancel: restore the old settings and regenerate if Simple Sum of Best was toggled.
            bool regenerate = _appSettings.SimpleSumOfBest != simpleSumOfBestBefore;
            session.RollbackIfUncommitted();
            if (regenerate)
            {
                _session.RegenerateComparisons();
            }

            return;
        }

        // SettingsWindow OnOk already wrote general/hotkey/appearance into _appSettings.
        _layout.Settings.Assign(result.Value.Settings);
        _session.SetTimingMethod(result.Value.Method);
        session.Commit();
        // Windows Settings OK: SwitchComparisonGenerators (Choose Active Comparisons + regenerate).
        _session.SwitchComparisonGenerators(_appSettings.ComparisonGeneratorStates);
        AppThemeController.Apply(_appSettings.Appearance);
        RefreshHotkeyHook();
        _uiTimer.Interval = AppSettings.RefreshInterval(_appSettings.RefreshRate);
        // UpdateRaceProviderIntegration: a racing service enabled in Manage Racing Services reads its races.
        _racetime.Refresh();
        ApplyLayoutWindowChrome();
        ApplyLayoutChange();
        PersistAppSettingsQuiet();
    }

    /// <summary>Root of the shared immediate-owner dialog chain.</summary>
    private async Task<T> ShowOwnedDialogAsync<T>(Window dialog)
    {
        _ownedDialogDepth++;
        try
        {
            return await OwnedDialog.ShowAsync<T>(this, dialog);
        }
        finally
        {
            _ownedDialogDepth = Math.Max(0, _ownedDialogDepth - 1);
            RestoreTimerInteractionAfterDialog();
        }
    }

    /// <summary>About (the timer's menu, and the app menu's About LiveSplit).</summary>
    internal Task ShowAboutAsync() => ShowOwnedDialogAsync(new AboutWindow());

    private async Task ShowOwnedDialogAsync(Window dialog)
    {
        _ownedDialogDepth++;
        try
        {
            await OwnedDialog.ShowAsync(this, dialog);
        }
        finally
        {
            _ownedDialogDepth = Math.Max(0, _ownedDialogDepth - 1);
            RestoreTimerInteractionAfterDialog();
        }
    }

    private void RestoreTimerInteractionAfterDialog()
    {
        ClearGeometryInteraction();
        CanResize = _layout.Settings.AllowResizing;
        Topmost = _layout.Settings.AlwaysOnTop;
        _mousePassThroughApplied = null;
        ApplyMousePassThrough();
        if (_ownedDialogDepth == 0)
        {
            // Only a real AppKit window needs the app activated (headless tests and proofs do not,
            // and activating would give the test host a Dock icon).
            if (TryGetPlatformHandle()?.HandleDescriptor?.Contains("NSWindow", StringComparison.OrdinalIgnoreCase) == true)
            {
                MacOsAppActivation.ActivateIgnoringOtherApps();
            }

            Activate();
        }
    }

    /// <summary>Windows TimerForm.UpdateRecentSplits: recent splits by game and category, then From File.</summary>
    private List<NativeMenuEntry> BuildOpenSplitsMenuItems()
    {
        var items = new List<NativeMenuEntry>();
        foreach (RecentSplitsMenuEntry entry in RecentSplitsMenu.Build(_appSettings.RecentSplits))
        {
            items.Add(ToNativeMenuEntry(entry));
        }

        if (items.Count > 0)
        {
            items.Add(new NativeMenuSeparator());
        }

        items.Add(NativeItem("From File...", async () => await OpenSplitsAsync()));
        items.Add(NativeItem("From URL...", async () => await OpenSplitsFromUrlAsync()));
        items.Add(new NativeMenuSeparator());
        items.Add(NativeItem("Edit History", async () => await EditSplitsHistoryAsync()));
        return items;
    }

    private NativeMenuEntry ToNativeMenuEntry(RecentSplitsMenuEntry entry)
    {
        if (entry.Children.Count > 0)
        {
            return NativeSubmenu(entry.Text, entry.Children.Select(ToNativeMenuEntry).ToList());
        }

        string path = entry.Path!;
        return NativeItem(entry.Text, async () => await OpenRunFromFileAsync(path));
    }

    /// <summary>Windows TimerForm.UpdateRecentLayouts: recent paths first, then From File / Default.</summary>
    private List<NativeMenuEntry> BuildOpenLayoutMenuItems()
    {
        var items = new List<NativeMenuEntry>();
        foreach (string path in _appSettings.RecentLayouts
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Reverse())
        {
            string layoutPath = path;
            string header = Path.GetFileNameWithoutExtension(layoutPath);
            if (string.IsNullOrWhiteSpace(header))
            {
                header = layoutPath;
            }

            items.Add(NativeItem(header, async () =>
            {
                if (await WarnUserAboutLayoutSaveAsync(canCancel: true))
                {
                    await OpenLayoutFromPathAsync(layoutPath);
                }
            }));
        }

        if (items.Count > 0)
        {
            items.Add(new NativeMenuSeparator());
        }

        items.Add(NativeItem("From File...", async () => await OpenLayoutAsync()));
        items.Add(NativeItem("From URL...", async () => await OpenLayoutFromUrlAsync()));
        items.Add(NativeItem("Default", async () => await LoadDefaultLayoutAsync()));
        items.Add(new NativeMenuSeparator());
        items.Add(NativeItem("Edit History", async () => await EditLayoutsHistoryAsync()));
        return items;
    }

    /// <summary>Downloads a file for From URL... (tests put their own here).</summary>
    internal static Func<Uri, CancellationToken, Task<byte[]>> DownloadFromUrl { get; set; } = DefaultDownloadAsync;

    private static readonly HttpClient UrlClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static Task<byte[]> DefaultDownloadAsync(Uri uri, CancellationToken token) => UrlClient.GetByteArrayAsync(uri, token);

    /// <summary>
    /// TimerForm.openSplitsFromURLMenuItem_Click (URLRunImporter): the splits at a URL, as an
    /// unsaved run (Save asks where), after Save Splits? and leaving timer-only mode.
    /// </summary>
    internal async Task<bool> OpenSplitsFromUrlAsync()
    {
        string? url = await DialogChrome.PromptAsync(this, "Open Splits from URL", "URL:", "", 360);
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        byte[] data;
        try
        {
            data = await DownloadFromUrl(new Uri(url.Trim()), CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Open splits from URL failed: " + ex.Message);
            await ShowErrorAsync("Error", UiLocalizer.T("The splits file couldn't be downloaded."));
            return false;
        }

        Run run;
        try
        {
            run = XmlRunSerializer.Load(new MemoryStream(data));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Open splits from URL: not a splits file: " + ex.Message);
            await ShowErrorAsync("Error", UiLocalizer.T("The selected file was not recognized as a splits file."));
            return false;
        }

        if (!await WarnUserAboutSplitsSaveAsync() || !await WarnAndRemoveTimerOnlyAsync(canCancel: true))
        {
            return false;
        }

        AddCurrentSplitsToRecent(_state.CurrentTimingMethod);
        run.HasChanged = true;
        _session.SetRun(run, TimerPhase.NotRunning, -1);
        _splitsPath = null;
        Title = "LiveSplit";
        await OpenRunLayoutAsync(run);
        return true;
    }

    /// <summary>
    /// TimerForm.openLayoutFromURLMenuItem_Click: the layout at a URL (a ge.tt share link as its
    /// download), as an unsaved layout. Unlike Windows, Save Layout? is asked first, as for a file.
    /// </summary>
    internal async Task<bool> OpenLayoutFromUrlAsync()
    {
        string? url = await DialogChrome.PromptAsync(this, "Open Layout from URL", "URL:", "", 360);
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        byte[] data;
        try
        {
            var uri = new Uri(url.Trim());
            if (uri.Host.Equals("ge.tt", StringComparison.OrdinalIgnoreCase)
                && uri.LocalPath.Length > 0
                && !uri.LocalPath[1..].Contains('/'))
            {
                uri = new Uri(string.Format(System.Globalization.CultureInfo.InvariantCulture, "http://ge.tt/api/1/files{0}/0/blob?download", uri.LocalPath));
            }

            data = await DownloadFromUrl(uri, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Open layout from URL failed: " + ex.Message);
            await ShowErrorAsync("Error", UiLocalizer.T("The layout file couldn't be downloaded."));
            return false;
        }

        Layout layout;
        try
        {
            layout = XmlLayoutSerializer.Load(new MemoryStream(data));
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Error", UiLocalizer.T("The selected file was not recognized as a layout file. (") + ex.Message + ")");
            return false;
        }

        if (!await WarnUserAboutLayoutSaveAsync(canCancel: true))
        {
            return false;
        }

        layout.HasChanged = true;
        SetLayout(layout, null);
        return true;
    }

    /// <summary>TimerForm.editSplitHistoryMenuItem_Click: the recent splits left after Edit History.</summary>
    internal async Task EditSplitsHistoryAsync()
    {
        var dialog = new EditHistoryWindow(_appSettings.RecentSplits.Select(x => x.Path));
        UiLocalizer.Apply(dialog);
        if (await ShowOwnedDialogAsync<bool>(dialog))
        {
            HashSet<string> kept = [.. dialog.History];
            _appSettings.RecentSplits.RemoveAll(x => !kept.Contains(x.Path));
            PersistAppSettingsQuiet();
        }
    }

    /// <summary>
    /// TimerForm.shareMenuItem_Click: the Run Sharer on a copy of the timer's state, so sharing
    /// never changes the run or the attempt that is going.
    /// </summary>
    internal async Task ShareAsync(Action<ShareRunWindow>? prepareForTests = null)
    {
        var dialog = new ShareRunWindow(_state.Clone(), ScreenshotPngAsync, _fileDialogs);
        UiLocalizer.Apply(dialog);
        prepareForTests?.Invoke(dialog);
        await ShowOwnedDialogAsync(dialog);
    }

    /// <summary>
    /// TimerForm.MakeScreenShot: the timer window as drawn, in the screen's pixels (the Run
    /// Sharer puts it on black, as upstream does, when saving).
    /// </summary>
    internal Task<byte[]?> ScreenshotPngAsync()
    {
        try
        {
            double scale = RenderScaling;
            var size = new PixelSize(
                Math.Max(1, (int)Math.Ceiling(Bounds.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(Bounds.Height * scale)));
            using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
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

    /// <summary>TimerForm.editLayoutHistoryMenuItem_Click: the recent layouts after Edit History.</summary>
    internal async Task EditLayoutsHistoryAsync()
    {
        var dialog = new EditHistoryWindow(_appSettings.RecentLayouts);
        UiLocalizer.Apply(dialog);
        if (await ShowOwnedDialogAsync<bool>(dialog))
        {
            List<string> kept = [.. dialog.History];
            _appSettings.RecentLayouts.Clear();
            _appSettings.RecentLayouts.AddRange(kept);
            PersistAppSettingsQuiet();
        }
    }

    private async Task OpenLayoutAsync()
    {
        try
        {
            // Windows OpenLayout: offer to save the current layout before choosing another.
            if (!await WarnUserAboutLayoutSaveAsync(canCancel: true))
            {
                return;
            }

            IReadOnlyList<IStorageFile> files = await _fileDialogs.OpenFilesAsync(this, new FilePickerOpenOptions
            {
                Title = "Open Layout",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("LiveSplit Layout") { Patterns = ["*.lsl"] },
                ],
            });
            if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
            {
                return;
            }

            await OpenLayoutFromPathAsync(path);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Open Layout failed", ex.Message);
        }
    }

    internal Task<bool> OpenLayoutFromPathForTestsAsync(string path) => OpenLayoutFromPathAsync(path);

    private async Task<bool> OpenLayoutFromPathAsync(string path)
    {
        try
        {
            Layout layout = XmlLayoutSerializer.LoadFile(path);
            RememberAndPersistLayout(path);
            SetLayout(layout, path);
            return true;
        }
        catch (Exception ex)
        {
            // TimerForm.OpenLayoutFromFile: a missing or broken file is the same error, and its
            // entry stays in the recent list (Edit History removes it).
            await ShowErrorAsync("Error", UiLocalizer.T("The selected file was not recognized as a layout file. (") + ex.Message + ")");
            return false;
        }
    }

    /// <summary>Saves the layout (Save As when it has no file yet); false when cancelled or failed.</summary>
    private async Task<bool> SaveLayoutAsync(bool saveAs)
    {
        try
        {
            string? path = _layoutPath;
            if (saveAs || path is null)
            {
                IStorageFile? file = await _fileDialogs.SaveFileAsync(this, new FilePickerSaveOptions
                {
                    Title = "Save Layout As",
                    DefaultExtension = "lsl",
                    SuggestedFileName = "layout.lsl",
                    FileTypeChoices =
                    [
                        new FilePickerFileType("LiveSplit Layout") { Patterns = ["*.lsl"] },
                    ],
                });
                path = file?.TryGetLocalPath();
                if (path is null)
                {
                    return false;
                }
            }

            CaptureGeometryToModel();
            // ASRComponent.GetSettings: the auto splitters' settings as they are now.
            _asr.SyncSettings();
            XmlLayoutSerializer.SaveFile(_layout, path);
            _layout.HasChanged = false;
            _layoutPath = path;
            RememberAndPersistLayout(path);
            return true;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Save Layout failed", ex.Message);
            return false;
        }
    }

    private enum QuestionAnswer
    {
        Yes,
        No,
        Cancel,
    }

    /// <summary>A Yes / No (/ Cancel) question in the dialogs' style; closing it counts as Cancel (No without Cancel).</summary>
    private async Task<QuestionAnswer> ShowQuestionAsync(string title, string message, bool canCancel)
    {
        QuestionAnswer answer = canCancel ? QuestionAnswer.Cancel : QuestionAnswer.No;
        var yes = DialogChrome.Style(new Button { Content = "Yes", IsDefault = true });
        var no = DialogChrome.Style(new Button { Content = "No" });
        Button? cancel = canCancel
            ? DialogChrome.Style(new Button { Content = "Cancel", IsCancel = true })
            : null;
        var dialog = cancel is null
            ? DialogChrome.Message(title, message, 420, no, yes)
            : DialogChrome.Message(title, message, 420, cancel, no, yes);
        yes.Click += (_, _) => { answer = QuestionAnswer.Yes; dialog.Close(); };
        no.Click += (_, _) => { answer = QuestionAnswer.No; dialog.Close(); };
        if (cancel is not null)
        {
            cancel.Click += (_, _) => { answer = QuestionAnswer.Cancel; dialog.Close(); };
        }

        await ShowOwnedDialogAsync(dialog);
        return answer;
    }

    /// <summary>
    /// TimerForm.InitializeLanguageMenu: Follow System, then every language by its own name, the
    /// one in settings.cfg checked. Its texts come by key in the language the UI shows now.
    /// </summary>
    private NativeMenuAction LanguageMenu()
    {
        bool isAuto = LanguageResolver.IsAuto(_appSettings.UILanguage);
        AppLanguage configured = LanguageResolver.Resolve(_appSettings.UILanguage);
        var items = new List<NativeMenuEntry>
        {
            NativeItem(
                UiLocalizer.TranslateKey(LocalizationKeys.LanguageFollowSystem, "Follow System"),
                async () => await SwitchUILanguageAsync(AppLanguage.Auto),
                enabled: true,
                @checked: isAuto),
            new NativeMenuSeparator(),
        };
        foreach (AppLanguage language in UiTextCatalog.Languages)
        {
            items.Add(NativeItem(
                language.DisplayName,
                async () => await SwitchUILanguageAsync(language),
                enabled: true,
                @checked: !isAuto && language.Equals(configured)));
        }

        return NativeSubmenu(UiLocalizer.TranslateKey(LocalizationKeys.LanguageMenu, "Language"), items);
    }

    /// <summary>TimerForm.SwitchUILanguage: saved at once, shown after a restart.</summary>
    private async Task SwitchUILanguageAsync(AppLanguage language)
    {
        _appSettings.UILanguage = LanguageResolver.ToSettingValue(language);
        PersistAppSettingsQuiet();
        await ShowErrorAsync(
            UiLocalizer.TranslateKey(LocalizationKeys.LanguageMenu, "Language"),
            UiLocalizer.TranslateKey(LocalizationKeys.LanguageRestartRequired, "Language change will take effect after restarting LiveSplit."));
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var ok = new Button { Content = "OK", IsDefault = true };
        var dialog = DialogChrome.Message(title, message, 420, ok);
        ok.Click += (_, _) => dialog.Close();
        await ShowOwnedDialogAsync(dialog);
    }
}
