using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;

namespace LiveSplitNative.Core.UI;

/// <summary>Plays sound files (NAudio's WaveOut on Windows). Each Sound Effects component has its own.</summary>
public interface ISoundPlayer
{
    /// <summary>Stops what this player is playing.</summary>
    void Stop();

    /// <summary>Plays the file at <paramref name="volume"/> (0-1) on the output device with that index.</summary>
    void Play(string path, float volume, int outputDevice);
}

/// <summary>Port of LiveSplit.Sound SoundComponent's event handling (93a11b85).</summary>
public static class SoundLogic
{
    /// <summary>
    /// State_OnSplit: the last split is Personal Best or Not a Personal Best; any other split is
    /// ahead or behind, gaining or losing time against the current comparison, or a Best Segment
    /// (which wins), falling back to Split when that situation has no files.
    /// </summary>
    public static SoundEventType SplitEvent(LiveSplitState state, SoundComponentSettings settings)
    {
        TimingMethod method = state.CurrentTimingMethod;
        if (state.CurrentPhase == TimerPhase.Ended)
        {
            Segment last = state.Run[^1];
            return last.PersonalBestSplitTime[method] == null || last.SplitTime[method] < last.PersonalBestSplitTime[method]
                ? SoundEventType.PersonalBest
                : SoundEventType.NotAPersonalBest;
        }

        SoundEventType type = SoundEventType.Split;
        int splitIndex = state.CurrentSplitIndex - 1;
        Segment split = state.Run[splitIndex];
        TimeSpan? comparison = split.Comparisons.TryGetValue(state.CurrentComparison, out SplitTime time) ? time[method] : null;
        TimeSpan? timeDifference = split.SplitTime[method] - comparison;
        if (timeDifference != null)
        {
            TimeSpan? segmentDelta = LiveSplitStateHelper.GetPreviousSegmentDelta(state, splitIndex, state.CurrentComparison, method);
            if (timeDifference < TimeSpan.Zero)
            {
                type = segmentDelta > TimeSpan.Zero ? SoundEventType.SplitAheadLosing : SoundEventType.SplitAheadGaining;
            }
            else
            {
                type = segmentDelta < TimeSpan.Zero ? SoundEventType.SplitBehindGaining : SoundEventType.SplitBehindLosing;
            }
        }

        // Check for best segment.
        TimeSpan? segment = LiveSplitStateHelper.GetPreviousSegmentTime(state, splitIndex, method);
        if (segment != null && (split.BestSegmentTime[method] == null || segment < split.BestSegmentTime[method]))
        {
            type = SoundEventType.BestSegment;
        }

        if (settings.Sounds[type].FilePaths.Count == 0)
        {
            type = SoundEventType.Split;
        }

        return type;
    }

    /// <summary>
    /// PlaySound: stops this player, then plays one of the situation's files that exist, at
    /// random, at its volume times the general volume.
    /// </summary>
    public static void Play(
        ISoundPlayer player,
        SoundComponentSettings settings,
        SoundEventType type,
        Func<string, bool> fileExists,
        Random random)
    {
        player.Stop();
        string[] existing = settings.Sounds[type].FilePaths.Where(fileExists).ToArray();
        if (existing.Length == 0)
        {
            return;
        }

        string path = existing[random.Next(0, existing.Length)];
        float volume = settings.Sounds[type].Volume / 100f * (settings.GeneralVolume / 100f);
        player.Play(path, volume, settings.OutputDevice);
    }
}
