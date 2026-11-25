using System.Text;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Model.Comparisons;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.Core.Tests;

/// <summary>Choose Active Comparisons settings and the Windows TimerForm comparison bookkeeping.</summary>
public class ComparisonSwitchingTests
{
    [Fact]
    public void Component_Comparisons_Fall_Back_To_The_Current_One_Like_Upstream()
    {
        var run = new Run();
        run.AddSegment("S");
        run.CustomComparisons.Add("Rival");
        var state = new LiveSplitState(run) { CurrentComparison = "Rival" };

        Assert.Equal("Rival", ComparisonSwitching.ResolveComponentComparison(state, "Current Comparison"));
        Assert.Equal("Rival", ComparisonSwitching.ResolveComponentComparison(state, ""));
        Assert.Equal("Best Segments", ComparisonSwitching.ResolveComponentComparison(state, "Best Segments"));
        Assert.Equal("Personal Best", ComparisonSwitching.ResolveComponentComparison(state, "Personal Best"));
        // Median Segments is not an active generator here, so the component follows the timer.
        Assert.Equal("Rival", ComparisonSwitching.ResolveComponentComparison(state, "Median Segments"));
        Assert.Equal("Rival", ComparisonSwitching.ResolveComponentComparison(state, "Deleted Comparison"));

        Assert.True(run.HasComparison("Average Segments"));
        Assert.False(run.HasComparison("average segments"));
        Assert.Equal(run.Comparisons.Contains("Median Segments"), run.HasComparison("Median Segments"));
    }

    [Fact]
    public void Default_Generator_States_Match_Windows()
    {
        var settings = new AppSettings();

        Assert.Equal(
            [
                ("Best Segments", true), ("Best Split Times", false), ("Average Segments", true),
                ("Median Segments", false), ("Worst Segments", false), ("Balanced PB", false),
                ("Latest Run", false), ("Golf HCP", false), ("None", false),
            ],
            settings.ComparisonGeneratorStates.Select(p => (p.Key, p.Value)));
        Assert.Equal(20, settings.HcpHistorySize);
        Assert.Equal(8, settings.HcpNBestRuns);
    }

    [Fact]
    public void Generator_States_Round_Trip_Through_Settings_Cfg()
    {
        var settings = new AppSettings { HcpHistorySize = 12, HcpNBestRuns = 4 };
        Dictionary<string, bool> states = AppSettings.CreateDefaultComparisonGeneratorStates();
        states["Average Segments"] = false;
        states["Golf HCP"] = true;
        settings.SetComparisonGeneratorStates(states);

        using var stream = new MemoryStream();
        XmlAppSettingsSerializer.Save(settings, stream);
        stream.Position = 0;
        AppSettings loaded = XmlAppSettingsSerializer.Load(stream);

        Assert.Equal(settings.ComparisonGeneratorStates, loaded.ComparisonGeneratorStates);
        Assert.Equal(12, loaded.HcpHistorySize);
        Assert.Equal(4, loaded.HcpNBestRuns);
    }

