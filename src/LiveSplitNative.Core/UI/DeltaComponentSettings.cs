using System.Xml;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.UI;

/// <summary>LiveSplit.Delta DeltaSettings (settings version 1.4). The value color comes from the delta.</summary>
public sealed class DeltaComponentSettings : IInfoComponentSettings
{
    public ArgbColor TextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTextColor { get; set; }
    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Tenths;
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";
    public string Comparison { get; set; } = "Current Comparison";
    public bool Display2Rows { get; set; }
    public bool DropDecimals { get; set; } = true;
    public bool OverrideText { get; set; }
    public bool DifferentialText { get; set; }
    public string CustomText { get; set; } = "";
    public string CustomTextAhead { get; set; } = "";

    public static DeltaComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new DeltaComponentSettings
        {
            TextColor = ComponentSettingsXml.Color(root, "TextColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            Accuracy = ComponentSettingsXml.Accuracy(root, "Accuracy", TimeAccuracy.Tenths),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            Comparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison"),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
            DropDecimals = ComponentSettingsXml.Bool(root, "DropDecimals", true),
            OverrideText = ComponentSettingsXml.Bool(root, "OverrideText", false),
            DifferentialText = ComponentSettingsXml.Bool(root, "DifferentialText", false),
            CustomText = ComponentSettingsXml.String(root, "CustomText", ""),
            CustomTextAhead = ComponentSettingsXml.String(root, "CustomTextAhead", ""),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.4");
        ComponentSettingsXml.SetText(doc, parent, "TextColor", TextColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTextColor", OverrideTextColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "Accuracy", Accuracy.ToString());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "Comparison", Comparison);
        ComponentSettingsXml.SetText(doc, parent, "Display2Rows", Display2Rows.ToString());
        ComponentSettingsXml.SetText(doc, parent, "DropDecimals", DropDecimals.ToString());
        ComponentSettingsXml.SetText(doc, parent, "OverrideText", OverrideText.ToString());
        ComponentSettingsXml.SetText(doc, parent, "DifferentialText", DifferentialText.ToString());
        ComponentSettingsXml.SetText(doc, parent, "CustomText", CustomText);
        ComponentSettingsXml.SetText(doc, parent, "CustomTextAhead", CustomTextAhead);
    });
}
