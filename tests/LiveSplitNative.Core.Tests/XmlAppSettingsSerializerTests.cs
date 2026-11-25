using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Serialization;
using AppAppearanceMode = LiveSplitNative.Core.Options.AppAppearanceMode;

namespace LiveSplitNative.Core.Tests;

public class XmlAppSettingsSerializerTests
{
    [Fact]
    public void RoundTrip_Recent_Paths_And_Hotkey_Stubs()
    {
        var settings = new AppSettings
        {
            LastComparison = "Rival",
            WarnOnReset = false,
            RefreshRate = 60,
            ServerPort = 16834,
            HotkeyProfile = new HotkeyProfileStub
            {
                SplitKey = "",
                ResetKey = "",
                GlobalHotkeysEnabled = false,
                DoubleTapPrevention = true,
                HotkeyDelay = 0,
            },
        };
        settings.AddToRecentSplits(new RecentSplitsFile("/tmp/demo.lss", "Demo Game", "Any%", "RealTime"));
        settings.AddToRecentLayouts("/tmp/demo.lsl");

        using var stream = new MemoryStream();
        XmlAppSettingsSerializer.Save(settings, stream);
        stream.Position = 0;
        AppSettings again = XmlAppSettingsSerializer.Load(stream);

        Assert.Equal("Rival", again.LastComparison);
        Assert.False(again.WarnOnReset);
        Assert.Equal(60, again.RefreshRate);
        Assert.Equal("/tmp/demo.lss", again.LastSplitsPath);
        Assert.Equal("/tmp/demo.lsl", again.LastLayoutPath);
        Assert.Single(again.RecentSplits);
        Assert.Equal("Demo Game", again.RecentSplits[0].GameName);
        Assert.Equal("Any%", again.RecentSplits[0].CategoryName);
        Assert.Equal("/tmp/demo.lsl", Assert.Single(again.RecentLayouts));
        Assert.False(again.HotkeyProfile.GlobalHotkeysEnabled);
        Assert.True(again.HotkeyProfile.DoubleTapPrevention);
        Assert.Equal("", again.HotkeyProfile.SplitKey);
        Assert.Equal(AppAppearanceMode.System, again.Appearance);
    }

    [Fact]
    public void RoundTrip_Appearance_And_Hotkey_Bindings()
    {
        var settings = new AppSettings
        {
            Appearance = AppAppearanceMode.Dark,
            HotkeyProfile = new HotkeyProfileStub
            {
                SplitKey = "Space",
                ResetKey = "R",
                UndoKey = "U",
                SwitchComparisonNext = "Meta+Right",
                SwitchComparisonPrevious = "Meta+Left",
            },
        };

        using var stream = new MemoryStream();
        XmlAppSettingsSerializer.Save(settings, stream);
        stream.Position = 0;
        AppSettings again = XmlAppSettingsSerializer.Load(stream);

        Assert.Equal(AppAppearanceMode.Dark, again.Appearance);
        Assert.Equal("Space", again.HotkeyProfile.SplitKey);
        Assert.Equal("Meta+Right", again.HotkeyProfile.SwitchComparisonNext);
        Assert.Equal("Meta+Left", again.HotkeyProfile.SwitchComparisonPrevious);
    }

    /// <summary>Don't ask again for the hotkey permission is kept; asking (the default) writes nothing.</summary>
    [Fact]
    public void RoundTrip_Ask_For_Hotkey_Permission()
    {
        using var asking = new MemoryStream();
        XmlAppSettingsSerializer.Save(new AppSettings(), asking);
        Assert.DoesNotContain("AskForHotkeyPermission", System.Text.Encoding.UTF8.GetString(asking.ToArray()));
        asking.Position = 0;
        Assert.True(XmlAppSettingsSerializer.Load(asking).AskForHotkeyPermission);

        using var notAsking = new MemoryStream();
        XmlAppSettingsSerializer.Save(new AppSettings { AskForHotkeyPermission = false }, notAsking);
        notAsking.Position = 0;
        AppSettings again = XmlAppSettingsSerializer.Load(notAsking);
        Assert.False(again.AskForHotkeyPermission);
        Assert.DoesNotContain(again.UnknownXml, x => x.Contains("AskForHotkeyPermission", StringComparison.Ordinal));
    }

