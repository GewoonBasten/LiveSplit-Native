using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>Portable subset of LiveSplit.BlankSpace BlankSpaceSettings.</summary>
public sealed class BlankSpaceComponentSettings
{
    public float SpaceHeight { get; set; } = 24;
    public float SpaceWidth { get; set; } = 24;
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";

    public static BlankSpaceComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new BlankSpaceComponentSettings
        {
            SpaceHeight = ComponentSettingsXml.Float(root, "SpaceHeight", 24),
            SpaceWidth = ComponentSettingsXml.Float(root, "SpaceWidth", 24),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.7");
        ComponentSettingsXml.SetText(doc, parent, "SpaceHeight", SpaceHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "SpaceWidth", SpaceWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
    });
}
