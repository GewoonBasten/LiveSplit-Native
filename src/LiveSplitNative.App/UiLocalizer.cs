using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.LogicalTree;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Localization;

namespace LiveSplitNative.App;

/// <summary>
/// Port of LiveSplit.Localization.UiLocalizer for Avalonia: a window's texts (title, labels,
/// buttons, check boxes, tabs, menu items, tool tips, list and combo box items, grid column
/// headers) are swapped for the language's translation of exactly that English text, as Windows
/// walks a form's controls. Text boxes, grid cells and anything typed by the user are left alone.
/// A text without a translation stays English. Windows applies this when a form opens and every
/// second to every open form (PerSecondTimer_Elapsed), which App does too.
/// </summary>
internal static class UiLocalizer
{
    private static readonly ConditionalWeakTable<ItemsControl, object> TranslatedItemsControls = new();

    public static AppLanguage Current => LanguageResolver.ResolveCurrentCultureLanguage();

    public static string T(string source) => Translate(source, Current);

    public static string Translate(string source, AppLanguage language) => UiText.Translate(source, language);

    public static string TranslateKey(string key, string fallback) => TranslateKey(key, fallback, Current);

    public static string TranslateKey(string key, string fallback, AppLanguage language) =>
        UiTextCatalog.TryGetKeyTranslation(language, key, out string translated)
            ? translated
            : Translate(fallback, language);

    public static void Apply(ILogical root) => Apply(root, Current);

    public static void Apply(ILogical root, AppLanguage language)
    {
        if (language.RequiresLocalization)
        {
            ApplyNode(root, language);
        }
    }

    /// <summary>The menu with every header translated (Windows translates RightClickMenu's items).</summary>
    public static NativeMenuModel Apply(NativeMenuModel model) => Apply(model, Current);

    public static NativeMenuModel Apply(NativeMenuModel model, AppLanguage language) =>
        language.RequiresLocalization ? new NativeMenuModel { Items = Translate(model.Items, language) } : model;

    private static IReadOnlyList<NativeMenuEntry> Translate(IReadOnlyList<NativeMenuEntry> entries, AppLanguage language) =>
        [.. entries.Select(entry => entry is NativeMenuAction action
            ? new NativeMenuAction
            {
                Header = Translate(action.Header, language),
                IsEnabled = action.IsEnabled,
                IsChecked = action.IsChecked,
                OnClick = action.OnClick,
                OnClickAsync = action.OnClickAsync,
                Children = action.Children is { } children ? Translate(children, language) : null,
            }
            : entry)];

    private static void ApplyNode(ILogical node, AppLanguage language)
    {
        switch (node)
        {
            // What the user types or picked by name (CanTranslateControlText), and the timer,
            // which draws its own texts.
            case TextBox or AutoCompleteBox or NumericUpDown or TimerSurfaceControl:
                return;
            case DataGrid grid:
                foreach (DataGridColumn column in grid.Columns)
                {
                    if (column.Header is string columnHeader)
                    {
                        column.Header = Translate(columnHeader, language);
                    }
                }

                return;
        }

        if (node is Window window && window.Title is { } title)
        {
            window.Title = Translate(title, language);
        }

        // A settings row's label: its Windows text, translated whole (unit and colon included).
        if (node is SettingsField { Label: { } fieldLabel } field)
        {
            field.Label = Translate(fieldLabel, language);
        }

        if (node is TextBlock { Inlines: null or { Count: 0 } } textBlock && textBlock.Text is { } text)
        {
            textBlock.Text = Translate(text, language);
        }

        if (node is ContentControl { Content: string content } contentControl)
        {
            contentControl.Content = Translate(content, language);
        }

        if (node is HeaderedContentControl { Header: string header } headered)
        {
            headered.Header = Translate(header, language);
        }

        if (node is HeaderedItemsControl { Header: string itemsHeader } headeredItems)
        {
            headeredItems.Header = Translate(itemsHeader, language);
        }

        if (node is Control control && ToolTip.GetTip(control) is string tip)
        {
            ToolTip.SetTip(control, Translate(tip, language));
        }

        // ListControl.Format: string items show their translation and keep their value.
        if (node is ItemsControl { ItemTemplate: null } items and (ComboBox or ListBox)
            && items.Items.OfType<object>().Any(i => i is string)
            && !TranslatedItemsControls.TryGetValue(items, out _))
        {
            TranslatedItemsControls.Add(items, new object());
            items.ItemTemplate = new FuncDataTemplate<object?>(
                (item, _) => new TextBlock { Text = item is string s ? Translate(s, language) : item?.ToString() ?? "" },
                supportsRecycling: true);
        }

        foreach (ILogical child in node.LogicalChildren.ToList())
        {
            ApplyNode(child, language);
        }
    }
}
