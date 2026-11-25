using System.Text;
using System.Xml;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// M-B layout corpus: real layouts (Windows' default, livesplit-core's test layouts, Basten's)
/// survive being opened and saved. Everything in the file comes back with the same value, apart
/// from what Windows itself converts (the version, pre-1.3 font names, the pre-1.6.1 gradient);
/// unknown components, component elements and settings are kept; a second save is identical.
/// </summary>
public class LayoutCorpusTests
{
    private static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "fixtures");

    public static IEnumerable<object[]> Layouts() =>
        Directory.GetFiles(FixtureDirectory, "*.lsl")
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new object[] { name! });

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Everything_In_The_Layout_Is_Saved_Again(string file)
    {
        string path = Path.Combine(FixtureDirectory, file);
        var original = new XmlDocument();
        original.Load(path);
        Version version = Version.Parse(original.DocumentElement!.GetAttribute("version") is { Length: > 0 } v ? v : "1.0");

        Layout layout = XmlLayoutSerializer.LoadFile(path);
        XmlDocument saved = Save(layout);

        Dictionary<string, string> before = Values(original);
        Dictionary<string, string> after = Values(saved);
        var missing = new List<string>();
        foreach ((string key, string value) in before)
        {
            if (ConvertedByWindows(key, version))
            {
                continue;
            }

            if (!after.TryGetValue(key, out string? now))
            {
                missing.Add($"  {key}: {Short(value)} is gone");
            }
            else if (now != value)
            {
                missing.Add($"  {key}: {Short(value)} became {Short(now)}");
            }
        }

        Assert.True(missing.Count == 0, $"{file}:\n" + string.Join("\n", missing.Take(40)));
        Assert.Equal("1.6.1", saved.DocumentElement!.GetAttribute("version"));
    }

    [Theory]
    [MemberData(nameof(Layouts))]
    public void A_Saved_Layout_Saves_The_Same_Again(string file)
    {
        Layout layout = XmlLayoutSerializer.LoadFile(Path.Combine(FixtureDirectory, file));
        string first = Save(layout).OuterXml;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(first));
        string second = Save(XmlLayoutSerializer.Load(stream)).OuterXml;
        Assert.Equal(first, second);
    }

    [Fact]
    public void Old_Layouts_Are_Read_As_Windows_Reads_Them()
    {
        // Before 1.3: the fonts are MainFont and SplitNamesFont and the second background color
        // is the first. Before 1.6.1: BackgroundGradient gives the background type.
        string font = new XmlDocument().CreateCDataSection(Font()).OuterXml;
        Layout old = Load($"""
            <Layout version="1.2">
              <Mode>Vertical</Mode>
              <Settings>
                <BackgroundColor>FF102030</BackgroundColor>
                <MainFont>{font}</MainFont>
                <BackgroundGradient>Vertical</BackgroundGradient>
              </Settings>
              <Components />
            </Layout>
            """);
        Assert.Equal(ArgbColor.FromArgb(255, 0x10, 0x20, 0x30), old.Settings.BackgroundColor2);
        Assert.Equal("Arial", old.Settings.TimesFontFamily);
        Assert.Equal("VerticalGradient", old.Settings.BackgroundType);
        // The old names are written as Windows writes them now.
        XmlElement settings = Save(old).DocumentElement!["Settings"]!;
        Assert.Null(settings["MainFont"]);
        Assert.Null(settings["BackgroundGradient"]);
        Assert.Equal("VerticalGradient", settings["BackgroundType"]!.InnerText);

        Assert.Equal("HorizontalGradient", Load("""<Layout version="1.6"><Settings><BackgroundGradient>Horizontal</BackgroundGradient></Settings><Components /></Layout>""").Settings.BackgroundType);
        // From 1.6.1 BackgroundGradient means nothing.
        Assert.Equal("SolidColor", Load("""<Layout version="1.6.1"><Settings><BackgroundGradient>Vertical</BackgroundGradient></Settings><Components /></Layout>""").Settings.BackgroundType);
        // Layouts LiveSplitNative saved as 1.3 with BackgroundType keep it.
        Assert.Equal("Image", Load("""<Layout version="1.3"><Settings><BackgroundType>Image</BackgroundType></Settings><Components /></Layout>""").Settings.BackgroundType);
    }

    [Fact]
    public void Missing_Values_Are_Windows_Defaults_And_Bad_Values_Make_The_Layout_Unreadable()
    {
        Layout layout = Load("""<Layout version="1.6.1"><Settings /><Components /></Layout>""");
        Assert.Equal(LayoutMode.Horizontal, layout.Mode);
        Assert.Equal((0, 0, 0, 0), (layout.X, layout.Y, layout.VerticalWidth, layout.VerticalHeight));
        Assert.Equal(default(ArgbColor), layout.Settings.TextColor);
        Assert.Equal(ArgbColor.FromArgb(255, 122, 122, 122), layout.Settings.PausedColor);
        Assert.False(layout.Settings.AlwaysOnTop);
        Assert.False(layout.Settings.ShowBestSegments);
        Assert.True(layout.Settings.AntiAliasing);

        Assert.Throws<InvalidDataException>(() => Load("""<Layout version="1.6.1"><Components /></Layout>"""));
        Assert.Throws<InvalidDataException>(() => Load("""<Layout version="1.6.1"><Settings><TextColor>red</TextColor></Settings><Components /></Layout>"""));
        Assert.Throws<InvalidDataException>(() => Load("""<Layout version="1.6.1"><X>left</X><Settings /><Components /></Layout>"""));
        Assert.Throws<InvalidDataException>(() => Load("""<Layout version="1.6.1"><Mode>vertical</Mode><Settings /><Components /></Layout>"""));
    }

    [Fact]
    public void Unknown_Layout_Elements_Are_Kept()
    {
        Layout layout = Load("""
            <Layout version="1.6.1">
              <Mode>Vertical</Mode>
              <Settings><TextColor>FFFFFFFF</TextColor><FutureColor>FF000000</FutureColor></Settings>
              <Components><Component><Path>Other.dll</Path><Settings><A>1</A></Settings><FontOverrides><OverrideTimerFont>False</OverrideTimerFont></FontOverrides></Component></Components>
              <FuturePanel side="left">x</FuturePanel>
            </Layout>
            """);
        XmlElement saved = Save(layout.Clone()).DocumentElement!;
        Assert.Equal("FF000000", saved["Settings"]!["FutureColor"]!.InnerText);
        Assert.Equal("left", saved["FuturePanel"]!.GetAttribute("side"));
        Assert.Equal("False", saved["Components"]!["Component"]!["FontOverrides"]!["OverrideTimerFont"]!.InnerText);
    }

    private static Layout Load(string xml) => XmlLayoutSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    private static XmlDocument Save(Layout layout)
    {
        using var stream = new MemoryStream();
        XmlLayoutSerializer.Save(layout, stream);
        stream.Position = 0;
        var document = new XmlDocument();
        document.Load(stream);
        return document;
    }

    /// <summary>A layout font blob of Arial 12 (WindowsFontBlob writes Windows' bytes).</summary>
    private static string Font()
    {
        var holder = new XmlDocument().CreateElement("f");
        holder.InnerXml = WindowsFontBlob.ToElementInnerXml("Arial", 16, false, false);
        return holder.InnerText;
    }

    /// <summary>What Windows reads and writes in a newer form, so it is not compared.</summary>
    private static bool ConvertedByWindows(string key, Version version)
    {
        const string settings = "Layout/Settings[0]/";
        return key == "Layout/@version"
            || (version < new Version(1, 3) && (key.StartsWith(settings + "MainFont[", StringComparison.Ordinal)
                || key.StartsWith(settings + "SplitNamesFont[", StringComparison.Ordinal)
                || key.StartsWith(settings + "BackgroundColor2[", StringComparison.Ordinal)))
            || (version < new Version(1, 6, 1) && key.StartsWith(settings + "BackgroundGradient[", StringComparison.Ordinal));
    }

    /// <summary>Every attribute and leaf text by its path (element names with their index among same-named siblings).</summary>
    private static Dictionary<string, string> Values(XmlDocument document)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        Walk(document.DocumentElement!, document.DocumentElement!.Name, values);
        return values;
    }

    private static void Walk(XmlElement element, string path, Dictionary<string, string> values)
    {
        foreach (XmlAttribute attribute in element.Attributes)
        {
            values[$"{path}/@{attribute.Name}"] = attribute.Value;
        }

        List<XmlElement> children = element.ChildNodes.OfType<XmlElement>().ToList();
        if (children.Count == 0)
        {
            values[path] = element.InnerText;
            return;
        }

        foreach (IGrouping<string, XmlElement> group in children.GroupBy(c => c.Name))
        {
            int index = 0;
            foreach (XmlElement child in group)
            {
                Walk(child, $"{path}/{child.Name}[{index++}]", values);
            }
        }
    }

    private static string Short(string value) => value.Length > 60 ? value[..60] + "…" : $"\"{value}\"";
}
