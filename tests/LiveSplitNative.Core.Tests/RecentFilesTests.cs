using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// Windows' Open Splits menu (TimerForm.UpdateRecentSplits), the extended run names
/// (IRun.GetExtended*) and the Title texts that use them.
/// </summary>
public class RecentFilesTests
{
    [Fact]
    public void Open_Splits_Menu_Groups_By_Game_And_Category_Newest_First()
    {
        var recent = new[]
        {
            File("/r/sms-old.lss", "Super Mario Sunshine", "Any%"),
            File("/r/ww.lss", "The Wind Waker", "Any%"),
            File("/r/sms-120.lss", "Super Mario Sunshine", "120 Shines"),
            File("/r/sms-new.lss", "Super Mario Sunshine", "Any%"),
            File("", "", ""),
        };

        List<RecentSplitsMenuEntry> menu = RecentSplitsMenu.Build(recent);

        // Newest game first; the empty entry (Close Splits) is not listed.
        Assert.Equal(new[] { "Super Mario Sunshine", "The Wind Waker - Any%" }, menu.Select(x => x.Text));
        RecentSplitsMenuEntry sms = menu[0];
        Assert.Null(sms.Path);
        Assert.Equal(new[] { "Any%", "120 Shines" }, sms.Children.Select(x => x.Text));
        Assert.Equal(new[] { "sms-new.lss", "sms-old.lss" }, sms.Children[0].Children.Select(x => x.Text));
        Assert.Equal("/r/sms-new.lss", sms.Children[0].Children[0].Path);
        Assert.Equal("/r/sms-120.lss", sms.Children[1].Path);
        Assert.Equal("/r/ww.lss", menu[1].Path);
    }

    [Theory]
    [InlineData("Game", "", "Game (a.lss)")]
    [InlineData("", "Any%", "Any%")]
    [InlineData("", "", "a.lss")]
    public void A_Single_File_Folds_Into_One_Item(string game, string category, string text)
    {
        RecentSplitsMenuEntry entry = Assert.Single(RecentSplitsMenu.Build([File("/r/a.lss", game, category)]));
        Assert.Equal(text, entry.Text);
        Assert.Equal("/r/a.lss", entry.Path);
    }

    [Fact]
    public void Unknown_Game_And_Category_Labels_Follow_Windows()
    {
        // Several files without names: "Unknown" with the files under it.
        RecentSplitsMenuEntry unknown = Assert.Single(RecentSplitsMenu.Build([File("/r/a.lss", "", ""), File("/r/b.lss", "", "")]));
        Assert.Equal("Unknown", unknown.Text);
        Assert.Equal(new[] { "b.lss", "a.lss" }, unknown.Children.Select(x => x.Text));

        // A named game with several unnamed-category files: just the game name.
        RecentSplitsMenuEntry game = Assert.Single(RecentSplitsMenu.Build([File("/r/a.lss", "G", ""), File("/r/b.lss", "G", "")]));
        Assert.Equal("G", game.Text);
        Assert.Equal(2, game.Children.Count);

        // No game name, two categories: "Unknown Game".
        RecentSplitsMenuEntry noGame = Assert.Single(RecentSplitsMenu.Build([File("/r/a.lss", "", "X"), File("/r/b.lss", "", "Y")]));
        Assert.Equal("Unknown Game", noGame.Text);
        Assert.Equal(new[] { "Y", "X" }, noGame.Children.Select(x => x.Text));
    }

    [Fact]
    public void Extended_Category_Name_Adds_Variables_Then_Region_And_Platform()
    {
        Run run = NewRun("Any%");
        run.Metadata.VariableValueNames["Arbitrary Code Execution"] = "No ACE";
        run.Metadata.VariableValueNames["Glitchless?"] = "Yes";
        run.Metadata.VariableValueNames["Turbo"] = "no";
        run.Metadata.RegionName = "JPN / NTSC";
        run.Metadata.PlatformName = "Wii";

        Assert.Equal("Any% (No ACE, Glitchless, No Turbo)", run.GetExtendedCategoryName());
        Assert.Equal("Any%", run.GetExtendedCategoryName(showVariables: false));
        Assert.Equal("Any% (JPN / NTSC, Wii)", run.GetExtendedCategoryName(showRegion: true, showPlatform: true, showVariables: false));
        run.Metadata.UsesEmulator = true;
        Assert.Equal("Any% (Wii Emulator)", run.GetExtendedCategoryName(showPlatform: true, showVariables: false));
        run.Metadata.PlatformName = "";
        Assert.Equal("Any% (Emulator)", run.GetExtendedCategoryName(showPlatform: true, showVariables: false));
    }

