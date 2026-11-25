using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Racing;

namespace LiveSplitNative.Core.Web;

/// <summary>The speedrun.com account an API key belongs to (SpeedrunComClient.Profile).</summary>
public sealed record SrcUser(string Id, string Name);

/// <summary>
/// What speedrun.com knows about a run's game, category, platform, region and variables
/// (upstream RunMetadata.Game, Category, Platform, Region and VariableValues). A variable without
/// a value maps to null; a custom value of a user-defined variable is marked as such.
/// </summary>
public sealed record SrcRunMetadata(
    SrcGame? Game,
    SrcCategory? Category,
    SrcNamed? Platform,
    SrcNamed? Region,
    IReadOnlyList<(SrcVariable Variable, SrcVariableValue? Value)> VariableValues,
    bool LookupFailed = false);

/// <summary>
/// A run on speedrun.com as RunMetadata.PatchRun reads it: its ID and page, game, category,
/// platform, region, emulator and variables (variable name → value label).
/// </summary>
public sealed record SrcRunInfo(
    string Id,
    Uri? WebLink,
    string GameName,
    string CategoryName,
    string PlatformName,
    string RegionName,
    bool Emulated,
    IReadOnlyDictionary<string, string> VariableValueNames);

/// <summary>A variable's value: its ID, or for a user-defined variable its own text (IsCustomValue).</summary>
public sealed record SrcVariableValue(string VariableId, string Value, bool IsCustomValue);

/// <summary>The outcome of <see cref="SpeedrunComSubmitter.SubmitRunAsync"/>: the run's page, or why not.</summary>
public sealed record SrcSubmitResult(bool Submitted, string? ReasonForRejection, Uri? WebLink);

/// <summary>
/// Port of LiveSplit's SpeedrunCom (93a11b85: ValidateRun, SubmitRun, MakeSureUserIsAuthenticated)
/// with SpeedrunComSharp's requests: the API key is checked against /profile, kept like Windows'
/// WebCredentials under "LiveSplit_SpeedrunComAccessToken", and a run goes to POST /runs.
/// </summary>
public sealed class SpeedrunComSubmitter
{
    public const string CredentialName = "LiveSplit_SpeedrunComAccessToken";

    /// <summary>The page with the user's API key (SpeedrunComApiKeyPrompt opens it).</summary>
    public const string ApiKeyPage = "https://www.speedrun.com/settings/api";

