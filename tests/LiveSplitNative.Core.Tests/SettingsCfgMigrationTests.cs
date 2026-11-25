using System.Text;
using System.Xml;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.Core.Tests;

/// <summary>
/// M-B settings.cfg migration: a Windows LiveSplit settings.cfg (as XMLSettingsSaver writes it,
/// and older versions as XMLSettingsFactory reads them) is read, and saving it gives Windows
/// back everything it had: settings this app does not use, other hotkey profiles, unknown
/// elements, and hotkeys in Windows' key names.
/// </summary>
public class SettingsCfgMigrationTests
{
    /// <summary>A settings.cfg as Windows LiveSplit 1.8 writes it (XMLSettingsSaver, version 1.8.18).</summary>
    private const string Windows = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Settings version="1.8.18">
          <HotkeyProfiles>
            <HotkeyProfile name="Default">
              <SplitKey>NumPad1</SplitKey>
              <ResetKey>NumPad3</ResetKey>
              <SkipKey>NumPad2</SkipKey>
              <UndoKey>NumPad8</UndoKey>
              <PauseKey>D1, Control</PauseKey>
              <ToggleGlobalHotkeys>F5, Shift, Control, Alt</ToggleGlobalHotkeys>
              <SwitchComparisonPrevious>NumPad4</SwitchComparisonPrevious>
              <SwitchComparisonNext>NumPad6</SwitchComparisonNext>
              <GlobalHotkeysEnabled>True</GlobalHotkeysEnabled>
              <DeactivateHotkeysForOtherPrograms>False</DeactivateHotkeysForOtherPrograms>
              <DoubleTapPrevention>True</DoubleTapPrevention>
              <HotkeyDelay>0.5</HotkeyDelay>
              <AllowGamepadsAsHotkeys>False</AllowGamepadsAsHotkeys>
            </HotkeyProfile>
            <HotkeyProfile name="Pad">
              <SplitKey>Button 1 Xbox Controller</SplitKey>
              <ResetKey />
              <SkipKey />
              <UndoKey />
              <PauseKey />
              <ToggleGlobalHotkeys />
              <SwitchComparisonPrevious />
              <SwitchComparisonNext />
              <GlobalHotkeysEnabled>True</GlobalHotkeysEnabled>
              <DeactivateHotkeysForOtherPrograms>False</DeactivateHotkeysForOtherPrograms>
              <DoubleTapPrevention>True</DoubleTapPrevention>
              <HotkeyDelay>0</HotkeyDelay>
              <AllowGamepadsAsHotkeys>True</AllowGamepadsAsHotkeys>
            </HotkeyProfile>
          </HotkeyProfiles>
          <WarnOnReset>False</WarnOnReset>
          <RaceViewer>Kadgar</RaceViewer>
          <AgreedToSRLRules>True</AgreedToSRLRules>
          <EnableDPIAwareness>True</EnableDPIAwareness>
          <UILanguage>nl-NL</UILanguage>
          <RecentSplits>
            <SplitsFile gameName="Super Mario Sunshine" categoryName="Any%" lastTimingMethod="GameTime" lastHotkeyProfile="Pad">C:\Splits\sms.lss</SplitsFile>
            <SplitsFile gameName="" categoryName="" lastTimingMethod="RealTime" lastHotkeyProfile="Default"></SplitsFile>
          </RecentSplits>
          <RecentLayouts>
            <LayoutPath>C:\Layouts\main.lsl</LayoutPath>
          </RecentLayouts>
          <LastComparison>Best Segments</LastComparison>
          <SimpleSumOfBest>True</SimpleSumOfBest>
          <RefreshRate>60</RefreshRate>
          <ServerPort>16835</ServerPort>
          <ServerStartup>1</ServerStartup>
          <ServerState>2</ServerState>
          <ComparisonGeneratorStates>
            <Generator name="Best Segments">True</Generator>
            <Generator name="Best Split Times">True</Generator>
            <Generator name="Average Segments">False</Generator>
            <Generator name="Median Segments">False</Generator>
            <Generator name="Worst Segments">False</Generator>
            <Generator name="Balanced PB">False</Generator>
            <Generator name="Latest Run">True</Generator>
            <Generator name="Golf HCP" HcpHistorySize="30" HcpNBestRuns="5">True</Generator>
            <Generator name="None">False</Generator>
          </ComparisonGeneratorStates>
          <RaceProviderPlugins>
            <Plugin name="LiveSplit.Racetime.dll" enabled="True"><LoadChatHistory>False</LoadChatHistory><HideResults>True</HideResults></Plugin>
            <Plugin name="LiveSplit.SRL.dll" enabled="False"><Channel>#speedrunslive</Channel></Plugin>
          </RaceProviderPlugins>
          <ActiveAutoSplitters>
            <AutoSplitter>Super Mario Sunshine</AutoSplitter>
          </ActiveAutoSplitters>
          <TimerDrift><![CDATA[HOviNhoA8D8=]]></TimerDrift>
        </Settings>
        """;

    private static AppSettings Load(string xml) => XmlAppSettingsSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    private static XmlDocument Save(AppSettings settings)
    {
        using var stream = new MemoryStream();
        XmlAppSettingsSerializer.Save(settings, stream);
        stream.Position = 0;
        var document = new XmlDocument();
        document.Load(stream);
        return document;
    }

    [Fact]
    public void A_Windows_Settings_Cfg_Is_Read()
    {
        AppSettings settings = Load(Windows);
        Assert.Equal(("NumPad1", "NumPad3", "NumPad2", "NumPad8"), (settings.HotkeyProfile.SplitKey, settings.HotkeyProfile.ResetKey, settings.HotkeyProfile.SkipKey, settings.HotkeyProfile.UndoKey));
        Assert.Equal("Ctrl+D1", settings.HotkeyProfile.PauseKey);
        Assert.Equal("Ctrl+Alt+Shift+F5", settings.HotkeyProfile.ToggleGlobalHotkeys);
        Assert.True(settings.HotkeyProfile.GlobalHotkeysEnabled);
        Assert.Equal(0.5f, settings.HotkeyProfile.HotkeyDelay);
        Assert.False(settings.WarnOnReset);
        Assert.Equal(("Best Segments", true, 60, 16835), (settings.LastComparison, settings.SimpleSumOfBest, settings.RefreshRate, settings.ServerPort));
        Assert.Equal(ServerStartupType.TCP, settings.ServerStartup);
        Assert.Equal("C:\\Splits\\sms.lss", settings.RecentSplits[0].Path);
        Assert.Equal(("GameTime", "Pad"), (settings.RecentSplits[0].LastTimingMethod, settings.RecentSplits[0].LastHotkeyProfile));
        Assert.Equal("", settings.LastSplitsPath);
        Assert.Equal("C:\\Layouts\\main.lsl", settings.LastLayoutPath);
        Assert.True(settings.ComparisonGeneratorStates["Latest Run"]);
        Assert.Equal((30, 5), (settings.HcpHistorySize, settings.HcpNBestRuns));
        Assert.False(settings.Racetime.LoadChatHistory);
        Assert.Equal(1.000025, settings.TimerDrift);
    }

    [Fact]
    public void A_Drift_Too_Far_From_1_Is_Reset_As_LoadDrift_Does()
    {
        string tooFar = Windows.Replace(
            "<TimerDrift><![CDATA[HOviNhoA8D8=]]></TimerDrift>",
            "<TimerDrift><![CDATA[mpmZmZmZ8T8=]]></TimerDrift>",
            StringComparison.Ordinal);
        Assert.Equal(1.0, Load(tooFar).TimerDrift);
        Assert.Equal(1.0, Load(Windows.Replace("<TimerDrift><![CDATA[HOviNhoA8D8=]]></TimerDrift>", "<TimerDrift>junk</TimerDrift>", StringComparison.Ordinal)).TimerDrift);
    }

    [Fact]
    public void Saving_Gives_Windows_Back_Everything_It_Had()
    {
        var original = new XmlDocument();
        original.LoadXml(Windows);
        XmlDocument saved = Save(Load(Windows));

        Dictionary<string, string> before = Values(original);
        Dictionary<string, string> after = Values(saved);
        List<string> differences = before
            .Where(p => !after.TryGetValue(p.Key, out string? now) || now != p.Value)
            .Select(p => $"  {p.Key}: \"{p.Value}\" became {(after.TryGetValue(p.Key, out string? now) ? $"\"{now}\"" : "nothing")}")
            .ToList();
        Assert.True(differences.Count == 0, string.Join("\n", differences));
        // XMLSettingsSaver's order.
        Assert.Equal(
            [
                "HotkeyProfiles", "WarnOnReset", "RaceViewer", "AgreedToSRLRules", "EnableDPIAwareness", "UILanguage",
                "RecentSplits", "RecentLayouts", "LastComparison", "SimpleSumOfBest", "RefreshRate", "ServerPort",
                "ServerStartup", "ServerState", "ComparisonGeneratorStates", "RaceProviderPlugins",
                "ActiveAutoSplitters", "TimerDrift",
            ],
            saved.DocumentElement!.ChildNodes.OfType<XmlElement>().Select(e => e.Name));
    }

    [Fact]
    public void A_New_Settings_Cfg_Has_What_Windows_Needs_To_Read_It()
    {
        // XMLSettingsFactory reads these without checking that they are there.
        XmlElement root = Save(new AppSettings()).DocumentElement!;
        foreach (string name in new[] { "HotkeyProfiles", "RecentSplits", "RecentLayouts", "RaceViewer", "ActiveAutoSplitters", "ComparisonGeneratorStates", "RaceProviderPlugins" })
        {
            Assert.NotNull(root[name]);
        }

        Assert.Equal("SpeedRunsLive", root["RaceViewer"]!.InnerText);
        Assert.Equal(1.0, BitConverter.ToDouble(Convert.FromBase64String(root["TimerDrift"]!.InnerText)));
        Assert.Null(root["Appearance"]);
        Assert.Null(root["LiveSplitNativeHotkeys"]);
    }

    [Fact]
    public void Hotkeys_Are_Written_In_Windows_Key_Names_And_Command_Ones_Kept_For_This_App()
    {
        var settings = new AppSettings();
        settings.HotkeyProfile.SplitKey = "Ctrl+Shift+Add";
        settings.HotkeyProfile.ResetKey = "Meta+R";
        XmlElement root = Save(settings).DocumentElement!;
        XmlElement profile = root["HotkeyProfiles"]!["HotkeyProfile"]!;
        Assert.Equal("Add, Shift, Control", profile["SplitKey"]!.InnerText);
        Assert.Equal("", profile["ResetKey"]!.InnerText);
        Assert.Equal("Meta+R", root["LiveSplitNativeHotkeys"]!["ResetKey"]!.InnerText);

        AppSettings again = Load(root.OwnerDocument.OuterXml);
        Assert.Equal(("Ctrl+Shift+Add", "Meta+R"), (again.HotkeyProfile.SplitKey, again.HotkeyProfile.ResetKey));
    }

    [Fact]
    public void Older_Windows_Settings_Are_Read_By_Their_Version()
    {
        // 1.7: one profile at the root, splits without hotkey profile; before 1.8.17 Allow
        // Gamepads is dropped when no hotkey is a button.
        AppSettings v17 = Load("""
            <Settings version="1.7">
              <SplitKey>Space</SplitKey><ResetKey>R</ResetKey><SkipKey /><UndoKey /><PauseKey>P</PauseKey>
              <ToggleGlobalHotkeys /><SwitchComparisonPrevious>Left</SwitchComparisonPrevious><SwitchComparisonNext />
              <GlobalHotkeysEnabled>True</GlobalHotkeysEnabled><AllowGamepadsAsHotkeys>True</AllowGamepadsAsHotkeys>
              <RecentSplits><SplitsFile gameName="G" categoryName="C" lastTimingMethod="GameTime" lastHotkeyProfile="X">/s.lss</SplitsFile></RecentSplits>
              <RecentLayouts />
            </Settings>
            """);
        Assert.Equal(("Space", "R", "P", "Left"), (v17.HotkeyProfile.SplitKey, v17.HotkeyProfile.ResetKey, v17.HotkeyProfile.PauseKey, v17.HotkeyProfile.SwitchComparisonPrevious));
        Assert.True(v17.HotkeyProfile.GlobalHotkeysEnabled);
        Assert.False(v17.HotkeyProfile.AllowGamepadsAsHotkeys);
        Assert.Equal(("GameTime", "Default"), (v17.RecentSplits[0].LastTimingMethod, v17.RecentSplits[0].LastHotkeyProfile));

        // 1.2: no comparison keys yet. 1.6: no timing method in the recent splits.
        AppSettings v12 = Load("""<Settings version="1.2"><SplitKey>A</SplitKey><SwitchComparisonNext>B</SwitchComparisonNext><RecentSplits /><RecentLayouts /></Settings>""");
        Assert.Equal(("A", ""), (v12.HotkeyProfile.SplitKey, v12.HotkeyProfile.SwitchComparisonNext));
        AppSettings v16 = Load("""<Settings version="1.6"><RecentSplits><SplitsFile gameName="G" categoryName="C" lastTimingMethod="GameTime">/s.lss</SplitsFile></RecentSplits><RecentLayouts /></Settings>""");
        Assert.Equal("RealTime", v16.RecentSplits[0].LastTimingMethod);
    }

    [Fact]
    public void Before_1_6_Recent_Splits_Are_Named_From_The_Files_And_Missing_Ones_Left_Out()
    {
        string splits = Path.Combine(AppContext.BaseDirectory, "fixtures", "small-representative.lss");
        AppSettings settings = Load($"""
            <Settings version="1.5">
              <RecentSplits><SplitsPath>{splits}</SplitsPath><SplitsPath>/nowhere/gone.lss</SplitsPath></RecentSplits>
              <RecentLayouts />
            </Settings>
            """);
        RecentSplitsFile only = Assert.Single(settings.RecentSplits);
        Assert.Equal((splits, "Test Game", "Any%"), (only.Path, only.GameName, only.CategoryName));
    }

    [Fact]
    public void Unknown_Elements_Are_Kept()
    {
        XmlElement root = Save(Load("""<Settings version="1.8.18"><FutureOption mode="x">1</FutureOption><RecentSplits /><RecentLayouts /></Settings>""")).DocumentElement!;
        Assert.Equal("x", root["FutureOption"]!.GetAttribute("mode"));
    }

    private static Dictionary<string, string> Values(XmlDocument document)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        Walk(document.DocumentElement!, "Settings", values);
        return values;
    }

    private static void Walk(XmlElement element, string path, Dictionary<string, string> values)
    {
        foreach (XmlAttribute attribute in element.Attributes)
        {
            values[$"{path}/@{attribute.Name}"] = attribute.Value;
        }

        List<XmlElement> children = element.ChildNodes.OfType<XmlElement>().ToList();
        if (children.Count == 0)
        {
            values[path] = element.InnerText;
            return;
        }

        foreach (IGrouping<string, XmlElement> group in children.GroupBy(c => c.Name))
        {
            int index = 0;
            foreach (XmlElement child in group)
            {
                Walk(child, $"{path}/{child.Name}[{index++}]", values);
            }
        }
    }
}