    [Fact]
    public void Extended_Category_Name_Keeps_What_Was_In_Parentheses_First()
    {
        Run run = NewRun("Any% (Glitched) Hard Mode");
        run.Metadata.VariableValueNames["Version"] = "JP";
        Assert.Equal("Any% (Glitched, JP) Hard Mode", run.GetExtendedCategoryName());
        Assert.Equal("", NewRun("").GetExtendedCategoryName());
    }

    [Fact]
    public void Extended_File_Name_Is_Game_Dash_Category_Without_Forbidden_Characters()
    {
        Run run = NewRun("Any% (No ACE)");
        run.GameName = "Super Mario: Sunshine?";
        Assert.Equal("Super Mario: Sunshine? - Any% (No ACE)", run.GetExtendedName());
        Assert.Equal("Super Mario Sunshine - Any% (No ACE)", run.GetExtendedFileName());
        Assert.Equal("Any% (No ACE)", NewRun("Any% (No ACE)").GetExtendedName());
    }

    [Fact]
    public void Title_Finished_Runs_Count_Includes_The_Attempt_That_Just_Ended()
    {
        Run run = NewRun("Any%");
        run.AttemptCount = 5;
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(TimeSpan.FromSeconds(10)), null, null, null));
        run.AttemptHistory.Add(new Attempt(2, new SplitTime(null, TimeSpan.FromSeconds(9)), null, null, null));
        run.AttemptHistory.Add(new Attempt(3, default, null, null, null));
        var both = new TitleComponentSettings { ShowAttemptCount = true, ShowFinishedRunsCount = true };

        // Only real-time finishes count, as upstream.
        Assert.Equal("1/5", TitleLogic.CountText(run, TimerPhase.NotRunning, both));
        Assert.Equal("2/5", TitleLogic.CountText(run, TimerPhase.Ended, both));
        Assert.Equal("5", TitleLogic.CountText(run, TimerPhase.Ended, new TitleComponentSettings { ShowAttemptCount = true }));
        Assert.Equal("", TitleLogic.CountText(run, TimerPhase.Ended, new TitleComponentSettings { ShowAttemptCount = false }));
    }

    private static RecentSplitsFile File(string path, string game, string category) => new(path, game, category, "RealTime");

    private static Run NewRun(string category)
    {
        var run = new Run { GameName = "", CategoryName = category };
        run.AddSegment("S");
        return run;
    }
}

/// <summary>The component settings' Comparison dropdowns (upstream *Settings controls).</summary>
public class ComparisonChoicesTests
{
    private static readonly string[] RunComparisons =
        ["Personal Best", "Rival", "Best Segments", "Best Split Times", "Average Segments", "None"];

    [Fact]
    public void Each_Dropdown_Leaves_Out_What_Upstream_Leaves_Out()
    {
        Assert.Equal(
            new[] { "Current Comparison", "Personal Best", "Rival", "Best Segments", "Average Segments" },
            ComparisonChoices.PreviousSegment(RunComparisons, "Current Comparison"));
        Assert.Equal(
            new[] { "Current Comparison", "Personal Best", "Rival", "Average Segments" },
            ComparisonChoices.PossibleTimeSave(RunComparisons, "Current Comparison"));
        Assert.Equal(
            new[] { "Current Comparison", "Personal Best", "Rival", "Best Segments", "Best Split Times", "Average Segments" },
            ComparisonChoices.SubsplitsHeader(RunComparisons, "Current Comparison"));
        Assert.Equal(
            ComparisonChoices.PreviousSegment(RunComparisons, "Best Segments"),
            ComparisonChoices.DetailedTimer(RunComparisons, "Best Segments"));
    }

    [Fact]
    public void A_Saved_Comparison_The_Run_Lacks_Stays_Selectable()
    {
        List<string> items = ComparisonChoices.PreviousSegment(RunComparisons, "Median Segments");
        Assert.Equal("Median Segments", items[^1]);
    }

    [Fact]
    public void Segment_Columns_Drop_Best_Split_Times_And_Reset_It()
    {
        string saved = "Best Split Times";
        List<string> split = ComparisonChoices.Column(RunComparisons, ColumnType.SplitTime, ref saved);
        Assert.Contains("Best Split Times", split);
        Assert.Equal("Best Split Times", saved);

        List<string> segment = ComparisonChoices.Column(RunComparisons, ColumnType.SegmentTime, ref saved);
        Assert.DoesNotContain("Best Split Times", segment);
        Assert.DoesNotContain("None", segment);
        Assert.Equal("Current Comparison", saved);
    }
}
