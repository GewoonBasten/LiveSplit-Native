using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// The Auto Splitting Runtime component (LiveSplit.AutoSplittingRuntime ASRComponent): each
/// component's WebAssembly auto splitter is loaded when its Script Path is set, runs at its tick
/// rate on the timer's thread (ASRComponent.UpdateTimerElapsed invokes onto the form), and drives
/// the timer through the session. Its settings map is kept in the layout as &lt;CustomSettings&gt;.
/// </summary>
internal sealed class AutoSplittingRuntimeController : IAsrTimer
{
    /// <summary>The default tick rate until an auto splitter sets its own (120 a second).</summary>
    private static readonly TimeSpan DefaultTick = TimeSpan.FromSeconds(1.0 / 120);

    private readonly TimerSession _session;
    private readonly Func<Layout> _layout;
    private readonly HashSet<AsrComponentRuntime> _tracked = [];
    private readonly DispatcherTimer _timer;

    public AutoSplittingRuntimeController(TimerSession session, Func<Layout> layout)
    {
        _session = session;
        _layout = layout;
        Current = this;
        _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = DefaultTick };
        _timer.Tick += (_, _) => StepAll();
    }

    /// <summary>The main window's controller (the settings page loads auto splitters through it).</summary>
    internal static AutoSplittingRuntimeController? Current { get; private set; }

    /// <summary>Recent messages from the auto splitters and the runtime (Log.Info upstream).</summary>
    internal List<string> LogLines { get; } = [];

    /// <summary>The component's runtime holder (shared by clones of the component).</summary>
    internal static AsrComponentRuntime For(LayoutComponent component)
    {
        if (component.RuntimeState is not AsrComponentRuntime runtime)
        {
            runtime = new AsrComponentRuntime();
            component.RuntimeState = runtime;
        }

        return runtime;
    }

    /// <summary>
    /// SetSettings for every Auto Splitting Runtime component: a new Script Path loads its auto
    /// splitter with the saved settings; the same one takes the saved settings when they differ
    /// (a cancelled settings dialog). Components that left the layout drop theirs.
    /// </summary>
    public void LayoutChanged()
    {
        var current = new HashSet<AsrComponentRuntime>();
        foreach (LayoutComponent component in _layout().Components.Where(c => c.Path == StandardLayoutFactory.AutoSplittingRuntimePath))
        {
            AsrComponentRuntime runtime = For(component);
            current.Add(runtime);
            _tracked.Add(runtime);
            Apply(runtime, AutoSplittingRuntimeSettings.FromInnerXml(component.SettingsInnerXml));
        }

        foreach (AsrComponentRuntime gone in _tracked.Where(t => !current.Contains(t)).ToList())
        {
            _tracked.Remove(gone);
            gone.Unload();
        }

        UpdateTimer();
    }

    /// <summary>The settings page's Script Path / settings changes, applied at once.</summary>
    internal void Apply(AsrComponentRuntime runtime, AutoSplittingRuntimeSettings settings)
    {
        _tracked.Add(runtime);
        if (runtime.ScriptPath != settings.ScriptPath)
        {
            // ScriptPath setter: ReloadRuntime.
            runtime.Load(settings.ScriptPath, settings.CustomSettings, this);
        }
        else if (runtime.Runtime is { } loaded && settings.CustomSettings is { } saved
            && Describe(saved) != Describe(loaded.GetSettings()))
        {
            loaded.SetSettings(saved);
            runtime.SettingsVersion++;
        }

        UpdateTimer();
    }

    /// <summary>GetSettings: each component's settings map as it is now goes into its layout XML.</summary>
    public void SyncSettings()
    {
        foreach (LayoutComponent component in _layout().Components.Where(c => c.Path == StandardLayoutFactory.AutoSplittingRuntimePath))
        {
            if (For(component).Runtime is not { } runtime)
            {
                continue;
            }

            AutoSplittingRuntimeSettings settings = AutoSplittingRuntimeSettings.FromInnerXml(component.SettingsInnerXml);
            settings.CustomSettings = runtime.GetSettings();
            component.SettingsInnerXml = settings.ToInnerXml();
        }
    }

    /// <summary>LiveSplit closing: every auto splitter is dropped (Dispose).</summary>
    public void DisposeAll()
    {
        _timer.Stop();
        foreach (AsrComponentRuntime runtime in _tracked)
        {
            runtime.Unload();
        }

        _tracked.Clear();
    }

    internal static string Describe(IReadOnlyList<KeyValuePair<string, AsrSettingValue>> map) =>
        AsrSettingValue.FromMap(map).ToString();

    /// <summary>UpdateTimerElapsed: every loaded auto splitter runs once; the interval follows the tick rate.</summary>
    internal void StepAll()
    {
        foreach (AsrComponentRuntime runtime in _tracked.ToList())
        {
            if (runtime.Runtime is { } loaded)
            {
                loaded.Step();
            }
        }

        UpdateTimer();
    }

    private void UpdateTimer()
    {
        List<AsrRuntime> loaded = [.. _tracked.Select(t => t.Runtime).OfType<AsrRuntime>()];
        if (loaded.Count == 0)
        {
            _timer.Stop();
            return;
        }

        TimeSpan tick = loaded.Select(r => r.TickRate).Where(t => t > TimeSpan.Zero).DefaultIfEmpty(DefaultTick).Min();
        if (_timer.Interval != tick)
        {
            _timer.Interval = tick;
        }

        if (!_timer.IsEnabled && !Avalonia.Controls.Design.IsDesignMode)
        {
            _timer.Start();
        }
    }

    // IAsrTimer: the callbacks of ComponentSettings, on the session.
    int IAsrTimer.State => _session.State.CurrentPhase switch
    {
        TimerPhase.Running => 1,
        TimerPhase.Paused => 2,
        TimerPhase.Ended => 3,
        _ => 0,
    };

    public int CurrentSplitIndex => _session.State.CurrentSplitIndex;

    public int SegmentSplitted(int index) =>
        index >= 0 && index < _session.State.CurrentSplitIndex
            ? (_session.State.Run[index].SplitTime.RealTime is not null ? 1 : 0)
            : -1;

    public void Start() => _session.Update((m, _) => m.Start(), SessionChange.Timer | SessionChange.Comparison);

    public void Split() => _session.Update((m, _) => m.Split(), SessionChange.Timer | SessionChange.Comparison);

    public void SkipSplit() => _session.Update((m, _) => m.SkipSplit(), SessionChange.Timer);

    public void UndoSplit() => _session.Update((m, _) => m.UndoSplit(), SessionChange.Timer);

    public void Reset() => _session.Update((m, _) => m.Reset(), SessionChange.Timer | SessionChange.Comparison);

    public void SetGameTime(TimeSpan time) => _session.Update((_, s) => s.SetGameTime(time), SessionChange.Timer);

    public void PauseGameTime() => _session.Update((_, s) => s.IsGameTimePaused = true, SessionChange.Timer);

    public void ResumeGameTime() => _session.Update((_, s) => s.IsGameTimePaused = false, SessionChange.Timer);

    public void SetCustomVariable(string name, string value) => _session.Update((_, s) =>
    {
        if (s.Run.Metadata.SetCustomVariable(name, value))
        {
            s.Run.HasChanged = true;
        }
    }, SessionChange.None);

    public void Log(string message)
    {
        Console.WriteLine("[Auto Splitting Runtime] " + message);
        LogLines.Add(message);
        if (LogLines.Count > 200)
        {
            LogLines.RemoveAt(0);
        }
    }
}

/// <summary>A component's loaded auto splitter (ComponentSettings.runtime) and the path it came from.</summary>
internal sealed class AsrComponentRuntime
{
    public string ScriptPath { get; private set; } = "";

    public AsrRuntime? Runtime { get; private set; }

    /// <summary>Counts settings changes made from outside the settings page (it rebuilds then).</summary>
    public int SettingsVersion { get; set; }

    /// <summary>ReloadRuntime: the old auto splitter goes, the new path's is loaded (none for an empty path).</summary>
    public void Load(string path, IReadOnlyList<KeyValuePair<string, AsrSettingValue>>? settings, IAsrTimer timer)
    {
        Unload();
        ScriptPath = path;
        if (!string.IsNullOrEmpty(path))
        {
            try
            {
                Runtime = AsrRuntime.Load(path, settings, timer);
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                timer.Log("The Auto Splitting Runtime library could not be used: " + e.Message);
                Runtime = null;
            }
        }

        SettingsVersion++;
    }

    public void Unload()
    {
        Runtime?.Dispose();
        Runtime = null;
        ScriptPath = "";
    }
}
