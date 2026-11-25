using System.Xml;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.UI;

/// <summary>Portable subset of LiveSplit.PreviousSegment settings.</summary>
public sealed class PreviousSegmentComponentSettings
{
    public TimeAccuracy DeltaAccuracy { get; set; } = TimeAccuracy.Tenths;
    public bool DropDecimals { get; set; } = true;
    public bool Display2Rows { get; set; }
    public bool ShowPossibleTimeSave { get; set; }
    public TimeAccuracy TimeSaveAccuracy { get; set; } = TimeAccuracy.Tenths;
    public string Comparison { get; set; } = "Current Comparison";
    public bool OverrideTextColor { get; set; }
    public ArgbColor TextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(255, 28, 28, 28);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(255, 13, 13, 13);
    public string BackgroundGradient { get; set; } = "Vertical";

    public static PreviousSegmentComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new PreviousSegmentComponentSettings
        {
            DeltaAccuracy = ComponentSettingsXml.Accuracy(root, "DeltaAccuracy", TimeAccuracy.Tenths),
            DropDecimals = ComponentSettingsXml.Bool(root, "DropDecimals", true),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
            ShowPossibleTimeSave = ComponentSettingsXml.Bool(root, "ShowPossibleTimeSave", false),
            TimeSaveAccuracy = ComponentSettingsXml.Accuracy(root, "TimeSaveAccuracy", TimeAccuracy.Tenths),
            Comparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison"),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            TextColor = ComponentSettingsXml.Color(root, "TextColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(255, 28, 28, 28)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(255, 13, 13, 13)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Vertical"),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.5");
        ComponentSettingsXml.SetText(doc, parent, "DeltaAccuracy", DeltaAccuracy.ToString());
        ComponentSettingsXml.SetText(doc, parent, "DropDecimals", DropDecimals.ToString());
        ComponentSettingsXml.SetText(doc, parent, "Display2Rows", Display2Rows.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowPossibleTimeSave", ShowPossibleTimeSave.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimeSaveAccuracy", TimeSaveAccuracy.ToString());
        ComponentSettingsXml.SetText(doc, parent, "Comparison", Comparison);
        ComponentSettingsXml.SetText(doc, parent, "OverrideTextColor", OverrideTextColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TextColor", TextColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
    });
}
