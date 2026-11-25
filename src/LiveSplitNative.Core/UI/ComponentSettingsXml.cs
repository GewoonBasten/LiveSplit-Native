using System.Globalization;
using System.Xml;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.UI;

/// <summary>Shared helpers for component Settings InnerXml used by .lsl components.</summary>
public static class ComponentSettingsXml
{
    public static XmlElement LoadRoot(string? innerXml)
    {
        var document = new XmlDocument();
        if (string.IsNullOrWhiteSpace(innerXml))
        {
            return document.CreateElement("Settings");
        }

        // SettingsInnerXml is the *inner* content of <Settings>, wrap for parse.
        document.LoadXml("<Settings>" + innerXml + "</Settings>");
        return document.DocumentElement!;
    }

    public static string ToInnerXml(Action<XmlDocument, XmlElement> write)
    {
        var document = new XmlDocument();
        XmlElement root = document.CreateElement("Settings");
        document.AppendChild(root);
        write(document, root);
        return root.InnerXml;
    }

    public static void SetText(XmlDocument document, XmlElement parent, string name, string value)
    {
        XmlElement el = document.CreateElement(name);
        el.InnerText = value ?? "";
        parent.AppendChild(el);
    }

    public static bool Bool(XmlElement root, string name, bool fallback) =>
        root[name] is { } el && bool.TryParse(el.InnerText, out bool v) ? v : fallback;

    public static int Int(XmlElement root, string name, int fallback) =>
        root[name] is { } el && int.TryParse(el.InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : fallback;

    public static float Float(XmlElement root, string name, float fallback) =>
        root[name] is { } el && float.TryParse(el.InnerText, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            ? v
            : fallback;

    public static string String(XmlElement root, string name, string fallback) =>
        root[name]?.InnerText ?? fallback;

    public static ArgbColor Color(XmlElement root, string name, ArgbColor fallback) =>
        ArgbColor.ParseHex(root[name]?.InnerText, fallback);

    public static TimeAccuracy Accuracy(XmlElement root, string name, TimeAccuracy fallback)
    {
        string text = root[name]?.InnerText ?? "";
        return Enum.TryParse(text, ignoreCase: true, out TimeAccuracy value) ? value : fallback;
    }
}
