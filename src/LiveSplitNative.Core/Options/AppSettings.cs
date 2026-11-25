using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;

namespace LiveSplitNative.Core.Options;

/// <summary>Portable subset of LiveSplit settings.cfg (XMLSettingsSaver fields we support).</summary>
public sealed class AppSettings
{
    public const string DefaultHotkeyProfileName = "Default";

    /// <summary>How many recent splits and layouts Windows Settings keeps (AddToRecentSplits / AddToRecentLayouts).</summary>
    public const int MaxRecentSplits = 50;
    public const int MaxRecentLayouts = 10;

    /// <summary>
    /// Recently used splits, oldest first and the current one last, as Windows keeps them. An
    /// entry with an empty path means "no splits" (Close Splits); startup opens the last entry.
    /// </summary>
    public List<RecentSplitsFile> RecentSplits { get; } = [];

    /// <summary>Recently used layouts, oldest first; an empty path is the default layout.</summary>
    public List<string> RecentLayouts { get; } = [];
    public string LastComparison { get; set; } = "Personal Best";
    public bool WarnOnReset { get; set; } = true;
    public bool SimpleSumOfBest { get; set; }

    /// <summary>
    /// Choose Active Comparisons (settings.cfg ComparisonGeneratorStates): which generated
    /// comparisons the run gets. Defaults and order as Windows StandardSettingsFactory.
    /// </summary>
    public Dictionary<string, bool> ComparisonGeneratorStates { get; private set; } = CreateDefaultComparisonGeneratorStates();

    /// <summary>Golf HCP: how many latest finished attempts to consider (Windows 1–50, default 20).</summary>
    public int HcpHistorySize { get; set; } = 20;

    /// <summary>Golf HCP: how many of those, fastest first, to use (Windows 1–50, default 8).</summary>
    public int HcpNBestRuns { get; set; } = 8;
    public const int MinRefreshRate = 20;
    public const int MaxRefreshRate = 300;
    public int RefreshRate { get; set; } = 40;
    public int ServerPort { get; set; } = 16834;

    /// <summary>settings.cfg ServerStartup: which server starts with LiveSplit (Windows default Off).</summary>
    public ServerStartupType ServerStartup { get; set; } = ServerStartupType.Off;

    /// <summary>settings.cfg ServerState: the server running when LiveSplit last closed (for Restore Previous State).</summary>
    public ServerStateType ServerState { get; set; } = ServerStateType.Off;
    /// <summary>The splits startup opens (Windows RecentSplits[^1]); empty for none.</summary>
    public string LastSplitsPath => RecentSplits.Count > 0 ? RecentSplits[^1].Path : "";

    /// <summary>The layout startup opens (Windows RecentLayouts[^1]); empty for the default layout.</summary>
    public string LastLayoutPath => RecentLayouts.Count > 0 ? RecentLayouts[^1] : "";
    /// <summary>
    /// settings.cfg UILanguage: the language code of the UI (Windows' Language menu); empty
    /// follows the system.
    /// </summary>
    public string UILanguage { get; set; } = "";

    /// <summary>Settings / OS chrome appearance. Default System. Does not alter .lsl timer colors.</summary>
    public AppAppearanceMode Appearance { get; set; } = AppAppearanceMode.System;

    /// <summary>
    /// macOS: at start, LiveSplit asks for the permission global hotkeys need while it is missing;
    /// false after Don't ask again (settings.cfg AskForHotkeyPermission, this app's own).
    /// </summary>
    public bool AskForHotkeyPermission { get; set; } = true;

    /// <summary>
    /// settings.cfg RaceProviderPlugins (Windows Settings.RaceProvider), in its order: racetime.gg,
    /// SRL and plugins this app does not have. Manage Racing Services edits a copy.
    /// </summary>
    public List<Racing.RaceProviderSettings> RaceProviders { get; set; } = Racing.RaceProviderSettings.CreateStandard();

