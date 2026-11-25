using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// SoundComponent's timer events for every Sound Effects component in the layout: start,
/// split, skip, undo, pause, resume, and a reset that did not end the run. Each component has
/// its own player (on its RuntimeState), which stops before the next sound, as Windows' WaveOut.
/// </summary>
internal sealed class SoundEffectsController(LiveSplitState state, Func<Layout> layout)
{
    private readonly Random _random = new();

    /// <summary>Makes a component's player (the tests record what would be played).</summary>
    internal static Func<ISoundPlayer> CreatePlayer { get; set; } =
        () => OperatingSystem.IsLinux() ? new LinuxSoundPlayer() : new MacOsSoundPlayer();

    public void OnStart() => Play(_ => SoundEventType.StartTimer);

    public void OnSplit() => Play(settings => SoundLogic.SplitEvent(state, settings));

    public void OnSkipSplit() => Play(_ => SoundEventType.SkipSplit);

    public void OnUndoSplit() => Play(_ => SoundEventType.UndoSplit);

    public void OnPause() => Play(_ => SoundEventType.Pause);

    public void OnResume() => Play(_ => SoundEventType.Resume);

    /// <summary>State_OnReset: no sound after a finished run (its last split already played one).</summary>
    public void OnReset(TimerPhase previousPhase)
    {
        if (previousPhase != TimerPhase.Ended)
        {
            Play(_ => SoundEventType.Reset);
        }
    }

    private void Play(Func<SoundComponentSettings, SoundEventType> situation)
    {
        foreach (LayoutComponent component in layout().Components)
        {
            if (component.Path != StandardLayoutFactory.SoundPath)
            {
                continue;
            }

            if (component.RuntimeState is not ISoundPlayer player)
            {
                player = CreatePlayer();
                component.RuntimeState = player;
            }

            var settings = SoundComponentSettings.FromInnerXml(component.SettingsInnerXml);
            SoundLogic.Play(player, settings, situation(settings), File.Exists, _random);
        }
    }
}
