using System.Xml;

namespace LiveSplitNative.Core.Racing;

/// <summary>
/// LiveSplit.Options.RaceProviderSettings: a racing service's entry in settings.cfg
/// &lt;RaceProviderPlugins&gt;&lt;Plugin name="…" enabled="…"&gt;…&lt;/Plugin&gt;, listed in Manage
/// Racing Services.
/// </summary>
public abstract class RaceProviderSettings
{
    public bool Enabled { get; set; } = true;

    public abstract string Name { get; }

    public abstract string DisplayName { get; }

    public abstract string WebsiteLink { get; }

    public abstract string RulesLink { get; }

    /// <summary>
    /// What GetSettingsControl shows when the service has no options (the base class's label, and
    /// RacetimeSettingsControl's).
    /// </summary>
    public const string NoOptionsText = "There are no options available for this racing service.";

    /// <summary>UnloadedRaceProviderSettings.GetSettingsControl (red).</summary>
    public const string NotLoadedText = "Plugin could not be loaded";

    public abstract RaceProviderSettings Clone();

    /// <summary>
    /// The factories Windows knows (ComponentManager.RaceProviderFactories): the race provider
    /// components (LiveSplit.Racetime.dll), then SRL, which TimerForm adds itself.
    /// </summary>
    public static IReadOnlyList<(string Name, Func<RaceProviderSettings> Create)> Factories { get; } =
    [
        (RacetimeSettings.PluginName, () => new RacetimeSettings()),
        (SrlSettings.PluginName, () => new SrlSettings()),
    ];

    /// <summary>StandardSettingsFactory: each factory's settings.</summary>
    public static List<RaceProviderSettings> CreateStandard() => [.. Factories.Select(f => f.Create())];

    /// <summary>
    /// XMLSettingsFactory: each &lt;Plugin&gt; in the file's order (a name no factory has is
    /// kept unloaded), then the settings of every factory the file did not have.
    /// </summary>
    public static List<RaceProviderSettings> Load(XmlElement? plugins)
    {
        var providers = new List<RaceProviderSettings>();
        foreach (XmlElement plugin in plugins?.ChildNodes.OfType<XmlElement>() ?? [])
        {
            string name = plugin.GetAttribute("name");
            RaceProviderSettings settings = Factories.FirstOrDefault(f => f.Name == name).Create?.Invoke()
                ?? new UnloadedRaceProviderSettings();
            settings.FromXml(plugin);
            providers.Add(settings);
        }

        foreach ((_, Func<RaceProviderSettings> create) in Factories)
        {
            RaceProviderSettings settings = create();
            if (!providers.Any(p => p.GetType() == settings.GetType()))
            {
                providers.Add(settings);
            }
        }

        return providers;
    }

    public virtual void FromXml(XmlElement element) =>
        Enabled = !bool.TryParse(element.GetAttribute("enabled"), out bool enabled) || enabled;

    public virtual XmlElement ToXml(XmlDocument document)
    {
        XmlElement plugin = document.CreateElement("Plugin");
        plugin.SetAttribute("name", Name);
        plugin.SetAttribute("enabled", Enabled.ToString());
        return plugin;
    }
}

/// <summary>LiveSplit.Web.SRL.SRLSettings.</summary>
public sealed class SrlSettings : RaceProviderSettings
{
    public const string PluginName = "SRL";

    public override string Name => PluginName;

    public override string DisplayName => "SRL";

    public override string WebsiteLink => "https://www.speedrunslive.com";

    public override string RulesLink => "https://www.speedrunslive.com/rules-faq/rules";

    public override RaceProviderSettings Clone() => new SrlSettings { Enabled = Enabled };
}

/// <summary>
/// UnloadedRaceProviderSettings: a plugin this app does not have, written back with its name and
/// content as they were.
/// </summary>
public sealed class UnloadedRaceProviderSettings : RaceProviderSettings
{
    private string _name = "";
    private string _content = "";

    public override string Name => _name;

    public override string DisplayName => _name;

    public override string WebsiteLink => "";

    public override string RulesLink => "";

    public override RaceProviderSettings Clone() =>
        new UnloadedRaceProviderSettings { Enabled = Enabled, _name = _name, _content = _content };

    public override void FromXml(XmlElement element)
    {
        base.FromXml(element);
        _content = element.InnerXml;
        _name = element.GetAttribute("name");
    }

    public override XmlElement ToXml(XmlDocument document)
    {
        XmlElement plugin = base.ToXml(document);
        plugin.InnerXml = _content;
        return plugin;
    }
}
