using Avalonia.Controls;
using Avalonia.LogicalTree;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App;

/// <summary>
/// Updates (Windows LiveSplit looks for a new version of itself when it starts and asks whether
/// to update): at start, and from Check for Updates..., the newest release on GitHub; Update
/// downloads it, and LiveSplit quits (asking about unsaved splits and layout as always)
/// and opens again as the new version.
/// </summary>
public partial class MainWindow
{
    private static readonly HttpClient UpdateHttp = new() { Timeout = TimeSpan.FromMinutes(10) };
    private bool _updating;

    /// <summary>Tests: the release the check finds (instead of GitHub's).</summary>
    internal static Func<Task<AppRelease?>>? LatestReleaseForTests { get; set; }

    /// <summary>Tests: the version this app is (instead of its bundle's).</summary>
    internal static Version? CurrentVersionForTests { get; set; }

    private static Version? CurrentVersion => CurrentVersionForTests ?? AppUpdater.CurrentVersion;

    /// <summary>The check at start: quiet unless there is an update (a developer build never looks).</summary>
    private void CheckForUpdatesAtStart()
    {
        if (CurrentVersion is null)
        {
            return;
        }

        _ = CheckForUpdatesAsync(userAsked: false);
    }

    /// <summary>Check for Updates...: says so when there is none, or why it couldn't look.</summary>
    internal async Task CheckForUpdatesAsync(bool userAsked)
    {
        if (_updating)
        {
            return;
        }

        Version? current = CurrentVersion;
        if (current is null)
        {
            if (userAsked)
            {
                await ShowErrorAsync("Check for Updates", "This copy of LiveSplit was not installed from a release, so it can't update itself.");
            }

            return;
        }

        AppRelease? release;
        try
        {
            release = LatestReleaseForTests is { } fake
                ? await fake()
                : await AppUpdates.GetLatestAsync(UpdateHttp, AppUpdater.AssetName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            if (userAsked)
            {
                await ShowErrorAsync("Check for Updates", "Couldn't look for updates. Check your internet connection and try again.");
            }

            return;
        }

        if (release is null || !AppUpdates.IsNewer(release, current))
        {
            if (userAsked)
            {
                await ShowErrorAsync("Check for Updates", $"You have the newest version of LiveSplit ({VersionText(current)}).");
            }

            return;
        }

        if (await AskToUpdateAsync(release, current))
        {
            await UpdateAsync(release);
        }
    }

    /// <summary>UpdateHelper's question: what the new version brings, and whether to update now.</summary>
    private async Task<bool> AskToUpdateAsync(AppRelease release, Version current)
    {
        (Window dialog, Button notNow, Button update) = UpdateQuestion(release, current);
        bool yes = false;
        notNow.Click += (_, _) => dialog.Close();
        update.Click += (_, _) =>
        {
            yes = true;
            dialog.Close();
        };
        await ShowOwnedDialogAsync(dialog);
        return yes;
    }

    internal static (Window Dialog, Button NotNow, Button Update) UpdateQuestion(AppRelease release, Version current)
    {
        string notes = string.Join('\n', release.Notes.Split('\n').Take(12));
        string message = $"LiveSplit {VersionText(release.Version)} is available. You have {VersionText(current)}."
            + (notes.Length > 0 ? "\n\n" + notes : "")
            + "\n\nDo you want to update now? LiveSplit will close and open again as the new version.";
        var notNow = new Button { Content = "Not Now" };
        var update = new Button { Content = "Update", IsDefault = true };
        return (DialogChrome.Message("New Version Available", message, 460, notNow, update), notNow, update);
    }

    /// <summary>Downloads the release, then quits so it can be put in place (the app opens again).</summary>
    private async Task UpdateAsync(AppRelease release)
    {
        string? bundle = AppUpdater.BundlePath;
        if (release.DownloadUrl is null || bundle is null || !AppUpdater.CanReplace(bundle))
        {
            await ShowErrorAsync(
                "Update",
                "LiveSplit can't update itself here. The release page opens in your browser: download the new version there and replace this one with it.");
            SpeedrunComSharing.OpenBrowser(release.PageUrl);
            return;
        }

        _updating = true;
        var cancel = new Button { Content = "Cancel" };
        Window progress = DialogChrome.Message("Update", "Downloading the update...", 360, cancel);
        TextBlock text = progress.GetLogicalDescendants().OfType<TextBlock>().Last();
        using var stop = new CancellationTokenSource();
        cancel.Click += (_, _) => stop.Cancel();
        Task shown = ShowOwnedDialogAsync(progress);
        try
        {
            string app = await AppUpdater.DownloadAsync(
                UpdateHttp,
                release.DownloadUrl,
                new Progress<double>(p => text.Text = $"Downloading the update... {p:P0}"),
                stop.Token);
            AppUpdater.InstallWhenQuitting(app);
            progress.Close();
            await shown;
            Close();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            progress.Close();
            await shown;
            if (!stop.IsCancellationRequested)
            {
                await ShowErrorAsync("Update", "The update could not be downloaded. Try again later.");
            }
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>A version as its release tag names it: 2026.11.2.915 is "2026.11.02.0915".</summary>
    internal static string VersionText(Version version) =>
        version.Major >= 2000
            ? $"{version.Major}.{version.Minor:00}.{Math.Max(0, version.Build):00}" + (version.Revision > 0 ? $".{version.Revision:0000}" : "")
            : version.ToString();
}
