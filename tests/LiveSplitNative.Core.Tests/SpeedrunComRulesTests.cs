using LiveSplitNative.Core.Web;

namespace LiveSplitNative.Core.Tests;

/// <summary>The Additional Info tab's rules (MetadataControl.refreshRules, AppendBBCode) and run addresses.</summary>
public class SpeedrunComRulesTests
{
    private static SrcGame Game(SrcTimingMethod time, bool video) =>
        new("g", "Game", time, [], []) { RequiresVideo = video };

    [Fact]
    public void Intro_Names_The_Timing_And_The_Video_Rule()
    {
        Assert.Null(SpeedrunComRules.Intro(Game(SrcTimingMethod.RealTime, false)));
        Assert.Equal("Runs of this game require video proof.", SpeedrunComRules.Intro(Game(SrcTimingMethod.RealTime, true)));
        Assert.Equal("Runs of this game are timed with the Game Time.", SpeedrunComRules.Intro(Game(SrcTimingMethod.GameTime, false)));
        Assert.Equal(
            "Runs of this game are timed without the loading times and require video proof.",
            SpeedrunComRules.Intro(Game(SrcTimingMethod.RealTimeWithoutLoads, true)));
    }

    [Fact]
    public void BBCode_Styles_Text_And_Keeps_Unknown_Codes()
    {
        IReadOnlyList<BBCodeSpan> spans = SpeedrunComRules.ParseBBCode("A [b]bold[/b] [i]it[/i] [x] [url=https://a.b]link[/url] [big]B[/big][*]item");
        Assert.Equal("A bold it [x] link: https://a.b B    ●  item", string.Concat(spans.Select(s => s.Text)));
        Assert.True(spans.Single(s => s.Text == "bold").Bold);
        Assert.True(spans.Single(s => s.Text == "it").Italic);
        Assert.Equal(12, spans.Single(s => s.Text == "B").Size);
        Assert.False(spans.Single(s => s.Text == "[x] ").Bold);

        IReadOnlyList<BBCodeSpan> spoiler = SpeedrunComRules.ParseBBCode("x[spoiler]secret[/spoiler]y");
        Assert.True(spoiler.Single(s => s.Text == "secret").Hidden);
        Assert.Contains(spoiler, s => s.Text == "Spoiler: " && s.Bold);
    }

    [Theory]
    [InlineData("https://www.speedrun.com/sms/runs/zx8q7l6m", "zx8q7l6m")]
    [InlineData("speedrun.com/run/abc", "abc")]
    [InlineData("http://speedrun.com/runs/abc", "abc")]
    [InlineData("abc123", "abc123")]
    [InlineData("https://www.speedrun.com/sms", null)]
    [InlineData("not a url!", null)]
    [InlineData("", null)]
    public void Run_Ids_Come_From_Speedrun_Com_Run_Addresses(string address, string? id) =>
        Assert.Equal(id, SpeedrunComSubmitter.GetRunIdFromSiteUri(address));
}
