using Avalonia.Threading;
using LiveSplitNative.Core.Session;

namespace LiveSplitNative.App;

/// <summary>Runs timer-session work on Avalonia's UI thread.</summary>
internal sealed class AvaloniaSessionDispatcher : ISessionDispatcher
{
    public static readonly AvaloniaSessionDispatcher Instance = new();

    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
