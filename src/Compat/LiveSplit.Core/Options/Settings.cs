// LiveSplit.Options.ISettings and HotkeyProfile as components see them (LiveSplit.Core @ 93a11b85,
// MIT), over the app's settings (settings.cfg), read live; and LiveSplit.Model.Input.KeyOrButton.
// Only what components read is here: the hotkey profiles and the options of the Settings dialog.
using System;
using System.Collections.Generic;
using System.Linq;
using LiveSplit.Model.Input;
using Native = LiveSplitNative.Core;

namespace LiveSplit.Model.Input
{
    /// <summary>A hotkey: a key chord or a gamepad button ("Buttons0 Wireless Controller 054C-09CC").</summary>
    public class KeyOrButton
    {
        private readonly string _text;

        public KeyOrButton(string stringRepresentation) => _text = stringRepresentation ?? "";

        /// <summary>A gamepad button: a space and no ", " (Windows' Keys names separate modifiers with ", ").</summary>
        public bool IsButton => Native.Input.GamepadInput.IsGamepadButton(_text);

        public bool IsKey => !IsButton;

        public override string ToString() => _text;

        public override bool Equals(object obj) => obj is KeyOrButton other && other._text == _text;

        public override int GetHashCode() => _text.GetHashCode(StringComparison.Ordinal);

        public static bool operator ==(KeyOrButton a, KeyOrButton b) => a is null ? b is null : a.Equals(b);

        public static bool operator !=(KeyOrButton a, KeyOrButton b) => !(a == b);
    }
}

namespace LiveSplit.Options
{
    public class HotkeyProfile : ICloneable
    {
        public const string DefaultHotkeyProfileName = "Default";

        public KeyOrButton SplitKey { get; set; }

        public KeyOrButton ResetKey { get; set; }

        public KeyOrButton SkipKey { get; set; }

        public KeyOrButton UndoKey { get; set; }

        public KeyOrButton PauseKey { get; set; }

        public KeyOrButton ToggleGlobalHotkeys { get; set; }

        public KeyOrButton SwitchComparisonPrevious { get; set; }

        public KeyOrButton SwitchComparisonNext { get; set; }

        public float HotkeyDelay { get; set; }

        public bool GlobalHotkeysEnabled { get; set; }

        public bool DeactivateHotkeysForOtherPrograms { get; set; }

        public bool DoubleTapPrevention { get; set; }

        public bool AllowGamepadsAsHotkeys { get; set; }

        public object Clone() => MemberwiseClone();

        internal static HotkeyProfile From(Native.Options.HotkeyProfileStub profile) => new()
        {
            SplitKey = Key(profile.SplitKey),
            ResetKey = Key(profile.ResetKey),
            SkipKey = Key(profile.SkipKey),
            UndoKey = Key(profile.UndoKey),
            PauseKey = Key(profile.PauseKey),
            ToggleGlobalHotkeys = Key(profile.ToggleGlobalHotkeys),
            SwitchComparisonPrevious = Key(profile.SwitchComparisonPrevious),
            SwitchComparisonNext = Key(profile.SwitchComparisonNext),
            HotkeyDelay = profile.HotkeyDelay,
            GlobalHotkeysEnabled = profile.GlobalHotkeysEnabled,
            DeactivateHotkeysForOtherPrograms = profile.DeactivateHotkeysForOtherPrograms,
            DoubleTapPrevention = profile.DoubleTapPrevention,
            AllowGamepadsAsHotkeys = profile.AllowGamepadsAsHotkeys,
        };

        private static KeyOrButton Key(string text) => string.IsNullOrEmpty(text) ? null : new KeyOrButton(text);
    }

    public interface ISettings : ICloneable
    {
        IDictionary<string, HotkeyProfile> HotkeyProfiles { get; set; }

        IList<string> RecentLayouts { get; set; }

        string LastComparison { get; set; }

        int HcpHistorySize { get; set; }

        int HcpNBestRuns { get; set; }

        bool WarnOnReset { get; set; }

        bool SimpleSumOfBest { get; set; }

        int RefreshRate { get; set; }

        int ServerPort { get; set; }

        IDictionary<string, bool> ComparisonGeneratorStates { get; set; }

        bool AgreedToSRLRules { get; set; }

        string UILanguage { get; set; }
    }

    /// <summary>
    /// The app's settings as ISettings: values are read when asked; setting one changes the app's
    /// setting (the hotkey profiles and lists are copies, as a component should not change them).
    /// </summary>
    internal sealed class HostSettings(Func<Native.Options.AppSettings> settings) : ISettings
    {
        private Native.Options.AppSettings S => settings() ?? new Native.Options.AppSettings();

        public IDictionary<string, HotkeyProfile> HotkeyProfiles
        {
            get => S.HotkeyProfiles.ToDictionary(p => p.Key, p => HotkeyProfile.From(p.Value), StringComparer.Ordinal);
            set
            {
            }
        }

        public IList<string> RecentLayouts
        {
            get => [.. S.RecentLayouts];
            set
            {
            }
        }

        public string LastComparison { get => S.LastComparison; set => S.LastComparison = value; }

        public int HcpHistorySize { get => S.HcpHistorySize; set => S.HcpHistorySize = value; }

        public int HcpNBestRuns { get => S.HcpNBestRuns; set => S.HcpNBestRuns = value; }

        public bool WarnOnReset { get => S.WarnOnReset; set => S.WarnOnReset = value; }

        public bool SimpleSumOfBest { get => S.SimpleSumOfBest; set => S.SimpleSumOfBest = value; }

        public int RefreshRate { get => S.RefreshRate; set => S.RefreshRate = value; }

        public int ServerPort { get => S.ServerPort; set => S.ServerPort = value; }

        public IDictionary<string, bool> ComparisonGeneratorStates
        {
            get => new Dictionary<string, bool>(S.ComparisonGeneratorStates);
            set
            {
            }
        }

        public bool AgreedToSRLRules { get; set; }

        public string UILanguage { get => S.UILanguage; set => S.UILanguage = value; }

        public object Clone() => new HostSettings(settings);
    }
}
