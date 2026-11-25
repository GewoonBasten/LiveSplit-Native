using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using Avalonia.Platform;
using SkiaSharp;

namespace LiveSplitNative.App;

/// <summary>One face of an installed family: its style name and what it is.</summary>
internal readonly record struct InstalledFace(string StyleName, FontWeight Weight, FontStretch Stretch, FontStyle Style);

/// <summary>The face a layout font name asks for: an installed family and the style to request from it.</summary>
internal readonly record struct LayoutFace(string Family, FontWeight Weight, FontStretch Stretch, FontStyle Style);

/// <summary>
/// A family as Windows lists it: the faces of an installed family that differ only in Regular /
/// Bold / Italic ("Barlow Condensed SemiBold" is SemiBold and SemiBold Italic). The faces without
/// such a name are the family itself.
/// </summary>
internal sealed record FaceGroup(string Name, string Family, IReadOnlyList<InstalledFace> Faces);

/// <summary>
/// A layout's fonts, found by the names Windows gives them (Basten, 12 Oct 2026: "condensed fonts
/// don't show properly").
/// <para>
/// Windows draws a layout font with <c>new Font(name, size, style)</c>, and GDI+ looks the name up
/// as it is. Avalonia's own system fonts read the words "Condensed", "Light", "Bold" and the like
/// in a family name as a style instead (FontCollectionBase.GetImplicitTypeface): "Avenir Next
/// Condensed" became Avenir Next at condensed width, which CoreText answers with Avenir Next's
/// normal face because the condensed cuts are a family of their own; "DIN Condensed" became a "DIN"
/// that does not exist, so Helvetica; "Roboto Condensed" found Roboto, failed the family-name check
/// of a font list and fell through to Helvetica Neue (fonts-condensed.txt).
/// </para>
/// <para>
/// Here an installed family is used by its exact name. A name that is no installed family is read
/// the way Windows names faces outside Regular/Bold/Italic, as a family followed by a face
/// ("Barlow Condensed SemiBold", "Helvetica Neue Condensed Bold") or by style words. Anything else
/// draws with the fallback, as Windows draws a missing font with its default.
/// </para>
/// </summary>
internal sealed class LayoutFontCollection : FontCollectionBase
{
    /// <summary>Text and Times fonts: Segoe UI's stand-in when the font is missing.</summary>
    public static readonly Uri TextKey = new("fonts:LayoutText", UriKind.Absolute);

    /// <summary>The Timer font: the bundled Inter when the font is missing.</summary>
    public static readonly Uri TimerKey = new("fonts:LayoutTimer", UriKind.Absolute);

    private static readonly ConcurrentDictionary<string, IReadOnlyList<InstalledFace>> s_faces = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, IReadOnlyList<FaceGroup>> s_groups = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string>? s_families;
    private static IReadOnlyList<string>? s_listed;

    private readonly Uri _key;
    private readonly FontFamily _fallback;
    private readonly ConcurrentDictionary<(string Name, FontCollectionKey Request), IGlyphTypeface?> _resolved = new();
    private IFontManagerImpl? _platform;

    public LayoutFontCollection(Uri key, string fallbackFamily)
    {
        _key = key;
        _fallback = new FontFamily(fallbackFamily);
    }

    public override Uri Key => _key;

    public override int Count => Families.Count;

    public override FontFamily this[int index] => new(_key.AbsoluteUri + "#" + Families.Values.ElementAt(index));

    public override IEnumerator<FontFamily> GetEnumerator() =>
        Families.Values.Select(name => new FontFamily(_key.AbsoluteUri + "#" + name)).GetEnumerator();

    public override void Initialize(IFontManagerImpl fontManager) => _platform = fontManager;

