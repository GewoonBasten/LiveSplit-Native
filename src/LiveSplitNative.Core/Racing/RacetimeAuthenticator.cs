using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LiveSplitNative.Core.Racing;

/// <summary>Where racetime.gg's tokens are kept (WebCredentials: Windows' Credential Manager; a Mac's Keychain).</summary>
public interface ICredentialStore
{
    string? Read(string name);

    void Write(string name, string? value);
}

/// <summary>A store that keeps nothing past the session (tests, platforms without one).</summary>
public sealed class MemoryCredentialStore : ICredentialStore
{
    private readonly Dictionary<string, string> _values = [];

    public string? Read(string name) => _values.TryGetValue(name, out string? value) ? value : null;

    public void Write(string name, string? value)
    {
        if (value is null)
        {
            _values.Remove(name);
        }
        else
        {
            _values[name] = value;
        }
    }
}

public enum AuthResult
{
    Pending,
    Success,
    Failure,
    Cancelled,
    Stale,
}

/// <summary>The signed-in racetime.gg account (userinfo).</summary>
public sealed record RacetimeIdentity(string Id, string Name, string FullName);

/// <summary>
/// Port of LiveSplit.Racetime's RacetimeAuthenticator: OAuth2 with PKCE against racetime.gg.
/// The browser opens racetime.gg's authorize page; racetime.gg sends it back to
/// http://127.0.0.1:4888/ with the code, which is exchanged for an access and a refresh token.
/// </summary>
public sealed class RacetimeAuthenticator(HttpMessageHandler handler, ICredentialStore credentials, Action<string> openBrowser)
{
    public const string AuthServer = "https://racetime.gg/";
    public const string ClientId = "dKgJFc2jZjgMtUT4Ik9kyJSCZkVqRK1poCcvpKox";
    // LiveSplit's own racetime.gg app secret (LiveSplit.Racetime Resources.resx OAUTH_CLIENTSECRET).
    // LiveSplit.Racetime's OAUTH_CLIENTSECRET (Properties/Resources.resx; the same in 1.8.37's DLL).
    public const string ClientSecret = "Rl6gGXy9zPw5xkSa0gSdE69lnlqbTwuZDAFNSDTgY5QKbQ9AqsqDYlotNGMBboDGkZFbJm9fSIpfI3sCGc3UiiZ2sEpk8KpxYf4kh0G7TjmYmgn0AXyNJNzkgIorHAcB";
    public const string Scopes = "read chat_message race_action";
    public const string RedirectAddress = "127.0.0.1";
    public const int RedirectPort = 4888;
    public const string AccessTokenName = "LiveSplit_racetimegg_accesstoken";
    public const string RefreshTokenName = "LiveSplit_racetimegg_refreshtoken";

    private readonly HttpClient _http = new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
    private HttpListener? _localEndpoint;
    private TaskCompletionSource? _localEndpointStopped;
    private string _verifier = "";
    private string? _code;

    public static string RedirectUri => $"http://{RedirectAddress}:{RedirectPort}/";

    // The saved sign-in is read from the credential store (macOS: the Keychain, which may ask the
    // user each time) once, the first time it is needed, and kept in memory after that.
    private string? _accessToken;
    private bool _accessTokenRead;
    private string? _refreshToken;
    private bool _refreshTokenRead;

    public string? AccessToken
    {
        get
        {
            if (!_accessTokenRead)
            {
                _accessToken = credentials.Read(AccessTokenName);
                _accessTokenRead = true;
            }

            return _accessToken;
        }
        set
        {
            _accessToken = value;
            _accessTokenRead = true;
            credentials.Write(AccessTokenName, value);
        }
    }

    /// <summary>The access token only when it was already read this session (never reads the store).</summary>
    public string? AccessTokenIfRead => _accessTokenRead ? _accessToken : null;

    public string? RefreshToken
    {
        get
        {
            if (!_refreshTokenRead)
            {
                _refreshToken = credentials.Read(RefreshTokenName);
                _refreshTokenRead = true;
            }

            return _refreshToken;
        }
        set
        {
            _refreshToken = value;
            _refreshTokenRead = true;
            credentials.Write(RefreshTokenName, value);
        }
    }

    public RacetimeIdentity? Identity { get; private set; }

    /// <summary>
    /// The port the browser's answer is taken on: racetime.gg sends it to <see cref="RedirectUri"/>
    /// (4888); tests use a free one.
    /// </summary>
    public int ListenPort { get; init; } = RedirectPort;

    /// <summary>The sign-in's steps, without tokens or codes (the app writes them to its output).</summary>
    public Action<string>? Log { get; set; }

    public string? Error { get; private set; }

    public bool IsAuthorizing { get; private set; }

    public void ResetTokens()
    {
        _code = null;
        AccessToken = null;
    }

