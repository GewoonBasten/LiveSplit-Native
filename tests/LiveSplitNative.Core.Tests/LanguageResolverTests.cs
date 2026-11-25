using System.Globalization;
using System.Text;
using LiveSplitNative.Core.Localization;

namespace LiveSplitNative.Core.Tests;

/// <summary>Port of LiveSplit.Tests.Localization.LanguageResolverMust, and the catalog beside the app.</summary>
[Collection(nameof(LanguageResolverTests))]
public class LanguageResolverTests
{
    [Fact]
    public void Prefer_Configured_Language_Over_Current_Ui_Culture()
    {
        string root = CreateTemporaryLocalizationDirectory();
        try
        {
            UiTextCatalog.Initialize(root);
            LanguageResolver.SetCurrentLanguageSetting("zh-CN");

            Assert.Equal("zh-CN", LanguageResolver.ResolveCurrentCultureLanguage(new CultureInfo("en-US")).Code);
        }
        finally
        {
            Reset(root);
        }
    }

    [Fact]
    public void Fall_Back_To_Current_Ui_Culture_When_Configured_Language_Is_Auto()
    {
        string root = CreateTemporaryLocalizationDirectory();
        try
        {
            UiTextCatalog.Initialize(root);
            LanguageResolver.SetCurrentLanguageSetting(string.Empty);

            Assert.Equal("en-US", LanguageResolver.ResolveCurrentCultureLanguage(new CultureInfo("en-US")).Code);
            // A system language with no catalog is English; Chinese of another region is zh-CN.
            Assert.Equal("en-US", LanguageResolver.Resolve("", new CultureInfo("nl-NL")).Code);
            Assert.Equal("zh-CN", LanguageResolver.Resolve("auto", new CultureInfo("zh-TW")).Code);
        }
        finally
        {
            Reset(root);
        }
    }

    [Fact]
    public void Windows_Locale_Files_Are_Beside_The_App()
    {
        UiTextCatalog.Initialize(AppContext.BaseDirectory);
        AppLanguage chinese = Assert.Single(UiTextCatalog.Languages, l => l.Code == "zh-CN");
        Assert.True(UiTextCatalog.TryGetTranslation("Edit Splits...", chinese, out string translated));
        Assert.Equal("编辑节点...", translated);
        Assert.True(UiTextCatalog.TryGetKeyTranslation(AppLanguage.English, LocalizationKeys.LanguageFollowSystem, out string english));
        Assert.Equal("Follow System", english);
        Assert.Equal("", LanguageResolver.ToSettingValue(AppLanguage.Auto));
        Assert.Equal("zh-CN", LanguageResolver.NormalizeSettingValue("zh"));
    }

    private static string CreateTemporaryLocalizationDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "LiveSplitNative.LocalizationTests", Guid.NewGuid().ToString("N"));
        string localization = Path.Combine(root, "Localization");
        Directory.CreateDirectory(localization);
        File.WriteAllText(
            Path.Combine(localization, "zh-CN.json"),
            """
            {
              "code": "zh-CN",
              "displayName": "简体中文",
              "cultureName": "zh-CN",
              "keys": {},
              "sources": {}
            }
            """,
            new UTF8Encoding(false));
        return root;
    }

    private static void Reset(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        LanguageResolver.SetCurrentLanguageSetting(string.Empty);
        UiTextCatalog.Initialize(AppContext.BaseDirectory);
    }
}