    /// <summary>Every hotkey profile is kept, in order, with its name; the first is in use after loading.</summary>
    [Fact]
    public void RoundTrip_Several_Hotkey_Profiles()
    {
        var settings = new AppSettings();
        settings.HotkeyProfile.SplitKey = "Space";
        settings.HotkeyProfiles["Race"] = new HotkeyProfileStub { SplitKey = "NumPad1", PauseKey = "Meta+P", GlobalHotkeysEnabled = true };
        settings.CurrentHotkeyProfile = "Race";
        settings.AddToRecentSplits(new RecentSplitsFile("/tmp/race.lss", "Game", "Any%", "RealTime") { LastHotkeyProfile = "Race" });

        using var stream = new MemoryStream();
        XmlAppSettingsSerializer.Save(settings, stream);
        string xml = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("<HotkeyProfile name=\"Race\">", xml);
        stream.Position = 0;
        AppSettings again = XmlAppSettingsSerializer.Load(stream);

        Assert.Equal(["Default", "Race"], again.HotkeyProfiles.Keys);
        Assert.Equal("Default", again.CurrentHotkeyProfile);
        Assert.Equal("Space", again.HotkeyProfile.SplitKey);
        Assert.Equal("NumPad1", again.HotkeyProfiles["Race"].SplitKey);
        // A Command chord (no Windows key) comes back in its own profile.
        Assert.Equal("Meta+P", again.HotkeyProfiles["Race"].PauseKey);
        Assert.True(again.HotkeyProfiles["Race"].GlobalHotkeysEnabled);
        Assert.Equal("Race", again.RecentSplits[0].LastHotkeyProfile);
    }

    [Fact]
    public void DefaultPath_Uses_LiveSplit_Native_Directory()
    {
        string path = XmlAppSettingsSerializer.DefaultPath;
        Assert.Contains("LiveSplit Native", path);
        Assert.EndsWith("settings.cfg", path);
    }

    [Fact]
    public void Missing_File_Returns_Defaults()
    {
        string path = Path.Combine(Path.GetTempPath(), "lsn-missing-" + Guid.NewGuid() + ".cfg");
        AppSettings settings = XmlAppSettingsSerializer.LoadFile(path);
        Assert.Equal("Personal Best", settings.LastComparison);
        Assert.True(settings.WarnOnReset);
        Assert.Empty(settings.RecentSplits);
    }

    [Fact]
    public void Recent_Files_Are_Kept_Like_Windows_Newest_Last()
    {
        var settings = new AppSettings();
        settings.AddToRecentSplits(new RecentSplitsFile("/tmp/corona.lss", "SMS", "120 Shines", "RealTime"));
        settings.AddToRecentLayouts("/tmp/default.lsl");
        Assert.Equal("/tmp/corona.lss", settings.LastSplitsPath);
        Assert.Equal("/tmp/default.lsl", settings.LastLayoutPath);

        settings.AddToRecentSplits(new RecentSplitsFile("/tmp/other.lss", "WW", "Any%", "GameTime"));
        Assert.Equal("/tmp/other.lss", settings.LastSplitsPath);
        Assert.Equal(new[] { "/tmp/corona.lss", "/tmp/other.lss" }, settings.RecentSplits.Select(x => x.Path));

        // Opening one again moves it to the end; Close Splits / Default add an empty entry.
        settings.AddToRecentSplits(new RecentSplitsFile("/tmp/corona.lss", "SMS", "120 Shines", "GameTime"));
        Assert.Equal(new[] { "/tmp/other.lss", "/tmp/corona.lss" }, settings.RecentSplits.Select(x => x.Path));
        Assert.Equal("GameTime", settings.RecentSplits[^1].LastTimingMethod);
        settings.AddToRecentSplits("", null, LiveSplitNative.Core.Model.TimingMethod.RealTime, "Default");
        settings.AddToRecentLayouts("");
        Assert.Equal("", settings.LastSplitsPath);
        Assert.Equal("", settings.LastLayoutPath);
        Assert.Equal("", settings.RecentSplits[^1].GameName);
    }

    [Fact]
    public void Recent_Lists_Keep_50_Splits_And_10_Layouts_Dropping_The_Oldest()
    {
        var settings = new AppSettings();
        for (int i = 0; i < 60; i++)
        {
            settings.AddToRecentSplits(new RecentSplitsFile($"/tmp/{i}.lss", "G", "C", "RealTime"));
            settings.AddToRecentLayouts($"/tmp/{i}.lsl");
        }

        Assert.Equal(50, settings.RecentSplits.Count);
        Assert.Equal("/tmp/10.lss", settings.RecentSplits[0].Path);
        Assert.Equal(10, settings.RecentLayouts.Count);
        Assert.Equal("/tmp/50.lsl", settings.RecentLayouts[0]);
        Assert.Equal("/tmp/59.lsl", settings.LastLayoutPath);
    }

    [Fact]
    public void A_Windows_Settings_Cfg_Is_Read_In_Its_Own_Order_Empty_Entries_Included()
    {
        const string windows = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Settings version="1.8.29">
              <RecentSplits>
                <SplitsFile gameName="Super Mario Sunshine" categoryName="Any% (No ACE)" lastTimingMethod="RealTime" lastHotkeyProfile="Default">C:\Runs\old.lss</SplitsFile>
                <SplitsFile gameName="Super Mario Sunshine" categoryName="Any% (No ACE)" lastTimingMethod="GameTime" lastHotkeyProfile="Default">C:\Runs\new.lss</SplitsFile>
              </RecentSplits>
              <RecentLayouts>
                <LayoutPath>C:\Layouts\a.lsl</LayoutPath>
                <LayoutPath></LayoutPath>
              </RecentLayouts>
            </Settings>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(windows));
        AppSettings settings = XmlAppSettingsSerializer.Load(stream);

