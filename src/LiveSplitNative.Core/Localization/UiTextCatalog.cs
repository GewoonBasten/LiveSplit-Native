using System.Globalization;
using System.Text.Json;

namespace LiveSplitNative.Core.Localization;

/// <summary>
/// Port of LiveSplit.Localization.UiTextCatalog: the languages in Localization/*.json next to the
/// app (Windows LiveSplit's own files), each with texts by key and translations of English UI texts.
/// English is always there and translates nothing.
/// </summary>
public static class UiTextCatalog
{
    private const string LocalizationDirectoryName = "Localization";

    private static readonly object SyncRoot = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static Dictionary<string, CatalogEntry> catalogs = new(StringComparer.OrdinalIgnoreCase)
    {
        [AppLanguage.English.Code] = CatalogEntry.CreateDefault(),
    };

    private static bool initialized;
    private static string? initializedBaseDirectory;
    private static IReadOnlyList<AppLanguage> languages = [AppLanguage.English];

    /// <summary>English first, then by display name.</summary>
    public static IReadOnlyList<AppLanguage> Languages
    {
        get
        {
            EnsureLoaded();
            return languages;
        }
    }

    public static AppLanguage DefaultLanguage
    {
        get
        {
            EnsureLoaded();
            return catalogs.TryGetValue(AppLanguage.English.Code, out CatalogEntry? catalog)
                ? catalog.Language
                : AppLanguage.English;
        }
    }

