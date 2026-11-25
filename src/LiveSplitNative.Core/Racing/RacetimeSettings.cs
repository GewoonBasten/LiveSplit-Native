using System.Xml;

namespace LiveSplitNative.Core.Racing;

/// <summary>
/// LiveSplit.Racetime RacetimeSettings (a RaceProviderSettings), kept in settings.cfg as
/// &lt;RaceProviderPlugins&gt;&lt;Plugin name="LiveSplit.Racetime.dll" enabled="True"&gt;
/// &lt;LoadChatHistory&gt;…&lt;/LoadChatHistory&gt;&lt;HideResults&gt;…&lt;/HideResults&gt;&lt;/Plugin&gt;.
/// Its settings control only says there are no options.
/// </summary>
public sealed class RacetimeSettings : RaceProviderSettings
{
    public const string PluginName = "LiveSplit.Racetime.dll";

    public override string Name => PluginName;

    public override string DisplayName => "racetime.gg";

    public override string WebsiteLink => "https://racetime.gg";

    public override string RulesLink => "https://racetime.gg/about/rules";

    public bool LoadChatHistory { get; set; } = true;

    public bool HideResults { get; set; }

    public override RaceProviderSettings Clone() => new RacetimeSettings
    {
        Enabled = Enabled,
        LoadChatHistory = LoadChatHistory,
        HideResults = HideResults,
    };

    /// <summary>RacetimeSettings.FromXml (HideResults reads as True when missing, as upstream).</summary>
    public override void FromXml(XmlElement element)
    {
        base.FromXml(element);
        LoadChatHistory = Bool(element["LoadChatHistory"], true);
        HideResults = Bool(element["HideResults"], true);
    }

    public override XmlElement ToXml(XmlDocument document)
    {
        XmlElement plugin = base.ToXml(document);
        Append(document, plugin, "LoadChatHistory", LoadChatHistory.ToString());
        Append(document, plugin, "HideResults", HideResults.ToString());
        return plugin;
    }

    private static bool Bool(XmlElement? element, bool fallback) =>
        element is not null && bool.TryParse(element.InnerText, out bool value) ? value : fallback;

    private static void Append(XmlDocument document, XmlElement parent, string name, string value)
    {
        XmlElement element = document.CreateElement(name);
        element.InnerText = value;
        parent.AppendChild(element);
    }
}
