using System.Globalization;
using System.Xml;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.Core.UI;

/// <summary>Port of LiveSplit.DetailedTimer DetailedTimerSettings (v1.5).</summary>
public sealed class DetailedTimerComponentSettings
{
    public float Height { get; set; } = 75f;
    public float Width { get; set; } = 200f;
    public float SegmentTimerSizeRatio { get; set; } = 40f;
    public bool TimerShowGradient { get; set; } = true;
    public bool SegmentTimerShowGradient { get; set; } = true;
    public bool OverrideTimerColors { get; set; }
    public bool ShowSplitName { get; set; }
    public bool DisplayIcon { get; set; }
    public float IconSize { get; set; } = 40f;
    public float DecimalsSize { get; set; } = 35f;
    public float SegmentTimerDecimalsSize { get; set; } = 35f;
    public TimeAccuracy Accuracy { get; set; } = TimeAccuracy.Hundredths;
    public TimeAccuracy SegmentAccuracy { get; set; } = TimeAccuracy.Hundredths;
    public TimeAccuracy SegmentTimesAccuracy { get; set; } = TimeAccuracy.Hundredths;
    public ArgbColor TimerColor { get; set; } = ArgbColor.FromArgb(255, 170, 170, 170);
    public ArgbColor SegmentTimerColor { get; set; } = ArgbColor.FromArgb(255, 170, 170, 170);
    public ArgbColor SegmentLabelsColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor SegmentTimesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor SplitNameColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string BackgroundGradient { get; set; } = "Plain";
    public string DigitsFormat { get; set; } = "1";
    public string SegmentDigitsFormat { get; set; } = "1";
    public string TimingMethod { get; set; } = "Current Timing Method";
    public string Comparison { get; set; } = "Current Comparison";
    public string Comparison2 { get; set; } = "Best Segments";
    public bool HideComparison { get; set; }

    // SegmentLabelsFont / SegmentTimesFont / SplitNameFont (System.Drawing.Font: family, size in
    // pixels, Bold / Italic), saved as Windows saves them (WindowsFontBlob).
    public string SegmentLabelsFontFamily { get; set; } = "Segoe UI";
    public float SegmentLabelsFontSizePx { get; set; } = 13f;
    public bool SegmentLabelsFontBold { get; set; }
    public bool SegmentLabelsFontItalic { get; set; }
    public string SegmentTimesFontFamily { get; set; } = "Segoe UI";
    public float SegmentTimesFontSizePx { get; set; } = 13f;
    public bool SegmentTimesFontBold { get; set; } = true;
    public bool SegmentTimesFontItalic { get; set; }
    public string SplitNameFontFamily { get; set; } = "Segoe UI";
    public float SplitNameFontSizePx { get; set; } = 15f;
    public bool SplitNameFontBold { get; set; }
    public bool SplitNameFontItalic { get; set; }

    /// <summary>True when a font element was there but could not be read (the default was used).</summary>
    public bool UsedPortableFontFallback { get; private set; }

    public string TimerFormat
    {
        get => DetailedTimerLogic.CombineTimerFormat(DigitsFormat, Accuracy);
        set
        {
            DetailedTimerLogic.SplitTimerFormat(value, out string digits, out string accuracy);
            DigitsFormat = digits;
            Accuracy = DetailedTimerLogic.ParseAccuracyToken(accuracy);
        }
    }

    public string SegmentTimerFormat
    {
        get => DetailedTimerLogic.CombineTimerFormat(SegmentDigitsFormat, SegmentAccuracy);
        set
        {
            DetailedTimerLogic.SplitTimerFormat(value, out string digits, out string accuracy);
            SegmentDigitsFormat = digits;
            SegmentAccuracy = DetailedTimerLogic.ParseAccuracyToken(accuracy);
        }
    }

