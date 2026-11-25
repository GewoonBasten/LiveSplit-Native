using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>Portable fields from LiveSplit.Title TitleSettings (XML only; fonts opaque elsewhere).</summary>
public sealed class TitleComponentSettings
{
    public bool ShowGameName { get; set; } = true;
    public bool ShowCategoryName { get; set; } = true;
    public bool ShowAttemptCount { get; set; } = true;
    public bool ShowFinishedRunsCount { get; set; }
    public bool SingleLine { get; set; }
    public bool DisplayGameIcon { get; set; } = true;
    public bool ShowRegion { get; set; }
    public bool ShowPlatform { get; set; }
    public bool ShowVariables { get; set; } = true;
    public bool OverrideTitleColor { get; set; }
    public ArgbColor TitleColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(255, 42, 42, 42);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(255, 19, 19, 19);
    public string BackgroundGradient { get; set; } = "Vertical";
    public int TextAlignment { get; set; }

    public static TitleComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new TitleComponentSettings
        {
            ShowGameName = ComponentSettingsXml.Bool(root, "ShowGameName", true),
            ShowCategoryName = ComponentSettingsXml.Bool(root, "ShowCategoryName", true),
            ShowAttemptCount = ComponentSettingsXml.Bool(root, "ShowAttemptCount", true),
            ShowFinishedRunsCount = ComponentSettingsXml.Bool(root, "ShowFinishedRunsCount", false),
            SingleLine = ComponentSettingsXml.Bool(root, "SingleLine", false),
            DisplayGameIcon = ComponentSettingsXml.Bool(root, "DisplayGameIcon", true),
            ShowRegion = ComponentSettingsXml.Bool(root, "ShowRegion", false),
            ShowPlatform = ComponentSettingsXml.Bool(root, "ShowPlatform", false),
            ShowVariables = ComponentSettingsXml.Bool(root, "ShowVariables", true),
            OverrideTitleColor = ComponentSettingsXml.Bool(root, "OverrideTitleColor", false),
            TitleColor = ComponentSettingsXml.Color(root, "TitleColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(255, 42, 42, 42)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(255, 19, 19, 19)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Vertical"),
            TextAlignment = ComponentSettingsXml.Int(root, "TextAlignment", 0),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.7.3");
        ComponentSettingsXml.SetText(doc, parent, "ShowGameName", ShowGameName.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowCategoryName", ShowCategoryName.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowAttemptCount", ShowAttemptCount.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowFinishedRunsCount", ShowFinishedRunsCount.ToString());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTitleColor", OverrideTitleColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "SingleLine", SingleLine.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TitleColor", TitleColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "DisplayGameIcon", DisplayGameIcon.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowRegion", ShowRegion.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowPlatform", ShowPlatform.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowVariables", ShowVariables.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TextAlignment", TextAlignment.ToString());
    });
}
