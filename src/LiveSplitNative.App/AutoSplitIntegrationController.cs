using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// AutoSplit Integration (Basten's Pilgrim build of LiveSplit.AutoSplitIntegration): each
/// component's autosplitter starts when its layout is opened, its commands drive the timer, and
/// the timer's start, split, reset, skip and undo are sent back to it. A component leaving the
/// layout, and every one when LiveSplit closes, asks its autosplitter to quit.
/// </summary>
internal sealed class AutoSplitIntegrationController
{
    private readonly TimerSession _session;
    private readonly Func<Layout> _layout;
    private readonly HashSet<AutoSplitIntegrationRuntime> _tracked = [];

    public AutoSplitIntegrationController(TimerSession session, Func<Layout> layout)
    {
        _session = session;
        _layout = layout;
        Current = this;
    }

    /// <summary>The main window's controller (the settings page starts autosplitters through it).</summary>
    internal static AutoSplitIntegrationController? Current { get; private set; }

    private LiveSplitState State => _session.State;

    /// <summary>The component's autosplitter, made (not started) when it has none yet.</summary>
    internal static AutoSplitIntegrationRuntime RuntimeFor(LayoutComponent component)
    {
        if (component.RuntimeState is not AutoSplitIntegrationRuntime runtime)
        {
            runtime = new AutoSplitIntegrationRuntime(action => Dispatcher.UIThread.Post(action));
            component.RuntimeState = runtime;
        }

        if (runtime.CommandReceived is null && Current is { } controller)
        {
            runtime.CommandReceived = line => controller.Handle(runtime, line);
        }

        return runtime;
    }

    private List<(AutoSplitIntegrationRuntime Runtime, AutoSplitIntegrationSettings Settings)> Runtimes()
    {
        var runtimes = new List<(AutoSplitIntegrationRuntime, AutoSplitIntegrationSettings)>();
        foreach (LayoutComponent component in _layout().Components)
        {
            if (component.Path == StandardLayoutFactory.AutoSplitIntegrationPath)
            {
                AutoSplitIntegrationRuntime runtime = RuntimeFor(component);
                _tracked.Add(runtime);
                runtimes.Add((runtime, AutoSplitIntegrationSettings.FromInnerXml(component.SettingsInnerXml)));
            }
        }

        return runtimes;
    }

    /// <summary>
    /// After the layout changed: new components start their autosplitter (SetSettings, the first
    /// time), and the ones that left the layout ask theirs to quit (Dispose).
    /// </summary>
    public void LayoutChanged()
    {
        List<(AutoSplitIntegrationRuntime Runtime, AutoSplitIntegrationSettings Settings)> current = Runtimes();
        foreach ((AutoSplitIntegrationRuntime runtime, AutoSplitIntegrationSettings settings) in current)
        {
            if (!runtime.HasStarted)
            {
                runtime.Start(settings);
            }
        }

        foreach (AutoSplitIntegrationRuntime gone in _tracked.Where(t => current.All(c => c.Runtime != t)).ToList())
        {
            _tracked.Remove(gone);
            gone.Close();
        }
    }

    /// <summary>LiveSplit closing: every autosplitter is asked to quit.</summary>
    public void DisposeAll()
    {
        Runtimes();
        foreach (AutoSplitIntegrationRuntime runtime in _tracked)
        {
            runtime.Close();
        }

        _tracked.Clear();
    }

    /// <summary>The components' Control menu items (ContextMenuControls): Start AutoSplit or Kill AutoSplit.</summary>
    public IReadOnlyList<IReadOnlyList<(string Label, Action Action)>> MenuSections()
    {
        var sections = new List<IReadOnlyList<(string, Action)>>();
        foreach ((AutoSplitIntegrationRuntime runtime, AutoSplitIntegrationSettings settings) in Runtimes())
        {
            if (runtime.IsRunning)
            {
                sections.Add([("Kill AutoSplit", runtime.Kill)]);
            }
            else if (AutoSplitIntegrationRuntime.Program(settings.AutoSplitPath) is not null)
            {
                sections.Add([("Start AutoSplit", () => runtime.Start(settings))]);
            }
        }

        return sections;
    }

    private AutoSplitIntegrationSettings SettingsOf(AutoSplitIntegrationRuntime runtime) =>
        _layout().Components.FirstOrDefault(c => ReferenceEquals(c.RuntimeState, runtime)) is { } component
            ? AutoSplitIntegrationSettings.FromInnerXml(component.SettingsInnerXml)
            : new AutoSplitIntegrationSettings();

    /// <summary>A command from the autosplitter (AutoSplitProcess.StartupProcess_OutputDataReceived).</summary>
    internal void Handle(AutoSplitIntegrationRuntime runtime, string line)
    {
        AutoSplitIntegrationSettings settings = SettingsOf(runtime);
        switch (line)
        {
            case AutoSplitCommands.Start:
                StartTimer(runtime, settings);
                return;
            case AutoSplitCommands.Split:
                // Pilgrim's patch: start-or-split (Pilgrim has no separate start image).
                if (State.CurrentPhase == TimerPhase.NotRunning)
                {
                    StartTimer(runtime, settings);
                }
                else if (State.CurrentPhase == TimerPhase.Running)
                {
                    runtime.IgnoreNext(AutoSplitCommands.Split);
                    _session.Execute(TimerCommand.StartOrSplit);
                }

                return;
            case AutoSplitCommands.Reset:
                if (State.CurrentPhase != TimerPhase.NotRunning)
                {
                    runtime.IgnoreNext(AutoSplitCommands.Reset);
                    _session.Execute(TimerCommand.Reset);
                }

                return;
            case AutoSplitCommands.Pause:
                if (settings.GameTimePausing)
                {
                    State.IsGameTimePaused ^= true;
                }
                else
                {
                    _session.Execute(TimerCommand.Pause);
                }

                return;
        }
    }

    private void StartTimer(AutoSplitIntegrationRuntime runtime, AutoSplitIntegrationSettings settings)
    {
        if (State.CurrentPhase != TimerPhase.NotRunning)
        {
            return;
        }

        runtime.IgnoreNext(AutoSplitCommands.Start);
        _session.Execute(TimerCommand.StartOrSplit);
        if (settings.GameTimePausing)
        {
            State.IsGameTimeInitialized = true;
        }
    }

    // The timer's events, sent to every autosplitter (unless the autosplitter caused them).
    public void OnStart() => Send(AutoSplitCommands.Start, (runtime, settings) =>
    {
        if (settings.GameTimePausing)
        {
            State.IsGameTimeInitialized = true;
        }
    });

    public void OnSplit() => Send(AutoSplitCommands.Split);

    public void OnReset() => Send(AutoSplitCommands.Reset);

    public void OnSkipSplit() => Send(AutoSplitCommands.Skip, ignorable: false);

    public void OnUndoSplit() => Send(AutoSplitCommands.Undo, ignorable: false);

    private void Send(
        string command,
        Action<AutoSplitIntegrationRuntime, AutoSplitIntegrationSettings>? after = null,
        bool ignorable = true)
    {
        foreach ((AutoSplitIntegrationRuntime runtime, AutoSplitIntegrationSettings settings) in Runtimes())
        {
            if (ignorable && runtime.ConsumeIgnore(command))
            {
                continue;
            }

            runtime.Send(command);
            after?.Invoke(runtime, settings);
        }
    }
}
