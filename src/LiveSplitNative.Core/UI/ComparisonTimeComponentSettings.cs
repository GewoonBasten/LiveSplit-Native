using System.Xml;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.UI;

/// <summary>LiveSplit.ComparisonTime TimeType: which time of the comparison is shown.</summary>
public enum ComparisonTimeType
{
    FinalTime,
    SplitTime,
    SegmentTime,
}

/// <summary>LiveSplit.ComparisonTime ComparisonTimeSettings (settings version 1.4).</summary>
public sealed class ComparisonTimeComponentSettings : IInfoComponentSettings
{
    public ArgbColor TextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTextColor { get; set; }
    public ArgbColor TimeColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTimeColor { get; set; }
    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Seconds;
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";
    public string Comparison { get; set; } = "Current Comparison";

    /// <summary>"Current Timing Method", "Real Time" or "Game Time".</summary>
    public string TimingMethod { get; set; } = "Current Timing Method";

    public bool Display2Rows { get; set; }
    public ComparisonTimeType Type { get; set; } = ComparisonTimeType.FinalTime;

    public static ComparisonTimeComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new ComparisonTimeComponentSettings
        {
            TextColor = ComponentSettingsXml.Color(root, "TextColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            TimeColor = ComponentSettingsXml.Color(root, "TimeColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTimeColor = ComponentSettingsXml.Bool(root, "OverrideTimeColor", false),
            Accuracy = ComponentSettingsXml.Accuracy(root, "Accuracy", TimeAccuracy.Seconds),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            Comparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison"),
            TimingMethod = ComponentSettingsXml.String(root, "TimingMethod", "Current Timing Method"),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
            Type = Enum.TryParse(root["Type"]?.InnerText, ignoreCase: true, out ComparisonTimeType type)
                ? type
                : ComparisonTimeType.FinalTime,
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
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "Comparison", Comparison);
        ComponentSettingsXml.SetText(doc, parent, "TimingMethod", TimingMethod);
        ComponentSettingsXml.SetText(doc, parent, "Display2Rows", Display2Rows.ToString());
        ComponentSettingsXml.SetText(doc, parent, "Type", Type.ToString());
    });
}
