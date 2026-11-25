using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App;

/// <summary>
/// CollectorComponent's timer events for every therun.gg component in the layout (start, split,
/// skip, undo and undo all pauses send the live run; pause, resume and reset too), each with its
/// collector on its RuntimeState. A component that leaves the layout, and every one when
/// LiveSplit closes, is disposed: its splits are uploaded when Sync on Reset is off.
/// </summary>
internal sealed class TheRunController(LiveSplitState state, Func<Layout> layout)
{
    /// <summary>The collectors last seen in the layout, with their settings, to dispose the ones that leave it.</summary>
    private readonly Dictionary<TheRunCollector, string?> _tracked = [];

    public void OnSplit() => Handle((collector, settings, key) => collector.HandleSplitAsync(state, settings, key));

    public void OnPause() => Handle((collector, settings, key) => collector.HandlePauseAsync(state, settings, key));

    public void OnResume() => Handle((collector, settings, key) => collector.HandleResumeAsync(state, settings, key));

    public void OnReset() => Handle((collector, settings, key) => collector.HandleResetAsync(state, settings, key));

    private void Handle(Func<TheRunCollector, TheRunComponentSettings, string, Task> handler)
    {
        foreach ((TheRunCollector collector, TheRunComponentSettings settings) in Collectors())
        {
            string key = TheRunUploadKey.Resolve(settings);
            _ = handler(collector, settings, key);
        }
    }

    private List<(TheRunCollector Collector, TheRunComponentSettings Settings)> Collectors()
    {
        var collectors = new List<(TheRunCollector, TheRunComponentSettings)>();
        foreach (LayoutComponent component in layout().Components)
        {
            if (component.Path != StandardLayoutFactory.TheRunPath)
            {
                continue;
            }

            if (component.RuntimeState is not TheRunCollector collector)
            {
                collector = new TheRunCollector(TheRunClient.Current);
                component.RuntimeState = collector;
            }

            _tracked[collector] = component.SettingsInnerXml;
            collectors.Add((collector, TheRunComponentSettings.FromInnerXml(component.SettingsInnerXml)));
        }

        return collectors;
    }

    /// <summary>After the layout changed: the components that left it are disposed (CollectorComponent.Dispose).</summary>
    public void LayoutChanged()
    {
        var current = Collectors().Select(c => c.Collector).ToHashSet();
        foreach ((TheRunCollector collector, string? settings) in _tracked.Where(t => !current.Contains(t.Key)).ToList())
        {
            _tracked.Remove(collector);
            Dispose(collector, settings);
        }
    }

    /// <summary>LiveSplit closing: every component is disposed.</summary>
    public void DisposeAll()
    {
        Collectors();
        foreach ((TheRunCollector collector, string? settings) in _tracked.ToList())
        {
            Dispose(collector, settings);
        }

        _tracked.Clear();
    }

    private void Dispose(TheRunCollector collector, string? settingsXml)
    {
        var settings = TheRunComponentSettings.FromInnerXml(settingsXml);
        collector.Dispose(state, settings, TheRunUploadKey.Resolve(settings));
    }
}
