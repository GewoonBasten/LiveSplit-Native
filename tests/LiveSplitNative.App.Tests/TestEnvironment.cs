using System.Net;
using System.Runtime.CompilerServices;
using LiveSplitNative.Core.Racing;
using LiveSplitNative.Core.UI;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Before any App test: therun.gg's upload key file lives in a temporary folder (never the
/// user's own key), and therun.gg is answered offline (no key is ever sent anywhere).
/// racetime.gg is offline too (no races, no account), its tokens stay in memory (never the
/// Keychain), and no browser or websocket is ever opened. speedrun.com's game list is offline
/// (no games, no cache file), and Share's speedrun.com is offline with its API key in memory.
/// The global keyboard hook never starts (no permission prompts), nor the atomic clock sync (no NTP);
/// gamepad buttons come from <see cref="Gamepads"/>. A window opened before a test names its own
/// settings file reads and writes one in a temporary folder, never the user's settings.cfg.
/// </summary>
internal static class TestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        TheRunUploadKey.Folder = Path.Combine(Path.GetTempPath(), "livesplitnative-app-tests-" + Environment.ProcessId);
        string settingsFolder = Path.Combine(Path.GetTempPath(), "livesplitnative-app-tests-settings-" + Environment.ProcessId);
        Directory.CreateDirectory(settingsFolder);
        AppCli.Parse(["--settings", Path.Combine(settingsFolder, "settings.cfg")]);
        TheRunClient.Current = new TheRunClient(new OfflineTheRun());
        RacetimeController.Handler = new OfflineRacetime();
        RacetimeController.Credentials = new MemoryCredentialStore();
        RacetimeController.OpenBrowser = _ => { };
        RacetimeController.SocketFactory = () => new ClosedSocket();
        RacetimeController.ResetApi();
        MainWindow.UseGlobalKeyboardHook = false;
        MainWindow.SyncAtomicClock = false;
        Platform.Gamepads.Current = Gamepads;
        SpeedrunComGameList.Current = new SpeedrunComGameList((_, _) => throw new HttpRequestException("offline"), null);
        SpeedrunComSharing.Handler = new OfflineTheRun();
        SpeedrunComSharing.Credentials = new MemoryCredentialStore();
        SpeedrunComSharing.OpenBrowser = _ => { };
        SpeedrunComSharing.Reset();
    }

    /// <summary>The controllers the tests press buttons on (never the Mac's own).</summary>
    internal static FakeGamepads Gamepads { get; } = new();

    internal sealed class FakeGamepads : Platform.IGamepadSource
    {
        private int _leases;

        public event Action<string>? ButtonPressed;

        public int Leases => _leases;

        public IDisposable Acquire()
        {
            Interlocked.Increment(ref _leases);
            return new Lease(this);
        }

        public void Press(string button) => ButtonPressed?.Invoke(button);

        private sealed class Lease(FakeGamepads owner) : IDisposable
        {
            private int _disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    Interlocked.Decrement(ref owner._leases);
                }
            }
        }
    }

    /// <summary>racetime.gg with no races and no signed-in account.</summary>
    internal sealed class OfflineRacetime : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == "/races/data"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"races\":[]}") }
                : new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{}") });
    }

    /// <summary>A race websocket that never connects.</summary>
    internal sealed class ClosedSocket : IRacetimeSocket
    {
        public bool IsOpen => false;

        public bool ClosedCleanly => true;

        public Task ConnectAsync(Uri uri, string accessToken, CancellationToken token) =>
            Task.FromException(new InvalidOperationException("offline"));

        public Task SendAsync(string text, CancellationToken token) => Task.CompletedTask;

        public Task<string?> ReceiveAsync(CancellationToken token) => Task.FromResult<string?>(null);

        public void Dispose()
        {
        }
    }

    /// <summary>therun.gg refusing every request (an invalid key, nothing uploaded).</summary>
    internal sealed class OfflineTheRun : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") });
    }
}
