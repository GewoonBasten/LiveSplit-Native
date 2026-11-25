using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Installs a new version of the app over the running one: the release's zip is downloaded and
/// unpacked next to nothing of the user's (a temporary folder); when the app quits, a small script
/// waits for it to be gone, puts the new app where the old one was (the old one is kept until the
/// new one is in place) and opens it. Only an app run from its .app bundle (made by
/// tools/mac/make-app.command, which writes its version) updates itself.
/// </summary>
internal static partial class AppUpdater
{
    private static string? _pendingApp;
    private static DateTime _pendingSince;

    /// <summary>
    /// The running app's bundle ("…/LiveSplit.app"), or on Linux its folder ("…/linux-x64", with the
    /// version.txt tools/linux/publish.sh writes); null when it is not run from either.
    /// </summary>
    public static string? BundlePath
    {
        get
        {
            if (OperatingSystem.IsLinux())
            {
                string folder = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
                return File.Exists(Path.Combine(folder, VersionFile)) ? folder : null;
            }

            string macOs = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
            if (!string.Equals(Path.GetFileName(macOs), "MacOS", StringComparison.Ordinal))
            {
                return null;
            }

            string? bundle = Path.GetDirectoryName(Path.GetDirectoryName(macOs));
            return bundle is not null && bundle.EndsWith(".app", StringComparison.OrdinalIgnoreCase) ? bundle : null;
        }
    }

    /// <summary>The bundle's version (Info.plist CFBundleShortVersionString), or null for a developer build.</summary>
    public static Version? CurrentVersion
    {
        get
        {
            if (BundlePath is not { } bundle)
            {
                return null;
            }

            try
            {
                if (OperatingSystem.IsLinux())
                {
                    return AppUpdates.ParseVersion(File.ReadAllText(Path.Combine(bundle, VersionFile)).Trim());
                }

                string plist = File.ReadAllText(Path.Combine(bundle, "Contents", "Info.plist"));
                Match match = ShortVersion().Match(plist);
                return match.Success ? AppUpdates.ParseVersion(match.Groups[1].Value) : null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }

    /// <summary>A Linux release's version, next to the app (tools/linux/publish.sh).</summary>
    public const string VersionFile = "version.txt";

    /// <summary>This Mac's chip as the release zips name it.</summary>
    public static string Arch => RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x86_64";

    /// <summary>This system's release file: the Mac's zip, or Linux's archive for its chip.</summary>
    public static string AssetName => OperatingSystem.IsLinux()
        ? $"LiveSplit-Native-linux-{(RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64")}.tar.gz"
        : AppUpdates.AssetName(Arch);

    /// <summary>The app's folder can be written (so the new app can take the old one's place).</summary>
    public static bool CanReplace(string bundle)
    {
        string? parent = Path.GetDirectoryName(bundle);
        if (parent is null)
        {
            return false;
        }

        string probe = Path.Combine(parent, $".lsn-update-{Environment.ProcessId}");
        try
        {
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The release's zip, downloaded and unpacked: the new app's path.</summary>
    public static async Task<string> DownloadAsync(HttpClient http, string url, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        string folder = Path.Combine(Path.GetTempPath(), $"lsn-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        string zip = Path.Combine(folder, "update.zip");
        using (HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;
            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using FileStream target = File.Create(zip);
            byte[] buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                done += read;
                if (total is > 0)
                {
                    progress?.Report((double)done / total.Value);
                }
            }
        }

        // ditto keeps what a zip of an app needs (permissions, symbolic links, the signature);
        // Linux's archive is a tar.gz of the app's folder.
        string unpacked = Path.Combine(folder, "app");
        Directory.CreateDirectory(unpacked);
        ProcessStartInfo unpack = OperatingSystem.IsLinux()
            ? new ProcessStartInfo("tar", ["-xzf", zip, "-C", unpacked]) { UseShellExecute = false }
            : new ProcessStartInfo("/usr/bin/ditto", ["-x", "-k", zip, unpacked]) { UseShellExecute = false };
        using (Process process = Process.Start(unpack)!)
        {
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                throw new IOException("The update could not be unpacked.");
            }
        }

        string? app = OperatingSystem.IsLinux()
            ? Directory.EnumerateDirectories(unpacked).FirstOrDefault(d => File.Exists(Path.Combine(d, "LiveSplitNative.App")))
            : Directory.EnumerateDirectories(unpacked, "*.app").FirstOrDefault(d => File.Exists(Path.Combine(d, "Contents", "MacOS", "LiveSplitNative.App")));
        if (app is null)
        {
            throw new IOException("The update does not hold LiveSplit.");
        }

        return app;
    }

    /// <summary>The new app takes the old one's place when the app quits (and is opened then).</summary>
    public static void InstallWhenQuitting(string newApp)
    {
        _pendingApp = newApp;
        _pendingSince = DateTime.UtcNow;
    }

    /// <summary>
    /// The app quits: the waiting update is put in place by a script that outlives it. The new
    /// app is opened when the quit followed the update (not when the user put the quit off, say
    /// to keep a run going, and quit much later).
    /// </summary>
    public static void OnExit()
    {
        if (_pendingApp is not { } newApp || BundlePath is not { } bundle)
        {
            return;
        }

        bool reopen = DateTime.UtcNow - _pendingSince < TimeSpan.FromMinutes(2);
        string script = Path.Combine(Path.GetDirectoryName(newApp)!, "..", "install.sh");
        File.WriteAllText(script, InstallScript(Environment.ProcessId, bundle, newApp, reopen, OperatingSystem.IsLinux()));
        Process.Start(new ProcessStartInfo("/bin/bash", ["-c", $"nohup /bin/bash {Quote(Path.GetFullPath(script))} >/dev/null 2>&1 &"]) { UseShellExecute = false });
        _pendingApp = null;
    }

    /// <summary>
    /// The script: wait for the app to quit, swap the bundles (folders on Linux; the old one back
    /// if that fails), open it.
    /// </summary>
    internal static string InstallScript(int pid, string bundle, string newApp, bool reopen, bool linux = false) =>
        $"""
        #!/bin/bash
        while kill -0 {pid} 2>/dev/null; do sleep 0.2; done
        old={Quote(bundle)}
        new={Quote(newApp)}
        rm -rf "$old.previous"
        if mv "$old" "$old.previous"; then
          if mv "$new" "$old"; then
            rm -rf "$old.previous"
          else
            mv "$old.previous" "$old"
          fi
        fi
        {(linux ? "chmod +x \"$old/LiveSplitNative.App\"" : "xattr -dr com.apple.quarantine \"$old\" 2>/dev/null")}
        {(reopen ? (linux ? "cd \"$old\" && setsid ./LiveSplitNative.App >/dev/null 2>&1 &" : "open \"$old\"") : "")}
        """;

    private static string Quote(string path) => "'" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    [GeneratedRegex(@"<key>CFBundleShortVersionString</key>\s*<string>([^<]*)</string>")]
    private static partial Regex ShortVersion();
}
