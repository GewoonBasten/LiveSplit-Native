using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class SubsplitsColumnsSettingsTests
{
    [Fact]
    public void Subsplits_Columns_Edit_And_Persist_RoundTrip()
    {
        var settings = new SubsplitsComponentSettings
        {
            ShowColumnLabels = true,
            LabelsColor = ArgbColor.FromArgb(255, 200, 200, 200),
            AutomaticAbbreviation = true,
            DisplayIcons = true,
            IconShadows = true,
            OverrideTextColor = true,
            BeforeNamesColor = ArgbColor.FromArgb(255, 1, 2, 3),
            SectionTimer = true,
            SectionTimerGradient = true,
            Visibility = SubsplitsComponentSettings.SubsplitVisibility.Contextual,
        };
        settings.Columns.Clear();
        settings.Columns.Add(new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method"));
        settings.Columns.Add(new ColumnData("Seg", ColumnType.SegmentTime, "Current Comparison", "Game Time"));
        settings.Columns.Add(new ColumnData("Time", ColumnType.SplitTime, "Rival", "Real Time"));

        // Simulate editor: reorder + rename selected column
        (settings.Columns[0], settings.Columns[1]) = (settings.Columns[1], settings.Columns[0]);
        settings.Columns[0].Name = "Segment";

        SubsplitsComponentSettings again = SubsplitsComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.True(again.ShowColumnLabels);
        Assert.Equal(settings.LabelsColor, again.LabelsColor);
        Assert.True(again.AutomaticAbbreviation);
        Assert.True(again.IconShadows);
        Assert.True(again.OverrideTextColor);
        Assert.Equal(settings.BeforeNamesColor, again.BeforeNamesColor);
        Assert.True(again.SectionTimer);
        Assert.True(again.SectionTimerGradient);
        Assert.Equal(3, again.Columns.Count);
        Assert.Equal("Segment", again.Columns[0].Name);
        Assert.Equal(ColumnType.SegmentTime, again.Columns[0].Type);
        Assert.Equal("Game Time", again.Columns[0].TimingMethod);
        Assert.Equal("+/-", again.Columns[1].Name);
        Assert.Equal("Rival", again.Columns[2].Comparison);
        Assert.Equal("Real Time", again.Columns[2].TimingMethod);
    }

    [Fact]
    public void Subsplits_Controls_Presence_Inventory_In_Xml()
    {
        var settings = new SubsplitsComponentSettings
        {
            LockLastSplit = true,
            Display2Rows = true,
            IndentBlankIcons = true,
            OverrideDeltasColor = true,
            OverrideTimesColor = true,
            OverrideSubsplitColor = true,
            ShowSectionHeader = true,
            HeaderText = true,
            HeaderTimes = true,
            DeltasAccuracy = TimeAccuracy.Tenths,
            SplitTimesAccuracy = TimeAccuracy.Seconds,
        };
        string xml = settings.ToInnerXml();
        Assert.Contains("LockLastSplit", xml);
        Assert.Contains("Display2Rows", xml);
        Assert.Contains("IndentBlankIcons", xml);
        Assert.Contains("OverrideDeltasColor", xml);
        Assert.Contains("OverrideTimesColor", xml);
        Assert.Contains("OverrideSubsplitColor", xml);
        Assert.Contains("ShowHeader", xml);
        Assert.Contains("HeaderText", xml);
        Assert.Contains("HeaderTimes", xml);
        Assert.Contains("<Columns>", xml);
        Assert.Contains("<Name>+/-</Name>", xml);
    }

    [Fact]
    public void Splits_OverrideColors_And_Icons_Persist()
    {
        var settings = new SplitsComponentSettings
        {
            IconShadows = true,
            Display2Rows = true,
            AutomaticAbbreviations = true,
            ShowColumnLabels = true,
            LabelsColor = ArgbColor.FromArgb(255, 10, 20, 30),
            OverrideTextColor = true,
            BeforeNamesColor = ArgbColor.FromArgb(255, 1, 1, 1),
            OverrideTimesColor = true,
            AfterTimesColor = ArgbColor.FromArgb(255, 2, 2, 2),
            OverrideDeltasColor = true,
            DeltasColor = ArgbColor.FromArgb(255, 3, 3, 3),
            BackgroundGradient = "Horizontal",
        };
        SplitsComponentSettings again = SplitsComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.True(again.IconShadows);
        Assert.True(again.Display2Rows);
        Assert.True(again.AutomaticAbbreviations);
        Assert.True(again.ShowColumnLabels);
        Assert.Equal(settings.LabelsColor, again.LabelsColor);
        Assert.True(again.OverrideTextColor);
        Assert.Equal(settings.BeforeNamesColor, again.BeforeNamesColor);
        Assert.True(again.OverrideTimesColor);
        Assert.Equal(settings.AfterTimesColor, again.AfterTimesColor);
        Assert.True(again.OverrideDeltasColor);
        Assert.Equal(settings.DeltasColor, again.DeltasColor);
        Assert.Equal("Horizontal", again.BackgroundGradient);
    }

    [Fact]
    public void PreviousSegment_PossibleTimeSave_And_TextColor_Persist()
    {
        var settings = new PreviousSegmentComponentSettings
        {
            ShowPossibleTimeSave = true,
            TimeSaveAccuracy = TimeAccuracy.Hundredths,
            OverrideTextColor = true,
            TextColor = ArgbColor.FromArgb(255, 9, 8, 7),
            BackgroundGradient = "Horizontal",
        };
        PreviousSegmentComponentSettings again =
            PreviousSegmentComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.True(again.ShowPossibleTimeSave);
        Assert.Equal(TimeAccuracy.Hundredths, again.TimeSaveAccuracy);
        Assert.True(again.OverrideTextColor);
        Assert.Equal(settings.TextColor, again.TextColor);
        Assert.Equal("Horizontal", again.BackgroundGradient);
    }
}
