namespace LiveSplitNative.Core.Options;

/// <summary>One entry of the Open Splits menu: a file to open, or a submenu of further entries.</summary>
public sealed class RecentSplitsMenuEntry
{
    internal RecentSplitsMenuEntry(string text, string? path, string? tag)
    {
        Text = text;
        Path = path;
        Tag = tag;
    }

    public string Text { get; internal set; }

    /// <summary>The splits file this entry opens; null for a submenu.</summary>
    public string? Path { get; }

    public List<RecentSplitsMenuEntry> Children { get; } = [];

    internal string? Tag { get; set; }
}

/// <summary>
/// Port of Windows TimerForm.UpdateRecentSplits: the recent splits newest first, grouped by game
/// and then category, with single-item groups folded into their parent ("Game - Category",
/// "Game (file.lss)").
/// </summary>
public static class RecentSplitsMenu
{
    private const string CategoryTag = "Category";
    private const string FileNameTag = "FileName";

    public static List<RecentSplitsMenuEntry> Build(IEnumerable<RecentSplitsFile> recentSplits)
    {
        var entries = new List<RecentSplitsMenuEntry>();
        foreach (IGrouping<string, RecentSplitsFile> game in recentSplits
                     .Reverse()
                     .Where(x => !string.IsNullOrEmpty(x.Path))
                     .GroupBy(x => x.GameName ?? ""))
        {
            var gameEntry = new RecentSplitsMenuEntry("", null, null);
            foreach (IGrouping<string, RecentSplitsFile> category in game.GroupBy(x => x.CategoryName ?? ""))
            {
                var categoryEntry = new RecentSplitsMenuEntry("", null, CategoryTag);
                foreach (RecentSplitsFile splitsFile in category)
                {
                    categoryEntry.Children.Add(new RecentSplitsMenuEntry(
                        System.IO.Path.GetFileName(splitsFile.Path), splitsFile.Path, FileNameTag));
                }

                if (categoryEntry.Children.Count == 1)
                {
                    categoryEntry = categoryEntry.Children[0];
                    if (!string.IsNullOrEmpty(category.Key))
                    {
                        categoryEntry.Text = category.Key;
                        categoryEntry.Tag = CategoryTag;
                    }
                }
                else
                {
                    categoryEntry.Text = string.IsNullOrEmpty(category.Key) ? "Unknown Category" : category.Key;
                }

                gameEntry.Children.Add(categoryEntry);
            }

            string gameName;
            if (string.IsNullOrEmpty(game.Key))
            {
                gameName = "Unknown Game";
                if (gameEntry.Children.Count == 1)
                {
                    gameEntry = gameEntry.Children[0];
                    gameName = gameEntry.Text;
                    if (gameEntry.Text == "Unknown Category")
                    {
                        gameName = "Unknown";
                    }
                }
            }
            else
            {
                gameName = game.Key;
                if (gameEntry.Children.Count == 1)
                {
                    gameEntry = gameEntry.Children[0];
                    if (gameEntry.Tag == CategoryTag)
                    {
                        if (!gameEntry.Text.StartsWith("Unknown Category", StringComparison.Ordinal))
                        {
                            gameName += " - " + gameEntry.Text;
                        }
                    }
                    else
                    {
                        gameName += " (" + gameEntry.Text + ")";
                    }
                }
            }

            gameEntry.Text = gameName;
            entries.Add(gameEntry);
        }

        return entries;
    }
}
