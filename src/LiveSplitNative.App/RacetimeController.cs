using System.Diagnostics;
using Avalonia.Threading;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Options;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.Session;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// racetime.gg (LiveSplit.Racetime, a race provider): the "racetime.gg Races" menu with the open
/// and running races and New Race..., and the race window a race opens in. The account's tokens
/// are kept in the Keychain; signing in happens in the browser.
/// </summary>
internal sealed class RacetimeController
{
    private static RacetimeApi? _api;
    private readonly TimerSession _session;
    private readonly Func<AppSettings> _settings;
    private readonly Func<Layout> _layout;

    public RacetimeController(TimerSession session, Func<AppSettings> settings, Func<Layout> layout)
    {
        _session = session;
        _settings = settings;
        _layout = layout;
    }

    // The network, the token store and the browser (tests and proofs replace them).
    internal static HttpMessageHandler Handler { get; set; } = new SocketsHttpHandler();

    internal static ICredentialStore Credentials { get; set; } =
        OperatingSystem.IsMacOS() ? new MacKeychainCredentialStore()
        : OperatingSystem.IsLinux() ? new LinuxSecretCredentialStore()
        : new MemoryCredentialStore();

    internal static Action<string> OpenBrowser { get; set; } = OpenInBrowser;

    internal static Func<IRacetimeSocket> SocketFactory { get; set; } = () => new ClientRacetimeSocket();

    /// <summary>RacetimeAPI.Instance.</summary>
    internal static RacetimeApi Api => _api ??= new RacetimeApi(
        Handler,
        new RacetimeAuthenticator(Handler, Credentials, url => OpenBrowser(url)) { Log = line => Console.WriteLine("racetime sign-in: " + line) });

    /// <summary>Drops the API so the next use takes the current Handler / Credentials (tests).</summary>
    internal static void ResetApi() => _api = null;

    /// <summary>The race windows that are open (for proofs and tests).</summary>
    internal List<RacetimeChannelWindow> Windows { get; } = [];

    private bool Enabled => _settings().Racetime.Enabled;

    /// <summary>UpdateRaceProviderIntegration: the race list is read when LiveSplit starts.</summary>
    public void Refresh()
    {
        if (Enabled)
        {
            _ = Api.RefreshRacesAsync();
        }
    }

    /// <summary>
    /// The "racetime.gg Races" submenu from the races last read (none while the service is
    /// disabled). The list is read again for the next time the menu opens, as Windows reads it
    /// whenever the menu item is hovered.
    /// </summary>
    public NativeMenuAction? MenuItem()
    {
        if (!Enabled)
        {
            return null;
        }

        var children = new List<NativeMenuEntry>();
        foreach (RaceMenuEntry entry in RaceMenu.Build(Api.Races, DateTime.UtcNow))
        {
            if (entry.IsSeparator)
            {
                children.Add(new NativeMenuSeparator());
            }
            else if (entry.IsNewRace)
            {
                children.Add(new NativeMenuAction { Header = entry.Title!, OnClick = Create });
            }
            else
            {
                string id = entry.RaceId!;
                children.Add(new NativeMenuAction { Header = entry.Title!, OnClick = () => Join(id) });
            }
        }

        _ = Api.RefreshRacesAsync();
        return new NativeMenuAction { Header = string.Format(UiLocalizer.T("{0} Races"), RacetimeApi.ProviderName), Children = children };
    }

    /// <summary>RacetimeAPI.Join: the race window opens and joins the race.</summary>
    public RacetimeChannelWindow Join(string id)
    {
        var channel = new RacetimeChannel(
            _session,
            _settings().Racetime,
            Api.Authenticator,
            SocketFactory,
            action => Dispatcher.UIThread.Post(action));
        // The channel's messages (Windows shows them in the race's chat) go to the app's output.
        channel.SystemMessage += (text, _) => Console.WriteLine("racetime: " + text);
        var window = new RacetimeChannelWindow(channel, id, _layout().Settings.AlwaysOnTop);
        Windows.Add(window);
        window.Closed += (_, _) => Windows.Remove(window);
        window.Start(id);
        return window;
    }

    /// <summary>RacetimeAPI.Create: New Race... opens racetime.gg, where races are started.</summary>
    public static void Create() => OpenBrowser(RacetimeApi.CreateRaceAddress);

    internal static void OpenInBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo("open") { ArgumentList = { url }, UseShellExecute = false })?.Dispose();
            }
            else
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Could not open " + url + ": " + e.Message);
        }
    }
}
