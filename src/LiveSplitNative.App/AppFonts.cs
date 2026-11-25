using Avalonia.Controls;
using Avalonia.Media;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// LiveSplit Windows defaults vs what we ship on macOS.
/// Windows chrome/dialogs: Segoe UI (~9–11pt WinForms ≈ 12–13 CSS px).
/// Windows timer digits: Century Gothic Bold (LayoutSettings.TimerFont / DefaultLayout.lsl).
/// Century Gothic is Monotype's and may not be shipped (Basten 29 Oct): the timer's default here
/// is the free Inter Bold (SIL OFL, bundled through Avalonia.Fonts.Inter); a layout's Timer font
/// that is installed (Century Gothic included) is used as named.
/// </summary>
internal static class AppFonts
{
    /// <summary>
    /// Dialogs, menus and tabs: San Francisco's text cut, SF Pro Text (Basten found the Helvetica
    /// Neue stand-in for Segoe UI cheap-looking). Not "System Font": Avalonia draws that variable
    /// face at its default (display) optical size, so 12 px text came out cramped and soft, where
    /// macOS itself uses the text size (78-font-comparison.png shows both). Without SF Pro Text:
    /// the bundled Inter, a UI face of the same kind.
    /// </summary>
    public const string ChromeFamily = "SF Pro Text, Segoe UI, fonts:Inter#Inter, sans-serif";

    /// <summary>
    /// The timer's own text when the layout asks for Segoe UI (Windows' default text and times
    /// font), which macOS does not have: Helvetica Neue stands in, as before. Linux has neither:
    /// Liberation Sans (Arimo), which most distributions install and which is as wide as
    /// Helvetica, then the bundled Inter, so a layout never falls to a much wider face such as
    /// DejaVu Sans (which cut split names short).
    /// </summary>
    public const string LayoutTextFamily = "Segoe UI, Helvetica Neue, Liberation Sans, Arimo, fonts:Inter#Inter, sans-serif";

    /// <summary>
    /// Timer / Detailed Timer digits when the layout's Timer font is not installed (Windows'
    /// default, Century Gothic, on most Macs and Linux): the bundled Inter, drawn Bold as the
    /// layout asks (Windows <c>StandardLayoutSettingsFactory</c>:
    /// <c>new Font("Century Gothic", 43.75f, FontStyle.Bold, GraphicsUnit.Pixel)</c>).
    /// Basten chose Inter Bold from free candidates (29 Oct, timer-fonts.png).
    /// </summary>
    public const string TimerFamily = "fonts:Inter#Inter, sans-serif";

    /// <summary>The bundled timer face (for proofs / docs).</summary>
    public const string TimerFontFile = "Inter-Bold.ttf (Avalonia.Fonts.Inter)";

    /// <summary>Typographic family name of the bundled timer face.</summary>
    public const string TimerBundledFamilyName = "Inter";

    /// <summary>Windows default timer weight (FontStyle.Bold).</summary>
    public const string TimerFontWeightName = "Bold";

    /// <summary>Mac/dialog control size (not Fluent Display).</summary>
    public const double ControlFontSize = 12;

    public const double MenuFontSize = 12;

    /// <summary>
    /// Apply layout TimesFont to a splits/info time cell. Same family/size/weight for every
    /// column — avoids mixed Bold/Normal that makes some "0:00:00" cells look heavier.
    /// </summary>
    public static void ApplyTimesFont(TextBlock block, LayoutSettings settings)
    {
        string family = string.IsNullOrWhiteSpace(settings.TimesFontFamily)
            ? ChromeFamily
            : ForTextFont(settings.TimesFontFamily);
        block.FontFamily = new FontFamily(family);
        block.FontSize = Math.Clamp(settings.TimesFontSizePx > 0 ? settings.TimesFontSizePx : 16, 1, 200);
        // Single weight from layout (Windows default Bold). Do not layer synthetic Bold on top.
        block.FontWeight = settings.TimesFontBold ? FontWeight.Bold : FontWeight.Normal;
        block.FontStyle = settings.TimesFontItalic ? FontStyle.Italic : FontStyle.Normal;
    }

    /// <summary>Apply layout TextFont to segment names (Windows default Regular).</summary>
    public static void ApplyTextFont(TextBlock block, LayoutSettings settings)
    {
        string family = string.IsNullOrWhiteSpace(settings.TextFontFamily)
            ? LayoutTextFamily
            : ForTextFont(settings.TextFontFamily);
        block.FontFamily = new FontFamily(family);
        block.FontSize = Math.Clamp(settings.TextFontSizePx > 0 ? settings.TextFontSizePx : 16, 1, 200);
        block.FontWeight = settings.TextFontBold ? FontWeight.Bold : FontWeight.Normal;
        block.FontStyle = settings.TextFontItalic ? FontStyle.Italic : FontStyle.Normal;
    }

    /// <summary>
    /// What a layout font's family is drawn with here: Windows' timer default (Century Gothic) as
    /// the Timer font is, otherwise as <see cref="ForTextFont"/>.
    /// </summary>
    public static string ForLayoutFont(string family) =>
        family.StartsWith("Century Gothic", StringComparison.OrdinalIgnoreCase)
            ? ForTimerFont(family)
            : ForTextFont(family);

    /// <summary>
    /// A Text or Times font (and the Detailed Timer's): Helvetica Neue for Windows' Segoe UI, any
    /// other family by its exact name through <see cref="LayoutFontCollection"/> (Helvetica Neue
    /// when it is not installed).
    /// </summary>
    public static string ForTextFont(string? family) =>
        string.IsNullOrWhiteSpace(family) || string.Equals(family.Trim(), "Segoe UI", StringComparison.OrdinalIgnoreCase)
            ? LayoutTextFamily
            : Installed(LayoutFontCollection.TextKey, family, LayoutTextFamily);

    /// <summary>
    /// The Timer font: any family by its exact name, Century Gothic too when it is installed;
    /// the bundled Inter when it is not (or when the layout names none).
    /// </summary>
    public static string ForTimerFont(string? family) =>
        string.IsNullOrWhiteSpace(family)
            ? TimerFamily
            : Installed(LayoutFontCollection.TimerKey, family, TimerFamily);

    /// <summary>
    /// "fonts:LayoutText#Avenir Next Condensed": the family looked up as named. A name Avalonia's
    /// font list syntax cannot hold (a comma or #) goes in a plain list as before.
    /// </summary>
    private static string Installed(Uri collection, string family, string fallback)
    {
        LayoutFontCollection.EnsureRegistered();
        family = family.Trim();
        return family.IndexOfAny([',', '#']) >= 0
            ? family + ", " + fallback
            : collection.AbsoluteUri + "#" + family;
    }
}
