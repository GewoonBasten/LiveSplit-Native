using System.Globalization;
using System.Xml;
using LiveSplitNative.Core.Imaging;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Serialization;

/// <summary>
/// Load/save LiveSplit .lsl XML (settings + component paths).
/// Font BinaryFormatter blobs and component Settings InnerXml are preserved when present.
/// </summary>
public static class XmlLayoutSerializer
{
    /// <summary>The &lt;Layout&gt; elements XMLLayoutSaver writes.</summary>
    private static readonly HashSet<string> KnownRootElements = new(StringComparer.Ordinal)
    {
        "Mode", "X", "Y", "VerticalWidth", "VerticalHeight", "HorizontalWidth", "HorizontalHeight", "Settings", "Components",
    };

    /// <summary>
    /// The layout &lt;Settings&gt; Windows reads or writes (with the pre-1.3 font names and the
    /// pre-1.6.1 gradient, which it converts) and LiveSplitNative's Component Spacing.
    /// </summary>
    private static readonly HashSet<string> KnownSettingElements = new(StringComparer.Ordinal)
    {
        "TextColor", "BackgroundColor", "BackgroundColor2", "ThinSeparatorsColor", "SeparatorsColor",
        "PersonalBestColor", "AheadGainingTimeColor", "AheadLosingTimeColor", "BehindGainingTimeColor",
        "BehindLosingTimeColor", "BestSegmentColor", "UseRainbowColor", "NotRunningColor", "PausedColor",
        "TextOutlineColor", "ShadowsColor", "TimesFont", "TimerFont", "TextFont", "AlwaysOnTop",
        "ShowBestSegments", "AntiAliasing", "DropShadows", "BackgroundType", "BackgroundImage",
        "ImageOpacity", "ImageBlur", "Opacity", "MousePassThroughWhileRunning", "AllowResizing",
        "AllowMoving", "MainFont", "SplitNamesFont", "BackgroundGradient", "ComponentSpacing", "BackgroundColorType",
    };

    /// <summary>
    /// XMLLayoutFactory.Create: settings by the layout's version (fonts named MainFont and
    /// SplitNamesFont before 1.3, BackgroundGradient before 1.6.1), Windows' values for missing
    /// elements, and a value Windows cannot read makes the layout unreadable
    /// (<see cref="InvalidDataException"/>). Components Windows does not have are kept, and so
    /// are elements that are not LiveSplit's.
    /// </summary>
    public static Layout Load(Stream stream)
    {
        var document = new XmlDocument();
        document.Load(stream);
        XmlElement root = document.DocumentElement
            ?? throw new InvalidDataException("Missing Layout root element.");
        if (!string.Equals(root.Name, "Layout", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected <Layout>, got <{root.Name}>.");
        }

        Version version = root.HasAttribute("version") ? ParseVersion(root.GetAttribute("version")) : new Version(1, 0, 0, 0);
        XmlElement settings = root["Settings"] ?? throw new InvalidDataException("The layout has no <Settings>.");
        XmlElement components = root["Components"] ?? throw new InvalidDataException("The layout has no <Components>.");
        var layout = new Layout
        {
            Version = root.HasAttribute("version") ? root.GetAttribute("version") : null,
            X = ParseInt(root["X"]),
            Y = ParseInt(root["Y"]),
            VerticalWidth = ParseInt(root["VerticalWidth"]),
            VerticalHeight = ParseInt(root["VerticalHeight"]),
            HorizontalWidth = ParseInt(root["HorizontalWidth"]),
            HorizontalHeight = ParseInt(root["HorizontalHeight"]),
            Mode = ParseEnum(root["Mode"], LayoutMode.Horizontal),
            Settings = ParseSettings(settings, version),
        };

        layout.UnknownOuterXml.AddRange(root.ChildNodes.OfType<XmlElement>()
            .Where(e => !KnownRootElements.Contains(e.Name))
            .Select(e => e.OuterXml));

        foreach (XmlElement componentEl in components.ChildNodes.OfType<XmlElement>().Where(e => e.Name == "Component"))
        {
            // ComponentManager.LoadLayoutComponent: an empty path is a Separator.
            string path = (componentEl["Path"] ?? throw new InvalidDataException("A component has no <Path>.")).InnerText;
            string? settingsXml = componentEl["Settings"]?.InnerXml;
            var component = new LayoutComponent(path, settingsXml);
            component.UnknownOuterXml.AddRange(componentEl.ChildNodes.OfType<XmlElement>()
                .Where(e => e.Name is not ("Path" or "Settings"))
                .Select(e => e.OuterXml));
            layout.Components.Add(component);
        }

        return layout;
    }

    public static Layout LoadFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Load(stream);
    }

