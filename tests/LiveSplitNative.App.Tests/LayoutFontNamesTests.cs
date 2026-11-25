using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Layout fonts by the names Windows gives them (LayoutFontCollection). Basten, 12 Oct 2026:
/// "condensed fonts don't show properly": Avalonia read "Condensed" in "Avenir Next Condensed" as
/// a style and drew Avenir Next (or Helvetica Neue) instead.
/// </summary>
public class LayoutFontNamesTests
{
    private static readonly Dictionary<string, InstalledFace[]> Installed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Avenir Next"] =
        [
            new("Regular", FontWeight.Normal, FontStretch.Normal, FontStyle.Normal),
            new("Bold", FontWeight.Bold, FontStretch.Normal, FontStyle.Normal),
        ],
        ["Avenir Next Condensed"] =
        [
            new("Regular", FontWeight.Normal, FontStretch.Condensed, FontStyle.Normal),
            new("Demi Bold", FontWeight.SemiBold, FontStretch.Condensed, FontStyle.Normal),
            new("Demi Bold Italic", FontWeight.SemiBold, FontStretch.Condensed, FontStyle.Italic),
            new("Bold", FontWeight.Bold, FontStretch.Condensed, FontStyle.Normal),
        ],
        ["Helvetica Neue"] =
        [
            new("Regular", FontWeight.Normal, FontStretch.Normal, FontStyle.Normal),
            new("Bold", FontWeight.Bold, FontStretch.Normal, FontStyle.Normal),
            new("Condensed Bold", FontWeight.Bold, FontStretch.Condensed, FontStyle.Normal),
        ],
        ["Barlow Condensed"] =
        [
            new("Regular", FontWeight.Normal, FontStretch.Condensed, FontStyle.Normal),
            new("SemiBold", FontWeight.SemiBold, FontStretch.Condensed, FontStyle.Normal),
            new("Bold", FontWeight.Bold, FontStretch.Condensed, FontStyle.Normal),
        ],
    };

    private static LayoutFace? Resolve(string name, FontWeight weight = FontWeight.Normal, FontStyle style = FontStyle.Normal) =>
        LayoutFontCollection.Resolve(name, style, weight, FontStretch.Normal,
            n => Installed.Keys.FirstOrDefault(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase)),
            n => Installed.TryGetValue(n, out InstalledFace[]? faces) ? faces : []);

    [Fact]
    public void An_Installed_Family_Is_Taken_As_Named_Even_With_Style_Words_In_It()
    {
        // Its own Regular and Bold, not Demi Bold: the face's own weight and width are asked for.
        Assert.Equal(new LayoutFace("Avenir Next Condensed", FontWeight.Normal, FontStretch.Condensed, FontStyle.Normal), Resolve("Avenir Next Condensed"));
        Assert.Equal(new LayoutFace("Avenir Next Condensed", FontWeight.Bold, FontStretch.Condensed, FontStyle.Normal), Resolve(" avenir next condensed ", FontWeight.Bold));
    }

    [Fact]
    public void Faces_Beyond_Regular_Bold_Italic_Are_Listed_As_Families_Of_Their_Own()
    {
        Assert.Equal("", LayoutFontCollection.GroupKey("Bold Italic"));
        Assert.Equal("", LayoutFontCollection.GroupKey("Regular"));
        Assert.Equal("Light", LayoutFontCollection.GroupKey("Light Italic"));
        Assert.Equal("Condensed", LayoutFontCollection.GroupKey("Condensed Bold"));
        Assert.Equal("Semi Squeezed", LayoutFontCollection.GroupKey("Bold Italic Semi Squeezed"));
        Assert.Equal("Semi Squeezed", LayoutFontCollection.GroupKey("Regular Semi Squeezed"));
        Assert.Equal("Demi Bold", LayoutFontCollection.GroupKey("Demi Bold Italic"));
        Assert.Equal("Semi Condensed Medium", LayoutFontCollection.GroupKey("Semi Condensed Medium"));

        Assert.Equal(
            ["Avenir Next Condensed", "Avenir Next Condensed Demi Bold"],
            LayoutFontCollection.GroupFaces("Avenir Next Condensed", Installed["Avenir Next Condensed"]).Select(g => g.Name));
        Assert.Equal(
            ["Helvetica Neue", "Helvetica Neue Condensed"],
            LayoutFontCollection.GroupFaces("Helvetica Neue", Installed["Helvetica Neue"]).Select(g => g.Name));

        // A listed family draws its faces: Helvetica Neue Condensed is Condensed Bold only.
        Assert.Equal(new LayoutFace("Helvetica Neue", FontWeight.Bold, FontStretch.Condensed, FontStyle.Normal), Resolve("Helvetica Neue Condensed"));
    }

    [Fact]
    public void A_Windows_Face_Name_Finds_That_Face_In_Its_Family()
    {
        Assert.Equal(new LayoutFace("Barlow Condensed", FontWeight.SemiBold, FontStretch.Condensed, FontStyle.Normal), Resolve("Barlow Condensed SemiBold"));
        Assert.Equal(new LayoutFace("Helvetica Neue", FontWeight.Bold, FontStretch.Condensed, FontStyle.Normal), Resolve("Helvetica Neue Condensed Bold"));
        Assert.Equal(
            new LayoutFace("Avenir Next Condensed", FontWeight.SemiBold, FontStretch.Condensed, FontStyle.Italic),
            Resolve("Avenir Next Condensed Demi Bold", style: FontStyle.Italic));
    }

    [Fact]
    public void Style_Words_After_A_Family_Ask_For_That_Style()
    {
        Assert.Equal(new LayoutFace("Avenir Next", FontWeight.Light, FontStretch.Normal, FontStyle.Italic), Resolve("Avenir Next Light Italic"));
        Assert.Equal(FontWeight.SemiBold, Resolve("Avenir Next Semi Bold")?.Weight);
        Assert.Equal(FontStretch.Condensed, Resolve("Avenir Next Condensed Regular")?.Stretch);
        // Bold asked of a lighter named face: Bold, as Windows emboldens it.
        Assert.Equal(FontWeight.Bold, Resolve("Avenir Next Light", FontWeight.Bold)?.Weight);
    }

    [Fact]
    public void Anything_Else_Is_Not_Installed_And_Draws_With_The_Fallback()
    {
        Assert.Null(Resolve("Segoe UI Semibold"));
        Assert.Null(Resolve("Avenir Next Rounded"));
        Assert.Null(Resolve("  "));
    }

    [AvaloniaFact]
    public void Condensed_Families_Draw_Their_Condensed_Faces()
    {
        // macOS ships both; where they are not installed there is nothing to draw.
        foreach (string family in new[] { "Avenir Next Condensed", "DIN Condensed" })
        {
            if (!LayoutFontCollection.Families.ContainsKey(family))
            {
                continue;
            }

            foreach (FontWeight weight in new[] { FontWeight.Normal, FontWeight.Bold })
            {
                Assert.True(FontManager.Current.TryGetGlyphTypeface(
                    new Typeface(new FontFamily(AppFonts.ForTextFont(family)), FontStyle.Normal, weight), out IGlyphTypeface? face));
                Assert.Equal(family, face.FamilyName);
                Assert.Equal(FontStretch.Condensed, face.Stretch);
            }

            Assert.True(FontManager.Current.TryGetGlyphTypeface(
                new Typeface(new FontFamily(AppFonts.ForTimerFont(family)), FontStyle.Normal, FontWeight.Bold), out IGlyphTypeface? timer));
            Assert.Equal(family, timer.FamilyName);
        }

        // DIN Condensed is Bold only, as the font dialog then shows it.
        if (LayoutFontCollection.Families.ContainsKey("DIN Condensed"))
        {
            Assert.False(LayoutFontCollection.IsStyleAvailable("DIN Condensed", FontWeight.Normal, FontStyle.Normal));
            Assert.True(LayoutFontCollection.IsStyleAvailable("DIN Condensed", FontWeight.Bold, FontStyle.Normal));
        }

        // A font that is not installed: the stand-ins, as before.
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(AppFonts.ForTextFont("No Such Font"))), out IGlyphTypeface? missing));
        Assert.NotEqual("No Such Font", missing.FamilyName);
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(AppFonts.ForTimerFont("No Such Font")), FontStyle.Normal, FontWeight.Bold), out IGlyphTypeface? timerMissing));
        // Century Gothic is not shipped (Monotype's): the bundled Inter stands in.
        Assert.Equal("Inter", timerMissing.FamilyName);
    }
}