    /// <summary>racetime.gg's settings among <see cref="RaceProviders"/>.</summary>
    public Racing.RacetimeSettings Racetime
    {
        get
        {
            if (RaceProviders.OfType<Racing.RacetimeSettings>().FirstOrDefault() is { } racetime)
            {
                return racetime;
            }

            var added = new Racing.RacetimeSettings();
            RaceProviders.Add(added);
            return added;
        }
    }
    /// <summary>
    /// Settings.HotkeyProfiles: the hotkey profiles by name, in settings.cfg's order (Windows'
    /// Dictionary, so a removed name's place is taken by the next one added, as there).
    /// </summary>
    public Dictionary<string, HotkeyProfileStub> HotkeyProfiles { get; } = new(StringComparer.Ordinal)
    {
        [DefaultHotkeyProfileName] = HotkeyProfileStub.CreateDefault(),
    };

    /// <summary>
    /// LiveSplitState.CurrentHotkeyProfile: the profile the hotkeys use now (the first one at
    /// start; Settings OK and opening splits change it). Not saved.
    /// </summary>
    public string CurrentHotkeyProfile { get; set; } = DefaultHotkeyProfileName;

    /// <summary>The current profile's hotkeys (the first profile when the current one is gone).</summary>
    public HotkeyProfileStub HotkeyProfile
    {
        get => HotkeyProfiles.TryGetValue(CurrentHotkeyProfile, out HotkeyProfileStub? profile)
            ? profile
            : HotkeyProfiles.Values.First();
        set => HotkeyProfiles[HotkeyProfiles.ContainsKey(CurrentHotkeyProfile) ? CurrentHotkeyProfile : HotkeyProfiles.Keys.First()] = value;
    }

    /// <summary>The Race Viewer choices (Settings → Racing), as Windows' RaceViewer.FromName knows them.</summary>
    public static readonly string[] RaceViewers = ["SpeedRunsLive", "MultiTwitch", "Kadgar", "Speedrun.tv"];

    /// <summary>
    /// Settings.RaceViewer: where a race opens when you are not in it. racetime.gg's races always
    /// count you as in them (RacetimeRace.IsParticipant), so they join instead, as on Windows.
    /// </summary>
    public string RaceViewer { get; set; } = "SpeedRunsLive";

    /// <summary>
    /// TimerDrift: the drift of the timer's clock against the atomic clock, measured in earlier
    /// sessions (TimeStamp.NewDrift when saved; the clock is divided by it from the next start).
    /// </summary>
    public double TimerDrift { get; set; } = 1.0;

    /// <summary>
    /// Windows settings this app does not use (AgreedToSRLRules, EnableDPIAwareness,
    /// ActiveAutoSplitters) by element name, kept as they were.
    /// </summary>
    public Dictionary<string, string> WindowsOnlyXml { get; } = new(StringComparer.Ordinal);

    /// <summary>Elements of settings.cfg that are neither Windows' nor this app's, kept as they were.</summary>
    public List<string> UnknownXml { get; } = [];

    /// <summary>
    /// Display refresh period for a Refresh Rate setting. Windows SettingsDialog clamps the
    /// value to 20–300 Hz and TimerForm.RefreshTimerWorker sleeps 1000 / RefreshRate ms
    /// (integer division). Only the display uses this; recorded times never depend on it.
    /// </summary>
    public static TimeSpan RefreshInterval(int refreshRate) =>
        TimeSpan.FromMilliseconds(1000 / Math.Clamp(refreshRate, MinRefreshRate, MaxRefreshRate));

    public static Dictionary<string, bool> CreateDefaultComparisonGeneratorStates() => new(StringComparer.Ordinal)
    {
        [BestSegmentsComparisonGenerator.ComparisonName] = true,
        [BestSplitTimesComparisonGenerator.ComparisonName] = false,
        [AverageSegmentsComparisonGenerator.ComparisonName] = true,
        [MedianSegmentsComparisonGenerator.ComparisonName] = false,
        [WorstSegmentsComparisonGenerator.ComparisonName] = false,
        [PercentileComparisonGenerator.ComparisonName] = false,
        [LatestRunComparisonGenerator.ComparisonName] = false,
        [HCPComparisonGenerator.ComparisonName] = false,
        [NoneComparisonGenerator.ComparisonName] = false,
    };

    /// <summary>Replaces the generator states (Choose Active Comparisons OK), keeping every known name.</summary>
    public void SetComparisonGeneratorStates(IReadOnlyDictionary<string, bool> states)
    {
        Dictionary<string, bool> next = CreateDefaultComparisonGeneratorStates();
        foreach (string name in next.Keys.ToList())
        {
            if (states.TryGetValue(name, out bool enabled))
            {
                next[name] = enabled;
            }
        }

        ComparisonGeneratorStates = next;
    }

