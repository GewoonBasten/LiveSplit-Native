using System.Xml;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>AutoSplit Integration (Basten's Pilgrim build of LiveSplit.AutoSplitIntegration).</summary>
public sealed class AutoSplitIntegrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "autosplit-tests-" + Guid.NewGuid().ToString("N"));

    public AutoSplitIntegrationTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Settings_Are_Version_1_8()
    {
        var settings = new AutoSplitIntegrationSettings { AutoSplitPath = "/Applications/Pilgrim Autosplitter.app", SettingsPath = "/p.json", GameTimePausing = true };
        XmlElement root = ComponentSettingsXml.LoadRoot(settings.ToInnerXml());
        Assert.Equal(["Version", "AutoSplitPath", "SettingsPath", "GameTimePausing"], root.ChildNodes.OfType<XmlElement>().Select(e => e.Name));
        Assert.Equal("1.8", root["Version"]!.InnerText);
        AutoSplitIntegrationSettings back = AutoSplitIntegrationSettings.FromInnerXml(settings.ToInnerXml());
        Assert.Equal(("/Applications/Pilgrim Autosplitter.app", "/p.json", true), (back.AutoSplitPath, back.SettingsPath, back.GameTimePausing));
        Assert.False(AutoSplitIntegrationSettings.FromInnerXml(null).GameTimePausing);
    }

    [Fact]
    public void A_Mac_App_Runs_The_Program_It_Bundles()
    {
        string app = Path.Combine(_dir, "Pilgrim Autosplitter.app");
        Directory.CreateDirectory(Path.Combine(app, "Contents", "MacOS"));
        Directory.CreateDirectory(Path.Combine(app, "Contents", "Resources"));
        File.WriteAllText(Path.Combine(app, "Contents", "MacOS", "Pilgrim Autosplitter"), "launcher");
        Assert.Equal(Path.Combine(app, "Contents", "MacOS", "Pilgrim Autosplitter"), AutoSplitIntegrationRuntime.Program(app));

        // Pilgrim's Platypus app only re-opens its bundled program: that program is run instead.
        File.WriteAllText(Path.Combine(app, "Contents", "Resources", "Pilgrim Autosplitter"), "program");
        Assert.Equal(Path.Combine(app, "Contents", "Resources", "Pilgrim Autosplitter"), AutoSplitIntegrationRuntime.Program(app));
        Assert.Null(AutoSplitIntegrationRuntime.Program(Path.Combine(_dir, "missing")));
        Assert.Null(AutoSplitIntegrationRuntime.Program(""));
    }

    [Fact]
    public async Task Talks_With_The_Autosplitter_Over_Its_Input_And_Output()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string script = Path.Combine(_dir, "autosplitter.sh");
        File.WriteAllText(script, """
            #!/bin/sh
            echo "v1.1.3"
            echo $$
            echo split
            echo killme
            while read line; do
              echo "$line" >> received.txt
              [ "$line" = kill ] && exit 0
            done
            """.Replace("\r", ""));
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        string profile = Path.Combine(_dir, "profile.json");
        File.WriteAllText(profile, "{}");

        var commands = new List<string>();
        var runtime = new AutoSplitIntegrationRuntime(action =>
        {
            lock (commands)
            {
                action();
            }
        });
        runtime.CommandReceived = commands.Add;
        runtime.Start(new AutoSplitIntegrationSettings { AutoSplitPath = script, SettingsPath = profile });
        Assert.True(runtime.HasStarted);

        string received = Path.Combine(_dir, "received.txt");
        for (int i = 0; i < 200 && (runtime.IsRunning || !File.Exists(received)); i++)
        {
            await Task.Delay(25);
        }

        Assert.False(runtime.IsRunning);
        Assert.Equal(["split"], commands);
        Assert.Equal(["settings|" + profile, "kill"], File.ReadAllLines(received));
        Assert.Equal(AutoSplitIntegrationRuntime.NotAvailable, runtime.Version);
    }

    [Fact]
    public void Ignored_Commands_Are_Used_Once()
    {
        var runtime = new AutoSplitIntegrationRuntime(action => action());
        runtime.IgnoreNext(AutoSplitCommands.Start);
        Assert.True(runtime.ConsumeIgnore(AutoSplitCommands.Start));
        Assert.False(runtime.ConsumeIgnore(AutoSplitCommands.Start));
    }
}
