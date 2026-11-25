using System.Xml;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// Layout fonts as Windows LiveSplit stores them (SettingsHelper.CreateSetting / GetFontFromElement:
/// a BinaryFormatter System.Drawing.Font, base64 in CDATA): read, and written byte for byte as
/// .NET Framework writes them, so layouts move between Windows and here.
/// </summary>
public class WindowsFontBlobTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    private static string FontText(string layoutPath, string element)
    {
        var doc = new XmlDocument();
        doc.Load(layoutPath);
        return doc.SelectSingleNode($"/Layout/Settings/{element}")!.InnerText;
    }

    [Theory]
    [InlineData("TimerFont", "Century Gothic", 43.75f, WindowsFontBlob.Bold)]
    [InlineData("TimesFont", "Segoe UI", 16f, WindowsFontBlob.Bold)]
    [InlineData("TextFont", "Segoe UI", 16f, 0)]
    public void Writes_The_Same_Bytes_As_Windows(string element, string family, float size, int style)
    {
        byte[] windows = Convert.FromBase64String(FontText(Fixture("default-layout.lsl"), element));
        Assert.Equal(windows, WindowsFontBlob.Write(family, size, style, WindowsFontBlob.UnitPixel));
    }

    [Fact]
    public void Reads_Family_Size_Style_And_Unit()
    {
        Assert.True(WindowsFontBlob.TryRead(FontText(Fixture("default-layout.lsl"), "TimerFont"), out WindowsFontBlob.Font timer));
        Assert.Equal(new WindowsFontBlob.Font("Century Gothic", 43.75f, WindowsFontBlob.Bold, WindowsFontBlob.UnitPixel), timer);
        Assert.True(timer.IsBold);
        Assert.False(timer.IsItalic);

        string italic = Convert.ToBase64String(WindowsFontBlob.Write("Helvetica Neue", 18, WindowsFontBlob.Bold | WindowsFontBlob.Italic, WindowsFontBlob.UnitPixel));
        Assert.True(WindowsFontBlob.TryRead("<![CDATA[" + italic + "]]>", out WindowsFontBlob.Font read));
        Assert.Equal("Helvetica Neue", read.Family);
        Assert.True(read.IsBold && read.IsItalic);
        Assert.Equal(18f, read.SizePx);
    }

    [Fact]
    public void Point_Sizes_Become_Pixels_At_96_Dpi()
    {
        string points = Convert.ToBase64String(WindowsFontBlob.Write("Segoe UI", 12, 0, WindowsFontBlob.UnitPoint));
        Assert.True(WindowsFontBlob.TryRead(points, out WindowsFontBlob.Font font));
        Assert.Equal(16f, font.SizePx);
    }

    [Fact]
    public void Garbage_Is_Not_A_Font()
    {
        Assert.False(WindowsFontBlob.TryRead("not base64 !", out _));
        Assert.False(WindowsFontBlob.TryRead(Convert.ToBase64String(new byte[40]), out _));
        Assert.False(WindowsFontBlob.TryRead("", out _));
    }

    [Fact]
    public void An_Unchanged_Layout_Keeps_Its_Font_Bytes_And_A_Changed_Font_Is_Written_As_Windows_Would()
    {
        string path = Fixture("default-layout.lsl");
        Layout layout = XmlLayoutSerializer.LoadFile(path);
        Assert.Equal(("Segoe UI", 16f, false, false), (layout.Settings.TextFontFamily, layout.Settings.TextFontSizePx, layout.Settings.TextFontBold, layout.Settings.TextFontItalic));
        Assert.Equal(("Century Gothic", 43.75f, true), (layout.Settings.TimerFontFamily, layout.Settings.TimerFontSizePx, layout.Settings.TimerFontBold));

        string dir = Path.Combine(Path.GetTempPath(), "lsn-fonts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string same = Path.Combine(dir, "same.lsl");
            XmlLayoutSerializer.SaveFile(layout, same);
            foreach (string element in new[] { "TimerFont", "TimesFont", "TextFont" })
            {
                Assert.Equal(FontText(path, element), FontText(same, element));
            }

            layout.Settings.TextFontFamily = "Helvetica Neue";
            layout.Settings.TextFontSizePx = 20;
            layout.Settings.TextFontItalic = true;
            string changed = Path.Combine(dir, "changed.lsl");
            XmlLayoutSerializer.SaveFile(layout, changed);
            Assert.True(WindowsFontBlob.TryRead(FontText(changed, "TextFont"), out WindowsFontBlob.Font text));
            Assert.Equal(new WindowsFontBlob.Font("Helvetica Neue", 20, WindowsFontBlob.Italic, WindowsFontBlob.UnitPixel), text);
            Assert.Equal(FontText(path, "TimesFont"), FontText(changed, "TimesFont"));

            Layout reloaded = XmlLayoutSerializer.LoadFile(changed);
            Assert.Equal(("Helvetica Neue", 20f, false, true), (reloaded.Settings.TextFontFamily, reloaded.Settings.TextFontSizePx, reloaded.Settings.TextFontBold, reloaded.Settings.TextFontItalic));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void A_New_Layout_Gets_Windows_Font_Elements()
    {
        var layout = StandardLayoutFactory.Create();
        using var ms = new MemoryStream();
        XmlLayoutSerializer.Save(layout, ms);
        ms.Position = 0;
        var doc = new XmlDocument();
        doc.Load(ms);
        Assert.True(WindowsFontBlob.TryRead(doc.SelectSingleNode("/Layout/Settings/TimerFont")!.InnerText, out WindowsFontBlob.Font timer));
        Assert.Equal(layout.Settings.TimerFontFamily, timer.Family);
        Assert.True(timer.IsBold);
    }
}