        Assert.Equal(new[] { @"C:\Runs\old.lss", @"C:\Runs\new.lss" }, settings.RecentSplits.Select(x => x.Path));
        Assert.Equal(@"C:\Runs\new.lss", settings.LastSplitsPath);
        Assert.Equal("GameTime", settings.RecentSplits[^1].LastTimingMethod);
        Assert.Equal(new[] { @"C:\Layouts\a.lsl", "" }, settings.RecentLayouts);
        Assert.Equal("", settings.LastLayoutPath);

        using var saved = new MemoryStream();
        XmlAppSettingsSerializer.Save(settings, saved);
        string xml = System.Text.Encoding.UTF8.GetString(saved.ToArray());
        Assert.DoesNotContain("LastSplitsPath", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("LastLayoutPath", xml, StringComparison.Ordinal);
        Assert.True(xml.IndexOf("old.lss", StringComparison.Ordinal) < xml.IndexOf("new.lss", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/r/b.lss", "/r/c.lsl", new[] { "/r/a.lss", "/r/b.lss" }, new[] { "/r/d.lsl", "/r/c.lsl" })]
    [InlineData("", "", new[] { "/r/a.lss", "/r/b.lss", "" }, new[] { "/r/d.lsl", "/r/c.lsl", "" })]
    public void Settings_From_Older_Builds_Are_Turned_Newest_Last(
        string lastSplits, string lastLayout, string[] splitsAfter, string[] layoutsAfter)
    {
        // Builds before 11 Oct 2026: lists newest first, plus LastSplitsPath / LastLayoutPath.
        string old = $"""
            <Settings version="1.8.18">
              <LastSplitsPath>{lastSplits}</LastSplitsPath>
              <LastLayoutPath>{lastLayout}</LastLayoutPath>
              <RecentSplits>
                <SplitsFile gameName="G" categoryName="C" lastTimingMethod="RealTime" lastHotkeyProfile="Default">/r/b.lss</SplitsFile>
                <SplitsFile gameName="G" categoryName="C" lastTimingMethod="RealTime" lastHotkeyProfile="Default">/r/a.lss</SplitsFile>
              </RecentSplits>
              <RecentLayouts>
                <LayoutPath>/r/c.lsl</LayoutPath>
                <LayoutPath>/r/d.lsl</LayoutPath>
              </RecentLayouts>
            </Settings>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(old));
        AppSettings settings = XmlAppSettingsSerializer.Load(stream);

        Assert.Equal(splitsAfter, settings.RecentSplits.Select(x => x.Path));
        Assert.Equal(layoutsAfter, settings.RecentLayouts);
        Assert.Equal(lastSplits, settings.LastSplitsPath);
        Assert.Equal(lastLayout, settings.LastLayoutPath);
    }

    [Fact]
    public void File_RoundTrip_Persists_Last_Session_Paths()
    {
        string path = Path.Combine(Path.GetTempPath(), "lsn-session-" + Guid.NewGuid() + ".cfg");
        try
        {
            var settings = new AppSettings();
            settings.AddToRecentSplits(new RecentSplitsFile("/Users/me/Runs/demo.lss", "Demo", "Any%", "RealTime"));
            settings.AddToRecentLayouts("/Users/me/Layouts/timer.lsl");
            XmlAppSettingsSerializer.SaveFile(settings, path);

            AppSettings again = XmlAppSettingsSerializer.LoadFile(path);
            Assert.Equal("/Users/me/Runs/demo.lss", again.LastSplitsPath);
            Assert.Equal("/Users/me/Layouts/timer.lsl", again.LastLayoutPath);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Window_Geometry_Is_Not_Kept_In_Settings_Cfg()
    {
        // Basten, 11 Oct 2026: exactly like Windows, position and size live only in the layout.
        const string old = """
            <Settings version="1.8.18">
              <WindowX>120</WindowX><WindowY>80</WindowY>
              <WindowWidth>300</WindowWidth><WindowHeight>240</WindowHeight>
              <WindowScreen>0,0,1440x900</WindowScreen>
              <WindowGeometryMigrationVersion>1</WindowGeometryMigrationVersion>
              <LastComparison>Rival</LastComparison>
            </Settings>
            """;
        AppSettings loaded = XmlAppSettingsSerializer.Load(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(old)));

        using var stream = new MemoryStream();
        XmlAppSettingsSerializer.Save(loaded, stream);
        string saved = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.Equal("Rival", loaded.LastComparison);
        Assert.DoesNotContain("<Window", saved, StringComparison.Ordinal);
    }
}
