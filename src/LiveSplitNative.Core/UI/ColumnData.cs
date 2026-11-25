using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>Port of LiveSplit.UI.ColumnData for Splits component columns.</summary>
public sealed class ColumnData
{
    public ColumnData(string name, ColumnType type, string comparison, string timingMethod)
    {
        Name = name;
        Type = type;
        Comparison = comparison;
        TimingMethod = timingMethod;
    }

    public string Name { get; set; }
    public ColumnType Type { get; set; }
    public string Comparison { get; set; }
    public string TimingMethod { get; set; }

    public static ColumnData FromXml(XmlElement element) => new(
        element["Name"]?.InnerText ?? "",
        Enum.TryParse(element["Type"]?.InnerText, ignoreCase: true, out ColumnType type) ? type : ColumnType.SplitTime,
        element["Comparison"]?.InnerText ?? "Current Comparison",
        element["TimingMethod"]?.InnerText ?? "Current Timing Method");

    public void WriteXml(XmlDocument document, XmlElement parent)
    {
        ComponentSettingsXml.SetText(document, parent, "Version", "1.5");
        ComponentSettingsXml.SetText(document, parent, "Name", Name);
        ComponentSettingsXml.SetText(document, parent, "Type", Type.ToString());
        ComponentSettingsXml.SetText(document, parent, "Comparison", Comparison);
        ComponentSettingsXml.SetText(document, parent, "TimingMethod", TimingMethod);
    }

    public ColumnData Clone() => new(Name, Type, Comparison, TimingMethod);

    public static IReadOnlyList<ColumnData> CreateDefault() =>
    [
        new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method"),
        // SplitsSettings(): the second column is named T("Time").
        new ColumnData(Localization.UiText.T("Time"), ColumnType.SplitTime, "Current Comparison", "Current Timing Method"),
    ];
}
