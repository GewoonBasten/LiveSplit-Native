using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Localization;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.UI;
using Xunit;
using Xunit.Abstractions;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// M-D localization coverage: with Windows LiveSplit's Simplified Chinese catalog, every text our
/// windows and menus show that Windows translates is translated (texts Windows does not know stay
/// English, and are listed in the test output). The language is passed explicitly, so other tests
/// keep seeing English.
/// </summary>
[Collection(MainWindowCollection.Name)]
public class LocalizationAppTests(ITestOutputHelper output)
{
    private static AppLanguage Chinese => UiTextCatalog.FindLanguage("zh-CN")
        ?? throw new InvalidOperationException("Localization/zh-CN.json is not beside the tests.");

    [Fact]
    public void Windows_Catalog_Is_Beside_The_App()
    {
        Assert.Equal(["en-US", "zh-CN"], UiTextCatalog.Languages.Select(l => l.Code));
        Assert.Equal("简体中文", Chinese.DisplayName);
        Assert.Equal("编辑节点...", UiLocalizer.Translate("Edit Splits...", Chinese));
        Assert.Equal("Edit Splits...", UiLocalizer.Translate("Edit Splits...", AppLanguage.English));
        Assert.Equal("语言", UiLocalizer.TranslateKey(LocalizationKeys.LanguageMenu, "Language", Chinese));
        Assert.Equal("Language", UiLocalizer.TranslateKey(LocalizationKeys.LanguageMenu, "x", AppLanguage.English));
    }

    public static IEnumerable<object[]> Windows() =>
    [
        ["Settings"], ["Layout Editor"], ["Layout Settings"], ["Splits Editor"], ["Choose Comparisons"],
        ["Hotkeys"], ["About"], ["Manage Racing Services"], .. StandardLayoutFactory.BuiltinCatalog.Select(c => new object[] { c.Path }),
    ];

    private static Window Create(string name) => name switch
    {
        "Settings" => new SettingsWindow(),
        "Layout Editor" => new LayoutEditorWindow(),
        "Layout Settings" => new LayoutSettingsWindow(),
        "Splits Editor" => new SplitsEditorWindow(),
        "Choose Comparisons" => new ChooseComparisonsWindow(),
        "Hotkeys" => new HotkeySettingsWindow(),
        "About" => new AboutWindow(),
        "Manage Racing Services" => new RaceProvidersWindow(),
        _ => new ComponentSettingsWindow(StandardLayoutFactory.CreateBuiltin(name)),
    };

    [AvaloniaTheory]
    [MemberData(nameof(Windows))]
    public void Every_Text_Windows_Translates_Is_Translated(string name)
    {
        Window window = Create(name);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            UiLocalizer.Apply(window, Chinese);
            Dispatcher.UIThread.RunJobs();
            HashSet<string> catalog = [.. UiTextCatalog.SourceTexts(Chinese)];
            List<string> texts = Texts(window).Distinct().ToList();

            List<string> untranslated = texts.Where(t => catalog.Contains(t) && UiLocalizer.Translate(t, Chinese) != t).ToList();
            List<string> unknown = texts.Where(t => !catalog.Contains(t) && t.Any(char.IsAsciiLetter)).ToList();
            output.WriteLine($"{name}: {texts.Count} texts, not in Windows' catalog: {string.Join(" | ", unknown)}");
            Assert.True(untranslated.Count == 0, $"{name} still shows: {string.Join(" | ", untranslated)}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Timer_Menu_Is_Translated_And_Has_The_Language_Menu()
    {
        string dir = HeadlessUi.NewTempDir();
        string settingsPath = Path.Combine(dir, "settings.cfg");
        XmlAppSettingsSerializer.SaveFile(new AppSettings(), settingsPath);
        AppCli.Parse(["--settings", settingsPath]);
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            NativeMenuModel english = window.ContextMenuModelForTests();
            List<string> headers = Headers(english.Items).ToList();
            // InitializeLanguageMenu: before the separator above About.
            int language = headers.IndexOf("Language");
            Assert.True(language > headers.IndexOf("Settings"));
            NativeMenuAction languageMenu = english.Items.OfType<NativeMenuAction>().Single(a => a.Header == "Language");
            Assert.Equal(
                ["Follow System", "English", "简体中文"],
                languageMenu.Children!.OfType<NativeMenuAction>().Select(a => a.Header));
            Assert.IsType<NativeMenuSeparator>(english.Items[english.Items.ToList().IndexOf(languageMenu) + 1]);

            List<string> chinese = Headers(UiLocalizer.Apply(english, Chinese).Items).ToList();
            Assert.Contains("编辑节点...", chinese);
            Assert.Contains("设置", chinese);
            Assert.DoesNotContain("Edit Splits...", chinese);
        }
        finally
        {
            window.Session.State.Run.HasChanged = false;
            window.CurrentLayout.HasChanged = false;
            window.Close();
            HeadlessUi.Delete(dir);
        }
    }

    private static IEnumerable<string> Headers(IEnumerable<NativeMenuEntry> entries) =>
        entries.OfType<NativeMenuAction>().SelectMany(a => (IEnumerable<string>)[a.Header, .. Headers(a.Children ?? [])]);

    /// <summary>The texts UiLocalizer looks at, as they are shown now.</summary>
    private static IEnumerable<string> Texts(ILogical node)
    {
        if (node is TextBox or AutoCompleteBox or NumericUpDown or DataGrid)
        {
            yield break;
        }

        if (node is Window { Title: { Length: > 0 } title })
        {
            yield return title;
        }

        if (node is TextBlock { Text: { Length: > 0 } text })
        {
            yield return text;
        }

        if (node is ContentControl { Content: string { Length: > 0 } content })
        {
            yield return content;
        }

        if (node is HeaderedContentControl { Header: string { Length: > 0 } header })
        {
            yield return header;
        }

        if (node is HeaderedItemsControl { Header: string { Length: > 0 } itemsHeader })
        {
            yield return itemsHeader;
        }

        if (node is Control control && ToolTip.GetTip(control) is string { Length: > 0 } tip)
        {
            yield return tip;
        }

        foreach (ILogical child in node.LogicalChildren.ToList())
        {
            foreach (string t in Texts(child))
            {
                yield return t;
            }
        }
    }
}
