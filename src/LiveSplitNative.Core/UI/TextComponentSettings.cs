using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>Portable subset of LiveSplit.Text TextComponentSettings (v1.5).</summary>
public sealed class TextComponentSettings
{
    public ArgbColor TextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTextColor { get; set; }
    public ArgbColor TimeColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTimeColor { get; set; }
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";
    public string Text1 { get; set; } = "Text";
    public string Text2 { get; set; } = "";
    public bool Display2Rows { get; set; }
    public bool CustomVariable { get; set; }

    public static TextComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new TextComponentSettings
        {
            TextColor = ComponentSettingsXml.Color(root, "TextColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            TimeColor = ComponentSettingsXml.Color(root, "TimeColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTimeColor = ComponentSettingsXml.Bool(root, "OverrideTimeColor", false),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            Text1 = ComponentSettingsXml.String(root, "Text1", "Text"),
            Text2 = ComponentSettingsXml.String(root, "Text2", ""),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
            CustomVariable = ComponentSettingsXml.Bool(root, "CustomVariable", false),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.5");
        ComponentSettingsXml.SetText(doc, parent, "TextColor", TextColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTextColor", OverrideTextColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimeColor", TimeColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTimeColor", OverrideTimeColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "Text1", Text1);
        ComponentSettingsXml.SetText(doc, parent, "Text2", Text2);
        ComponentSettingsXml.SetText(doc, parent, "Display2Rows", Display2Rows.ToString());
        ComponentSettingsXml.SetText(doc, parent, "CustomVariable", CustomVariable.ToString());
    });
}