    public override bool TryGetGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch,
        [NotNullWhen(true)] out IGlyphTypeface? glyphTypeface)
    {
        glyphTypeface = _resolved.GetOrAdd((familyName, new FontCollectionKey(style, weight, stretch)),
            request => Create(request.Name, style, weight, stretch));
        return glyphTypeface is not null;
    }

    private IGlyphTypeface? Create(string name, FontStyle style, FontWeight weight, FontStretch stretch)
    {
        if (_platform is not null && Resolve(name, style, weight, stretch) is { } face)
        {
            var faceKey = new FontCollectionKey(face.Style, face.Weight, face.Stretch);
            ConcurrentDictionary<FontCollectionKey, IGlyphTypeface?> faces = _glyphTypefaceCache.GetOrAdd(face.Family, _ => new());
            if (faces.TryGetValue(faceKey, out IGlyphTypeface? known) && known is not null)
            {
                return known;
            }

            if (CreateByName(_platform, face) is { } created)
            {
                return faces.GetOrAdd(faceKey, created) ?? created;
            }
        }

        // A narrow face that is not installed (Roboto Condensed on Linux) keeps its narrowness:
        // an installed narrow face before the regular stand-in, so names still fit their column.
        if (_key == TextKey && IsNarrow(name)
            && FontManager.Current.TryGetGlyphTypeface(new Typeface(NarrowFallback, style, weight, stretch), out IGlyphTypeface? narrow)
            && narrow.FamilyName is { } found
            && IsNarrow(found))
        {
            return narrow;
        }

        // A font list whose first name is missing goes on to the next; this one has a single name.
        return FontManager.Current.TryGetGlyphTypeface(new Typeface(_fallback, style, weight, stretch), out IGlyphTypeface? fallback)
            ? fallback
            : null;
    }

    /// <summary>Narrow faces most systems have (macOS: Avenir Next Condensed; Linux: Liberation Sans Narrow and its kin).</summary>
    private static readonly FontFamily NarrowFallback = new(
        "Roboto Condensed, Avenir Next Condensed, Arial Narrow, Liberation Sans Narrow, Nimbus Sans Narrow, DejaVu Sans Condensed");

    private static bool IsNarrow(string name) =>
        name.Contains("Condensed", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Narrow", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Compressed", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// IFontManagerImpl.TryCreateGlyphTypeface(family, style, weight, stretch): the renderer's own
    /// lookup of one family (Skia's MatchFamily), without the system collection's reading of the
    /// name. Avalonia 11.2's reference assemblies hide it, so it is called by reflection; without it
    /// the name goes to the system fonts as before.
    /// </summary>
    private static readonly System.Reflection.MethodInfo? s_createByName = typeof(IFontManagerImpl).GetMethod(
        "TryCreateGlyphTypeface",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
        binder: null,
        [typeof(string), typeof(FontStyle), typeof(FontWeight), typeof(FontStretch), typeof(IGlyphTypeface).MakeByRefType()],
        modifiers: null);

    /// <summary>Whether faces are looked up by their exact family (false: Avalonia's system fonts only).</summary>
    internal static bool LooksUpExactFamilies => s_createByName is not null;

    private static IGlyphTypeface? CreateByName(IFontManagerImpl platform, LayoutFace face)
    {
        if (s_createByName is null)
        {
            return FontManager.Current.SystemFonts.TryGetGlyphTypeface(face.Family, face.Style, face.Weight, face.Stretch, out IGlyphTypeface? system)
                ? system
                : null;
        }

        object?[] args = [face.Family, face.Style, face.Weight, face.Stretch, null];
        return s_createByName.Invoke(platform, args) is true ? args[4] as IGlyphTypeface : null;
    }

    /// <summary>Installed family names (case as installed), by name ignoring case, as Windows matches them.</summary>
    internal static IReadOnlyDictionary<string, string> Families =>
        s_families ??= SKFontManager.Default.GetFontFamilies()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(name => name, name => name, StringComparer.OrdinalIgnoreCase);

    /// <summary>The installed faces of a family (none when it is not installed).</summary>
    internal static IReadOnlyList<InstalledFace> Faces(string family) =>
        s_faces.GetOrAdd(family, name =>
        {
            using SKFontStyleSet set = SKFontManager.Default.GetFontStyles(name);
            var faces = new List<InstalledFace>(set.Count);
            for (int i = 0; i < set.Count; i++)
            {
                SKFontStyle face = set[i];
                faces.Add(new InstalledFace(
                    set.GetStyleName(i) ?? "",
                    (FontWeight)face.Weight,
                    (FontStretch)Math.Clamp(face.Width, 1, 9),
                    face.Slant == SKFontStyleSlant.Upright ? FontStyle.Normal : FontStyle.Italic));
            }

            return faces;
        });

    /// <summary>The face this name and style ask for among the installed fonts, or null when there is none.</summary>
    internal static LayoutFace? Resolve(string name, FontStyle style, FontWeight weight, FontStretch stretch) =>
        Resolve(name, style, weight, stretch, n => Families.TryGetValue(n, out string? found) ? found : null, Faces);

    /// <summary>
    /// <see cref="Resolve(string, FontStyle, FontWeight, FontStretch)"/> against any font list.
    /// An installed family is taken as it is (the font's own widths and weights then decide, so
    /// a condensed-only family draws condensed). Otherwise the longest installed family the name
    /// starts with, and the rest of the name as one of its faces' names or as style words.
    /// </summary>
    internal static LayoutFace? Resolve(string name, FontStyle style, FontWeight weight, FontStretch stretch,
        Func<string, string?> family, Func<string, IReadOnlyList<InstalledFace>> faces)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return null;
        }

        if (family(name) is { } exact)
        {
            // The family's own faces (Regular, Bold, Italic): not Avenir Next Condensed's Demi Bold
            // or Roboto's Condensed Regular, which CoreText may prefer by weight.
            FaceGroup? own = GroupFaces(exact, faces(exact)).FirstOrDefault(g => g.Name.Length == exact.Length);
            return own is null ? new LayoutFace(exact, weight, stretch, style) : Pick(own, weight, style);
        }

        string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int count = words.Length - 1; count >= 1; count--)
        {
            if (family(string.Join(' ', words[..count])) is not { } found)
            {
                continue;
            }

            string[] rest = words[count..];
            string restName = Squash(string.Join("", rest));
            IReadOnlyList<InstalledFace> installed = faces(found);

            // A family as listed here and on Windows: "PP Formula Semi Condensed Medium".
            if (GroupFaces(found, installed).FirstOrDefault(g => Squash(g.Name) == Squash(name)) is { } group)
            {
                return Pick(group, weight, style);
            }

            // One face by its name: "Helvetica Neue Condensed Bold".
            foreach (InstalledFace face in installed)
            {
                if (Squash(face.StyleName) == restName)
                {
                    return new LayoutFace(found, Heavier(face.Weight, weight), face.Stretch,
                        face.Style == FontStyle.Normal ? style : face.Style);
                }
            }

            return TryReadStyleWords(rest, out FontWeight? namedWeight, out FontStyle? namedStyle, out FontStretch? namedStretch)
                ? new LayoutFace(found, namedWeight is { } w ? Heavier(w, weight) : weight, namedStretch ?? stretch, namedStyle ?? style)
                : null;
        }

        return null;
    }

    /// <summary>
    /// The face of a listed family for Regular / Bold / Italic: the asked slant first, then the
    /// asked boldness, then the weight nearest Regular (400) or Bold (700). Its own weight, width
    /// and slant are what is asked of the renderer, so that face is the one drawn.
    /// </summary>
    internal static LayoutFace Pick(FaceGroup group, FontWeight weight, FontStyle style)
    {
        bool bold = weight >= FontWeight.SemiBold;
        bool italic = style != FontStyle.Normal;
        InstalledFace face = group.Faces
            .OrderBy(f => ((f.Style != FontStyle.Normal) != italic ? 2 : 0) + ((f.Weight >= FontWeight.SemiBold) != bold ? 1 : 0))
            .ThenBy(f => Math.Abs((int)f.Weight - (bold ? 700 : 400)))
            .First();
        return new LayoutFace(group.Family, face.Weight, face.Stretch, face.Style);
    }

    /// <summary>
    /// The families as Windows would list these fonts: every installed family's faces by name
    /// without Regular / Bold / Italic ("Barlow Condensed" for Regular, Bold and their italics,
    /// "Barlow Condensed SemiBold", "Helvetica Neue Condensed" for Condensed Bold...).
    /// </summary>
    internal static IReadOnlyList<FaceGroup> GroupFaces(string family, IReadOnlyList<InstalledFace> faces)
    {
        var groups = new List<(string Key, List<InstalledFace> Faces)>();
        foreach (InstalledFace face in faces)
        {
            string key = GroupKey(face.StyleName);
            int index = groups.FindIndex(g => Squash(g.Key) == Squash(key));
            if (index < 0)
            {
                groups.Add((key, [face]));
            }
            else
            {
                groups[index].Faces.Add(face);
            }
        }

        return groups
            .Select(g => new FaceGroup(g.Key.Length == 0 ? family : family + " " + g.Key, family, g.Faces))
            .ToList();
    }

    /// <summary>
    /// A face's style name without its Regular / Bold / Italic words: "Bold Italic" is the family
    /// itself, "Light Italic" is Light, "Condensed Bold" and "Bold Semi Squeezed" are the Bold of
    /// Condensed and Semi Squeezed. Semi / Demi / Extra / Ultra Bold are weights of their own.
    /// </summary>
    internal static string GroupKey(string styleName)
    {
        string[] words = styleName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>(words.Length);
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            bool plainBold = string.Equals(word, "Bold", StringComparison.OrdinalIgnoreCase)
                && (i == 0 || !IsOneOf(words[i - 1], "Semi", "Demi", "Extra", "Ultra"));
            if (plainBold || IsOneOf(word, "Italic", "Oblique", "Regular", "Normal", "Roman", "Plain"))
            {
                continue;
            }

            kept.Add(word);
        }

        return string.Join(' ', kept);
    }

    private static bool IsOneOf(string word, params string[] words) =>
        words.Any(w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase));

    /// <summary>The families of an installed family as listed (cached).</summary>
    internal static IReadOnlyList<FaceGroup> Groups(string family) =>
        s_groups.GetOrAdd(family, name => GroupFaces(name, Faces(name)));

    /// <summary>
    /// The font dialog's list (FontFamily.Families on Windows): every listed family of every
    /// installed font, without the macOS faces hidden behind a dot, alphabetically.
    /// </summary>
    internal static IReadOnlyList<string> ListedFamilies =>
        s_listed ??= Families.Values
            .Where(name => !name.StartsWith('.'))
            .SelectMany(name => Groups(name).Count > 0 ? Groups(name).Select(g => g.Name) : [name])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The faces a listed name stands for: its family's own, or a family as listed; null when it is none.</summary>
    internal static IReadOnlyList<InstalledFace>? FacesOf(string name)
    {
        name = name.Trim();
        if (Families.TryGetValue(name, out string? installed))
        {
            IReadOnlyList<FaceGroup> groups = Groups(installed);
            return groups.FirstOrDefault(g => g.Name.Length == installed.Length)?.Faces ?? Faces(installed);
        }

        string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int count = words.Length - 1; count >= 1; count--)
        {
            if (Families.TryGetValue(string.Join(' ', words[..count]), out string? found))
            {
                return Groups(found).FirstOrDefault(g => Squash(g.Name) == Squash(name))?.Faces;
            }
        }

        return null;
    }

    /// <summary>
    /// FontFamily.IsStyleAvailable: whether the listed family has a real face of that weight
    /// (Bold is SemiBold and heavier) and slant, not one the renderer would fake. A name that is
    /// no installed font counts as having them all, as its stand-in does.
    /// </summary>
    internal static bool IsStyleAvailable(string family, FontWeight weight, FontStyle style)
    {
        if (FacesOf(family) is not { Count: > 0 } faces)
        {
            return true;
        }

        bool bold = weight >= FontWeight.SemiBold;
        bool italic = style != FontStyle.Normal;
        return faces.Any(f => (f.Weight >= FontWeight.SemiBold) == bold && (f.Style != FontStyle.Normal) == italic);
    }

    /// <summary>A named face asked for Bold: Bold when the face is lighter (Windows emboldens it).</summary>
    private static FontWeight Heavier(FontWeight named, FontWeight requested) =>
        requested >= FontWeight.SemiBold && named < FontWeight.SemiBold ? FontWeight.Bold : named;

    private static string Squash(string text) =>
        new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>"SemiBold", "Semi Bold", "Light Italic", "Extra Condensed"...: every word a style word.</summary>
    private static bool TryReadStyleWords(string[] words, out FontWeight? weight, out FontStyle? style, out FontStretch? stretch)
    {
        weight = null;
        style = null;
        stretch = null;
        for (int i = 0; i < words.Length; i++)
        {
            // Two words that are one ("Semi Bold", "Extra Condensed") before the word alone.
            if (i + 1 < words.Length && TryReadStyleWord(words[i] + words[i + 1], ref weight, ref style, ref stretch))
            {
                i++;
                continue;
            }

            if (!TryReadStyleWord(words[i], ref weight, ref style, ref stretch))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadStyleWord(string word, ref FontWeight? weight, ref FontStyle? style, ref FontStretch? stretch)
    {
        if (word.Length == 0 || char.IsDigit(word[0]) || word[0] == '-')
        {
            return false;
        }

        if (Enum.TryParse(word, ignoreCase: true, out FontStretch parsedStretch) && Enum.IsDefined(parsedStretch))
        {
            if (parsedStretch != FontStretch.Normal)
            {
                stretch = parsedStretch;
            }

            return true;
        }

        if (Enum.TryParse(word, ignoreCase: true, out FontStyle parsedStyle) && Enum.IsDefined(parsedStyle))
        {
            if (parsedStyle != FontStyle.Normal)
            {
                style = parsedStyle;
            }

            return true;
        }

        if (Enum.TryParse(word, ignoreCase: true, out FontWeight parsedWeight) && Enum.IsDefined(parsedWeight))
        {
            if (parsedWeight != FontWeight.Normal)
            {
                weight = parsedWeight;
            }

            return true;
        }

        return false;
    }

    /// <summary>Adds the layout font collections to the font manager in use (once per font manager).</summary>
    internal static void EnsureRegistered()
    {
        FontManager manager = FontManager.Current;
        if (ReferenceEquals(s_registeredOn, manager))
        {
            return;
        }

        manager.AddFontCollection(new LayoutFontCollection(TextKey, AppFonts.LayoutTextFamily));
        manager.AddFontCollection(new LayoutFontCollection(TimerKey, AppFonts.TimerFamily));
        s_registeredOn = manager;
    }

    private static FontManager? s_registeredOn;
}
