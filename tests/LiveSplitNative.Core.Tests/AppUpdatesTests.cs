using LiveSplitNative.Core.Web;

namespace LiveSplitNative.Core.Tests;

/// <summary>The update check: release tags as versions, GitHub's release answer, which is newer.</summary>
public class AppUpdatesTests
{
    private const string Release = """
        {
          "tag_name": "v2026.11.02.0915",
          "name": "LiveSplit Native 2026.11.02",
          "draft": false,
          "prerelease": false,
          "html_url": "https://github.com/GewoonBasten/LiveSplit-Native/releases/tag/v2026.11.02.0915",
          "body": "- Fixed the timer\r\n- New icon",
          "assets": [
            { "name": "LiveSplit-Native-x86_64.zip", "browser_download_url": "https://example.com/x64.zip" },
            { "name": "LiveSplit-Native-arm64.zip", "browser_download_url": "https://example.com/arm64.zip" }
          ]
        }
        """;

    [Theory]
    [InlineData("v2026.11.02.0915", "2026.11.2.915")]
    [InlineData("2026.11.02", "2026.11.2")]
    [InlineData("V1.2", "1.2")]
    [InlineData("latest", null)]
    [InlineData("", null)]
    public void Tags_Are_Versions(string tag, string? version) =>
        Assert.Equal(version is null ? null : Version.Parse(version), AppUpdates.ParseVersion(tag));

    [Fact]
    public void A_Release_Gives_Its_Version_Notes_And_The_Zip_For_This_Chip()
    {
        AppRelease release = Assert.IsType<AppRelease>(AppUpdates.FromGitHubJson(Release, AppUpdates.AssetName("arm64")));
        Assert.Equal(new Version(2026, 11, 2, 915), release.Version);
        Assert.Equal("LiveSplit Native 2026.11.02", release.Name);
        Assert.Equal("- Fixed the timer\n- New icon", release.Notes);
        Assert.Equal("https://example.com/arm64.zip", release.DownloadUrl);

        // No zip for that chip: the release is still found (the page can be opened instead).
        Assert.Null(AppUpdates.FromGitHubJson(Release, AppUpdates.AssetName("ppc"))!.DownloadUrl);

        // Pre-releases and tags that are not versions are not updates.
        Assert.Null(AppUpdates.FromGitHubJson(Release.Replace("\"prerelease\": false", "\"prerelease\": true"), "x"));
        Assert.Null(AppUpdates.FromGitHubJson(Release.Replace("v2026.11.02.0915\",", "nightly\","), "x"));
    }

    [Fact]
    public void Only_A_Newer_Release_Is_An_Update_And_Developer_Builds_Never_Update()
    {
        AppRelease release = AppUpdates.FromGitHubJson(Release, "x")!;
        Assert.True(AppUpdates.IsNewer(release, new Version(2026, 11, 1, 1200)));
        Assert.False(AppUpdates.IsNewer(release, new Version(2026, 11, 2, 915)));
        Assert.False(AppUpdates.IsNewer(release, new Version(2026, 12, 1)));
        Assert.False(AppUpdates.IsNewer(release, null));
    }
}
