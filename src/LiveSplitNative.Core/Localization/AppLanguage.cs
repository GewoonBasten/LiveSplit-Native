namespace LiveSplitNative.Core.Localization;

/// <summary>Port of LiveSplit.Localization.AppLanguage: a language of the UI (Auto follows the system).</summary>
public sealed class AppLanguage : IEquatable<AppLanguage>
{
    public static readonly AppLanguage Auto = new(string.Empty, "Follow System", string.Empty);
    public static readonly AppLanguage English = new(LanguageResolver.EnglishCultureName, "English", LanguageResolver.EnglishCultureName);

    public AppLanguage(string? code, string? displayName, string? cultureName = null)
    {
        Code = code?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Code : displayName.Trim();
        CultureName = string.IsNullOrWhiteSpace(cultureName) ? Code : cultureName.Trim();
    }

    public string Code { get; }

    public string DisplayName { get; }

    public string CultureName { get; }

    public bool IsAuto => string.IsNullOrWhiteSpace(Code);

    public bool IsDefault => string.Equals(Code, LanguageResolver.EnglishCultureName, StringComparison.OrdinalIgnoreCase);

    public bool RequiresLocalization => !IsAuto && !IsDefault;

    public bool Equals(AppLanguage? other) =>
        other is not null && string.Equals(Code, other.Code, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as AppLanguage);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Code);

    public override string ToString() => DisplayName;
}