    /// <summary>
    /// Authorize: the saved token if userinfo accepts it; else a refreshed token; else the user
    /// signs in in the browser. While one runs, another gets Pending (the race window then calls
    /// <see cref="StopPendingAuthRequest"/> and asks again, which takes over the sign-in). A
    /// sign-in taken over this way ends Stale and leaves the flag to the one that took over, as
    /// Windows' Authorize does.
    /// </summary>
    public async Task<AuthResult> AuthorizeAsync()
    {
        if (IsAuthorizing)
        {
            return AuthResult.Pending;
        }

        IsAuthorizing = true;
        Error = null;
        AuthResult result = AuthResult.Failure;
        Log?.Invoke("authorizing (saved sign-in: " + (AccessToken is null ? "no" : "yes") + ")");
        try
        {
            result = await AuthorizeOnceAsync();
            return result;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log?.Invoke(ex.GetType().Name + ": " + ex.Message);
            Error ??= "Unknown Error";
            ResetTokens();
            result = AuthResult.Failure;
            return result;
        }
        finally
        {
            Log?.Invoke("result: " + result + (Error is null ? "" : " (" + Error + ")"));
            if (result != AuthResult.Stale)
            {
                IsAuthorizing = false;
            }
        }
    }

    private async Task<AuthResult> AuthorizeOnceAsync()
    {
        bool secondRun = false;
        while (true)
        {
            // 1st: the user's information; if that works, the token is good.
            try
            {
                if (await TryGetUserInfoAsync())
                {
                    return AuthResult.Success;
                }
            }
            catch (HttpRequestException)
            {
                Error = "No Connection to racetime.gg";
                ResetTokens();
                return AuthResult.Failure;
            }

            // 2nd: renew access with the refresh token, or with a code from a sign-in.
            if (await TryRefreshAccessAsync() || (_code is not null && await TryGetAccessAsync()))
            {
                continue;
            }

            // 3rd: everything failed: the user signs in.
            switch (await TryGetAuthenticatedAsync())
            {
                case 403:
                    return AuthResult.Cancelled;
                case 200:
                    if (await TryGetAccessAsync())
                    {
                        continue;
                    }

                    break;
                case 404:
                    return AuthResult.Stale;
            }

            if (secondRun || Error is not null)
            {
                ResetTokens();
                return AuthResult.Failure;
            }

            secondRun = true;
        }
    }

    /// <summary>
    /// StopPendingAuthRequest (Windows): the sign-in that is waiting no longer holds the flag, so
    /// the next Authorize goes ahead (its sign-in stops the waiting one's listener).
    /// </summary>
    public void StopPendingAuthRequest() => IsAuthorizing = false;

    private async Task<bool> TryGetUserInfoAsync()
    {
        if (AccessToken is not { } token)
        {
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, AuthServer + "o/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using HttpResponseMessage response = await _http.SendAsync(request);
        Log?.Invoke("user info: HTTP " + (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            AccessToken = null;
            return false;
        }

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement user = json.RootElement;
        Identity = new RacetimeIdentity(Json.String(user, "id") ?? "", Json.String(user, "name") ?? "", Json.String(user, "full_name") ?? "");
        return true;
    }

    private async Task<bool> TryRefreshAccessAsync()
    {
        if (RefreshToken is not { } refresh)
        {
            return false;
        }

        (int status, JsonElement body) = await TokenRequestAsync(
            $"code={_code}&redirect_uri={RedirectUri}&client_id={ClientId}&client_secret={ClientSecret}&refresh_token={refresh}&grant_type=refresh_token");
        Log?.Invoke("refresh: HTTP " + status);
        if (status != 200)
        {
            RefreshToken = null;
            return false;
        }

        AccessToken = Json.String(body, "access_token");
        RefreshToken = Json.String(body, "refresh_token");
        return AccessToken is not null;
    }

    private async Task<bool> TryGetAccessAsync()
    {
        (int status, JsonElement body) = await TokenRequestAsync(
            $"code={_code}&redirect_uri={RedirectUri}&client_id={ClientId}&code_verifier={_verifier}&client_secret={ClientSecret}&scope={Scopes}&grant_type=authorization_code");
        Log?.Invoke("token: HTTP " + status);
        _code = null;
        if (status == 400)
        {
            Error = "Access has been revoked. Reauthentication required";
            return false;
        }

        if (status != 200)
        {
            Error = "Authentication successful, but access wasn't granted by the server";
            return false;
        }

        AccessToken = Json.String(body, "access_token");
        RefreshToken = Json.String(body, "refresh_token");
        if (AccessToken is null || RefreshToken is null)
        {
            Error = "Final access check failed. Server responded with success, but hasn't delivered a valid Token.";
            return false;
        }

        return true;
    }

    private async Task<(int Status, JsonElement Body)> TokenRequestAsync(string body)
    {
        try
        {
            body += "&state=" + RandomBase64(32);
            using HttpContent content = TokenContent(body);
            using HttpResponseMessage response = await _http.PostAsync(AuthServer + "o/token", content);
            Log?.Invoke("token request: HTTP " + (int)response.StatusCode);
            string text = await response.Content.ReadAsStringAsync();
            JsonElement json = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text).RootElement.Clone();
            return ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500 ? 400 : (int)response.StatusCode, json);
        }
        catch (Exception ex)
        {
            Log?.Invoke("token request: " + ex.GetType().Name + ": " + ex.Message);
            return (500, default);
        }
    }

