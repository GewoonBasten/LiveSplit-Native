using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// LiveSplit.CurrentComparison CurrentComparisonSettings (settings version 1.4). Text Color is
/// the "Comparing Against" label, Time Color the comparison's name.
/// </summary>
public sealed class CurrentComparisonComponentSettings : IInfoComponentSettings
{
    public ArgbColor TextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTextColor { get; set; }
    public ArgbColor TimeColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTimeColor { get; set; }
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";
    public bool Display2Rows { get; set; }

    public static CurrentComparisonComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new CurrentComparisonComponentSettings
        {
            TextColor = ComponentSettingsXml.Color(root, "TextColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            TimeColor = ComponentSettingsXml.Color(root, "TimeColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTimeColor = ComponentSettingsXml.Bool(root, "OverrideTimeColor", false),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.4");
        ComponentSettingsXml.SetText(doc, parent, "TextColor", TextColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTextColor", OverrideTextColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimeColor", TimeColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTimeColor", OverrideTimeColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "Display2Rows", Display2Rows.ToString());
    });
}
