using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

public class SplitsColumnsTests
{
    [Fact]
    public void DefaultColumns_RoundTrip_Through_SplitsSettings()
    {
        var settings = new SplitsComponentSettings
        {
            ShowColumnLabels = true,
            Columns =
            {
                new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method"),
                new ColumnData("Time", ColumnType.SplitTime, "Current Comparison", "Current Timing Method"),
                new ColumnData("Rival", ColumnType.SplitTime, "Rival", "Current Timing Method"),
            },
        };

        SplitsComponentSettings again = SplitsComponentSettings.FromInnerXml(settings.ToInnerXml());
        Assert.True(again.ShowColumnLabels);
        Assert.Equal(3, again.Columns.Count);
        Assert.Equal("+/-", again.Columns[0].Name);
        Assert.Equal(ColumnType.Delta, again.Columns[0].Type);
        Assert.Equal("Rival", again.Columns[2].Name);
        Assert.Equal("Rival", again.Columns[2].Comparison);
        Assert.Equal(ColumnType.SplitTime, again.Columns[2].Type);
    }

    [Fact]
    public void Normalize_Does_Not_Add_Columns_Per_Comparison()
    {
        var settings = new SplitsComponentSettings();
        settings.Columns.AddRange(ColumnData.CreateDefault());
        Assert.False(settings.NormalizeComparisonColumns(["Personal Best", "Rival", "WR"]));
        Assert.Equal(2, settings.Columns.Count);
        Assert.All(settings.Columns, c => Assert.Equal("Current Comparison", c.Comparison));
    }

    [Fact]
    public void Normalize_Collapses_AutoExpanded_All_Comparisons_Pattern()
    {
        var settings = new SplitsComponentSettings();
        settings.Columns.AddRange(ColumnData.CreateDefault());
        settings.Columns.Add(new ColumnData("Rival", ColumnType.SplitTime, "Rival", "Current Timing Method"));
        settings.Columns.Add(new ColumnData("WR", ColumnType.SplitTime, "WR", "Current Timing Method"));

        Assert.True(settings.NormalizeComparisonColumns(["Personal Best", "Rival", "WR"]));
        Assert.Equal(2, settings.Columns.Count);
        Assert.DoesNotContain(settings.Columns, c => c.Comparison == "Rival");
        Assert.DoesNotContain(settings.Columns, c => c.Comparison == "WR");
        Assert.All(settings.Columns, c => Assert.Equal("Current Comparison", c.Comparison));
    }

    [Fact]
    public void Normalize_Preserves_Partial_Intentional_Extra_Column()
    {
        var settings = new SplitsComponentSettings();
        settings.Columns.AddRange(ColumnData.CreateDefault());
        // Only Rival — WR exists on run but was not auto-added → treat as intentional.
        settings.Columns.Add(new ColumnData("Rival", ColumnType.SplitTime, "Rival", "Current Timing Method"));

        Assert.False(settings.NormalizeComparisonColumns(["Personal Best", "Rival", "WR"]));
        Assert.Equal(3, settings.Columns.Count);
        Assert.Contains(settings.Columns, c => c.Comparison == "Rival");
    }

    [Fact]
    public void Normalize_Preserves_Custom_Named_Column()
    {
        var settings = new SplitsComponentSettings();
        settings.Columns.AddRange(ColumnData.CreateDefault());
        settings.Columns.Add(new ColumnData("Gold", ColumnType.SplitTime, "Rival", "Current Timing Method"));
        settings.Columns.Add(new ColumnData("WR", ColumnType.SplitTime, "WR", "Current Timing Method"));

        // Not Name==Comparison for Rival ("Gold") → not full auto set → keep.
        Assert.False(settings.NormalizeComparisonColumns(["Personal Best", "Rival", "WR"]));
        Assert.Equal(4, settings.Columns.Count);
    }

    [Fact]
    public void Legacy_Settings_Without_Columns_Synthesize_Default_Pair()
    {
        const string xml = """
            <Version>1.3</Version>
            <ShowSplitTimes>True</ShowSplitTimes>
            <Comparison>Personal Best</Comparison>
            """;
        SplitsComponentSettings settings = SplitsComponentSettings.FromInnerXml(xml);
        Assert.Equal(2, settings.Columns.Count);
        Assert.Equal(ColumnType.Delta, settings.Columns[0].Type);
        Assert.Equal(ColumnType.SplitTime, settings.Columns[1].Type);
        Assert.Equal("Personal Best", settings.Columns[0].Comparison);
    }
}
