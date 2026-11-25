using System.Globalization;
using System.Xml;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.UI;

/// <summary>Portable subset of LiveSplit.Timer TimerSettings.</summary>
public sealed class TimerComponentSettings
{
    public float TimerHeight { get; set; } = 69f;
    public float TimerWidth { get; set; } = 225f;
    public float DecimalsSize { get; set; } = 35f;
    public TimeAccuracy TimerAccuracy { get; set; } = TimeAccuracy.Hundredths;
    public bool ShowGradient { get; set; } = true;
    public bool CenterTimer { get; set; }
    public bool OverrideSplitColors { get; set; }
    public ArgbColor TimerColor { get; set; } = ArgbColor.FromArgb(255, 170, 170, 170);
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(255, 34, 34, 34);
    public string BackgroundGradient { get; set; } = "Plain";
    public string TimingMethod { get; set; } = "Current Timing Method";
    public string DigitsFormat { get; set; } = "1";

    public static TimerComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new TimerComponentSettings
        {
            TimerHeight = ComponentSettingsXml.Float(root, "TimerHeight", 69f),
            TimerWidth = ComponentSettingsXml.Float(root, "TimerWidth", 225f),
            DecimalsSize = ComponentSettingsXml.Float(root, "DecimalsSize", 35f),
            TimerAccuracy = ComponentSettingsXml.Accuracy(root, "TimerAccuracy", TimeAccuracy.Hundredths),
            ShowGradient = ComponentSettingsXml.Bool(root, "ShowGradient", true),
            CenterTimer = ComponentSettingsXml.Bool(root, "CenterTimer", false),
            OverrideSplitColors = ComponentSettingsXml.Bool(root, "OverrideSplitColors", false),
            TimerColor = ComponentSettingsXml.Color(root, "TimerColor", ArgbColor.FromArgb(255, 170, 170, 170)),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(255, 34, 34, 34)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            TimingMethod = ComponentSettingsXml.String(root, "TimingMethod", "Current Timing Method"),
            DigitsFormat = ComponentSettingsXml.String(root, "DigitsFormat", "1"),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.3");
        ComponentSettingsXml.SetText(doc, parent, "TimerHeight", TimerHeight.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "TimerWidth", TimerWidth.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "DecimalsSize", DecimalsSize.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "TimerAccuracy", TimerAccuracy.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowGradient", ShowGradient.ToString());
        ComponentSettingsXml.SetText(doc, parent, "CenterTimer", CenterTimer.ToString());
        ComponentSettingsXml.SetText(doc, parent, "OverrideSplitColors", OverrideSplitColors.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimerColor", TimerColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "TimingMethod", TimingMethod);
        ComponentSettingsXml.SetText(doc, parent, "DigitsFormat", DigitsFormat);
    });
}
