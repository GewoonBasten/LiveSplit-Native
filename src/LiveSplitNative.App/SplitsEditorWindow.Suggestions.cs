using Avalonia.Threading;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App;

/// <summary>
/// RunEditorDialog's Game Name and Run Category boxes: the game box suggests up to ten
/// speedrun.com games as you type (CustomAutoCompleteComboBox over SearchForGameName); the category
/// box completes and lists the game's full-game categories (Any%, Low% and 100% until the game is
/// found), read again when the game name is left (GameName set on validation).
/// </summary>
public partial class SplitsEditorWindow
{
    private static readonly string[] DefaultCategories = ["Any%", "Low%", "100%"];
    private readonly SpeedrunComGameList _games = SpeedrunComGameList.Current;
    private CancellationTokenSource? _categoryRefresh;
    private string? _categoriesFor;

    /// <summary>The categories the Run Category box offers now.</summary>
    internal IReadOnlyList<string> CategorySuggestions { get; private set; } = DefaultCategories;

    /// <summary>The last category refresh (tests and proofs wait for it).</summary>
    internal Task CategoriesRefreshed { get; private set; } = Task.CompletedTask;

    private void SetUpGameAndCategorySuggestions()
    {
        // FillCbxGameName: the game list is read when the editor opens (cache, then speedrun.com once).
        _ = _games.LoadAsync();
        GameNameBox.AsyncPopulator = async (text, token) =>
        {
            string typed = text ?? "";
            IReadOnlyList<string> games = await Task.Run(() => _games.Search(typed), token);

            // UpdateUI: no list for an empty box, or when the closest game is what was typed.
            return typed.Length > 0 && games.Count > 0 && typed != games[0] ? games : [];
        };
        GameNameBox.LostFocus += (_, _) => RefreshCategorySuggestions();
        GameNameBox.SelectionChanged += (_, _) =>
        {
            if (GameNameBox.SelectedItem is string)
            {
                RefreshCategorySuggestions();
            }
        };

        CategoryBox.ItemsSource = DefaultCategories;
        // The arrow lists them all under the box, as wide as box and arrow (the combo box's list).
        CategoryListButton.Click += (_, _) =>
        {
            CategoryList.ItemsSource = CategorySuggestions;
            CategoryList.SelectedItem = null;
            CategoryList.Width = CategoryField.Bounds.Width;
            CategoryListPopup.IsOpen = true;
        };
        CategoryList.SelectionChanged += (_, _) =>
        {
            if (CategoryList.SelectedItem is string category)
            {
                CategoryBox.Text = category;
                CategoryListPopup.IsOpen = false;
            }
        };
        Closed += (_, _) => _categoryRefresh?.Cancel();
        RefreshCategorySuggestions();
    }

    /// <summary>RefreshCategoryAutoCompleteList for the game in the Game Name box.</summary>
    private void RefreshCategorySuggestions()
    {
        string game = GameNameBox.Text?.Trim() ?? "";
        if (game == _categoriesFor)
        {
            return;
        }

        _categoriesFor = game;
        _categoryRefresh?.Cancel();
        var cancel = new CancellationTokenSource();
        _categoryRefresh = cancel;
        CategoriesRefreshed = RefreshAsync();

        async Task RefreshAsync()
        {
            IReadOnlyList<string> names = await _games.GetCategoryNamesAsync(game, cancel.Token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!cancel.IsCancellationRequested)
                {
                    CategorySuggestions = names;
                    CategoryBox.ItemsSource = names;
                }
            });
        }
    }
}
