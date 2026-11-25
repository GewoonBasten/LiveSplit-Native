using LiveSplitNative.ComponentHost.Compat;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

public partial class MainWindow
{
    /// <summary>
    /// The layout's metrics, after the layout's component DLLs have their components (with the
    /// layout's settings), so their sizes count.
    /// </summary>
    private IReadOnlyList<TimerComponentMetric> ComponentMetrics()
    {
        ExternalComponents.Attach(_layout);
        return TimerLayoutMetrics.FromLayout(_layout, _state.Run.Count);
    }

    /// <summary>
    /// The last session did not end normally, so the downloaded components were left off: which
    /// ones, and whether to turn them on now (Keep Off leaves them off until the next start).
    /// </summary>
    internal async Task AskAboutComponentsAfterCrashAsync(IReadOnlyList<string> dlls)
    {
        bool on = false;
        (Avalonia.Controls.Window dialog, Avalonia.Controls.Button keepOff, Avalonia.Controls.Button turnOn) = ComponentsAfterCrashDialog(dlls);
        keepOff.Click += (_, _) => dialog.Close();
        turnOn.Click += (_, _) =>
        {
            on = true;
            dialog.Close();
        };
        if (AskAboutComponentsAnswerForTests is { } answer)
        {
            on = answer;
        }
        else
        {
            await ShowOwnedDialogAsync(dialog);
        }

        if (on)
        {
            ExternalComponents.TurnOnUserComponents();
            ExternalComponents.Attach(_layout);
            ApplyLayoutChange();
        }
    }

    /// <summary>The question after a crash: the components left off, Keep Off and Turn On.</summary>
    internal static (Avalonia.Controls.Window Dialog, Avalonia.Controls.Button KeepOff, Avalonia.Controls.Button TurnOn) ComponentsAfterCrashDialog(IReadOnlyList<string> dlls)
    {
        string list = string.Join(Environment.NewLine, dlls.Select(d => "•  " + Path.GetFileNameWithoutExtension(d)));
        string message =
            "LiveSplit quit unexpectedly last time. In case a downloaded component caused it, "
            + "your downloaded components are turned off for now:"
            + Environment.NewLine + Environment.NewLine + list + Environment.NewLine + Environment.NewLine
            + "Turn them on again, or keep them off until LiveSplit is started again.";
        var keepOff = new Avalonia.Controls.Button { Content = "Keep Off" };
        var turnOn = new Avalonia.Controls.Button { Content = "Turn On", IsDefault = true };
        return (DialogChrome.Message("Downloaded Components", message, 440, keepOff, turnOn), keepOff, turnOn);
    }

    /// <summary>Tests: the answer to the question after a crash (true: Turn On), without the dialog.</summary>
    internal static bool? AskAboutComponentsAnswerForTests { get; set; }

    /// <summary>LiveSplitState.Layout's components as component DLLs see them (tests).</summary>
    internal IReadOnlyList<HostLayoutEntry> HostLayoutForTests => TimerSurface.HostLayoutEntries();

    /// <summary>This window's timer as component DLLs see it (LiveSplitState).</summary>
    private sealed class ExternalTimerSource(MainWindow window) : IHostTimerSource
    {
        public LiveSplitState State => window._state;

        public TimerModel Model => window._model;

        public LayoutSettings LayoutSettings => window._layout.Settings;

        public LayoutMode LayoutMode => window._layout.Mode;

        public Core.Options.AppSettings Settings => window._appSettings;

        public IHostDialogs Dialogs => AppHostDialogs.Instance;

        public IReadOnlyList<HostLayoutEntry> LayoutComponents => window.TimerSurface.HostLayoutEntries();

        public void Invalidate() => window.TimerSurface.InvalidateVisual();
    }
}