    public static void Initialize(string? baseDirectory = null)
    {
        lock (SyncRoot)
        {
            if (initialized && string.IsNullOrWhiteSpace(baseDirectory))
            {
                return;
            }

            string normalizedBaseDirectory = NormalizeBaseDirectory(baseDirectory);
            if (initialized && string.Equals(initializedBaseDirectory, normalizedBaseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            catalogs = LoadCatalogs(normalizedBaseDirectory);
            languages = catalogs.Values
                .Select(x => x.Language)
                .OrderBy(x => x.IsDefault ? 0 : 1)
                .ThenBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            initializedBaseDirectory = normalizedBaseDirectory;
            initialized = true;
        }
    }

    public static AppLanguage? FindLanguage(string? languageCode)
    {
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return null;
        }

        string normalizedCode = languageCode.Trim();
        if (catalogs.TryGetValue(normalizedCode, out CatalogEntry? exactMatch))
        {
            return exactMatch.Language;
        }

        string? twoLetterIsoLanguageName = TryGetTwoLetterIsoLanguageName(normalizedCode);
        if (string.IsNullOrEmpty(twoLetterIsoLanguageName))
        {
            return null;
        }

        return catalogs.Values
            .Select(x => x.Language)
            .FirstOrDefault(x => string.Equals(TryGetTwoLetterIsoLanguageName(x.CultureName), twoLetterIsoLanguageName, StringComparison.OrdinalIgnoreCase));
    }

    public static AppLanguage MatchLanguage(CultureInfo? culture)
    {
        EnsureLoaded();

        if (culture is null)
        {
            return DefaultLanguage;
        }

        AppLanguage? exactMatch = FindLanguage(culture.Name);
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        string? twoLetterIsoLanguageName = TryGetTwoLetterIsoLanguageName(culture.Name);
        if (!string.IsNullOrEmpty(twoLetterIsoLanguageName))
        {
            AppLanguage? languageMatch = catalogs.Values
                .Select(x => x.Language)
                .FirstOrDefault(x => string.Equals(TryGetTwoLetterIsoLanguageName(x.CultureName), twoLetterIsoLanguageName, StringComparison.OrdinalIgnoreCase));
            if (languageMatch is not null)
            {
                return languageMatch;
            }
        }

        return DefaultLanguage;
    }

    public static bool TryGetTranslation(string? source, AppLanguage? language, out string translated)
    {
        translated = "";
        EnsureLoaded();

        if (language is null || !language.RequiresLocalization || string.IsNullOrEmpty(source))
        {
            return false;
        }

        if (catalogs.TryGetValue(language.Code, out CatalogEntry? catalog) && catalog.Sources.TryGetValue(source, out string? found))
        {
            translated = found;
            return true;
        }

        return false;
    }

    public static bool TryGetKeyTranslation(AppLanguage? language, string? key, out string translated)
    {
        translated = "";
        EnsureLoaded();

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (language is not null && !language.IsAuto
            && catalogs.TryGetValue(language.Code, out CatalogEntry? catalog)
            && catalog.Keys.TryGetValue(key, out string? found))
        {
            translated = found;
            return true;
        }

        if (catalogs.TryGetValue(DefaultLanguage.Code, out CatalogEntry? defaultCatalog)
            && defaultCatalog.Keys.TryGetValue(key, out string? fallback))
        {
            translated = fallback;
            return true;
        }

        return false;
    }

    /// <summary>The English texts a language translates (for coverage checks).</summary>
    public static IReadOnlyCollection<string> SourceTexts(AppLanguage language)
    {
        EnsureLoaded();
        return catalogs.TryGetValue(language.Code, out CatalogEntry? catalog) ? catalog.Sources.Keys : [];
    }

    private static void EnsureLoaded()
    {
        if (!initialized)
        {
            Initialize();
        }
    }

    private static string NormalizeBaseDirectory(string? baseDirectory)
    {
        string normalizedPath = !string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.GetFullPath(baseDirectory.Trim())
            : AppDomain.CurrentDomain.BaseDirectory;
        return normalizedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static Dictionary<string, CatalogEntry> LoadCatalogs(string baseDirectory)
    {
        var loadedCatalogs = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase)
        {
            [AppLanguage.English.Code] = CatalogEntry.CreateDefault(),
        };

        string localizationDirectory = Path.Combine(baseDirectory, LocalizationDirectoryName);
        if (!Directory.Exists(localizationDirectory))
        {
            return loadedCatalogs;
        }

        foreach (string filePath in Directory.EnumerateFiles(localizationDirectory, "*.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                LocalizationFileDefinition? definition = JsonSerializer.Deserialize<LocalizationFileDefinition>(File.ReadAllText(filePath), JsonOptions);
                AppLanguage? language = CreateLanguage(definition);
                if (language is null || language.IsAuto)
                {
                    continue;
                }

                loadedCatalogs[language.Code] = new CatalogEntry(
                    language,
                    CreateMap(definition?.Keys),
                    CreateMap(definition?.Sources));
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                // Ignore invalid locale files and keep the rest of the catalog usable.
            }
        }

        return loadedCatalogs;
    }

    private static AppLanguage? CreateLanguage(LocalizationFileDefinition? definition)
    {
        if (definition is null || string.IsNullOrWhiteSpace(definition.Code))
        {
            return null;
        }

        string code = definition.Code.Trim();
        string displayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? code : definition.DisplayName.Trim();
        string cultureName = string.IsNullOrWhiteSpace(definition.CultureName) ? code : definition.CultureName.Trim();
        return new AppLanguage(code, displayName, cultureName);
    }

    private static Dictionary<string, string> CreateMap(IDictionary<string, string?>? source)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (source is null)
        {
            return map;
        }

        foreach (KeyValuePair<string, string?> pair in source)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
            {
                continue;
            }

            map[pair.Key] = pair.Value;
        }

        return map;
    }

    private static string? TryGetTwoLetterIsoLanguageName(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(cultureName).TwoLetterISOLanguageName;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private sealed class LocalizationFileDefinition
    {
        public string? Code { get; set; }

        public string? DisplayName { get; set; }

        public string? CultureName { get; set; }

        public Dictionary<string, string?>? Keys { get; set; }

        public Dictionary<string, string?>? Sources { get; set; }
    }

    private sealed class CatalogEntry(AppLanguage language, Dictionary<string, string> keys, Dictionary<string, string> sources)
    {
        public AppLanguage Language { get; } = language;

        public Dictionary<string, string> Keys { get; } = keys;

        public Dictionary<string, string> Sources { get; } = sources;

        public static CatalogEntry CreateDefault() => new(
            AppLanguage.English,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal));
    }
}
