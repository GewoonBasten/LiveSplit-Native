using System.Globalization;
using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// LiveSplit.HotkeyIndicator HotkeyIndicatorSettings: a bar in the "on" color while global
/// hotkeys are enabled, else the "off" color; Height (vertical layouts) / Width (horizontal),
/// 1-50. Windows writes no Version element here.
/// </summary>
public sealed class HotkeyIndicatorComponentSettings
{
    public ArgbColor HotkeysOnColor { get; set; } = ArgbColor.FromArgb(255, 41, 204, 84);
    public ArgbColor HotkeysOffColor { get; set; } = ArgbColor.FromArgb(255, 204, 55, 41);
    public float IndicatorHeight { get; set; } = 3f;
    public float IndicatorWidth { get; set; } = 3f;

    public static HotkeyIndicatorComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new HotkeyIndicatorComponentSettings
        {
            HotkeysOnColor = ComponentSettingsXml.Color(root, "HotkeysOnColor", ArgbColor.FromArgb(255, 41, 204, 84)),
            HotkeysOffColor = ComponentSettingsXml.Color(root, "HotkeysOffColor", ArgbColor.FromArgb(255, 204, 55, 41)),
            IndicatorHeight = ComponentSettingsXml.Float(root, "IndicatorHeight", 3f),
            IndicatorWidth = ComponentSettingsXml.Float(root, "IndicatorWidth", 3f),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "HotkeysOnColor", HotkeysOnColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "HotkeysOffColor", HotkeysOffColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "IndicatorHeight", IndicatorHeight.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "IndicatorWidth", IndicatorWidth.ToString(CultureInfo.InvariantCulture));
    });
}

/// <summary>
/// LiveSplit.ManualGameTime ManualGameTimeSettings (version 1.4): whether the times typed in the
/// "Enter Game Time" window are segment times (added to the game time so far) or split times.
/// </summary>
public sealed class ManualGameTimeComponentSettings
{
    public bool UseSegmentTimes { get; set; } = true;

    public static ManualGameTimeComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new ManualGameTimeComponentSettings
        {
            UseSegmentTimes = ComponentSettingsXml.Bool(root, "UseSegmentTimes", false),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.4");
        ComponentSettingsXml.SetText(doc, parent, "UseSegmentTimes", UseSegmentTimes.ToString());
    });
}
