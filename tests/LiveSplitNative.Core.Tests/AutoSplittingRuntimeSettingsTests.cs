using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>LiveSplit.AutoSplittingRuntime's layout settings (Version 1.0, CustomSettings).</summary>
public sealed class AutoSplittingRuntimeSettingsTests
{
    private const string Windows =
        "<Version>1.0</Version><ScriptPath>/Users/basten/splitters/sms.wasm</ScriptPath><CustomSettings>"
        + "<Setting id=\"start\" type=\"bool\">True</Setting>"
        + "<Setting id=\"mode\" type=\"string\" value=\"any%\" />"
        + "<Setting id=\"count\" type=\"i64\">-3</Setting>"
        + "<Setting id=\"scale\" type=\"f64\">1.5</Setting>"
        + "<Setting id=\"group\" type=\"map\"><Setting id=\"inner\" type=\"bool\">False</Setting></Setting>"
        + "<Setting id=\"items\" type=\"list\"><Setting type=\"i64\">1</Setting><Setting type=\"string\" value=\"two\" /></Setting>"
        + "</CustomSettings>";

    [Fact]
    public void Windows_Settings_Read_And_Write_Back_The_Same()
    {
        AutoSplittingRuntimeSettings settings = AutoSplittingRuntimeSettings.FromInnerXml(Windows);
        Assert.Equal("/Users/basten/splitters/sms.wasm", settings.ScriptPath);
        IReadOnlyList<KeyValuePair<string, AsrSettingValue>> map = settings.CustomSettings!;
        Assert.Equal(["start", "mode", "count", "scale", "group", "items"], map.Select(p => p.Key));
        Assert.True(map[0].Value.Bool);
        Assert.Equal("any%", map[1].Value.String);
        Assert.Equal(-3, map[2].Value.I64);
        Assert.Equal(1.5, map[3].Value.F64);
        Assert.Equal(AsrValueKind.Map, map[4].Value.Kind);
        Assert.False(map[4].Value.Map[0].Value.Bool);
        Assert.Equal(["1", "\"two\""], map[5].Value.List.Select(v => v.ToString()));
        Assert.Equal(Windows, settings.ToInnerXml());
    }

    [Fact]
    public void New_And_Unreadable_Settings()
    {
        Assert.Equal("<Version>1.0</Version><ScriptPath></ScriptPath><CustomSettings />", new AutoSplittingRuntimeSettings().ToInnerXml());
        AutoSplittingRuntimeSettings bad = AutoSplittingRuntimeSettings.FromInnerXml(
            "<Version>1.0</Version><ScriptPath>a.wasm</ScriptPath><CustomSettings><Setting id=\"x\" type=\"color\">1</Setting></CustomSettings>");
        Assert.Equal("a.wasm", bad.ScriptPath);
        Assert.Null(bad.CustomSettings);
        Assert.Null(AutoSplittingRuntimeSettings.FromInnerXml("<Version>1.0</Version><ScriptPath>a.wasm</ScriptPath>").CustomSettings);
    }
}
