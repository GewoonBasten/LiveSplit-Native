using System.Globalization;
using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>LiveSplit.Graph GraphSettings (settings version 1.5; 1.0-1.1 had one partial and one complete fill color).</summary>
public sealed class GraphComponentSettings
{
    public float GraphHeight { get; set; } = 120;
    public float GraphWidth { get; set; } = 180;
    public ArgbColor BehindGraphColor { get; set; } = ArgbColor.FromArgb(255, 115, 40, 40);
    public ArgbColor AheadGraphColor { get; set; } = ArgbColor.FromArgb(255, 40, 115, 52);
    public ArgbColor GridlinesColor { get; set; } = ArgbColor.FromArgb(0x50, 0, 0, 0);
    public ArgbColor PartialFillColorBehind { get; set; } = ArgbColor.FromArgb(25, 255, 255, 255);
    public ArgbColor CompleteFillColorBehind { get; set; } = ArgbColor.FromArgb(50, 255, 255, 255);
    public ArgbColor PartialFillColorAhead { get; set; } = ArgbColor.FromArgb(25, 255, 255, 255);
    public ArgbColor CompleteFillColorAhead { get; set; } = ArgbColor.FromArgb(50, 255, 255, 255);
    public ArgbColor GraphColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor ShadowsColor { get; set; } = ArgbColor.FromArgb(0x38, 0, 0, 0);
    public ArgbColor GraphLinesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool IsLiveGraph { get; set; } = true;
    public bool FlipGraph { get; set; }
    public string Comparison { get; set; } = "Current Comparison";
    public bool ShowBestSegments { get; set; }

    /// <summary>Best Segment Color (a layout without it reads as Color.Gold).</summary>
    public ArgbColor GraphGoldColor { get; set; } = ArgbColor.FromArgb(255, 216, 175, 31);

    public static GraphComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        var settings = new GraphComponentSettings();
        if (root.ChildNodes.Count == 0)
        {
            return settings;
        }

        var white = ArgbColor.FromArgb(255, 255, 255, 255);
        var transparent = ArgbColor.FromArgb(0, 0, 0, 0);
        settings.GraphHeight = ComponentSettingsXml.Float(root, "Height", 120);
        settings.GraphWidth = ComponentSettingsXml.Float(root, "Width", 180);
        settings.BehindGraphColor = ComponentSettingsXml.Color(root, "BehindGraphColor", transparent);
        settings.AheadGraphColor = ComponentSettingsXml.Color(root, "AheadGraphColor", transparent);
        settings.GridlinesColor = ComponentSettingsXml.Color(root, "GridlinesColor", transparent);
        settings.FlipGraph = ComponentSettingsXml.Bool(root, "FlipGraph", false);
        settings.Comparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison");
        settings.ShowBestSegments = ComponentSettingsXml.Bool(root, "ShowBestSegments", false);
        settings.GraphGoldColor = ComponentSettingsXml.Color(root, "GraphGoldColor", ArgbColor.FromArgb(255, 255, 215, 0));
        settings.GraphColor = ComponentSettingsXml.Color(root, "GraphColor", white);
        settings.ShadowsColor = ComponentSettingsXml.Color(root, "ShadowsColor", transparent);
        settings.GraphLinesColor = ComponentSettingsXml.Color(root, "GraphLinesColor", white);
        settings.IsLiveGraph = ComponentSettingsXml.Bool(root, "LiveGraph", false);
        if (Version.TryParse(root["Version"]?.InnerText, out Version? version) && version >= new Version(1, 2))
        {
            settings.PartialFillColorBehind = ComponentSettingsXml.Color(root, "PartialFillColorBehind", transparent);
            settings.CompleteFillColorBehind = ComponentSettingsXml.Color(root, "CompleteFillColorBehind", transparent);
            settings.PartialFillColorAhead = ComponentSettingsXml.Color(root, "PartialFillColorAhead", transparent);
            settings.CompleteFillColorAhead = ComponentSettingsXml.Color(root, "CompleteFillColorAhead", transparent);
        }
        else
        {
            settings.PartialFillColorAhead = settings.PartialFillColorBehind = ComponentSettingsXml.Color(root, "PartialFillColor", transparent);
            settings.CompleteFillColorAhead = settings.CompleteFillColorBehind = ComponentSettingsXml.Color(root, "CompleteFillColor", transparent);
        }

        return settings;
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.5");
        ComponentSettingsXml.SetText(doc, parent, "Height", GraphHeight.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "Width", GraphWidth.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "BehindGraphColor", BehindGraphColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "AheadGraphColor", AheadGraphColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "GridlinesColor", GridlinesColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "PartialFillColorBehind", PartialFillColorBehind.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "CompleteFillColorBehind", CompleteFillColorBehind.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "PartialFillColorAhead", PartialFillColorAhead.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "CompleteFillColorAhead", CompleteFillColorAhead.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "GraphColor", GraphColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "ShadowsColor", ShadowsColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "GraphLinesColor", GraphLinesColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "LiveGraph", IsLiveGraph.ToString());
        ComponentSettingsXml.SetText(doc, parent, "FlipGraph", FlipGraph.ToString());
        ComponentSettingsXml.SetText(doc, parent, "Comparison", Comparison);
        ComponentSettingsXml.SetText(doc, parent, "ShowBestSegments", ShowBestSegments.ToString());
        ComponentSettingsXml.SetText(doc, parent, "GraphGoldColor", GraphGoldColor.ToHex());
    });
}