    /// <summary>The ISettings values upstream generators read.</summary>
    public ComparisonGeneratorSettings ToComparisonGeneratorSettings() => new()
    {
        SimpleSumOfBest = SimpleSumOfBest,
        HcpHistorySize = HcpHistorySize,
        HcpNBestRuns = HcpNBestRuns,
    };

    /// <summary>
    /// Windows Settings.AddToRecentSplits: the file moves to the end with the run's game and
    /// extended category name and the timing method and hotkey profile it was last used with.
    /// A null run (Close Splits) leaves the names empty.
    /// </summary>
    public void AddToRecentSplits(string path, Run? run, TimingMethod lastTimingMethod, string lastHotkeyProfile) =>
        AddToRecentSplits(new RecentSplitsFile(
            path,
            run?.GameName ?? "",
            run?.GetExtendedCategoryName() ?? "",
            lastTimingMethod.ToString())
        {
            LastHotkeyProfile = lastHotkeyProfile,
        });

    public void AddToRecentSplits(RecentSplitsFile file)
    {
        int found = RecentSplits.FindIndex(x => x.Path == file.Path);
        if (found >= 0)
        {
            RecentSplits.RemoveAt(found);
        }

        RecentSplits.Add(file);
        while (RecentSplits.Count > MaxRecentSplits)
        {
            RecentSplits.RemoveAt(0);
        }
    }

    /// <summary>Windows Settings.AddToRecentLayouts: the path moves to the end; "" is the default layout.</summary>
    public void AddToRecentLayouts(string path)
    {
        RecentLayouts.Remove(path);
        RecentLayouts.Add(path);
        while (RecentLayouts.Count > MaxRecentLayouts)
        {
            RecentLayouts.RemoveAt(0);
        }
    }

    /// <summary>The recent entry for this file (Windows UpdateStateFromSplitsPath), or null.</summary>
    public RecentSplitsFile? FindRecentSplits(string path) => RecentSplits.LastOrDefault(x => x.Path == path);
}

public sealed class RecentSplitsFile
{
    public RecentSplitsFile(string path, string gameName, string categoryName, string lastTimingMethod)
    {
        Path = path;
        GameName = gameName;
        CategoryName = categoryName;
        LastTimingMethod = lastTimingMethod;
    }

    public string Path { get; set; }
    public string GameName { get; set; }
    public string CategoryName { get; set; }
    public string LastTimingMethod { get; set; }
    public string LastHotkeyProfile { get; set; } = AppSettings.DefaultHotkeyProfileName;
}

/// <summary>Hotkey stubs — keys stored as strings (unbound empty); global hotkeys not wired (M5).</summary>
public sealed class HotkeyProfileStub
{
    public string SplitKey { get; set; } = "";
    public string ResetKey { get; set; } = "";
    public string SkipKey { get; set; } = "";
    public string UndoKey { get; set; } = "";
    public string PauseKey { get; set; } = "";
    public string ToggleGlobalHotkeys { get; set; } = "";
    public string SwitchComparisonPrevious { get; set; } = "";
    public string SwitchComparisonNext { get; set; } = "";
    public bool GlobalHotkeysEnabled { get; set; }
    public bool DeactivateHotkeysForOtherPrograms { get; set; }
    public bool AllowGamepadsAsHotkeys { get; set; }
    public bool DoubleTapPrevention { get; set; } = true;
    /// <summary>Seconds (matches Windows SettingsDialog “Hotkey Delay (Seconds)”).</summary>
    public float HotkeyDelay { get; set; }

    /// <summary>HotkeyProfile.Clone.</summary>
    public HotkeyProfileStub Clone() => (HotkeyProfileStub)MemberwiseClone();

    public static HotkeyProfileStub CreateDefault() => new()
    {
        // Mirror StandardSettingsFactory intent as unbound stubs on macOS (no WinForms Keys).
        SplitKey = "",
        ResetKey = "",
        SkipKey = "",
        UndoKey = "",
        PauseKey = "",
        GlobalHotkeysEnabled = false,
        DoubleTapPrevention = true,
    };
}