    public static void Save(Layout layout, Stream stream)
    {
        var document = new XmlDocument();
        document.AppendChild(document.CreateXmlDeclaration("1.0", "UTF-8", null));
        XmlElement root = document.CreateElement("Layout");
        // XMLLayoutSaver always writes 1.6.1 (so Windows reads BackgroundType, not BackgroundGradient).
        root.SetAttribute("version", "1.6.1");
        document.AppendChild(root);

        AppendText(document, root, "Mode", layout.Mode.ToString());
        AppendText(document, root, "X", layout.X.ToString(CultureInfo.InvariantCulture));
        AppendText(document, root, "Y", layout.Y.ToString(CultureInfo.InvariantCulture));
        AppendText(document, root, "VerticalWidth", layout.VerticalWidth.ToString(CultureInfo.InvariantCulture));
        AppendText(document, root, "VerticalHeight", layout.VerticalHeight.ToString(CultureInfo.InvariantCulture));
        AppendText(document, root, "HorizontalWidth", layout.HorizontalWidth.ToString(CultureInfo.InvariantCulture));
        AppendText(document, root, "HorizontalHeight", layout.HorizontalHeight.ToString(CultureInfo.InvariantCulture));

        XmlElement settingsEl = document.CreateElement("Settings");
        root.AppendChild(settingsEl);
        WriteSettings(document, settingsEl, layout.Settings);

        XmlElement componentsEl = document.CreateElement("Components");
        root.AppendChild(componentsEl);
        foreach (LayoutComponent component in layout.Components)
        {
            XmlElement componentEl = document.CreateElement("Component");
            componentsEl.AppendChild(componentEl);
            AppendText(document, componentEl, "Path", component.Path);
            XmlElement componentSettings = document.CreateElement("Settings");
            if (!string.IsNullOrEmpty(component.SettingsInnerXml))
            {
                componentSettings.InnerXml = component.SettingsInnerXml;
            }

            componentEl.AppendChild(componentSettings);
            AppendUnknown(document, componentEl, component.UnknownOuterXml);
        }

        AppendUnknown(document, root, layout.UnknownOuterXml);
        document.Save(stream);
    }

    public static void SaveFile(Layout layout, string path)
    {
        using FileStream stream = File.Create(path);
        Save(layout, stream);
    }

