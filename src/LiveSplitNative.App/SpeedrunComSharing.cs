using Avalonia.Controls;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App;

/// <summary>
/// Share's speedrun.com connection (upstream's static SpeedrunCom.Client): one submitter for the
/// app, its API key in the Keychain as Windows keeps it in the Credential Manager. Tests replace
/// the network, the store and the browser.
/// </summary>
internal static class SpeedrunComSharing
{
    private static SpeedrunComSubmitter? _submitter;

    internal static HttpMessageHandler Handler { get; set; } = new SocketsHttpHandler();

    internal static ICredentialStore Credentials { get; set; } =
        OperatingSystem.IsMacOS() ? new MacKeychainCredentialStore()
        : OperatingSystem.IsLinux() ? new LinuxSecretCredentialStore()
        : new MemoryCredentialStore();

    internal static Action<string> OpenBrowser { get; set; } = RacetimeController.OpenInBrowser;

    internal static SpeedrunComSubmitter Submitter => _submitter ??= new SpeedrunComSubmitter(Handler, Credentials);

    /// <summary>Drops the submitter so the next use takes the current Handler / Credentials (tests).</summary>
    internal static void Reset() => _submitter = null;

    /// <summary>
    /// SpeedrunComApiKeyPrompt.GetAccessToken: opens speedrun.com's API key page and asks for the key.
    /// </summary>
    internal static Task<string?> AskForApiKeyAsync(Window owner)
    {
        OpenBrowser(SpeedrunComSubmitter.ApiKeyPage);
        return DialogChrome.PromptAsync(owner, "Speedrun.com Authentication", "Enter your Speedrun.com API Key:", labelWidth: 230);
    }
}
