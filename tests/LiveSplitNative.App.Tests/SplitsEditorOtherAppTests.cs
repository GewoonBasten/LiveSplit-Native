using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Model;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>The Splits Editor's Other... menu (Clear History, Clear Times, Clean Sum of Best) and Use Layout.</summary>
[Collection(MainWindowCollection.Name)]
public class SplitsEditorOtherAppTests
{
    /// <summary>Three splits; attempt 3 skipped B and took 5 s for B and C, faster than their golds' 20 s.</summary>
    private static Run RunWithATooFastTime()
    {
        var run = new Run { GameName = "Game", CategoryName = "Any%" };
        run.AddSegment("A");
        run.AddSegment("B");
        run.AddSegment("C");
        foreach (Segment segment in run.Segments)
        {
            segment.BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(10));
        }

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            run[0].SegmentHistory[attempt] = new SplitTime(TimeSpan.FromSeconds(10));
            run[1].SegmentHistory[attempt] = new SplitTime(TimeSpan.FromSeconds(10));
            run[2].SegmentHistory[attempt] = new SplitTime(TimeSpan.FromSeconds(10));
        }

        run[0].SegmentHistory[3] = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].SegmentHistory[3] = new SplitTime(null, null);
        run[2].SegmentHistory[3] = new SplitTime(TimeSpan.FromSeconds(5));
        return run;
    }

    private static void Invoke(SplitsEditorWindow editor, string item)
    {
        NativeMenuAction action = editor.OtherMenuModel().Items.OfType<NativeMenuAction>().Single(a => a.Header == item);
        action.Invoke();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Other_Has_Windows_Three_Tools_And_Is_On_While_Segments_Can_Change()
    {
        var editor = new SplitsEditorWindow(RunWithATooFastTime(), TimingMethod.RealTime);
        Assert.Equal(
            ["Clear History", "Clear Times", "Clean Sum of Best"],
            editor.OtherMenuModel().Items.OfType<NativeMenuAction>().Select(a => a.Header));
        Assert.True(editor.FindControl<Button>("BtnOther")!.IsEnabled);
        Assert.False(new SplitsEditorWindow(RunWithATooFastTime(), TimingMethod.RealTime, allowChangingSegments: false)
            .FindControl<Button>("BtnOther")!.IsEnabled);
    }

    [AvaloniaFact]
    public void Clean_Sum_Of_Best_Asks_About_The_Too_Fast_Time_And_Removes_It_On_Yes()
    {
        var editor = new SplitsEditorWindow(RunWithATooFastTime(), TimingMethod.RealTime);
        var asked = new List<string>();
        editor.CleanAnswerForTests = text =>
        {
            asked.Add(text);
            return true;
        };
        editor.MessagesForTests = [];
        Invoke(editor, "Clean Sum of Best");

        string question = Assert.Single(asked);
        Assert.Equal(
            "You had a Real Time segment time of 5.00 between A and C, which is faster than the Combined Best Segments of 20.00. "
                + "Do you think that this segment time is inaccurate and should be removed?",
            question);
        Run cleaned = editor.BuildRunForTests();
        Assert.False(cleaned[2].SegmentHistory.ContainsKey(3));
        Assert.True(cleaned[2].SegmentHistory.ContainsKey(1));
        Assert.Empty(editor.MessagesForTests);

        // Nothing left to ask: "No times to clean".
        asked.Clear();
        Invoke(editor, "Clean Sum of Best");
        Assert.Empty(asked);
        Assert.Equal(("No times to clean", "No times to clean. There are no potentially invalid segment history elements in the Sum of Best."), Assert.Single(editor.MessagesForTests));
    }

    [AvaloniaFact]
    public void Clean_Sum_Of_Best_Keeps_The_Time_On_No()
    {
        var editor = new SplitsEditorWindow(RunWithATooFastTime(), TimingMethod.RealTime)
        {
            CleanAnswerForTests = _ => false,
            MessagesForTests = [],
        };
        Invoke(editor, "Clean Sum of Best");
        Assert.True(editor.BuildRunForTests()[2].SegmentHistory.ContainsKey(3));
    }

    [AvaloniaFact]
    public void Clear_History_And_Clear_Times_Say_So()
    {
        var editor = new SplitsEditorWindow(RunWithATooFastTime(), TimingMethod.RealTime) { MessagesForTests = [] };
        Invoke(editor, "Clear History");
        Assert.All(editor.BuildRunForTests().Segments, s => Assert.Empty(s.SegmentHistory));
        Invoke(editor, "Clear Times");
        Run cleared = editor.BuildRunForTests();
        Assert.Equal(0, cleared.AttemptCount);
        Assert.All(cleared.Segments, s => Assert.Null(s.BestSegmentTime.RealTime));
        Assert.Equal([("History cleared", "History cleared!"), ("Times cleared", "Times cleared!")], editor.MessagesForTests);
    }

    [AvaloniaFact]
    public async Task Use_Layout_Opens_The_Layout_And_The_Splits_Keep_It()
    {
        var applied = new List<string>();
        var editor = new SplitsEditorWindow(RunWithATooFastTime(), TimingMethod.RealTime)
        {
            RecentLayouts = ["/layouts/old.lsl", "", "/layouts/main.lsl"],
            ApplyLayoutPath = path =>
            {
                applied.Add(path);
                return Task.FromResult(true);
            },
        };
        editor.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            ComboBox choices = editor.FindControl<ComboBox>("LayoutToUseBox")!;
            Assert.Equal(["Default Layout", "main", "old"], choices.Items.Select(i => i!.ToString()));
            Assert.False(editor.FindControl<CheckBox>("UseLayoutBox")!.IsChecked);

            Assert.True(await editor.UseLayoutForTestsAsync("/layouts/main.lsl"));
            Assert.Equal(["/layouts/main.lsl"], applied);
            Assert.Equal("/layouts/main.lsl", editor.BuildRunForTests().LayoutPath);

            // Unticked: the splits keep no layout (nothing is opened).
            editor.FindControl<CheckBox>("UseLayoutBox")!.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("", editor.BuildRunForTests().LayoutPath);
            Assert.Single(applied);
        }
        finally
        {
            editor.Close();
        }
    }
}