    /// <summary>
    /// RestRequest's body: ASCII, Content-Type application/x-www-form-urlencoded with no charset,
    /// as Windows sends it (racetime.gg answers "; charset=us-ascii" with a Bad Request page).
    /// </summary>
    public static HttpContent TokenContent(string body)
    {
        var content = new ByteArrayContent(Encoding.ASCII.GetBytes(body));
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
        return content;
    }

    /// <summary>
    /// TryGetAuthenticated: the browser sign-in, answered on the local redirect address. An HTTP
    /// listener rather than Windows' one accepted socket: a browser may open a connection first
    /// and send the answer on another one (the sign-in then waited for ever, Basten 2 Nov in Arc),
    /// and asks other things (favicon) that are answered and passed over.
    /// </summary>
    private async Task<int> TryGetAuthenticatedAsync()
    {
        _verifier = RandomBase64(32);
        Error = null;
        string state = RandomBase64(32);
        string challenge = Sha256(_verifier);
        HttpListener? listener = null;
        try
        {
            StopLocalEndpoint();
            var created = new HttpListener();
            created.Prefixes.Add($"http://{RedirectAddress}:{ListenPort}/");
            try
            {
                created.Start();
            }
            catch (Exception)
            {
                created.Close();
                throw;
            }

            listener = created;
            _localEndpoint = created;
            var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _localEndpointStopped = stopped;
            Log?.Invoke("waiting for the browser at " + RedirectUri);
            openBrowser($"{AuthServer}o/authorize?response_type=code&client_id={ClientId}&scope={Scopes}&redirect_uri={RedirectUri}&state={state}&code_challenge={challenge}&code_challenge_method=S256");

            HttpListenerContext context;
            System.Collections.Specialized.NameValueCollection query;
            while (true)
            {
                // Stopping the listener need not end a waiting GetContextAsync on every platform.
                Task<HttpListenerContext> next = listener.GetContextAsync();
                if (await Task.WhenAny(next, stopped.Task) != next)
                {
                    throw new ObjectDisposedException(nameof(HttpListener));
                }

                context = await next;
                query = context.Request.QueryString;
                if (query["state"] is not null || query["code"] is not null || query["error"] is not null)
                {
                    break;
                }

                Log?.Invoke("the browser asked for " + context.Request.Url?.AbsolutePath + " (passed over)");
                Answer(context, 404, null);
            }

            string? requestState = query["state"];
            _code = query["code"];
            string? error = query["error"];
            Log?.Invoke("the browser answered: code " + (_code is null ? "no" : "yes") + ", error " + (error ?? "none")
                + ", state " + (state == requestState ? "matches" : "differs"));
            if (error is not null)
            {
                Answer(context, 301, AuthServer + "o/done?error=access_denied");
                StopLocalEndpoint();
                Error = "Unable to authenticate: The server rejected the request";
                return 403;
            }

            if (state != requestState)
            {
                Answer(context, 301, AuthServer + "o/done?error=access_denied");
                StopLocalEndpoint();
                Error = "Unable to authenticate: The server hasn't responded correctly. Possible protocol error";
                return 400;
            }

            Answer(context, 301, AuthServer + "o/done");
            StopLocalEndpoint();
        }
        catch (Exception ex) when (listener is not null && !ReferenceEquals(_localEndpoint, listener))
        {
            // Another sign-in took over (it stopped this one's listener).
            Log?.Invoke("taken over by another sign-in (" + ex.GetType().Name + ")");
            return 404;
        }
        catch (Exception ex)
        {
            Log?.Invoke("listener: " + ex.GetType().Name + ": " + ex.Message);
            Error = "Unknown Error";
            StopLocalEndpoint();
        }

        return Error is null ? 200 : 500;
    }

    /// <summary>The answer to the browser: a redirect to racetime.gg (301) or nothing here (404).</summary>
    private static void Answer(HttpListenerContext context, int status, string? location)
    {
        try
        {
            context.Response.StatusCode = status;
            if (location is not null)
            {
                context.Response.RedirectLocation = location;
            }

            context.Response.Close();
        }
        catch (Exception)
        {
            // The browser went away.
        }
    }

    private void StopLocalEndpoint()
    {
        HttpListener? listener = _localEndpoint;
        _localEndpoint = null;
        _localEndpointStopped?.TrySetResult();
        _localEndpointStopped = null;
        try
        {
            listener?.Close();
        }
        catch (Exception)
        {
        }
    }

    private static string Sha256(string input) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(input))).Replace("+", "-").Replace("/", "_").Replace("=", "");

    private static string RandomBase64(int length) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(length)).Replace("+", "-").Replace("/", "_").Replace("=", "");
}
