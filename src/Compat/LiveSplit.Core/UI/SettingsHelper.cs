// Port of LiveSplit.Core UI/SettingsHelper.cs @ 93a11b85 (MIT). ColorButtonClick opens the app's
// color picker. Fonts are read and written as Windows writes them (a BinaryFormatter stream in
// base64, WindowsFontBlob); images in settings are not read yet.
using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Xml;
using LiveSplitNative.Compat;
using LiveSplitNative.Core.Serialization;

namespace LiveSplit.UI;

public class SettingsHelper
{
    public static string FormatFont(Font font) => $"{font.FontFamily.Name} {font.Style}";

    /// <summary>The app's color picker for the button's color; the button follows the choice.</summary>
    public static void ColorButtonClick(Button button, Control control)
    {
        if (CompatServices.Dialogs is not { } dialogs)
        {
            return;
        }

        // ColorPickerDialog: the button follows the color while picking, and keeps the old one on Cancel.
        Color original = button.BackColor;
        _ = dialogs.PickColorAsync(original, control, color => button.BackColor = color).ContinueWith(
            task => button.BackColor = task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion && task.Result is { } color ? color : original,
            System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
    }

    public static Color ParseColor(XmlElement colorElement, Color defaultColor = default) =>
        colorElement != null
            ? Color.FromArgb(int.Parse(colorElement.InnerText, NumberStyles.HexNumber))
            : defaultColor;

    /// <summary>
    /// The font a settings element holds (null when it is empty, as on Windows). A stream that is
    /// not a font is null here too, where Windows' BinaryFormatter would throw.
    /// </summary>
    public static Font GetFontFromElement(XmlElement element)
    {
        if (element != null && !element.IsEmpty && WindowsFontBlob.TryRead(element.InnerText, out WindowsFontBlob.Font font) && font.Size > 0 && !float.IsNaN(font.Size))
        {
            return new Font(font.Family, font.Size, (FontStyle)font.Style, (GraphicsUnit)font.Unit);
        }

        return null;
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string elementName, Font font)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(elementName);
            if (font != null)
            {
                byte[] data = WindowsFontBlob.Write(font.FontFamily.Name, font.Size, (int)font.Style, (int)font.Unit);
                element.AppendChild(document.CreateCDataSection(Convert.ToBase64String(data)));
            }

            parent.AppendChild(element);
        }

        if (font == null)
        {
            return 0;
        }

        int hash = 17;
        unchecked
        {
            hash = (hash * 23) + font.Name.GetHashCode();
            hash = (hash * 23) + font.FontFamily.GetHashCode();
            hash = (hash * 23) + font.Size.GetHashCode();
            hash = (hash * 23) + font.Style.GetHashCode();
        }

