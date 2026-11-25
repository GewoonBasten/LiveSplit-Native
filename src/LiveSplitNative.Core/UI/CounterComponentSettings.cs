using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// LiveSplit.Counter CounterComponentSettings (settings version 1.0). The hotkeys are kept as
/// Windows writes them (a <c>Keys</c> name such as "Add" or "Add, Control"; empty for none), so
/// the layout still opens on Windows. The legacy OverrideCounterFont / CounterFont elements are
/// read by Windows only to move them into font overrides and are not written there either.
/// </summary>
public sealed class CounterComponentSettings
{
    public bool GlobalHotkeysEnabled { get; set; }
    public bool OverrideTextColor { get; set; }
    public ArgbColor CounterTextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor CounterValueColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";
    public string CounterText { get; set; } = "Counter:";
    public int InitialValue { get; set; }
    public int Increment { get; set; } = 1;
    public string IncrementKey { get; set; } = "Add";
    public string DecrementKey { get; set; } = "Subtract";
    public string ResetKey { get; set; } = "NumPad0";

    public static CounterComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        return new CounterComponentSettings
        {
            GlobalHotkeysEnabled = ComponentSettingsXml.Bool(root, "GlobalHotkeysEnabled", false),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            CounterTextColor = ComponentSettingsXml.Color(root, "CounterTextColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            CounterValueColor = ComponentSettingsXml.Color(root, "CounterValueColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            CounterText = ComponentSettingsXml.String(root, "CounterText", "Counter:"),
            InitialValue = ComponentSettingsXml.Int(root, "InitialValue", 0),
            Increment = ComponentSettingsXml.Int(root, "Increment", 1),
            IncrementKey = ComponentSettingsXml.String(root, "IncrementKey", "Add"),
            DecrementKey = ComponentSettingsXml.String(root, "DecrementKey", "Subtract"),
            ResetKey = ComponentSettingsXml.String(root, "ResetKey", "NumPad0"),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.0");
        ComponentSettingsXml.SetText(doc, parent, "GlobalHotkeysEnabled", GlobalHotkeysEnabled.ToString());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTextColor", OverrideTextColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "CounterTextColor", CounterTextColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "CounterValueColor", CounterValueColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "CounterText", CounterText);
        ComponentSettingsXml.SetText(doc, parent, "InitialValue", InitialValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "Increment", Increment.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "IncrementKey", IncrementKey);
        ComponentSettingsXml.SetText(doc, parent, "DecrementKey", DecrementKey);
        ComponentSettingsXml.SetText(doc, parent, "ResetKey", ResetKey);
    });
}
