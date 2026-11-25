using System.Globalization;
using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>The kinds of an auto splitter's setting values (asr-capi SettingValue_get_type 1-6).</summary>
public enum AsrValueKind
{
    Map = 1,
    List = 2,
    Bool = 3,
    I64 = 4,
    F64 = 5,
    String = 6,
}

/// <summary>
/// An auto splitter's setting value as LiveSplit.AutoSplittingRuntime keeps it in the layout: a
/// map (ordered id → value), a list, a bool, an i64, an f64 or a string.
/// </summary>
public sealed class AsrSettingValue
{
    private AsrSettingValue(AsrValueKind kind) => Kind = kind;

    public AsrValueKind Kind { get; }

    public IReadOnlyList<KeyValuePair<string, AsrSettingValue>> Map { get; private init; } = [];

    public IReadOnlyList<AsrSettingValue> List { get; private init; } = [];

    public bool Bool { get; private init; }

    public long I64 { get; private init; }

    public double F64 { get; private init; }

    public string String { get; private init; } = "";

    public static AsrSettingValue FromMap(IEnumerable<KeyValuePair<string, AsrSettingValue>> map) => new(AsrValueKind.Map) { Map = [.. map] };

    public static AsrSettingValue FromList(IEnumerable<AsrSettingValue> list) => new(AsrValueKind.List) { List = [.. list] };

    public static AsrSettingValue FromBool(bool value) => new(AsrValueKind.Bool) { Bool = value };

    public static AsrSettingValue FromI64(long value) => new(AsrValueKind.I64) { I64 = value };

    public static AsrSettingValue FromF64(double value) => new(AsrValueKind.F64) { F64 = value };

    public static AsrSettingValue FromString(string value) => new(AsrValueKind.String) { String = value };

    public static string KindName(AsrValueKind kind) => kind switch
    {
        AsrValueKind.Map => "map",
        AsrValueKind.List => "list",
        AsrValueKind.Bool => "bool",
        AsrValueKind.I64 => "i64",
        AsrValueKind.F64 => "f64",
        _ => "string",
    };

    /// <summary>The value as text, for comparing two settings maps.</summary>
    public override string ToString() => Kind switch
    {
        AsrValueKind.Map => "{" + string.Join(",", Map.Select(p => p.Key + "=" + p.Value)) + "}",
        AsrValueKind.List => "[" + string.Join(",", List) + "]",
        AsrValueKind.Bool => Bool.ToString(),
        AsrValueKind.I64 => I64.ToString(CultureInfo.InvariantCulture),
        AsrValueKind.F64 => F64.ToString("R", CultureInfo.InvariantCulture),
        _ => "\"" + String + "\"",
    };
}

/// <summary>
/// LiveSplit.AutoSplittingRuntime ComponentSettings (settings Version 1.0): the WebAssembly
/// auto splitter's ScriptPath and its settings map as &lt;CustomSettings&gt; of
/// &lt;Setting id="…" type="bool|i64|f64|string|map|list"&gt; elements.
/// </summary>
public sealed class AutoSplittingRuntimeSettings
{
    public string ScriptPath { get; set; } = "";

    /// <summary>The saved settings map, or null when there is none (or it could not be read).</summary>
    public IReadOnlyList<KeyValuePair<string, AsrSettingValue>>? CustomSettings { get; set; }

    public static AutoSplittingRuntimeSettings FromInnerXml(string? innerXml)
    {
        var settings = new AutoSplittingRuntimeSettings();
        if (string.IsNullOrWhiteSpace(innerXml))
        {
            return settings;
        }

        XmlElement? root = ComponentSettingsXml.LoadRoot(innerXml);
        if (root is null)
        {
            return settings;
        }

        settings.ScriptPath = root["ScriptPath"]?.InnerText ?? "";
        try
        {
            if (root["CustomSettings"] is { } custom)
            {
                settings.CustomSettings = ParseMap(custom);
            }
        }
        catch (Exception e) when (e is FormatException or OverflowException or NullReferenceException or InvalidCastException)
        {
            settings.CustomSettings = null;
        }

        return settings;
    }

