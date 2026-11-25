using System.Text;
using System.Xml;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.Core.Tests;

/// <summary>settings.cfg RaceProviderPlugins as Windows' XMLSettingsFactory and XMLSettingsSaver read and write it.</summary>
public class RaceProviderSettingsTests
{
    private static AppSettings Load(string xml) => XmlAppSettingsSerializer.Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));

    private static string SavedPlugins(AppSettings settings)
    {
        using var stream = new MemoryStream();
        XmlAppSettingsSerializer.Save(settings, stream);
        stream.Position = 0;
        var document = new XmlDocument();
        document.Load(stream);
        return document.DocumentElement!["RaceProviderPlugins"]!.InnerXml;
    }

    [Fact]
    public void A_New_Settings_Cfg_Has_Racetime_Then_Srl()
    {
        // StandardSettingsFactory: LiveSplit.Racetime.dll's factory, then the SRL one TimerForm adds.
        Assert.Equal(["racetime.gg", "SRL"], new AppSettings().RaceProviders.Select(p => p.DisplayName));
        Assert.Equal(
            "<Plugin name=\"LiveSplit.Racetime.dll\" enabled=\"True\"><LoadChatHistory>True</LoadChatHistory><HideResults>False</HideResults></Plugin>"
            + "<Plugin name=\"SRL\" enabled=\"True\" />",
            SavedPlugins(new AppSettings()));
    }

    [Fact]
    public void The_Files_Order_Is_Kept_And_Plugins_This_App_Does_Not_Have_Are_Kept_Unloaded()
    {
        AppSettings settings = Load("""
            <Settings version="1.8.18"><RaceProviderPlugins>
              <Plugin name="SRL" enabled="False" />
              <Plugin name="LiveSplit.Other.dll" enabled="True"><Server a="1">x</Server></Plugin>
              <Plugin name="LiveSplit.Racetime.dll" enabled="False"><LoadChatHistory>False</LoadChatHistory><HideResults>True</HideResults></Plugin>
            </RaceProviderPlugins></Settings>
            """);
        Assert.Equal(["SRL", "LiveSplit.Other.dll", "racetime.gg"], settings.RaceProviders.Select(p => p.DisplayName));
        Assert.IsType<UnloadedRaceProviderSettings>(settings.RaceProviders[1]);
        Assert.Equal("", settings.RaceProviders[1].WebsiteLink);
        Assert.False(settings.Racetime.Enabled);

        // Manage Racing Services changes Enabled; the rest is written as it was.
        settings.RaceProviders[1].Enabled = false;
        settings.RaceProviders[0].Enabled = true;
        Assert.Equal(
            "<Plugin name=\"SRL\" enabled=\"True\" />"
            + "<Plugin name=\"LiveSplit.Other.dll\" enabled=\"False\"><Server a=\"1\">x</Server></Plugin>"
            + "<Plugin name=\"LiveSplit.Racetime.dll\" enabled=\"False\"><LoadChatHistory>False</LoadChatHistory><HideResults>True</HideResults></Plugin>",
            SavedPlugins(settings));
    }

    [Fact]
    public void Before_1_8_8_The_Plugins_Are_Not_Read()
    {
        AppSettings settings = Load("""
            <Settings version="1.8.7"><RaceProviderPlugins>
              <Plugin name="LiveSplit.Racetime.dll" enabled="False" />
            </RaceProviderPlugins></Settings>
            """);
        Assert.True(settings.Racetime.Enabled);
        Assert.Equal(["racetime.gg", "SRL"], settings.RaceProviders.Select(p => p.DisplayName));
    }

    [Fact]
    public void Clones_Are_Separate()
    {
        var settings = new AppSettings();
        List<RaceProviderSettings> copies = [.. settings.RaceProviders.Select(p => p.Clone())];
        copies.ForEach(c => c.Enabled = false);
        Assert.All(settings.RaceProviders, p => Assert.True(p.Enabled));
        Assert.IsType<RacetimeSettings>(copies[0]);
        Assert.IsType<SrlSettings>(copies[1]);
    }
}