    public static DetailedTimerComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        var settings = new DetailedTimerComponentSettings
        {
            Height = ComponentSettingsXml.Float(root, "Height", 75f),
            Width = ComponentSettingsXml.Float(root, "Width", 200f),
            SegmentTimerSizeRatio = ComponentSettingsXml.Float(root, "SegmentTimerSizeRatio", 40f),
            TimerShowGradient = ComponentSettingsXml.Bool(root, "TimerShowGradient", true),
            SegmentTimerShowGradient = ComponentSettingsXml.Bool(root, "SegmentTimerShowGradient", true),
            OverrideTimerColors = ComponentSettingsXml.Bool(root, "OverrideTimerColors", false),
            ShowSplitName = ComponentSettingsXml.Bool(root, "ShowSplitName", false),
            DisplayIcon = ComponentSettingsXml.Bool(root, "DisplayIcon", false),
            IconSize = ComponentSettingsXml.Float(root, "IconSize", 40f),
            DecimalsSize = ComponentSettingsXml.Float(root, "DecimalsSize", 35f),
            SegmentTimerDecimalsSize = ComponentSettingsXml.Float(root, "SegmentTimerDecimalsSize", 35f),
            TimerColor = ComponentSettingsXml.Color(root, "TimerColor", ArgbColor.FromArgb(255, 170, 170, 170)),
            SegmentTimerColor = ComponentSettingsXml.Color(root, "SegmentTimerColor", ArgbColor.FromArgb(255, 170, 170, 170)),
            SegmentLabelsColor = ComponentSettingsXml.Color(root, "SegmentLabelsColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            SegmentTimesColor = ComponentSettingsXml.Color(root, "SegmentTimesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            SplitNameColor = ComponentSettingsXml.Color(root, "SplitNameColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Plain"),
            TimingMethod = ComponentSettingsXml.String(root, "TimingMethod", "Current Timing Method"),
            Comparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison"),
            Comparison2 = ComponentSettingsXml.String(root, "Comparison2", "Best Segments"),
            HideComparison = ComponentSettingsXml.Bool(root, "HideComparison", false),
            SegmentTimesAccuracy = ComponentSettingsXml.Accuracy(root, "SegmentTimesAccuracy", TimeAccuracy.Hundredths),
        };

        if (root["TimerFormat"] is { } timerFormat)
        {
            settings.TimerFormat = timerFormat.InnerText;
        }
        else
        {
            settings.DigitsFormat = ComponentSettingsXml.String(root, "DigitsFormat", "1");
            settings.Accuracy = ComponentSettingsXml.Accuracy(root, "TimerAccuracy", TimeAccuracy.Hundredths);
        }

        if (root["SegmentTimerFormat"] is { } segmentFormat)
        {
            settings.SegmentTimerFormat = segmentFormat.InnerText;
        }
        else
        {
            settings.SegmentDigitsFormat = ComponentSettingsXml.String(root, "SegmentDigitsFormat", "1");
            settings.SegmentAccuracy = ComponentSettingsXml.Accuracy(root, "SegmentTimerAccuracy", TimeAccuracy.Hundredths);
        }

        settings.ReadPortableOrWindowsFont(root, "SegmentLabelsFont",
            family => settings.SegmentLabelsFontFamily = family,
            size => settings.SegmentLabelsFontSizePx = size,
            bold => settings.SegmentLabelsFontBold = bold,
            italic => settings.SegmentLabelsFontItalic = italic,
            "Segoe UI", 13f, false);
        settings.ReadPortableOrWindowsFont(root, "SegmentTimesFont",
            family => settings.SegmentTimesFontFamily = family,
            size => settings.SegmentTimesFontSizePx = size,
            bold => settings.SegmentTimesFontBold = bold,
            italic => settings.SegmentTimesFontItalic = italic,
            "Segoe UI", 13f, true);
        settings.ReadPortableOrWindowsFont(root, "SplitNameFont",
            family => settings.SplitNameFontFamily = family,
            size => settings.SplitNameFontSizePx = size,
            bold => settings.SplitNameFontBold = bold,
            italic => settings.SplitNameFontItalic = italic,
            "Segoe UI", 15f, false);

        return settings;
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.5");
        ComponentSettingsXml.SetText(doc, parent, "Height", Height.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "Width", Width.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "SegmentTimerSizeRatio", SegmentTimerSizeRatio.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "TimerShowGradient", TimerShowGradient.ToString());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTimerColors", OverrideTimerColors.ToString());
        ComponentSettingsXml.SetText(doc, parent, "SegmentTimerShowGradient", SegmentTimerShowGradient.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimerFormat", TimerFormat);
        ComponentSettingsXml.SetText(doc, parent, "SegmentTimerFormat", SegmentTimerFormat);
        ComponentSettingsXml.SetText(doc, parent, "SegmentTimesAccuracy", SegmentTimesAccuracy.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimerColor", TimerColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "SegmentTimerColor", SegmentTimerColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "SegmentLabelsColor", SegmentLabelsColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "SegmentTimesColor", SegmentTimesColor.ToHex());
        WriteFont(doc, parent, "SegmentLabelsFont", SegmentLabelsFontFamily, SegmentLabelsFontSizePx, SegmentLabelsFontBold, SegmentLabelsFontItalic);
        WriteFont(doc, parent, "SegmentTimesFont", SegmentTimesFontFamily, SegmentTimesFontSizePx, SegmentTimesFontBold, SegmentTimesFontItalic);
        WriteFont(doc, parent, "SplitNameFont", SplitNameFontFamily, SplitNameFontSizePx, SplitNameFontBold, SplitNameFontItalic);
        ComponentSettingsXml.SetText(doc, parent, "DisplayIcon", DisplayIcon.ToString());
        ComponentSettingsXml.SetText(doc, parent, "IconSize", IconSize.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "ShowSplitName", ShowSplitName.ToString());
        ComponentSettingsXml.SetText(doc, parent, "SplitNameColor", SplitNameColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "Comparison", Comparison);
        ComponentSettingsXml.SetText(doc, parent, "Comparison2", Comparison2);
        ComponentSettingsXml.SetText(doc, parent, "HideComparison", HideComparison.ToString());
        ComponentSettingsXml.SetText(doc, parent, "TimingMethod", TimingMethod);
        ComponentSettingsXml.SetText(doc, parent, "DecimalsSize", DecimalsSize.ToString(CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "SegmentTimerDecimalsSize", SegmentTimerDecimalsSize.ToString(CultureInfo.InvariantCulture));
    });

    /// <summary>
    /// SettingsHelper.GetFontFromElement: the Windows font blob (WindowsFontBlob); files from
    /// earlier LiveSplitNative builds have Family / Size / Bold elements instead.
    /// </summary>
    private void ReadPortableOrWindowsFont(
        XmlElement root,
        string elementName,
        Action<string> setFamily,
        Action<float> setSize,
        Action<bool> setBold,
        Action<bool> setItalic,
        string defaultFamily,
        float defaultSize,
        bool defaultBold)
    {
        setFamily(defaultFamily);
        setSize(defaultSize);
        setBold(defaultBold);
        setItalic(false);
        XmlElement? el = root[elementName];
        if (el is null || el.IsEmpty)
        {
            return;
        }

        string? family = el["Family"]?.InnerText;
        if (!string.IsNullOrWhiteSpace(family))
        {
            setFamily(family);
            setSize(ComponentSettingsXml.Float(el, "Size", defaultSize));
            setBold(ComponentSettingsXml.Bool(el, "Bold", defaultBold));
            setItalic(ComponentSettingsXml.Bool(el, "Italic", false));
            return;
        }

        string blob = el.InnerText?.Trim() ?? "";
        if (WindowsFontBlob.TryRead(blob, out WindowsFontBlob.Font font))
        {
            setFamily(font.Family);
            setSize(font.SizePx);
            setBold(font.IsBold);
            setItalic(font.IsItalic);
            return;
        }

        if (blob.Length > 20 && WindowsFontBlobParser.TryParse(blob, out var parsed))
        {
            setFamily(parsed.Family);
            setSize(parsed.SizePx);
            setBold(parsed.Bold);
            return;
        }

        if (blob.Length > 0)
        {
            UsedPortableFontFallback = true;
        }
    }

    /// <summary>SettingsHelper.CreateSetting(Font): the font as Windows saves it (pixels).</summary>
    private static void WriteFont(
        XmlDocument doc,
        XmlElement parent,
        string name,
        string family,
        float sizePx,
        bool bold,
        bool italic)
    {
        XmlElement el = doc.CreateElement(name);
        el.InnerXml = WindowsFontBlob.ToElementInnerXml(family, sizePx, bold, italic);
        parent.AppendChild(el);
    }
}