    public string ToInnerXml()
    {
        var document = new XmlDocument();
        XmlElement parent = document.CreateElement("Settings");
        document.AppendChild(parent);
        ComponentSettingsXml.SetText(document, parent, "Version", "1.0");
        ComponentSettingsXml.SetText(document, parent, "ScriptPath", ScriptPath);
        XmlElement custom = document.CreateElement("CustomSettings");
        if (CustomSettings is { } map)
        {
            BuildMap(document, custom, map);
        }

        parent.AppendChild(custom);
        return parent.InnerXml;
    }

    private static void BuildMap(XmlDocument document, XmlElement parent, IEnumerable<KeyValuePair<string, AsrSettingValue>> map)
    {
        foreach ((string id, AsrSettingValue value) in map)
        {
            XmlElement element = BuildValue(document, value);
            element.Attributes.Prepend(Attribute(document, "id", id));
            parent.AppendChild(element);
        }
    }

    private static XmlElement BuildValue(XmlDocument document, AsrSettingValue value)
    {
        XmlElement element = document.CreateElement("Setting");
        element.Attributes.Append(Attribute(document, "type", AsrSettingValue.KindName(value.Kind)));
        switch (value.Kind)
        {
            case AsrValueKind.Map:
                BuildMap(document, element, value.Map);
                break;
            case AsrValueKind.List:
                foreach (AsrSettingValue item in value.List)
                {
                    element.AppendChild(BuildValue(document, item));
                }

                break;
            case AsrValueKind.Bool:
                element.InnerText = value.Bool.ToString(CultureInfo.InvariantCulture);
                break;
            case AsrValueKind.I64:
                element.InnerText = value.I64.ToString(CultureInfo.InvariantCulture);
                break;
            case AsrValueKind.F64:
                element.InnerText = value.F64.ToString(CultureInfo.InvariantCulture);
                break;
            default:
                element.Attributes.Append(Attribute(document, "value", value.String));
                break;
        }

        return element;
    }

    private static XmlAttribute Attribute(XmlDocument document, string name, string value)
    {
        XmlAttribute attribute = document.CreateAttribute(name);
        attribute.Value = value;
        return attribute;
    }

    // ParseCustomSettingsFromXml: anything that is not a well-formed setting makes the whole map unreadable.
    private static List<KeyValuePair<string, AsrSettingValue>>? ParseMap(XmlElement mapNode)
    {
        var map = new List<KeyValuePair<string, AsrSettingValue>>();
        foreach (XmlElement element in mapNode.ChildNodes.OfType<XmlElement>())
        {
            if (element.Name != "Setting" || element.Attributes["id"]?.Value is not { } id || ParseValue(element) is not { } value)
            {
                return null;
            }

            map.Add(new KeyValuePair<string, AsrSettingValue>(id, value));
        }

        return map;
    }

    private static AsrSettingValue? ParseValue(XmlElement element)
    {
        switch (element.Attributes["type"]?.Value)
        {
            case "bool":
                return AsrSettingValue.FromBool(bool.Parse(element.InnerText));
            case "i64":
                return AsrSettingValue.FromI64(long.Parse(element.InnerText, CultureInfo.InvariantCulture));
            case "f64":
                return AsrSettingValue.FromF64(double.Parse(element.InnerText, CultureInfo.InvariantCulture));
            case "string":
                return element.Attributes["value"]?.Value is { } text ? AsrSettingValue.FromString(text) : null;
            case "map":
                return ParseMap(element) is { } map ? AsrSettingValue.FromMap(map) : null;
            case "list":
                var list = new List<AsrSettingValue>();
                foreach (XmlElement item in element.ChildNodes.OfType<XmlElement>())
                {
                    if (item.Name != "Setting" || ParseValue(item) is not { } value)
                    {
                        return null;
                    }

                    list.Add(value);
                }

                return AsrSettingValue.FromList(list);
            default:
                return null;
        }
    }
}
