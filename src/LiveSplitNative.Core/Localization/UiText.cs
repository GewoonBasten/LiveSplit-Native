namespace LiveSplitNative.Core.Localization;

/// <summary>
/// The components' T(source) (UiLocalizer.Translate in the language the UI shows): their drawn
/// names and texts in the language of Windows LiveSplit's catalog, English when it has none.
/// </summary>
public static class UiText
{
    public static string T(string source) => Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());

    public static string Translate(string source, AppLanguage language)
    {
        if (string.IsNullOrEmpty(source) || !language.RequiresLocalization)
        {
            return source;
        }

        return UiTextCatalog.TryGetTranslation(source, language, out string translated) ? translated : source;
    }
}