    private readonly HttpClient _http;
    private readonly ICredentialStore _credentials;
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);
    private SrcUser? _profile;
    private string? _profileToken;

    public SpeedrunComSubmitter(HttpMessageHandler handler, ICredentialStore credentials)
    {
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LiveSplitNative/1.0");
        _credentials = credentials;
        AccessToken = credentials.Read(CredentialName);
    }

    /// <summary>The API key requests are made with (SpeedrunComClient.AccessToken).</summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// ISpeedrunComAuthenticator.GetAccessToken: asks the user for an API key when the saved one
    /// is missing or no longer works (null or empty: they gave none).
    /// </summary>
    public Func<Task<string?>>? Authenticator { get; set; }

    /// <summary>SpeedrunComClient.Profile: the key's account, null when the key is missing or refused.</summary>
    public async Task<SrcUser?> GetProfileAsync(CancellationToken token = default)
    {
        if (AccessToken is null)
        {
            return null;
        }

        if (_profile is not null && _profileToken == AccessToken)
        {
            return _profile;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(SpeedrunComClient.BaseUri, "profile"));
            request.Headers.Add("X-API-Key", AccessToken);
            using HttpResponseMessage response = await _http.SendAsync(request, token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            JsonElement data = document.RootElement.GetProperty("data");
            _profile = new SrcUser(
                data.GetProperty("id").GetString() ?? "",
                data.GetProperty("names").GetProperty("international").GetString() ?? "");
            _profileToken = AccessToken;
            return _profile;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// SpeedrunCom.MakeSureUserIsAuthenticated: the saved key when it works; otherwise the
    /// authenticator's, which is kept when speedrun.com accepts it.
    /// </summary>
    public async Task<bool> MakeSureUserIsAuthenticatedAsync(CancellationToken token = default)
    {
        if (await GetProfileAsync(token).ConfigureAwait(false) is not null)
        {
            return true;
        }

        string? accessToken = Authenticator is null ? null : await Authenticator().ConfigureAwait(true);
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        AccessToken = accessToken;
        bool isTokenValid = await GetProfileAsync(token).ConfigureAwait(false) is not null;
        if (isTokenValid)
        {
            try
            {
                _credentials.Write(CredentialName, accessToken);
            }
            catch (Exception)
            {
                // Log.Error upstream; the key still works for this session.
            }
        }

        return isTokenValid;
    }

    /// <summary>SpeedrunCom.ClearAccessToken.</summary>
    public void ClearAccessToken() => AccessToken = null;

    /// <summary>
    /// RunMetadata's lookups: the game with the run's name (with its platforms and regions), its
    /// per-game category with the run's category name, the platform and region by name, and the
    /// full-game variables of that category with the run's values. A game or category that can't be
    /// fetched is null, as upstream's lookups give.
    /// </summary>
    public Task<SrcRunMetadata> LoadMetadataAsync(Run run, CancellationToken token = default) =>
        LoadMetadataAsync(run.GameName, run.CategoryName, run.Metadata.PlatformName, run.Metadata.RegionName, run.Metadata.VariableValueNames, token);

    /// <summary>
    /// <see cref="LoadMetadataAsync(Run, CancellationToken)"/> for names being edited (the Splits
    /// Editor's Additional Info). <see cref="SrcRunMetadata.LookupFailed"/> tells a game that
    /// speedrun.com doesn't have from one it couldn't be asked about.
    /// </summary>
    public async Task<SrcRunMetadata> LoadMetadataAsync(
        string gameName,
        string categoryName,
        string platformName,
        string regionName,
        IReadOnlyDictionary<string, string> variableValueNames,
        CancellationToken token = default)
    {
        bool failed = false;
        SrcGame? game = null;
        if (!string.IsNullOrEmpty(gameName))
        {
            try
            {
                string json = await GetAsync($"games?name={Uri.EscapeDataString(gameName)}&embed=platforms,regions&max=200", token)
                    .ConfigureAwait(false);
                game = SpeedrunComClient.ParseGames(json).FirstOrDefault(g => g.Name == gameName);
            }
            catch (Exception ex) when (IsLookupFailure(ex))
            {
                game = null;
                failed = true;
            }
        }

        SrcCategory? category = null;
        if (game is not null && !string.IsNullOrEmpty(categoryName))
        {
            try
            {
                category = SpeedrunComClient.ParseCategories(await GetAsync($"games/{Uri.EscapeDataString(game.Id)}/categories", token).ConfigureAwait(false))
                    .FirstOrDefault(c => c.IsPerGame && c.Name == categoryName);
            }
            catch (Exception ex) when (IsLookupFailure(ex))
            {
                category = null;
                failed = true;
            }
        }

        var values = new List<(SrcVariable, SrcVariableValue?)>();
        if (game is not null)
        {
            // Game.FullGameVariables of this category (or of all categories); a failure here is
            // ValidateRun's "unknown reason", as upstream's lazy Game.FullGameVariables throws.
            IEnumerable<SrcVariable> variables = SpeedrunComClient
                .ParseVariables(await GetAsync($"games/{Uri.EscapeDataString(game.Id)}/variables", token).ConfigureAwait(false))
                .Where(v => v.IsFullGame && (v.CategoryId is null || v.CategoryId == category?.Id));
            foreach (SrcVariable variable in variables)
            {
                SrcVariableValue? value = null;
                if (variableValueNames.TryGetValue(variable.Name, out string? label))
                {
                    string? valueId = variable.Values.FirstOrDefault(v => v.Value == label).Key;
                    if (valueId is not null)
                    {
                        value = new SrcVariableValue(variable.Id, valueId, IsCustomValue: false);
                    }
                    else if (variable.IsUserDefined)
                    {
                        value = new SrcVariableValue(variable.Id, label, IsCustomValue: true);
                    }
                }

                values.Add((variable, value));
            }
        }

        return new SrcRunMetadata(
            game,
            category,
            game?.Platforms.FirstOrDefault(p => p.Name == platformName),
            game?.Regions.FirstOrDefault(r => r.Name == regionName),
            values,
            failed);
    }

    /// <summary>
    /// RunsClient.GetRunIDFromSiteUri: the run ID in a speedrun.com run page's address (or the ID
    /// itself); null when there is none.
    /// </summary>
    public static string? GetRunIdFromSiteUri(string? siteUri)
    {
        if (siteUri is null)
        {
            return null;
        }

        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            siteUri, @"^(?:(?:https?://)?(?:www\.)?speedrun\.com/(?:\w+/)?runs?/)?(\w+)$");
        return match.Success && match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// RunsClient.GetRun with what RunMetadata.PatchRun reads: the game, category, platform and
    /// region embedded, the variables by name with their values' labels. Throws when speedrun.com
    /// refuses (no such run) or can't be reached.
    /// </summary>
    public async Task<SrcRunInfo> GetRunAsync(string runId, CancellationToken token = default)
    {
        string json = await GetAsync($"runs/{Uri.EscapeDataString(runId)}?embed=game,category,platform,region", token).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement data = document.RootElement.GetProperty("data");
        JsonElement game = Embedded(data, "game") ?? throw new JsonException("The run has no game.");
        string gameId = game.GetProperty("id").GetString() ?? "";
        string gameName = game.GetProperty("names").GetProperty("international").GetString() ?? "";
        string categoryName = Embedded(data, "category")?.GetProperty("name").GetString() ?? "";
        string platformName = Embedded(data, "platform")?.GetProperty("name").GetString() ?? "";
        string regionName = Embedded(data, "region")?.GetProperty("name").GetString() ?? "";
        bool emulated = data.TryGetProperty("system", out JsonElement system)
            && system.TryGetProperty("emulated", out JsonElement e) && e.ValueKind == JsonValueKind.True;

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (data.TryGetProperty("values", out JsonElement values) && values.ValueKind == JsonValueKind.Object && values.EnumerateObject().Any())
        {
            IReadOnlyList<SrcVariable> variables = SpeedrunComClient.ParseVariables(
                await GetAsync($"games/{Uri.EscapeDataString(gameId)}/variables", token).ConfigureAwait(false));
            foreach (JsonProperty value in values.EnumerateObject())
            {
                SrcVariable? variable = variables.FirstOrDefault(v => v.Id == value.Name);
                string valueId = value.Value.GetString() ?? "";
                if (variable is not null)
                {
                    names[variable.Name] = variable.Values.TryGetValue(valueId, out string? label) ? label : valueId;
                }
            }
        }

        return new SrcRunInfo(
            data.GetProperty("id").GetString() ?? runId,
            data.TryGetProperty("weblink", out JsonElement link) && Uri.TryCreate(link.GetString(), UriKind.Absolute, out Uri? webLink) ? webLink : null,
            gameName,
            categoryName,
            platformName,
            regionName,
            emulated,
            names);
    }

    /// <summary>RunMetadata.Run.WebLink: the page of the run with this ID.</summary>
    public async Task<Uri?> GetRunWebLinkAsync(string runId, CancellationToken token = default)
    {
        string json = await GetAsync($"runs/{Uri.EscapeDataString(runId)}", token).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("data").TryGetProperty("weblink", out JsonElement link)
            && Uri.TryCreate(link.GetString(), UriKind.Absolute, out Uri? webLink)
            ? webLink
            : null;
    }

    /// <summary>A file from speedrun.com (a game's icon or box art).</summary>
    public Task<byte[]> DownloadAsync(Uri uri, CancellationToken token = default) => _http.GetByteArrayAsync(uri, token);

    /// <summary>An embedded element ({"data": {...}}); null when it is missing or empty ({"data": []}).</summary>
    private static JsonElement? Embedded(JsonElement data, string name) =>
        data.TryGetProperty(name, out JsonElement element) && element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("data", out JsonElement inner) && inner.ValueKind == JsonValueKind.Object
            ? inner
            : null;

    /// <summary>
    /// SpeedrunCom.ValidateRun: null when the run can be submitted (with what speedrun.com knows
    /// about it), else the reason it can't.
    /// </summary>
    public async Task<(string? ReasonForRejection, SrcRunMetadata? Metadata)> ValidateRunAsync(Run run, CancellationToken token = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(run.Metadata.RunId))
            {
                return ("This run already exists on speedrun.com.", null);
            }

            if (!await MakeSureUserIsAuthenticatedAsync(token).ConfigureAwait(true))
            {
                return ("You can't submit a run without being authenticated.", null);
            }

            if (string.IsNullOrEmpty(run.GameName))
            {
                return ("You need to specify a game.", null);
            }

            SrcRunMetadata metadata = await LoadMetadataAsync(run, token).ConfigureAwait(false);
            if (metadata.Game is null)
            {
                return ("The game could not be found on speedrun.com.", null);
            }

            if (string.IsNullOrEmpty(run.CategoryName))
            {
                return ("You need to specify a category.", null);
            }

            if (metadata.Category is null)
            {
                return ("The category could not be found on speedrun.com.", null);
            }

            if (metadata.Category.Players > 1 && metadata.Category.PlayersExactly)
            {
                return ("Submitting runs for more than the currently authenticated user is not implemented yet.", null);
            }

            if (metadata.Platform is null)
            {
                return ("You need to specify the platform of the game.", null);
            }

            if (run.Count == 0 || run[^1].PersonalBestSplitTime.RealTime is null)
            {
                return ("You can't submit a run without a Real Time.", null);
            }

            // Upstream reads the pair's Value after picking the pair whose Value is null, so its
            // check never fires and speedrun.com refuses the run instead; this is the check it means.
            (SrcVariable Variable, SrcVariableValue? Value) missing = metadata.VariableValues
                .FirstOrDefault(x => x.Variable.IsMandatory && x.Value is null);
            if (missing.Variable is not null)
            {
                return ($"You need to specify a value for the variable \"{missing.Variable.Name}\".", null);
            }

            return (null, metadata);
        }
        catch (Exception)
        {
            return ("The run could not be validated for an unknown reason.", null);
        }
    }

    /// <summary>
    /// SpeedrunCom.FindPersonalBestAttemptDate: when the attempt that set the personal best ended
    /// (the attempt with the same real and game time), if known.
    /// </summary>
    public static DateTime? FindPersonalBestAttemptDate(Run run)
    {
        if (run.Count == 0)
        {
            return null;
        }

        SplitTime runTime = run[^1].PersonalBestSplitTime;
        Attempt? attempt = run.AttemptHistory.FirstOrDefault(x =>
            x.Time.GameTime == runTime.GameTime && x.Time.RealTime == runTime.RealTime);
        return attempt?.Ended?.Time;
    }

    /// <summary>
    /// SpeedrunComSubmitDialog.patchGameTime: a run without a game time gets the one entered,
    /// in its personal best and in the attempt that set it.
    /// </summary>
    public static void PatchGameTime(Run run, TimeSpan? gameTime)
    {
        if (gameTime is null || run.Count == 0)
        {
            return;
        }

        Segment lastSplit = run[^1];
        SplitTime runTime = lastSplit.PersonalBestSplitTime;
        if (runTime.GameTime.HasValue)
        {
            return;
        }

        Attempt? attempt = run.AttemptHistory.FirstOrDefault(x =>
            x.Time.GameTime == runTime.GameTime && x.Time.RealTime == runTime.RealTime);
        runTime.GameTime = gameTime;
        if (attempt is not null && attempt.Time.RealTime.HasValue)
        {
            attempt.Time = runTime;
        }

        lastSplit.PersonalBestSplitTime = runTime;
    }

    /// <summary>
    /// SpeedrunCom.SubmitRun: validates again, checks the video rule and the date, then submits
    /// the personal best with the game's timing methods (unverified). On success the run gets the
    /// new run's ID and the result its page.
    /// </summary>
    public async Task<SrcSubmitResult> SubmitRunAsync(
        Run run,
        string? comment = null,
        Uri? videoUri = null,
        DateTime? date = null,
        TimeSpan? withoutLoads = null,
        CancellationToken token = default)
    {
        try
        {
            (string? reason, SrcRunMetadata? metadata) = await ValidateRunAsync(run, token).ConfigureAwait(true);
            if (reason is not null || metadata is null)
            {
                return new SrcSubmitResult(false, reason, null);
            }

            SrcGame game = metadata.Game!;
            SrcUser? profile = await GetProfileAsync(token).ConfigureAwait(false);
            // Legal for moderators (series moderators aside), so they may leave the video out.
            if (game.RequiresVideo && videoUri is null && !game.ModeratorIds.Contains(profile?.Id ?? ""))
            {
                return new SrcSubmitResult(false, "Runs of this game require a video.", null);
            }

            IReadOnlyList<SrcTimingMethod> timingMethods = game.TimingMethods;
            SplitTime runTime = run[^1].PersonalBestSplitTime;
            date ??= FindPersonalBestAttemptDate(run);
            if (date.HasValue && date.Value.ToUniversalTime().Date > DateTime.UtcNow.Date)
            {
                return new SrcSubmitResult(false, "The date of the run can't be in the future.", null);
            }

            if (date.HasValue && game.YearOfRelease.HasValue && date.Value.ToUniversalTime().Date.Year < game.YearOfRelease)
            {
                return new SrcSubmitResult(false, "The date of the run can't be before the release date of the game.", null);
            }

            TimeSpan? realTime = timingMethods.Contains(SrcTimingMethod.RealTime) ? runTime.RealTime : null;
            TimeSpan? realTimeWithoutLoads = timingMethods.Contains(SrcTimingMethod.RealTimeWithoutLoads) ? runTime.GameTime : null;
            if (withoutLoads.HasValue)
            {
                realTimeWithoutLoads = withoutLoads;
            }

            TimeSpan? gameTime = timingMethods.Contains(SrcTimingMethod.GameTime) ? runTime.GameTime : null;
            if (realTime is null && realTimeWithoutLoads is null && gameTime is null)
            {
                // RunsClient.Submit's APIException, which has no errors to list.
                return new SrcSubmitResult(false, "You need to provide at least one time.", null);
            }

            string body = SubmitBody(
                metadata.Category!.Id,
                metadata.Platform!.Id,
                metadata.Region?.Id,
                realTime,
                realTimeWithoutLoads,
                gameTime,
                comment,
                videoUri,
                date,
                game.EmulatorsAllowed && run.Metadata.UsesEmulator,
                metadata.VariableValues.Select(x => x.Value).OfType<SrcVariableValue>());

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(SpeedrunComClient.BaseUri, "runs"))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-API-Key", AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using HttpResponseMessage response = await _http.SendAsync(request, token).ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new SrcSubmitResult(false, ApiErrors(text), null);
            }

            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement data = document.RootElement.GetProperty("data");
            run.Metadata.RunId = data.GetProperty("id").GetString();
            Uri? webLink = data.TryGetProperty("weblink", out JsonElement link) && link.GetString() is { } address
                ? new Uri(address)
                : null;
            return new SrcSubmitResult(true, null, webLink);
        }
        catch (Exception)
        {
            return new SrcSubmitResult(false, "The run could not be submitted for an unknown reason.", null);
        }
    }

    /// <summary>RunsClient.Submit's request body.</summary>
    public static string SubmitBody(
        string categoryId,
        string platformId,
        string? regionId,
        TimeSpan? realTime,
        TimeSpan? realTimeWithoutLoads,
        TimeSpan? gameTime,
        string? comment,
        Uri? videoUri,
        DateTime? date,
        bool emulated,
        IEnumerable<SrcVariableValue> variables)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteStartObject("run");
            json.WriteString("category", categoryId);
            json.WriteString("platform", platformId);
            if (date.HasValue)
            {
                json.WriteString("date", date.Value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrEmpty(regionId))
            {
                json.WriteString("region", regionId);
            }

            json.WriteBoolean("verified", false);
            json.WriteStartObject("times");
            if (realTime.HasValue)
            {
                json.WriteNumber("realtime", realTime.Value.TotalSeconds);
            }

            if (realTimeWithoutLoads.HasValue)
            {
                json.WriteNumber("realtime_noloads", realTimeWithoutLoads.Value.TotalSeconds);
            }

            if (gameTime.HasValue)
            {
                json.WriteNumber("ingame", gameTime.Value.TotalSeconds);
            }

            json.WriteEndObject();
            json.WriteBoolean("emulated", emulated);
            if (videoUri is not null)
            {
                json.WriteString("video", videoUri.AbsoluteUri);
            }

            if (!string.IsNullOrEmpty(comment))
            {
                json.WriteString("comment", comment);
            }

            var list = variables.ToList();
            if (list.Count > 0)
            {
                json.WriteStartObject("variables");
                foreach (SrcVariableValue variable in list)
                {
                    json.WriteStartObject(variable.VariableId);
                    json.WriteString("type", variable.IsCustomValue ? "user-defined" : "pre-defined");
                    json.WriteString("value", variable.Value);
                    json.WriteEndObject();
                }

                json.WriteEndObject();
            }

            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// SpeedrunComClient.ParseException as SubmitRun shows it: the errors, one per line. When the
    /// answer lists none, its message (upstream would show an empty text).
    /// </summary>
    public static string ApiErrors(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                return string.Join(Environment.NewLine, errors.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString()));
            }

            if (root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
        }

        return "The run could not be submitted for an unknown reason.";
    }

    /// <summary>SpeedrunComClient.DoRequest: GETs are kept, as upstream's client caches them.</summary>
    private async Task<string> GetAsync(string relative, CancellationToken token)
    {
        if (_cache.TryGetValue(relative, out string? cached))
        {
            return cached;
        }

        string text = await _http.GetStringAsync(new Uri(SpeedrunComClient.BaseUri, relative), token).ConfigureAwait(false);
        _cache[relative] = text;
        return text;
    }

    private static bool IsLookupFailure(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or FormatException;
}