        return hash;
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string elementName, Image image)
    {
        if (document != null)
        {
            parent.AppendChild(document.CreateElement(elementName));
        }

        return image != null ? image.GetHashCode() : 0;
    }

    public static Image GetImageFromElement(XmlElement element) => null;

    public static bool ParseBool(XmlElement boolElement, bool defaultBool = false) =>
        boolElement != null ? bool.Parse(boolElement.InnerText) : defaultBool;

    public static bool TryParseBool(XmlElement boolElement, out bool result, bool defaultBool = false)
    {
        if (boolElement != null && bool.TryParse(boolElement.InnerText, out result))
        {
            return true;
        }

        result = defaultBool;
        return false;
    }

    public static int ParseInt(XmlElement intElement, int defaultInt = 0) =>
        intElement != null ? int.Parse(intElement.InnerText) : defaultInt;

    public static bool TryParseInt(XmlElement intElement, out int result, int defaultInt = 0)
    {
        if (intElement != null && int.TryParse(intElement.InnerText, out result))
        {
            return true;
        }

        result = defaultInt;
        return false;
    }

    public static float ParseFloat(XmlElement floatElement, float defaultFloat = 0f) =>
        floatElement != null ? float.Parse(floatElement.InnerText.Replace(',', '.'), CultureInfo.InvariantCulture) : defaultFloat;

    public static bool TryParseFloat(XmlElement floatElement, out float result, float defaultFloat = 0f)
    {
        if (floatElement != null && float.TryParse(floatElement.InnerText, out result))
        {
            return true;
        }

        result = defaultFloat;
        return false;
    }

    public static double ParseDouble(XmlElement doubleElement, double defaultDouble = 0.0) =>
        doubleElement != null ? double.Parse(doubleElement.InnerText, CultureInfo.InvariantCulture) : defaultDouble;

    public static bool TryParseDouble(XmlElement doubleElement, out double result, double defaultDouble = 0.0)
    {
        if (doubleElement != null && double.TryParse(doubleElement.InnerText, out result))
        {
            return true;
        }

        result = defaultDouble;
        return false;
    }

    public static string ParseString(XmlElement stringElement, string defaultString = null)
    {
        defaultString ??= string.Empty;
        return stringElement != null ? stringElement.InnerText : defaultString;
    }

    public static TimeSpan ParseTimeSpan(XmlElement timeSpanElement, TimeSpan defaultTimeSpan = default) =>
        timeSpanElement != null ? TimeSpan.Parse(timeSpanElement.InnerText) : defaultTimeSpan;

    public static bool TryParseTimeSpan(XmlElement timeSpanElement, out TimeSpan result, TimeSpan defaultTimeSpan = default)
    {
        if (timeSpanElement != null && TimeSpan.TryParse(timeSpanElement.InnerText, out result))
        {
            return true;
        }

        result = defaultTimeSpan;
        return false;
    }

    public static XmlElement ToElement<T>(XmlDocument document, string name, T value)
    {
        XmlElement element = document.CreateElement(name);
        element.InnerText = value?.ToString();
        return element;
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string name, Color color)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = color.ToArgb().ToString("X8");
            parent.AppendChild(element);
        }

        return color.GetHashCode();
    }

    public static int CreateSetting<T>(XmlDocument document, XmlElement parent, string name, T value)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = value?.ToString();
            parent.AppendChild(element);
        }

        return value != null ? value.GetHashCode() : 0;
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string name, float value)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = value.ToString(CultureInfo.InvariantCulture);
            parent.AppendChild(element);
        }

        return value.GetHashCode();
    }

    public static int CreateSetting(XmlDocument document, XmlElement parent, string name, double value)
    {
        if (document != null)
        {
            XmlElement element = document.CreateElement(name);
            element.InnerText = value.ToString(CultureInfo.InvariantCulture);
            parent.AppendChild(element);
        }

        return value.GetHashCode();
    }

    public static XmlAttribute ToAttribute<T>(XmlDocument document, string name, T value)
    {
        XmlAttribute element = document.CreateAttribute(name);
        element.Value = value?.ToString();
        return element;
    }

    public static T ParseEnum<T>(XmlElement element, T defaultEnum = default) =>
        element != null ? (T)Enum.Parse(typeof(T), element.InnerText) : defaultEnum;

    public static bool TryParseEnum<T>(XmlElement element, out T result, T defaultEnum = default)
        where T : struct
    {
        if (element != null && Enum.TryParse(element.InnerText, out result))
        {
            return true;
        }

        result = defaultEnum;
        return false;
    }

    public static Version ParseVersion(XmlElement element) =>
        element != null ? Version.Parse(element.InnerText) : new Version(1, 0, 0, 0);

    public static bool TryParseVersion(XmlElement element, out Version result)
    {
        if (element != null && Version.TryParse(element.InnerText, out result))
        {
            return true;
        }

        result = new Version(1, 0, 0, 0);
        return false;
    }

    public static Version ParseAttributeVersion(XmlElement element) =>
        element.HasAttribute("version") ? Version.Parse(element.GetAttribute("version")) : new Version(1, 0, 0, 0);

    public static bool TryParseAttributeVersion(XmlElement element, out Version result)
    {
        if (element != null && element.HasAttribute("version") && Version.TryParse(element.GetAttribute("version"), out result))
        {
            return true;
        }

        result = new Version(1, 0, 0, 0);
        return false;
    }
}
