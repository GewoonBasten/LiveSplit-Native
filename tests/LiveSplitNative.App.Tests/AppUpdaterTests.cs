using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Web;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Updating the app: the question, and the script that puts the new app in place.</summary>
public class AppUpdaterTests
{
    private static AppRelease Release(string notes = "- Faster\n- Fixed") =>
        new(new Version(2026, 11, 2, 915), "v2026.11.02.0915", "LiveSplit 2026.11.02", notes, "https://example.com/a.zip", "https://example.com/page");

    [AvaloniaFact]
    public void The_Question_Names_Both_Versions_And_The_Notes()
    {
        (Window dialog, Button notNow, Button update) = MainWindow.UpdateQuestion(Release(), new Version(2026, 11, 1, 1200));
        string text = dialog.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).First(t => t?.Contains("available", StringComparison.Ordinal) == true)!;
        Assert.StartsWith("LiveSplit 2026.11.02.0915 is available. You have 2026.11.01.1200.\n\n- Faster\n- Fixed\n\n", text, StringComparison.Ordinal);
        Assert.Equal("Not Now", notNow.Content);
        Assert.Equal("Update", update.Content);
        Assert.True(update.IsDefault);
    }

    [Fact]
    public void The_Script_Waits_Swaps_Keeps_The_Old_App_On_Failure_And_Opens_The_New_One()
    {
        string script = AppUpdater.InstallScript(4242, "/Applications/LiveSplit Native.app", "/tmp/u/app/LiveSplit Native.app", reopen: true);
        Assert.Contains("while kill -0 4242", script, StringComparison.Ordinal);
        Assert.Contains("old='/Applications/LiveSplit Native.app'", script, StringComparison.Ordinal);
        Assert.Contains("mv \"$old.previous\" \"$old\"", script, StringComparison.Ordinal);
        Assert.Contains("open \"$old\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("open \"$old\"", AppUpdater.InstallScript(1, "/a.app", "/b.app", reopen: false), StringComparison.Ordinal);

        // A quote in a path stays one argument.
        Assert.Contains("old='/x/it'\\''s.app'", AppUpdater.InstallScript(1, "/x/it's.app", "/b.app", reopen: false), StringComparison.Ordinal);
    }

    /// <summary>Linux: the folders are swapped, the program made runnable and started again.</summary>
    [Fact]
    public void The_Linux_Script_Swaps_The_Folders_And_Starts_The_App()
    {
        string script = AppUpdater.InstallScript(7, "/home/me/linux-x64", "/tmp/u/app/linux-x64", reopen: true, linux: true);
        Assert.Contains("old='/home/me/linux-x64'", script, StringComparison.Ordinal);
        Assert.Contains("mv \"$new\" \"$old\"", script, StringComparison.Ordinal);
        Assert.Contains("chmod +x \"$old/LiveSplitNative.App\"", script, StringComparison.Ordinal);
        Assert.Contains("setsid ./LiveSplitNative.App", script, StringComparison.Ordinal);
        Assert.DoesNotContain("xattr", script, StringComparison.Ordinal);
        Assert.DoesNotContain("open \"$old\"", script, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Developer_Build_Is_Not_An_App_Bundle_And_Has_No_Version()
    {
        Assert.Null(AppUpdater.BundlePath);
        Assert.Null(AppUpdater.CurrentVersion);
    }

    [Theory]
    [InlineData(2026, 11, 2, 915, "2026.11.02.0915")]
    [InlineData(2026, 1, 5, -1, "2026.01.05")]
    [InlineData(1, 2, 3, -1, "1.2.3")]
    public void Versions_Read_As_Their_Tags(int major, int minor, int build, int revision, string text) =>
        Assert.Equal(text, MainWindow.VersionText(revision < 0 ? new Version(major, minor, build) : new Version(major, minor, build, revision)));
}