    [Fact]
    public void Windows_Settings_Cfg_Generator_States_Load_And_Unknown_Names_Are_Ignored()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Settings version="1.8.18">
              <ComparisonGeneratorStates>
                <Generator name="Best Segments">True</Generator>
                <Generator name="Best Split Times">True</Generator>
                <Generator name="Average Segments">False</Generator>
                <Generator name="Golf HCP" HcpHistorySize="30" HcpNBestRuns="5">True</Generator>
                <Generator name="Some Plugin Comparison">True</Generator>
              </ComparisonGeneratorStates>
            </Settings>
            """;
        AppSettings loaded = XmlAppSettingsSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        Assert.True(loaded.ComparisonGeneratorStates["Best Split Times"]);
        Assert.False(loaded.ComparisonGeneratorStates["Average Segments"]);
        Assert.True(loaded.ComparisonGeneratorStates["Golf HCP"]);
        Assert.False(loaded.ComparisonGeneratorStates["Median Segments"]);
        Assert.False(loaded.ComparisonGeneratorStates.ContainsKey("Some Plugin Comparison"));
        Assert.Equal(30, loaded.HcpHistorySize);
        Assert.Equal(5, loaded.HcpNBestRuns);
    }

    [Fact]
    public void Settings_Cfg_Without_Generator_States_Keeps_Windows_Defaults()
    {
        const string xml = """<Settings version="1.8.18"><SimpleSumOfBest>True</SimpleSumOfBest></Settings>""";
        AppSettings loaded = XmlAppSettingsSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        Assert.Equal(AppSettings.CreateDefaultComparisonGeneratorStates(), loaded.ComparisonGeneratorStates);
        Assert.True(loaded.ToComparisonGeneratorSettings().SimpleSumOfBest);
    }

    [Fact]
    public void SwitchComparisonGenerators_Uses_Upstream_Order_Falls_Back_And_Regenerates()
    {
        var state = new LiveSplitState(RunWithHistory());
        state.CurrentComparison = "Average Segments";
        Dictionary<string, bool> states = AppSettings.CreateDefaultComparisonGeneratorStates();
        states["Average Segments"] = false;
        states["Median Segments"] = true;
        states["None"] = true;

        ComparisonSwitching.SwitchComparisonGenerators(state, states, new ComparisonGeneratorSettings());

        Assert.Equal(["Best Segments", "Median Segments", "None"], state.Run.ComparisonGenerators.Select(g => g.Name));
        Assert.Equal(["Personal Best", "Rival", "Best Segments", "Median Segments", "None"], state.Run.Comparisons);
        Assert.Equal(Run.PersonalBestComparisonName, state.CurrentComparison);
        // Weighted median of 10 s (weight 0.75) and 11 s (weight 1): 11 s.
        Assert.Equal(TimeSpan.FromSeconds(11), state.Run[0].Comparisons["Median Segments"].RealTime);
        Assert.Equal(TimeSpan.FromSeconds(30), state.Run[1].Comparisons["Best Segments"].RealTime);
    }

    [Fact]
    public void SetRun_Carries_Generators_Regenerates_And_Keeps_Comparison_When_Present()
    {
        var state = new LiveSplitState(new Run());
        Dictionary<string, bool> states = AppSettings.CreateDefaultComparisonGeneratorStates();
        states["Latest Run"] = true;
        ComparisonSwitching.SwitchComparisonGenerators(state, states, new ComparisonGeneratorSettings());
        state.CurrentComparison = "Latest Run";

        Run loaded = RunWithHistory();
        ComparisonSwitching.SetRun(state, loaded, new ComparisonGeneratorSettings());

        Assert.Same(loaded, state.Run);
        Assert.Equal(["Best Segments", "Average Segments", "Latest Run"], loaded.ComparisonGenerators.Select(g => g.Name));
        Assert.All(loaded.ComparisonGenerators, g => Assert.Same(loaded, g.Run));
        Assert.Equal("Latest Run", state.CurrentComparison);
        Assert.Equal(TimeSpan.FromSeconds(32), loaded[1].Comparisons["Latest Run"].RealTime);

        state.CurrentComparison = "Rival";
        ComparisonSwitching.SetRun(state, new Run(), new ComparisonGeneratorSettings());
        Assert.Equal(Run.PersonalBestComparisonName, state.CurrentComparison);
    }

    [Fact]
    public void SwitchComparisonNext_And_Previous_Cycle_Run_Comparisons_As_Upstream()
    {
        Run run = RunWithHistory();
        var state = new LiveSplitState(run);
        var model = new TimerModel { CurrentState = state };

        model.SwitchComparisonNext();
        Assert.Equal("Rival", state.CurrentComparison);
        model.SwitchComparisonNext();
        Assert.Equal("Best Segments", state.CurrentComparison);
        model.SwitchComparisonNext();
        model.SwitchComparisonNext();
        Assert.Equal(Run.PersonalBestComparisonName, state.CurrentComparison);
        model.SwitchComparisonPrevious();
        Assert.Equal("Average Segments", state.CurrentComparison);

        // A comparison the run does not have counts as index -1 upstream.
        state.CurrentComparison = "Gone";
        model.SwitchComparisonNext();
        Assert.Equal(Run.PersonalBestComparisonName, state.CurrentComparison);
        state.CurrentComparison = "Gone";
        model.SwitchComparisonPrevious();
        Assert.Equal("Best Segments", state.CurrentComparison);
    }

    /// <summary>Two segments, PB 12 / 35, a "Rival" comparison and two finished attempts.</summary>
    private static Run RunWithHistory()
    {
        var run = new Run();
        run.AddSegment("A");
        run.AddSegment("B");
        run.CustomComparisons.Add("Rival");
        run[0].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(12));
        run[1].PersonalBestSplitTime = new SplitTime(TimeSpan.FromSeconds(35));
        run[0].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].BestSegmentTime = new SplitTime(TimeSpan.FromSeconds(20));
        run.AttemptHistory.Add(new Attempt(1, new SplitTime(TimeSpan.FromSeconds(33)), null, null, null));
        run.AttemptHistory.Add(new Attempt(2, new SplitTime(TimeSpan.FromSeconds(32)), null, null, null));
        run[0].SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(10));
        run[1].SegmentHistory[1] = new SplitTime(TimeSpan.FromSeconds(23));
        run[0].SegmentHistory[2] = new SplitTime(TimeSpan.FromSeconds(11));
        run[1].SegmentHistory[2] = new SplitTime(TimeSpan.FromSeconds(21));
        return run;
    }
}
