using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class WindowsFontBlobParserTests
{
    // DefaultLayout.lsl TimesFont — Segoe UI 16 Bold, Pixel unit.
    private const string TimesFontSegoeBold16 =
        "AAEAAAD/////AQAAAAAAAAAMAgAAAFFTeXN0ZW0uRHJhd2luZywgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWIwM2Y1ZjdmMTFkNTBhM2EFAQAAABNTeXN0ZW0uRHJhd2luZy5Gb250BAAAAAROYW1lBFNpemUFU3R5bGUEVW5pdAEABAQLGFN5c3RlbS5EcmF3aW5nLkZvbnRTdHlsZQIAAAAbU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AgAAAAIAAAAGAwAAAAhTZWdvZSBVSQAAgEEF/P///xhTeXN0ZW0uRHJhd2luZy5Gb250U3R5bGUBAAAAB3ZhbHVlX18ACAIAAAABAAAABfv///8bU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AQAAAAd2YWx1ZV9fAAgCAAAAAgAAAAs=";

    // DefaultLayout.lsl TimerFont — Century Gothic 43.75 Bold.
    private const string TimerFontCenturyGothic =
        "AAEAAAD/////AQAAAAAAAAAMAgAAAFFTeXN0ZW0uRHJhd2luZywgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWIwM2Y1ZjdmMTFkNTBhM2EFAQAAABNTeXN0ZW0uRHJhd2luZy5Gb250BAAAAAROYW1lBFNpemUFU3R5bGUEVW5pdAEABAQLGFN5c3RlbS5EcmF3aW5nLkZvbnRTdHlsZQIAAAAbU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AgAAAAIAAAAGAwAAAA5DZW50dXJ5IEdvdGhpYwAAL0IF/P///xhTeXN0ZW0uRHJhd2luZy5Gb250U3R5bGUBAAAAB3ZhbHVlX18ACAIAAAABAAAABfv///8bU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AQAAAAd2YWx1ZV9fAAgCAAAAAgAAAAs=";

    // DefaultLayout.lsl TextFont — Segoe UI 16 Regular (not bold).
    private const string TextFontSegoeRegular =
        "AAEAAAD/////AQAAAAAAAAAMAgAAAFFTeXN0ZW0uRHJhd2luZywgVmVyc2lvbj00LjAuMC4wLCBDdWx0dXJlPW5ldXRyYWwsIFB1YmxpY0tleVRva2VuPWIwM2Y1ZjdmMTFkNTBhM2EFAQAAABNTeXN0ZW0uRHJhd2luZy5Gb250BAAAAAROYW1lBFNpemUFU3R5bGUEVW5pdAEABAQLGFN5c3RlbS5EcmF3aW5nLkZvbnRTdHlsZQIAAAAbU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AgAAAAIAAAAGAwAAAAhTZWdvZSBVSQAAgEEF/P///xhTeXN0ZW0uRHJhd2luZy5Gb250U3R5bGUBAAAAB3ZhbHVlX18ACAIAAAAAAAAABfv///8bU3lzdGVtLkRyYXdpbmcuR3JhcGhpY3NVbml0AQAAAAd2YWx1ZV9fAAgCAAAAAgAAAAs=";

    [Fact]
    public void Parses_Segoe_UI_Bold_16()
    {
        Assert.True(WindowsFontBlobParser.TryParse(TimesFontSegoeBold16, out var font));
        Assert.Equal("Segoe UI", font.Family);
        Assert.Equal(16f, font.SizePx);
        Assert.True(font.Bold);
    }

    [Fact]
    public void Parses_Century_Gothic_Timer()
    {
        Assert.True(WindowsFontBlobParser.TryParse(TimerFontCenturyGothic, out var font));
        Assert.Equal("Century Gothic", font.Family);
        Assert.Equal(43.75f, font.SizePx);
        Assert.True(font.Bold);
    }

    [Fact]
    public void Parses_Regular_Not_Bold()
    {
        Assert.True(WindowsFontBlobParser.TryParse(TextFontSegoeRegular, out var font));
        Assert.Equal("Segoe UI", font.Family);
        Assert.False(font.Bold);
    }

    [Fact]
    public void DetailedTimer_Reads_And_Writes_Windows_Font_Blobs()
    {
        // SettingsInnerXml is the *inner* content of <Settings> (LoadRoot wraps it).
        string inner =
            "<Version>1.5</Version>"
            + "<SegmentLabelsFont><![CDATA[" + TimesFontSegoeBold16 + "]]></SegmentLabelsFont>"
            + "<SegmentTimesFont><![CDATA[" + TextFontSegoeRegular + "]]></SegmentTimesFont>"
            + "<SplitNameFont><![CDATA[" + TimerFontCenturyGothic + "]]></SplitNameFont>";

        var settings = DetailedTimerComponentSettings.FromInnerXml(inner);
        Assert.False(settings.UsedPortableFontFallback);
        Assert.Equal("Segoe UI", settings.SegmentLabelsFontFamily);
        Assert.Equal(16f, settings.SegmentLabelsFontSizePx);
        Assert.True(settings.SegmentLabelsFontBold);
        Assert.Equal("Segoe UI", settings.SegmentTimesFontFamily);
        Assert.False(settings.SegmentTimesFontBold);
        Assert.Equal("Century Gothic", settings.SplitNameFontFamily);
        Assert.Equal(43.75f, settings.SplitNameFontSizePx);

        // Saved as Windows saves fonts, so the blobs come back byte for byte.
        string saved = settings.ToInnerXml();
        Assert.DoesNotContain("<Family>", saved, StringComparison.Ordinal);
        Assert.Contains("<SegmentLabelsFont><![CDATA[" + TimesFontSegoeBold16 + "]]></SegmentLabelsFont>", saved, StringComparison.Ordinal);
        Assert.Contains("<SegmentTimesFont><![CDATA[" + TextFontSegoeRegular + "]]></SegmentTimesFont>", saved, StringComparison.Ordinal);
        Assert.Contains("<SplitNameFont><![CDATA[" + TimerFontCenturyGothic + "]]></SplitNameFont>", saved, StringComparison.Ordinal);
    }

    [Fact]
    public void DetailedTimer_Fonts_Keep_Any_Family_And_Italic()
    {
        var settings = new DetailedTimerComponentSettings
        {
            SegmentLabelsFontFamily = "Roboto Condensed",
            SegmentLabelsFontSizePx = 18f,
            SegmentLabelsFontItalic = true,
            SplitNameFontFamily = "PP Formula Semi Condensed Medium",
            SplitNameFontBold = true,
            SplitNameFontItalic = true,
        };

        var again = DetailedTimerComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.Equal("Roboto Condensed", again.SegmentLabelsFontFamily);
        Assert.Equal(18f, again.SegmentLabelsFontSizePx);
        Assert.False(again.SegmentLabelsFontBold);
        Assert.True(again.SegmentLabelsFontItalic);
        Assert.Equal("PP Formula Semi Condensed Medium", again.SplitNameFontFamily);
        Assert.True(again.SplitNameFontBold);
        Assert.True(again.SplitNameFontItalic);
        Assert.True(again.SegmentTimesFontBold);
        Assert.False(again.SegmentTimesFontItalic);
    }

    [Fact]
    public void DetailedTimer_Reads_Fonts_From_Earlier_Builds()
    {
        // Basten's layout (16 Oct), written by an earlier LiveSplitNative.
        string inner =
            "<SegmentLabelsFont><Family>Segoe UI</Family><Size>13</Size><Bold>False</Bold></SegmentLabelsFont>"
            + "<SegmentTimesFont><Family>Segoe UI</Family><Size>13</Size><Bold>True</Bold></SegmentTimesFont>"
            + "<SplitNameFont><Family>Segoe UI</Family><Size>15</Size><Bold>False</Bold></SplitNameFont>";

        var settings = DetailedTimerComponentSettings.FromInnerXml(inner);
        Assert.Equal(("Segoe UI", 13f, false, false),
            (settings.SegmentLabelsFontFamily, settings.SegmentLabelsFontSizePx, settings.SegmentLabelsFontBold, settings.SegmentLabelsFontItalic));
        Assert.Equal(("Segoe UI", 13f, true), (settings.SegmentTimesFontFamily, settings.SegmentTimesFontSizePx, settings.SegmentTimesFontBold));
        Assert.Equal(("Segoe UI", 15f, false), (settings.SplitNameFontFamily, settings.SplitNameFontSizePx, settings.SplitNameFontBold));
    }

    [Fact]
    public void Rejects_Garbage()
    {
        Assert.False(WindowsFontBlobParser.TryParse("not-base64!!!", out _));
        Assert.False(WindowsFontBlobParser.TryParse(Convert.ToBase64String([1, 2, 3, 4]), out _));
    }
}
