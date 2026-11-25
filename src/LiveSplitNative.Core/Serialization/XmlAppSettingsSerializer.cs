using System.Globalization;
using System.Xml;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Serialization;

/// <summary>
/// Load/save LiveSplit-compatible settings.cfg subset (RecentSplits/Layouts, LastComparison,
/// hotkey profile stubs, window position helpers). Unknown nodes are not inventively rewritten.
/// </summary>
public static class XmlAppSettingsSerializer
{
    /// <summary>
    /// macOS: ~/Library/Application Support/LiveSplit Native/settings.cfg
    /// Linux: $XDG_CONFIG_HOME/livesplit-native/settings.cfg (else ~/.config/...)
    /// Migrates from legacy LiveSplitNative/ (no space) when present.
    /// </summary>
    public static string DefaultPath
    {
        get
        {
            string dir = ResolveConfigDirectory();
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "settings.cfg");
            TryMigrateLegacySettings(path);
            return path;
        }
    }

    public static string ResolveConfigDirectory()
    {
        if (OperatingSystem.IsMacOS())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "LiveSplit Native");
        }

        if (OperatingSystem.IsLinux())
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            string root = !string.IsNullOrWhiteSpace(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(root, "livesplit-native");
        }

        // Windows / other — ApplicationData/LiveSplit Native
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LiveSplit Native");
    }

    private static void TryMigrateLegacySettings(string newPath)
    {
        if (File.Exists(newPath))
        {
            return;
        }

        string legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LiveSplitNative",
            "settings.cfg");
        if (!File.Exists(legacy))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
            File.Copy(legacy, newPath);
        }
        catch
        {
            // Non-fatal — start with defaults if migrate fails.
        }
    }

    public static AppSettings LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        using FileStream stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>
    /// LiveSplitNative's hotkeys Windows cannot write (with Command): only when there is one; the
    /// Windows profile then has no key there.
    /// </summary>
    private const string NativeHotkeysElement = "LiveSplitNativeHotkeys";

    /// <summary>Settings Windows writes that this app keeps but does not use.</summary>
    private static readonly string[] WindowsOnlyElements =
        ["AgreedToSRLRules", "EnableDPIAwareness", "ActiveAutoSplitters"];

    /// <summary>The hotkey elements, as HotkeyProfile.ToXml writes them (at the root before 1.8).</summary>
    private static readonly string[] HotkeyElements =
    [
        "SplitKey", "ResetKey", "SkipKey", "UndoKey", "PauseKey", "ToggleGlobalHotkeys",
        "SwitchComparisonPrevious", "SwitchComparisonNext", "GlobalHotkeysEnabled",
        "DeactivateHotkeysForOtherPrograms", "DoubleTapPrevention", "HotkeyDelay", "AllowGamepadsAsHotkeys",
    ];

    /// <summary>Every element of settings.cfg that is read here (Windows', this app's, older builds' of this app).</summary>
    private static readonly HashSet<string> KnownElements = new(
        [
            "HotkeyProfiles", "WarnOnReset", "RecentSplits", "RecentLayouts", "LastComparison", "SimpleSumOfBest",
            "RefreshRate", "ServerPort", "ServerStartup", "ServerState", "ComparisonGeneratorStates",
            "RaceProviderPlugins", "Appearance", "UILanguage", "LastSplitsPath", "LastLayoutPath", NativeHotkeysElement, "RaceViewer", "TimerDrift",
            "AskForHotkeyPermission", "WindowX", "WindowY", "WindowWidth", "WindowHeight", "WindowScreen", "WindowGeometryMigrationVersion",
            .. WindowsOnlyElements, .. HotkeyElements,
        ],
        StringComparer.Ordinal);

    /// <summary>
    /// XMLSettingsFactory.Create, by the file's version: the hotkeys in &lt;HotkeyProfiles&gt; from
    /// 1.8 (before, at the root), recent splits as SplitsFile from 1.6 (before, SplitsPath, named by
    /// opening the splits), their timing method from 1.6.1 and hotkey profile from 1.8. Windows
    /// settings this app does not use, other hotkey profiles and unknown elements are kept for
    /// saving. Values that cannot be read keep their defaults.
    /// </summary>
    public static AppSettings Load(Stream stream)
    {
        var document = new XmlDocument();
        document.Load(stream);
        XmlElement? root = document.DocumentElement;
        var settings = new AppSettings();
        if (root is null || !string.Equals(root.Name, "Settings", StringComparison.Ordinal))
        {
            return settings;
        }

        Version version = Version.TryParse(root.GetAttribute("version"), out Version? v) ? v : new Version(1, 0, 0, 0);
        settings.WarnOnReset = ParseBool(root["WarnOnReset"]?.InnerText, true);
        settings.SimpleSumOfBest = ParseBool(root["SimpleSumOfBest"]?.InnerText, false);
        settings.LastComparison = root["LastComparison"]?.InnerText ?? "Personal Best";
        settings.UILanguage = root["UILanguage"]?.InnerText ?? "";
        // LoadDrift: a base64 double; one more than 1 % from 1 is reset to 1.
        if (root["TimerDrift"]?.InnerText is { Length: > 0 } drift)
        {
            try
            {
                double loaded = BitConverter.ToDouble(Convert.FromBase64String(drift.Trim()), 0);
                settings.TimerDrift = double.IsNaN(loaded) || Math.Abs(loaded - 1) > 0.01 ? 1 : loaded;
            }
            catch (Exception e) when (e is FormatException or ArgumentException)
            {
                settings.TimerDrift = 1;
            }
        }

        // RaceViewer.FromName: one of the four viewers; anything else is the default.
        if (root["RaceViewer"]?.InnerText is { } raceViewer && AppSettings.RaceViewers.Contains(raceViewer))
        {
            settings.RaceViewer = raceViewer;
        }
        settings.RefreshRate = ParseInt(root["RefreshRate"]?.InnerText, 40);
        settings.ServerPort = ParseInt(root["ServerPort"]?.InnerText, 16834);
        // XMLSettingsFactory: the numbers of ServerStartupType / ServerStateType; others are ignored.
        int serverStartup = ParseInt(root["ServerStartup"]?.InnerText, (int)ServerStartupType.Off);
        if (Enum.IsDefined(typeof(ServerStartupType), serverStartup))
        {
            settings.ServerStartup = (ServerStartupType)serverStartup;
        }

        int serverState = ParseInt(root["ServerState"]?.InnerText, (int)ServerStateType.Off);
        if (Enum.IsDefined(typeof(ServerStateType), serverState))
        {
            settings.ServerState = (ServerStateType)serverState;
        }
        // Window position and size live in the layout (.lsl), as in Windows LiveSplit; WindowX /
        // WindowY / WindowWidth / WindowHeight / WindowScreen from older builds are ignored.
        settings.Appearance = ParseAppearance(root["Appearance"]?.InnerText);
        settings.AskForHotkeyPermission = ParseBool(root["AskForHotkeyPermission"]?.InnerText, true);
        LoadComparisonGeneratorStates(root, settings);
        LoadRaceProviders(root, version, settings);
        LoadRecentSplits(root, version, settings);

        if (root["RecentLayouts"] is { } recentLayouts)
        {
            foreach (XmlElement el in recentLayouts.SelectNodes("LayoutPath")!.OfType<XmlElement>())
            {
                settings.RecentLayouts.Add(el.InnerText);
            }
        }

        MigrateNewestFirstRecents(root, settings);
        LoadHotkeyProfiles(root, version, settings);
        // This app's chords Windows has no key for (Command): by profile (none named: the first).
        foreach (XmlElement nativeHotkeys in root.ChildNodes.OfType<XmlElement>().Where(e => e.Name == NativeHotkeysElement))
        {
            string name = nativeHotkeys.GetAttribute("profile");
            if (settings.HotkeyProfiles.TryGetValue(nativeHotkeys.HasAttribute("profile") ? name : settings.HotkeyProfiles.Keys.First(), out HotkeyProfileStub? profile))
            {
                foreach (XmlElement key in nativeHotkeys.ChildNodes.OfType<XmlElement>())
                {
                    SetHotkey(profile, key.Name, key.InnerText);
                }
            }
        }

        foreach (XmlElement element in root.ChildNodes.OfType<XmlElement>())
        {
            if (WindowsOnlyElements.Contains(element.Name))
            {
                settings.WindowsOnlyXml[element.Name] = element.OuterXml;
            }
            else if (!KnownElements.Contains(element.Name))
            {
                settings.UnknownXml.Add(element.OuterXml);
            }
        }

        return settings;
    }

    private static void LoadRecentSplits(XmlElement root, Version version, AppSettings settings)
    {
        if (root["RecentSplits"] is not { } recentSplits)
        {
            return;
        }

        if (version < new Version(1, 6))
        {
            // Before 1.6 only the paths were kept: Windows opens each to name it and leaves out
            // splits it cannot open.
            foreach (XmlElement el in recentSplits.SelectNodes("SplitsPath")!.OfType<XmlElement>())
            {
                try
                {
                    Model.Run run = XmlRunSerializer.LoadFile(el.InnerText);
                    settings.RecentSplits.Add(new RecentSplitsFile(el.InnerText, run.GameName, run.CategoryName, "RealTime"));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or XmlException or InvalidDataException or ArgumentException or NotSupportedException)
                {
                }
            }

            return;
        }

        foreach (XmlElement el in recentSplits.SelectNodes("SplitsFile")!.OfType<XmlElement>())
        {
            string method = version >= new Version(1, 6, 1) ? el.GetAttribute("lastTimingMethod") : "";
            string profile = version >= new Version(1, 8) ? el.GetAttribute("lastHotkeyProfile") : "";
            settings.RecentSplits.Add(new RecentSplitsFile(
                el.InnerText,
                el.GetAttribute("gameName"),
                el.GetAttribute("categoryName"),
                string.IsNullOrEmpty(method) ? "RealTime" : method)
            {
                LastHotkeyProfile = string.IsNullOrEmpty(profile) ? AppSettings.DefaultHotkeyProfileName : profile,
            });
        }
    }

    /// <summary>
    /// XMLSettingsFactory: every &lt;HotkeyProfile name="..."&gt; in its order; before 1.8 the one
    /// profile's elements are at the root and it is "Default". The hotkeys start with the first
    /// profile (TimerForm: CurrentHotkeyProfile = HotkeyProfiles.First().Key).
    /// </summary>
    private static void LoadHotkeyProfiles(XmlElement root, Version version, AppSettings settings)
    {
        if (version < new Version(1, 8))
        {
            if (root["SplitKey"] is not null)
            {
                settings.HotkeyProfile = ParseHotkeyProfile(root, version);
            }

            return;
        }

        List<XmlElement> profiles = root["HotkeyProfiles"]?.ChildNodes.OfType<XmlElement>().ToList() ?? [];
        if (profiles.Count == 0)
        {
            return;
        }

        settings.HotkeyProfiles.Clear();
        foreach (XmlElement profile in profiles)
        {
            settings.HotkeyProfiles.TryAdd(profile.GetAttribute("name"), ParseHotkeyProfile(profile, version));
        }

        settings.CurrentHotkeyProfile = settings.HotkeyProfiles.Keys.First();
    }

    /// <summary>HotkeyProfile.FromXml: Pause and Toggle Global Hotkeys from 1.0, the comparison keys from 1.3.</summary>
    private static HotkeyProfileStub ParseHotkeyProfile(XmlElement element, Version version)
    {
        // Windows writes System.Windows.Forms.Keys names ("D1, Control"); older builds of this
        // app wrote chords ("Ctrl+D1"); a single key or a gamepad button is the same in both.
        string Key(string name) => element[name]?.InnerText is { Length: > 0 } text
            ? text.Contains(',', StringComparison.Ordinal) ? WindowsKeys.ToChord(text) : text
            : "";
        var profile = new HotkeyProfileStub
        {
            SplitKey = Key("SplitKey"),
            ResetKey = Key("ResetKey"),
            SkipKey = Key("SkipKey"),
            UndoKey = Key("UndoKey"),
            PauseKey = version >= new Version(1, 0) ? Key("PauseKey") : "",
            ToggleGlobalHotkeys = version >= new Version(1, 0) ? Key("ToggleGlobalHotkeys") : "",
            SwitchComparisonPrevious = version >= new Version(1, 3) ? Key("SwitchComparisonPrevious") : "",
            SwitchComparisonNext = version >= new Version(1, 3) ? Key("SwitchComparisonNext") : "",
            GlobalHotkeysEnabled = ParseBool(element["GlobalHotkeysEnabled"]?.InnerText, false),
            DeactivateHotkeysForOtherPrograms = ParseBool(element["DeactivateHotkeysForOtherPrograms"]?.InnerText, false),
            DoubleTapPrevention = ParseBool(element["DoubleTapPrevention"]?.InnerText, true),
            HotkeyDelay = float.TryParse(element["HotkeyDelay"]?.InnerText?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float delay)
                ? delay
                : 0f,
            AllowGamepadsAsHotkeys = ParseBool(element["AllowGamepadsAsHotkeys"]?.InnerText, false),
        };

        // Before 1.8.17 the setting is only kept when a hotkey is a gamepad button
        // (KeyOrButton: a space and no ", ").
        string[] keys =
        [
            profile.SplitKey, profile.ResetKey, profile.SkipKey, profile.UndoKey, profile.PauseKey,
            profile.ToggleGlobalHotkeys, profile.SwitchComparisonPrevious, profile.SwitchComparisonNext,
        ];
        if (version < new Version(1, 8, 17) && !keys.Any(k => k.Contains(' ') && !k.Contains(", ", StringComparison.Ordinal)))
        {
            profile.AllowGamepadsAsHotkeys = false;
        }

        return profile;
    }

    /// <summary>
    /// Builds before 11 Oct 2026 kept the recent lists newest first and the open files in
    /// LastSplitsPath / LastLayoutPath, which Windows does not have. Such a file is turned into
    /// Windows order (newest last), with the file that was open as the last entry; an empty
    /// LastSplitsPath / LastLayoutPath becomes an empty last entry (no splits / default layout).
    /// A Windows settings.cfg has neither element and is read as it is.
    /// </summary>
    private static void MigrateNewestFirstRecents(XmlElement root, AppSettings settings)
    {
        if (root["LastSplitsPath"] is { } lastSplits)
        {
            settings.RecentSplits.Reverse();
            string path = lastSplits.InnerText;
            if (settings.LastSplitsPath != path)
            {
                settings.AddToRecentSplits(settings.FindRecentSplits(path) ?? new RecentSplitsFile(path, "", "", "RealTime"));
            }
        }

        if (root["LastLayoutPath"] is { } lastLayout)
        {
            settings.RecentLayouts.Reverse();
            string path = lastLayout.InnerText;
            if (settings.LastLayoutPath != path)
            {
                settings.AddToRecentLayouts(path);
            }
        }
    }

    public static void SaveFile(AppSettings settings, string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Atomic write: temp in same directory then replace.
        string temp = path + ".tmp";
        using (FileStream stream = File.Create(temp))
        {
            Save(settings, stream);
        }

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// XMLSettingsSaver.Save: Windows' elements in its order (the ones this app does not use as
    /// they were read, else Windows' defaults), then this app's Appearance when it is not System
    /// and the unknown elements.
    /// </summary>
    public static void Save(AppSettings settings, Stream stream)
    {
        var document = new XmlDocument();
        document.AppendChild(document.CreateXmlDeclaration("1.0", "UTF-8", null));
        XmlElement root = document.CreateElement("Settings");
        root.SetAttribute("version", "1.8.18");
        document.AppendChild(root);

        XmlElement hotkeyProfiles = document.CreateElement("HotkeyProfiles");
        root.AppendChild(hotkeyProfiles);
        // KeyOrButton.ToString: Windows' Keys names; a chord with Command has none, so it is kept
        // in LiveSplitNativeHotkeys (by profile) and Windows sees no key.
        var nativeOnly = new List<(string Profile, List<(string Name, string Chord)> Keys)>();
        foreach ((string profileName, HotkeyProfileStub hotkeys) in settings.HotkeyProfiles)
        {
            XmlElement profile = document.CreateElement("HotkeyProfile");
            hotkeyProfiles.AppendChild(profile);
            var native = new List<(string Name, string Chord)>();
            foreach ((string name, string chord) in Hotkeys(hotkeys))
            {
                string? windows = chord.Length == 0 ? "" : WindowsKeys.FromChord(chord);
                if (windows is null)
                {
                    native.Add((name, chord));
                }

                AppendText(document, profile, name, windows ?? "");
            }

            AppendText(document, profile, "GlobalHotkeysEnabled", hotkeys.GlobalHotkeysEnabled.ToString());
            AppendText(document, profile, "DeactivateHotkeysForOtherPrograms", hotkeys.DeactivateHotkeysForOtherPrograms.ToString());
            AppendText(document, profile, "DoubleTapPrevention", hotkeys.DoubleTapPrevention.ToString());
            AppendText(document, profile, "HotkeyDelay", hotkeys.HotkeyDelay.ToString(CultureInfo.InvariantCulture));
            AppendText(document, profile, "AllowGamepadsAsHotkeys", hotkeys.AllowGamepadsAsHotkeys.ToString());
            profile.SetAttribute("name", profileName);
            if (native.Count > 0)
            {
                nativeOnly.Add((profileName, native));
            }
        }

        AppendText(document, root, "WarnOnReset", settings.WarnOnReset.ToString());
        AppendText(document, root, "RaceViewer", settings.RaceViewer);
        AppendWindowsOnly(document, root, settings, "AgreedToSRLRules", "False");
        AppendWindowsOnly(document, root, settings, "EnableDPIAwareness", "False");
        AppendText(document, root, "UILanguage", settings.UILanguage);

        XmlElement recentSplits = document.CreateElement("RecentSplits");
        root.AppendChild(recentSplits);
        foreach (RecentSplitsFile file in settings.RecentSplits)
        {
            XmlElement el = document.CreateElement("SplitsFile");
            el.InnerText = file.Path;
            el.SetAttribute("gameName", file.GameName);
            el.SetAttribute("categoryName", file.CategoryName);
            el.SetAttribute("lastTimingMethod", file.LastTimingMethod);
            el.SetAttribute("lastHotkeyProfile", file.LastHotkeyProfile);
            recentSplits.AppendChild(el);
        }

        XmlElement recentLayouts = document.CreateElement("RecentLayouts");
        root.AppendChild(recentLayouts);
        foreach (string layoutPath in settings.RecentLayouts)
        {
            AppendText(document, recentLayouts, "LayoutPath", layoutPath);
        }

        AppendText(document, root, "LastComparison", settings.LastComparison);
        AppendText(document, root, "SimpleSumOfBest", settings.SimpleSumOfBest.ToString());
        AppendText(document, root, "RefreshRate", settings.RefreshRate.ToString(CultureInfo.InvariantCulture));
        AppendText(document, root, "ServerPort", settings.ServerPort.ToString(CultureInfo.InvariantCulture));
        AppendText(document, root, "ServerStartup", ((int)settings.ServerStartup).ToString(CultureInfo.InvariantCulture));
        AppendText(document, root, "ServerState", ((int)settings.ServerState).ToString(CultureInfo.InvariantCulture));
        SaveComparisonGeneratorStates(document, root, settings);
        SaveRaceProviders(document, root, settings);
        AppendWindowsOnly(document, root, settings, "ActiveAutoSplitters", "");
        // AddDriftToSettings: the drift as a base64 double in CDATA.
        XmlElement drift = document.CreateElement("TimerDrift");
        drift.AppendChild(document.CreateCDataSection(Convert.ToBase64String(BitConverter.GetBytes(settings.TimerDrift))));
        root.AppendChild(drift);

        foreach ((string profileName, List<(string Name, string Chord)> keys) in nativeOnly)
        {
            XmlElement native = document.CreateElement(NativeHotkeysElement);
            native.SetAttribute("profile", profileName);
            foreach ((string name, string chord) in keys)
            {
                AppendText(document, native, name, chord);
            }

            root.AppendChild(native);
        }

        // LiveSplitNative's light/dark setting, only when it was changed.
        if (settings.Appearance != AppAppearanceMode.System)
        {
            AppendText(document, root, "Appearance", settings.Appearance.ToString());
        }

        // LiveSplitNative's Don't ask again for the hotkey permission, only when it was ticked.
        if (!settings.AskForHotkeyPermission)
        {
            AppendText(document, root, "AskForHotkeyPermission", "False");
        }

        AppendXml(document, root, settings.UnknownXml);
        document.Save(stream);
    }

    /// <summary>The profile's keys by their settings.cfg names, in HotkeyProfile.ToXml's order.</summary>
    private static IEnumerable<(string Name, string Chord)> Hotkeys(HotkeyProfileStub p) =>
    [
        ("SplitKey", p.SplitKey), ("ResetKey", p.ResetKey), ("SkipKey", p.SkipKey), ("UndoKey", p.UndoKey),
        ("PauseKey", p.PauseKey), ("ToggleGlobalHotkeys", p.ToggleGlobalHotkeys),
        ("SwitchComparisonPrevious", p.SwitchComparisonPrevious), ("SwitchComparisonNext", p.SwitchComparisonNext),
    ];

    private static void SetHotkey(HotkeyProfileStub p, string name, string chord)
    {
        Action<string>? set = name switch
        {
            "SplitKey" => v => p.SplitKey = v,
            "ResetKey" => v => p.ResetKey = v,
            "SkipKey" => v => p.SkipKey = v,
            "UndoKey" => v => p.UndoKey = v,
            "PauseKey" => v => p.PauseKey = v,
            "ToggleGlobalHotkeys" => v => p.ToggleGlobalHotkeys = v,
            "SwitchComparisonPrevious" => v => p.SwitchComparisonPrevious = v,
            "SwitchComparisonNext" => v => p.SwitchComparisonNext = v,
            _ => null,
        };
        set?.Invoke(chord);
    }

    private static void AppendWindowsOnly(XmlDocument document, XmlElement root, AppSettings settings, string name, string fallback)
    {
        if (settings.WindowsOnlyXml.TryGetValue(name, out string? xml))
        {
            AppendXml(document, root, [xml]);
        }
        else
        {
            AppendText(document, root, name, fallback);
        }
    }

    private static void AppendXml(XmlDocument document, XmlElement parent, IEnumerable<string> outerXml)
    {
        foreach (string xml in outerXml)
        {
            XmlDocumentFragment fragment = document.CreateDocumentFragment();
            try
            {
                fragment.InnerXml = xml;
                parent.AppendChild(fragment);
            }
            catch (XmlException)
            {
            }
        }
    }

    /// <summary>
    /// Windows XMLSettingsFactory: &lt;ComparisonGeneratorStates&gt;&lt;Generator name="…"&gt;True&lt;/Generator&gt;…;
    /// unknown names are ignored, and the Golf HCP element carries HcpHistorySize / HcpNBestRuns.
    /// </summary>
    private static void LoadComparisonGeneratorStates(XmlElement root, AppSettings settings)
    {
        if (root["ComparisonGeneratorStates"] is not { } states)
        {
            return;
        }

        var loaded = new Dictionary<string, bool>(settings.ComparisonGeneratorStates, StringComparer.Ordinal);
        foreach (XmlElement generator in states.ChildNodes.OfType<XmlElement>())
        {
            string name = generator.GetAttribute("name");
            if (!loaded.ContainsKey(name))
            {
                continue;
            }

            loaded[name] = ParseBool(generator.InnerText, loaded[name]);
            if (name == HCPComparisonGenerator.ComparisonName)
            {
                settings.HcpHistorySize = ParseInt(NullIfEmpty(generator.GetAttribute("HcpHistorySize")), settings.HcpHistorySize);
                settings.HcpNBestRuns = ParseInt(NullIfEmpty(generator.GetAttribute("HcpNBestRuns")), settings.HcpNBestRuns);
            }
        }

        settings.SetComparisonGeneratorStates(loaded);
    }

    private static void SaveComparisonGeneratorStates(XmlDocument document, XmlElement root, AppSettings settings)
    {
        XmlElement states = document.CreateElement("ComparisonGeneratorStates");
        foreach ((string name, bool enabled) in settings.ComparisonGeneratorStates)
        {
            XmlElement generator = document.CreateElement("Generator");
            generator.SetAttribute("name", name);
            generator.InnerText = enabled.ToString();
            if (name == HCPComparisonGenerator.ComparisonName)
            {
                generator.SetAttribute("HcpHistorySize", settings.HcpHistorySize.ToString(CultureInfo.InvariantCulture));
                generator.SetAttribute("HcpNBestRuns", settings.HcpNBestRuns.ToString(CultureInfo.InvariantCulture));
            }

            states.AppendChild(generator);
        }

        root.AppendChild(states);
    }

    /// <summary>
    /// XMLSettingsFactory: &lt;RaceProviderPlugins&gt; (read from 1.8.8 on), then every race
    /// provider the file did not have.
    /// </summary>
    private static void LoadRaceProviders(XmlElement root, Version version, AppSettings settings) =>
        settings.RaceProviders = Racing.RaceProviderSettings.Load(
            version >= new Version(1, 8, 8) ? root["RaceProviderPlugins"] : null);

    /// <summary>XMLSettingsSaver: each race provider's &lt;Plugin&gt;.</summary>
    private static void SaveRaceProviders(XmlDocument document, XmlElement root, AppSettings settings)
    {
        XmlElement plugins = document.CreateElement("RaceProviderPlugins");
        root.AppendChild(plugins);
        foreach (Racing.RaceProviderSettings provider in settings.RaceProviders)
        {
            plugins.AppendChild(provider.ToXml(document));
        }
    }

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    private static void AppendText(XmlDocument document, XmlElement parent, string name, string value)
    {
        XmlElement el = document.CreateElement(name);
        el.InnerText = value ?? "";
        parent.AppendChild(el);
    }

    private static bool ParseBool(string? text, bool fallback) =>
        text is not null && bool.TryParse(text, out bool v) ? v : fallback;

    private static int ParseInt(string? text, int fallback) =>
        text is not null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : fallback;

    private static AppAppearanceMode ParseAppearance(string? text) =>
        text is not null && Enum.TryParse(text, ignoreCase: true, out AppAppearanceMode mode)
            ? mode
            : AppAppearanceMode.System;
}
