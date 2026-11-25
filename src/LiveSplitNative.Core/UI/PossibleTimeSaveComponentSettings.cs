using System.Xml;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.UI;

/// <summary>Portable subset of LiveSplit.PossibleTimeSave settings.</summary>
public sealed class PossibleTimeSaveComponentSettings
{
    public ArgbColor TextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTextColor { get; set; }
    public ArgbColor TimeColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTimeColor { get; set; }
    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Hundredths;
    public bool DropDecimals { get; set; }
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";
    public string Comparison { get; set; } = "Current Comparison";
    public bool Display2Rows { get; set; }
    public bool TotalTimeSave { get; set; }

    public static PossibleTimeSaveComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new PossibleTimeSaveComponentSettings
        {
            TextColor = ComponentSettingsXml.Color(root, "TextColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            TimeColor = ComponentSettingsXml.Color(root, "TimeColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTimeColor = ComponentSettingsXml.Bool(root, "OverrideTimeColor", false),
            Accuracy = ComponentSettingsXml.Accuracy(root, "Accuracy", TimeAccuracy.Hundredths),
            DropDecimals = ComponentSettingsXml.Bool(root, "DropDecimals", false),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            Comparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison"),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
            TotalTimeSave = ComponentSettingsXml.Bool(root, "TotalTimeSave", false),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.4");
        ComponentSettingsXml.SetText(doc, parent, "TextColor", TextColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTextColor", OverrideTextColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimeColor", TimeColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTimeColor", OverrideTimeColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "Accuracy", Accuracy.ToString());
        ComponentSettingsXml.SetText(doc, parent, "DropDecimals", DropDecimals.ToString());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "Comparison", Comparison);
        ComponentSettingsXml.SetText(doc, parent, "Display2Rows", Display2Rows.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TotalTimeSave", TotalTimeSave.ToString());
    });
}
