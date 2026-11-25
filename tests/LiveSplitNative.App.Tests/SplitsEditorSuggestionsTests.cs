using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.Web;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>The Splits Editor's Game Name and Run Category suggestions (speedrun.com).</summary>
[Collection(MainWindowCollection.Name)]
public class SplitsEditorSuggestionsTests
{
    private static Task<string> FakeSpeedrunCom(Uri uri, CancellationToken token) => Task.FromResult(uri.ToString() switch
    {
        "https://www.speedrun.com/api/v1/games?_bulk=yes&max=1000" =>
            "{\"data\":[{\"id\":\"v1pxjz68\",\"names\":{\"international\":\"Super Mario Sunshine\"}},"
            + "{\"id\":\"o1y9wo6q\",\"names\":{\"international\":\"Super Mario 64\"}}],\"pagination\":{\"links\":[]}}",
        "https://www.speedrun.com/api/v1/games/v1pxjz68/categories" =>
            "{\"data\":[{\"id\":\"a\",\"name\":\"Any%\",\"type\":\"per-game\"},{\"id\":\"b\",\"name\":\"120 Shines\",\"type\":\"per-game\"}]}",
        "https://www.speedrun.com/api/v1/games/o1y9wo6q/categories" =>
            "{\"data\":[{\"id\":\"c\",\"name\":\"120 Star\",\"type\":\"per-game\"},{\"id\":\"d\",\"name\":\"70 Star\",\"type\":\"per-game\"}]}",
        _ => throw new HttpRequestException("404"),
    });

    [AvaloniaFact]
    public async Task Game_Box_Suggests_Games_And_Category_Box_The_Games_Categories()
    {
        SpeedrunComGameList saved = SpeedrunComGameList.Current;
        try
        {
            var games = new SpeedrunComGameList(FakeSpeedrunCom, null);
            await games.LoadAsync();
            SpeedrunComGameList.Current = games;
            Run run = Run.CreateStandard();
            run.GameName = "Super Mario Sunshine";
            var editor = new SplitsEditorWindow(run, TimingMethod.RealTime);
            editor.Show();
            AutoCompleteBox game = editor.FindControl<AutoCompleteBox>("GameNameBox")!;
            AutoCompleteBox category = editor.FindControl<AutoCompleteBox>("CategoryBox")!;
            Assert.Equal("Super Mario Sunshine", game.Text);

            // The game's categories, read when the editor opens.
            await editor.CategoriesRefreshed;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["Any%", "120 Shines"], editor.CategorySuggestions);
            Assert.Equal(["Any%", "120 Shines"], category.ItemsSource!.Cast<string>());
            Assert.Equal(AutoCompleteFilterMode.StartsWith, category.FilterMode);
            Assert.True(category.IsTextCompletionEnabled);

            // Typing in the game box: the matching games (none for the exact name).
            Assert.Equal(["Super Mario Sunshine"], (await game.AsyncPopulator!("sms", CancellationToken.None)).Cast<string>());
            Assert.Equal(["Super Mario 64", "Super Mario Sunshine"], (await game.AsyncPopulator!("super", CancellationToken.None)).Cast<string>());
            Assert.Empty(await game.AsyncPopulator!("Super Mario 64", CancellationToken.None));
            Assert.Empty(await game.AsyncPopulator!("", CancellationToken.None));

            // Another game: its categories once the game box is left.
            game.Text = "Super Mario 64";
            game.Focus();
            category.Focus();
            await editor.CategoriesRefreshed;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["120 Star", "70 Star"], editor.CategorySuggestions);

            // A game speedrun.com does not have: Any%, Low%, 100%.
            game.Text = "My Own Game";
            game.Focus();
            category.Focus();
            await editor.CategoriesRefreshed;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(["Any%", "Low%", "100%"], editor.CategorySuggestions);
            editor.Close();
        }
        finally
        {
            SpeedrunComGameList.Current = saved;
        }
    }
}
