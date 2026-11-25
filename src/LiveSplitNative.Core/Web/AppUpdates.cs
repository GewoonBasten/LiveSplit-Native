using System.Text.Json;

namespace LiveSplitNative.Core.Web;

/// <summary>A release of LiveSplit Native on GitHub: its version, notes and the app's zip for this Mac.</summary>
public sealed record AppRelease(Version Version, string Tag, string Name, string Notes, string? DownloadUrl, string PageUrl);

/// <summary>
/// The update check (Windows LiveSplit's UpdateHelper looks for new versions of itself at start):
/// the newest release of LiveSplit Native's GitHub repository, whose tag is its version
/// ("v2026.11.01.1613", what tools/mac/make-app.command prints) and whose asset
/// "LiveSplit-Native-&lt;arch&gt;.zip" is the app for this Mac's chip.
/// </summary>
public static class AppUpdates
{
    /// <summary>Where releases are published (owner/repository).</summary>
    public const string Repository = "GewoonBasten/LiveSplit-Native";

    /// <summary>The release asset for a chip ("arm64" or "x86_64", as uname -m and make-app name it).</summary>
    public static string AssetName(string arch) => $"LiveSplit-Native-{arch}.zip";

    /// <summary>A tag as a version: "v2026.11.01.1613" is 2026.11.1.1613; null when it is not one.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        string text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        return Version.TryParse(text, out Version? version) ? version : null;
    }

    /// <summary>GitHub's answer for a release (releases/latest): null when its tag is not a version.</summary>
    public static AppRelease? FromGitHubJson(string json, string assetName)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string tag = Text(root, "tag_name");
        if (ParseVersion(tag) is not { } version
            || (root.TryGetProperty("draft", out JsonElement draft) && draft.ValueKind == JsonValueKind.True)
            || (root.TryGetProperty("prerelease", out JsonElement pre) && pre.ValueKind == JsonValueKind.True))
        {
            return null;
        }

        string? download = null;
        if (root.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement asset in assets.EnumerateArray())
            {
                if (string.Equals(Text(asset, "name"), assetName, StringComparison.OrdinalIgnoreCase))
                {
                    download = Text(asset, "browser_download_url");
                    break;
                }
            }
        }

        string name = Text(root, "name");
        return new AppRelease(
            version,
            tag,
            name.Length > 0 ? name : tag,
            Text(root, "body").Replace("\r\n", "\n", StringComparison.Ordinal).Trim(),
            string.IsNullOrEmpty(download) ? null : download,
            Text(root, "html_url"));
    }

    /// <summary>The newest release, or null when there is none (or no version-tagged one).</summary>
    public static async Task<AppRelease?> GetLatestAsync(HttpClient http, string assetName, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repository}/releases/latest");
        request.Headers.UserAgent.ParseAdd("LiveSplitNative/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return FromGitHubJson(json, assetName);
    }

    /// <summary>The release is newer than this app (an app without a version, a developer build, never updates).</summary>
    public static bool IsNewer(AppRelease release, Version? current) =>
        current is not null && release.Version > current;

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
