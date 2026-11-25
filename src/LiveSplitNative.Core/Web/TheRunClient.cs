using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace LiveSplitNative.Core.Web;

/// <summary>A therun.gg account an upload key belongs to: its name and profile picture.</summary>
public sealed record TheRunUser(string Username, string? Picture);

/// <summary>What checking an upload key found: the account, an invalid key, or no answer.</summary>
public sealed record TheRunKeyCheck(TheRunUser? User, bool Reached);

/// <summary>
/// therun.gg's endpoints as LiveSplit.TheRun 0.4.5 (CollectorComponent, CollectorSettings) calls
/// them: the live-run webhook, the splits upload (a presigned S3 URL, then a PUT of the .lss)
/// and the upload key check. The handler is given so tests can answer without a network.
/// </summary>
public sealed class TheRunClient
{
    public const string SplitWebhookUrl = "https://dspc6ekj2gjkfp44cjaffhjeue0fbswr.lambda-url.eu-west-1.on.aws/";
    public const string FileUploadBaseUrl = "https://2uxp372ks6nwrjnk6t7lqov4zu0solno.lambda-url.eu-west-1.on.aws/";
    public const string ValidateBaseUrl = "https://api.therun.gg/users/uploadKey/validate/";

    private readonly HttpClient _http;
    private readonly HttpMessageHandler _handler;

    public TheRunClient(HttpMessageHandler handler)
    {
        _handler = handler;
        // CollectorComponent's HttpClient: 15 s, Accept */*, Sec-Fetch-Site cross-site,
        // Content-Disposition attachment.
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("Accept", "*/*");
        _http.DefaultRequestHeaders.Add("Sec-Fetch-Site", "cross-site");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Content-Disposition", "attachment");
    }

    /// <summary>The client the app uses (the tests put an offline one here).</summary>
    public static TheRunClient Current { get; set; } = new(new HttpClientHandler());

    /// <summary>UpdateSplitsState: the live run, posted as JSON.</summary>
    public Task PostLiveAsync(string json, CancellationToken token) =>
        _http.PostAsync(SplitWebhookUrl, new StringContent(json), token);

    /// <summary>
    /// UploadSplitsCore, first half: asks for an upload URL for "Game-Category.lss" with the key
    /// and returns it encoded as therun.gg's bucket expects.
    /// </summary>
    public async Task<string> GetUploadUrlAsync(string game, string category, string uploadKey)
    {
        string fileName = HttpUtility.UrlEncode(game) + "-" + HttpUtility.UrlEncode(category) + ".lss";
        string fileUploadUrl = FileUploadBaseUrl + "?filename=" + fileName + "&uploadKey=" + uploadKey;

        HttpResponseMessage result = await _http.GetAsync(fileUploadUrl).ConfigureAwait(false);
        result.EnsureSuccessStatusCode();
        string responseBody = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
        using JsonDocument json = JsonDocument.Parse(responseBody);
        string url = HttpUtility.UrlDecode(json.RootElement.GetProperty("url").GetString() ?? "");
        return EncodeUrl(url, game, category);
    }

    /// <summary>UploadSplitsCore, second half: puts the splits at the upload URL.</summary>
    public async Task PutSplitsAsync(string uploadUrl, string lss)
    {
        var content = new StringContent(lss);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment");
        HttpResponseMessage putResult = await _http.PutAsync(uploadUrl, content).ConfigureAwait(false);
        putResult.EnsureSuccessStatusCode();
    }

    /// <summary>EncodeUrl: re-encodes the presigned URL's credential parts, the game, category and user name.</summary>
    public static string EncodeUrl(string url, string game, string category)
    {
        string[] urlParts =
        [
            .. url.Split('&').Select(urlPart =>
                urlPart.StartsWith("X-Amz-Credential", StringComparison.Ordinal)
                || urlPart.StartsWith("X-Amz-Security-Token", StringComparison.Ordinal)
                || urlPart.StartsWith("X-Amz-SignedHeaders", StringComparison.Ordinal)
                    ? HttpUtility.UrlEncode(urlPart).Replace("%3d", "=", StringComparison.Ordinal)
                    : urlPart),
        ];

        string newUrl = string.Join("&", urlParts)
            .Replace(game, HttpUtility.UrlEncode(game), StringComparison.Ordinal)
            .Replace(category, HttpUtility.UrlEncode(category), StringComparison.Ordinal);
        string username = newUrl.Replace("https://splits-bucket-main.s3.eu-west-1.amazonaws.com/", "", StringComparison.Ordinal).Split('/')[0];
        return newUrl.Replace(username, HttpUtility.UrlEncode(username), StringComparison.Ordinal);
    }

    /// <summary>
    /// ValidateKeyAsync: the account behind the key (result.data.username and picture), no
    /// account for a key therun.gg refuses, and Reached false when it cannot be reached.
    /// </summary>
    public async Task<TheRunKeyCheck> ValidateAsync(string uploadKey)
    {
        try
        {
            using var http = new HttpClient(_handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
            HttpResponseMessage result = await http.GetAsync(ValidateBaseUrl + uploadKey).ConfigureAwait(false);
            string body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!result.IsSuccessStatusCode)
            {
                return new TheRunKeyCheck(null, true);
            }

            using JsonDocument json = JsonDocument.Parse(body);
            JsonElement data = json.RootElement.GetProperty("result").GetProperty("data");
            string username = data.GetProperty("username").GetString() ?? "";
            string? picture = data.TryGetProperty("picture", out JsonElement p) && p.ValueKind == JsonValueKind.String
                ? p.GetString()
                : null;
            return new TheRunKeyCheck(new TheRunUser(username, picture), true);
        }
        catch (Exception)
        {
            return new TheRunKeyCheck(null, false);
        }
    }

    /// <summary>The profile picture's bytes (PictureBox.LoadAsync); null when it cannot be had.</summary>
    public async Task<byte[]?> DownloadPictureAsync(string url)
    {
        try
        {
            using var http = new HttpClient(_handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
            return await http.GetByteArrayAsync(url).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