    /// <summary>XMLLayoutSaver.ToElement: the settings in Windows' order, then LiveSplitNative's and unknown ones.</summary>
    private static void WriteSettings(XmlDocument document, XmlElement settingsEl, LayoutSettings s)
    {
        AppendText(document, settingsEl, "TextColor", s.TextColor.ToHex());
        AppendText(document, settingsEl, "BackgroundColor", s.BackgroundColor.ToHex());
        AppendText(document, settingsEl, "BackgroundColor2", s.BackgroundColor2.ToHex());
        AppendText(document, settingsEl, "ThinSeparatorsColor", s.ThinSeparatorsColor.ToHex());
        AppendText(document, settingsEl, "SeparatorsColor", s.SeparatorsColor.ToHex());
        AppendText(document, settingsEl, "PersonalBestColor", s.PersonalBestColor.ToHex());
        AppendText(document, settingsEl, "AheadGainingTimeColor", s.AheadGainingTimeColor.ToHex());
        AppendText(document, settingsEl, "AheadLosingTimeColor", s.AheadLosingTimeColor.ToHex());
        AppendText(document, settingsEl, "BehindGainingTimeColor", s.BehindGainingTimeColor.ToHex());
        AppendText(document, settingsEl, "BehindLosingTimeColor", s.BehindLosingTimeColor.ToHex());
        AppendText(document, settingsEl, "BestSegmentColor", s.BestSegmentColor.ToHex());
        AppendText(document, settingsEl, "UseRainbowColor", s.UseRainbowColor.ToString());
        AppendText(document, settingsEl, "NotRunningColor", s.NotRunningColor.ToHex());
        AppendText(document, settingsEl, "PausedColor", s.PausedColor.ToHex());
        AppendText(document, settingsEl, "TextOutlineColor", s.TextOutlineColor.ToHex());
        AppendText(document, settingsEl, "ShadowsColor", s.ShadowsColor.ToHex());

        // SettingsHelper.CreateSetting(Font): every layout font, as Windows writes it. A font that
        // was not changed keeps the bytes it was read with.
        WriteFont(document, settingsEl, "TimesFont", s.TimesFontXml, s.TimesFontFamily, s.TimesFontSizePx, s.TimesFontBold, s.TimesFontItalic);
        WriteFont(document, settingsEl, "TimerFont", s.TimerFontXml, s.TimerFontFamily, s.TimerFontSizePx, s.TimerFontBold, s.TimerFontItalic);
        WriteFont(document, settingsEl, "TextFont", s.TextFontXml, s.TextFontFamily, s.TextFontSizePx, s.TextFontBold, s.TextFontItalic);

        AppendText(document, settingsEl, "AlwaysOnTop", s.AlwaysOnTop.ToString());
        AppendText(document, settingsEl, "ShowBestSegments", s.ShowBestSegments.ToString());
        AppendText(document, settingsEl, "AntiAliasing", s.AntiAliasing.ToString());
        AppendText(document, settingsEl, "DropShadows", s.DropShadows.ToString());
        AppendText(document, settingsEl, "BackgroundType", s.BackgroundType);
        // SettingsHelper.CreateSetting(Image): the Bitmap blob in CDATA, or an empty element
        // (Windows reads an empty element as no image; an element with empty text would not load).
        XmlElement image = document.CreateElement("BackgroundImage");
        if (s.BackgroundImage is { Length: > 0 } imageBytes)
        {
            // The image as it was read when it still is that image, else Windows' blob of it.
            var probe = document.CreateElement("probe");
            if (!string.IsNullOrEmpty(s.BackgroundImageXml))
            {
                probe.InnerXml = s.BackgroundImageXml;
            }

            image.InnerXml = WindowsBitmapBlob.FromElementText(probe.InnerText) is { } read && read.AsSpan().SequenceEqual(imageBytes)
                ? s.BackgroundImageXml!
                : WindowsBitmapBlob.ToElementInnerXml(imageBytes);
        }

        settingsEl.AppendChild(image);
        AppendText(document, settingsEl, "ImageOpacity", s.ImageOpacity.ToString(CultureInfo.InvariantCulture));
        AppendText(document, settingsEl, "ImageBlur", s.ImageBlur.ToString(CultureInfo.InvariantCulture));
        AppendText(document, settingsEl, "Opacity", s.Opacity.ToString(CultureInfo.InvariantCulture));
        AppendText(document, settingsEl, "MousePassThroughWhileRunning", s.MousePassThroughWhileRunning.ToString());
        AppendText(document, settingsEl, "AllowResizing", s.AllowResizing.ToString());
        AppendText(document, settingsEl, "AllowMoving", s.AllowMoving.ToString());
        // LiveSplitNative's Component Spacing: only when changed, so a layout stays as Windows
        // writes it (Windows reads its settings by name and skips this one).
        if (Math.Abs(s.ComponentSpacing - LayoutSettings.DefaultComponentSpacing) > 0.0001f)
        {
            AppendText(document, settingsEl, "ComponentSpacing", s.ComponentSpacing.ToString(CultureInfo.InvariantCulture));
        }

        // LiveSplitNative's colors under the image: only with an image over gradient colors.
        if (string.Equals(s.BackgroundType, "Image", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(s.BackgroundColorType, "SolidColor", StringComparison.OrdinalIgnoreCase))
        {
            AppendText(document, settingsEl, "BackgroundColorType", s.BackgroundColorType);
        }

        AppendUnknown(document, settingsEl, s.UnknownOuterXml);
    }

    /// <summary>XMLLayoutFactory.ParseSettings, with SettingsHelper's values for missing elements.</summary>
    private static LayoutSettings ParseSettings(XmlElement element, Version version)
    {
        LayoutSettings settings = LayoutSettings.CreateStandard();
        settings.TextColor = ParseColor(element["TextColor"]);
        settings.BackgroundColor = ParseColor(element["BackgroundColor"]);
        settings.ThinSeparatorsColor = ParseColor(element["ThinSeparatorsColor"]);
        settings.SeparatorsColor = ParseColor(element["SeparatorsColor"]);
        settings.PersonalBestColor = ParseColor(element["PersonalBestColor"]);
        settings.AheadGainingTimeColor = ParseColor(element["AheadGainingTimeColor"]);
        settings.AheadLosingTimeColor = ParseColor(element["AheadLosingTimeColor"]);
        settings.BehindGainingTimeColor = ParseColor(element["BehindGainingTimeColor"]);
        settings.BehindLosingTimeColor = ParseColor(element["BehindLosingTimeColor"]);
        settings.BestSegmentColor = ParseColor(element["BestSegmentColor"]);
        settings.UseRainbowColor = ParseBool(element["UseRainbowColor"], false);
        settings.NotRunningColor = ParseColor(element["NotRunningColor"]);
        settings.PausedColor = ParseColor(element["PausedColor"], ArgbColor.FromArgb(255, 122, 122, 122));
        settings.AntiAliasing = ParseBool(element["AntiAliasing"], true);
        settings.DropShadows = ParseBool(element["DropShadows"], true);
        settings.Opacity = ParseFloat(element["Opacity"], 1f);
        settings.MousePassThroughWhileRunning = ParseBool(element["MousePassThroughWhileRunning"], false);
        settings.AllowResizing = ParseBool(element["AllowResizing"], true);
        settings.AllowMoving = ParseBool(element["AllowMoving"], true);
        settings.TextOutlineColor = ParseColor(element["TextOutlineColor"], ArgbColor.FromArgb(0, 0, 0, 0));
        settings.ShadowsColor = ParseColor(element["ShadowsColor"], ArgbColor.FromArgb(128, 0, 0, 0));
        settings.ShowBestSegments = ParseBool(element["ShowBestSegments"], false);
        settings.AlwaysOnTop = ParseBool(element["AlwaysOnTop"], false);
        ReadFont(element["TimerFont"], f => (settings.TimerFontXml, settings.TimerFontFamily, settings.TimerFontSizePx, settings.TimerFontBold, settings.TimerFontItalic) = f);
        settings.ImageOpacity = ParseFloat(element["ImageOpacity"], 1f);
        settings.ImageBlur = ParseFloat(element["ImageBlur"], 0f);

        if (version >= new Version(1, 3))
        {
            settings.BackgroundColor2 = ParseColor(element["BackgroundColor2"]);
            ReadFont(element["TimesFont"], f => (settings.TimesFontXml, settings.TimesFontFamily, settings.TimesFontSizePx, settings.TimesFontBold, settings.TimesFontItalic) = f);
            ReadFont(element["TextFont"], f => (settings.TextFontXml, settings.TextFontFamily, settings.TextFontSizePx, settings.TextFontBold, settings.TextFontItalic) = f);
        }
        else
        {
            // Upstream compares the parsed color with Color.Black, which a color read from a
            // number never equals (System.Drawing compares names too): the second color is the first.
            settings.BackgroundColor2 = settings.BackgroundColor;
            ReadFont(element["MainFont"], f => (settings.TimesFontXml, settings.TimesFontFamily, settings.TimesFontSizePx, settings.TimesFontBold, settings.TimesFontItalic) = f);
            ReadFont(element["SplitNamesFont"], f => (settings.TextFontXml, settings.TextFontFamily, settings.TextFontSizePx, settings.TextFontBold, settings.TextFontItalic) = f);
        }

        if (version >= new Version(1, 6, 1))
        {
            settings.BackgroundType = ParseEnum(element["BackgroundType"], BackgroundTypes.SolidColor).ToString();
        }
        else if (element["BackgroundGradient"] is null && element["BackgroundType"] is { } type)
        {
            // Layouts LiveSplitNative wrote as version 1.3 before it wrote 1.6.1 have BackgroundType
            // and no BackgroundGradient; Windows never writes that combination.
            settings.BackgroundType = ParseEnum(type, BackgroundTypes.SolidColor).ToString();
        }
        else
        {
            settings.BackgroundType = element["BackgroundGradient"]?.InnerText switch
            {
                null or "Plain" => nameof(BackgroundTypes.SolidColor),
                "Vertical" => nameof(BackgroundTypes.VerticalGradient),
                _ => nameof(BackgroundTypes.HorizontalGradient),
            };
        }

        settings.BackgroundColorType = element["BackgroundColorType"]?.InnerText switch
        {
            "VerticalGradient" => "VerticalGradient",
            "HorizontalGradient" => "HorizontalGradient",
            _ => "SolidColor",
        };
        settings.BackgroundImage = WindowsBitmapBlob.FromElementText(element["BackgroundImage"]?.InnerText);
        settings.BackgroundImageXml = settings.BackgroundImage is null ? null : element["BackgroundImage"]!.InnerXml;
        settings.ComponentSpacing = Math.Clamp(
            ParseFloat(element["ComponentSpacing"], LayoutSettings.DefaultComponentSpacing), 0f, 25f);
        settings.UnknownOuterXml.AddRange(element.ChildNodes.OfType<XmlElement>()
            .Where(e => !KnownSettingElements.Contains(e.Name))
            .Select(e => e.OuterXml));
        return settings;
    }

    /// <summary>
    /// SettingsHelper.GetFontFromElement: an element with a font blob sets the font (family, size
    /// in pixels at 96 DPI, bold, italic) and keeps its bytes; otherwise the standard font stays.
    /// </summary>
    private static void ReadFont(
        XmlElement? element,
        Action<(string? Xml, string Family, float SizePx, bool Bold, bool Italic)> set)
    {
        if (element is null || element.IsEmpty || !WindowsFontBlob.TryRead(element.InnerText, out WindowsFontBlob.Font font))
        {
            return;
        }

        set((element.InnerXml, font.Family, font.SizePx, font.IsBold, font.IsItalic));
    }

    /// <summary>
    /// The font as it was read when it still is that font (same family, size, bold, italic), so
    /// an untouched layout keeps its bytes; otherwise the font Windows' font dialog would make.
    /// </summary>
    private static void WriteFont(
        XmlDocument document,
        XmlElement parent,
        string name,
        string? originalInnerXml,
        string family,
        float sizePx,
        bool bold,
        bool italic)
    {
        XmlElement el = document.CreateElement(name);
        var probe = document.CreateElement("probe");
        if (!string.IsNullOrEmpty(originalInnerXml))
        {
            probe.InnerXml = originalInnerXml;
        }

        bool unchanged = WindowsFontBlob.TryRead(probe.InnerText, out WindowsFontBlob.Font original)
            && string.Equals(original.Family, family, StringComparison.Ordinal)
            && Math.Abs(original.SizePx - sizePx) < 0.001f
            && original.IsBold == bold
            && original.IsItalic == italic;
        el.InnerXml = unchanged
            ? originalInnerXml!
            : WindowsFontBlob.ToElementInnerXml(family, sizePx, bold, italic);
        parent.AppendChild(el);
    }

    private static void AppendText(XmlDocument document, XmlElement parent, string name, string value)
    {
        XmlElement el = document.CreateElement(name);
        el.InnerText = value ?? "";
        parent.AppendChild(el);
    }

    private static void AppendUnknown(XmlDocument document, XmlElement parent, IEnumerable<string> outerXml)
    {
        foreach (string xml in outerXml)
        {
            XmlDocumentFragment fragment = document.CreateDocumentFragment();
            fragment.InnerXml = xml;
            parent.AppendChild(fragment);
        }
    }

    /// <summary>The values of LiveSplit's Options.BackgroundType, in its order.</summary>
    private enum BackgroundTypes
    {
        SolidColor,
        VerticalGradient,
        HorizontalGradient,
        Image,
    }

    // SettingsHelper's parsing: a missing element gives the default, a value .NET cannot parse
    // makes the layout unreadable.
    private static Version ParseVersion(string text) =>
        Version.TryParse(text, out Version? version) ? version : throw Unreadable(text);

    private static int ParseInt(XmlElement? element, int fallback = 0) =>
        element is null ? fallback
        : int.TryParse(element.InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value
        : throw Unreadable(element.InnerText);

    private static float ParseFloat(XmlElement? element, float fallback) =>
        element is null ? fallback
        : float.TryParse(element.InnerText.Replace(',', '.'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float value) ? value
        : throw Unreadable(element.InnerText);

    private static bool ParseBool(XmlElement? element, bool fallback) =>
        element is null ? fallback
        : bool.TryParse(element.InnerText, out bool value) ? value
        : throw Unreadable(element.InnerText);

    /// <summary>SettingsHelper.ParseColor: Color.FromArgb of the hex number; missing is transparent.</summary>
    private static ArgbColor ParseColor(XmlElement? element, ArgbColor fallback = default)
    {
        if (element is null)
        {
            return fallback;
        }

        if (!int.TryParse(element.InnerText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int argb))
        {
            throw Unreadable(element.InnerText);
        }

        return ArgbColor.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }

    /// <summary>Enum.Parse: a name (exact case) or a number.</summary>
    private static T ParseEnum<T>(XmlElement? element, T fallback)
        where T : struct, Enum
    {
        if (element is null)
        {
            return fallback;
        }

        if (Enum.TryParse(element.InnerText, ignoreCase: false, out T value))
        {
            return value;
        }

        throw Unreadable(element.InnerText);
    }

    private static InvalidDataException Unreadable(string text) => new($"\"{text}\" cannot be read.");
}
