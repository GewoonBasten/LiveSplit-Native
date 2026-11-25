using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>LiveSplit.WorldRecord WorldRecordPrecisionType.</summary>
public enum WorldRecordPrecisionType
{
    FromLeaderboard,
    Seconds,
    Milliseconds,
}

/// <summary>LiveSplit.WorldRecord WorldRecordSettings (settings version 1.6).</summary>
public sealed class WorldRecordComponentSettings : IInfoComponentSettings
{
    public ArgbColor TextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTextColor { get; set; }
    public ArgbColor TimeColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTimeColor { get; set; }
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";
    public bool Display2Rows { get; set; }
    public bool CenteredText { get; set; } = true;
    public bool FilterRegion { get; set; }
    public bool FilterPlatform { get; set; }
    public bool FilterVariables { get; set; }
    public bool FilterSubcategories { get; set; } = true;

    /// <summary>"Default for Leaderboard", "Real Time", "Real Time Without Loads" or "Game Time".</summary>
    public string TimingMethod { get; set; } = "Default for Leaderboard";

    public WorldRecordPrecisionType PrecisionType { get; set; } = WorldRecordPrecisionType.FromLeaderboard;

    /// <summary>
    /// LiveSplitNative only (Basten 17 Oct, "Separate Time"): without Centered Text, the runners
    /// go with the name ("World Record by inkstar") and the time stands alone in the Times font,
    /// like the times above it. Written only when on, so the settings stay as Windows writes
    /// them otherwise (Windows skips the element).
    /// </summary>
    public bool SeparateTime { get; set; }

    public static WorldRecordComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        if (root.ChildNodes.Count == 0)
        {
            return new WorldRecordComponentSettings();
        }

        return new WorldRecordComponentSettings
        {
            TextColor = ComponentSettingsXml.Color(root, "TextColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            TimeColor = ComponentSettingsXml.Color(root, "TimeColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            OverrideTimeColor = ComponentSettingsXml.Bool(root, "OverrideTimeColor", false),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
            CenteredText = ComponentSettingsXml.Bool(root, "CenteredText", false),
            FilterRegion = ComponentSettingsXml.Bool(root, "FilterRegion", false),
            FilterPlatform = ComponentSettingsXml.Bool(root, "FilterPlatform", false),
            FilterVariables = ComponentSettingsXml.Bool(root, "FilterVariables", false),
            FilterSubcategories = ComponentSettingsXml.Bool(root, "FilterSubcategories", true),
            TimingMethod = ComponentSettingsXml.String(root, "TimingMethod", "Default for Leaderboard"),
            PrecisionType = Enum.TryParse(root["PrecisionType"]?.InnerText, out WorldRecordPrecisionType precision)
                ? precision
                : WorldRecordPrecisionType.FromLeaderboard,
            SeparateTime = ComponentSettingsXml.Bool(root, "SeparateTime", false)
                || ComponentSettingsXml.Bool(root, "RunnersWithName", false), // its first name (17 Oct)
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.6");
        ComponentSettingsXml.SetText(doc, parent, "TextColor", TextColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTextColor", OverrideTextColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimeColor", TimeColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTimeColor", OverrideTimeColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "Display2Rows", Display2Rows.ToString());
        ComponentSettingsXml.SetText(doc, parent, "CenteredText", CenteredText.ToString());
        ComponentSettingsXml.SetText(doc, parent, "FilterRegion", FilterRegion.ToString());
        ComponentSettingsXml.SetText(doc, parent, "FilterPlatform", FilterPlatform.ToString());
        ComponentSettingsXml.SetText(doc, parent, "FilterVariables", FilterVariables.ToString());
        ComponentSettingsXml.SetText(doc, parent, "FilterSubcategories", FilterSubcategories.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimingMethod", TimingMethod);
        ComponentSettingsXml.SetText(doc, parent, "PrecisionType", PrecisionType.ToString());
        if (SeparateTime)
        {
            ComponentSettingsXml.SetText(doc, parent, "SeparateTime", "True");
        }
    });
}
