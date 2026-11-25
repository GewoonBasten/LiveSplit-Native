using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Web;

namespace LiveSplitNative.Core.UI;

/// <summary>Port of LiveSplit.TheRun 0.4.5's CollectorComponent (93a11b85's components).</summary>
public static class TheRunLogic
{
    public const string ComponentName = "therun.gg";

    /// <summary>CollectorComponent.ComponentName: "therun.gg", with when the splits were last synced.</summary>
    public static string Name(DateTime? lastSyncTime, DateTime now) =>
        lastSyncTime is { } synced ? $"{ComponentName} (synced {FormatTimeAgo(synced, now)})" : ComponentName;

    /// <summary>FormatTimeAgo: "just now", "12m ago", "3h ago", then the date ("Oct 17 21:14").</summary>
    public static string FormatTimeAgo(DateTime time, DateTime now)
    {
        TimeSpan ago = now - time;
        if (ago.TotalSeconds < 60)
        {
            return "just now";
        }

        if (ago.TotalMinutes < 60)
        {
            return (int)ago.TotalMinutes + "m ago";
        }

        if (ago.TotalHours < 24)
        {
            return (int)ago.TotalHours + "h ago";
        }

        return time.ToString("MMM d HH:mm", CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// buildLiveRunData, as JavaScriptSerializer writes it: properties in their declared order,
    /// times as milliseconds in the current timing method, the timing method as its number,
    /// dates as "\/Date(ms)\/" and TimeSpans as objects of all their properties.
    /// </summary>
    public static string LiveRunJson(
        LiveSplitState state,
        string game,
        string category,
        string uploadKey,
        bool isPaused,
        TimeSpan currentPauseTime,
        bool wasJustResumed)
    {
        Run run = state.Run;
        TimingMethod method = state.CurrentTimingMethod;
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteStartObject("metadata");
            json.WriteString("game", game);
            json.WriteString("category", category);
            json.WriteString("platform", run.Metadata.PlatformName);
            json.WriteString("region", run.Metadata.RegionName);
            json.WriteBoolean("emulator", run.Metadata.UsesEmulator);
            json.WriteStartObject("variables");
            foreach ((string name, string value) in run.Metadata.VariableValueNames)
            {
                json.WriteString(name, value);
            }

            json.WriteEndObject();
            json.WriteEndObject();

            WriteTime(json, "currentTime", state.CurrentTime[method]);
            json.WriteString("currentSplitName", state.CurrentSplit?.Name ?? "");
            json.WriteNumber("currentSplitIndex", state.CurrentSplitIndex);
            json.WriteNumber("timingMethod", (int)method);
            json.WriteNumber("currentDuration", state.CurrentAttemptDuration.TotalMilliseconds);
            WriteDate(json, "startTime", state.AttemptStarted.Time);
            WriteDate(json, "endTime", state.AttemptEnded.Time);
            json.WriteString("uploadKey", uploadKey);
            json.WriteBoolean("isPaused", isPaused);
            json.WriteBoolean("isGameTimePaused", state.IsGameTimePaused);
            WriteTimeSpan(json, "gameTimePauseTime", state.GameTimePauseTime);
            WriteTimeSpan(json, "totalPauseTime", state.PauseTime);
            WriteTimeSpan(json, "currentPauseTime", currentPauseTime);
            json.WriteNumber("timePausedAt", state.TimePausedAt.TotalMilliseconds);
            json.WriteBoolean("wasJustResumed", wasJustResumed);
            json.WriteString("currentComparison", state.CurrentComparison);

            json.WriteStartArray("runData");
            foreach (Segment segment in run.Segments)
            {
                json.WriteStartObject();
                json.WriteString("name", segment.Name);
                WriteTime(json, "splitTime", segment.SplitTime[method]);
                WriteTime(json, "pbSplitTime", segment.PersonalBestSplitTime[method]);
                WriteTime(json, "bestPossible", segment.BestSegmentTime[method]);
                json.WriteStartArray("comparisons");
                foreach ((string name, SplitTime time) in segment.Comparisons)
                {
                    json.WriteStartObject();
                    json.WriteString("name", name);
                    WriteTime(json, "time", time[method]);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>ConvertTime: the time in milliseconds, or null.</summary>
    private static void WriteTime(Utf8JsonWriter json, string name, TimeSpan? time)
    {
        if (time is { } value)
        {
            json.WriteNumber(name, value.TotalMilliseconds);
        }
        else
        {
            json.WriteNull(name);
        }
    }

    /// <summary>JavaScriptSerializer's DateTime: "\/Date(milliseconds since 1970 UTC)\/".</summary>
    private static void WriteDate(Utf8JsonWriter json, string name, DateTime time)
    {
        long epochTicks = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        long milliseconds = (time.ToUniversalTime().Ticks - epochTicks) / 10000;
        json.WritePropertyName(name);
        json.WriteRawValue("\"\\/Date(" + milliseconds.ToString(CultureInfo.InvariantCulture) + ")\\/\"", skipInputValidation: true);
    }

    /// <summary>JavaScriptSerializer's TimeSpan: an object of its public properties (null for none).</summary>
    private static void WriteTimeSpan(Utf8JsonWriter json, string name, TimeSpan? value)
    {
        if (value is not { } time)
        {
            json.WriteNull(name);
            return;
        }

        json.WriteStartObject(name);
        json.WriteNumber("Ticks", time.Ticks);
        json.WriteNumber("Days", time.Days);
        json.WriteNumber("Hours", time.Hours);
        json.WriteNumber("Milliseconds", time.Milliseconds);
        json.WriteNumber("Minutes", time.Minutes);
        json.WriteNumber("Seconds", time.Seconds);
        json.WriteNumber("TotalDays", time.TotalDays);
        json.WriteNumber("TotalHours", time.TotalHours);
        json.WriteNumber("TotalMilliseconds", time.TotalMilliseconds);
        json.WriteNumber("TotalMinutes", time.TotalMinutes);
        json.WriteNumber("TotalSeconds", time.TotalSeconds);
        json.WriteEndObject();
    }

    /// <summary>XmlRunAsString: the splits as saved, without icons, and without the layout path unless asked.</summary>
    public static string SplitsForUpload(Run run, bool keepLayoutPath)
    {
        using var stream = new MemoryStream();
        XmlRunSerializer.Save(run, stream);
        stream.Position = 0;
        var document = new XmlDocument { PreserveWhitespace = true };
        document.Load(stream);

        XmlElement root = document.DocumentElement!;
        if (root["GameIcon"] is { } gameIcon)
        {
            gameIcon.InnerText = "";
        }

        if (!keepLayoutPath && root["LayoutPath"] is { } layoutPath)
        {
            layoutPath.InnerText = "";
        }

        if (root["Segments"] is { } segments)
        {
            foreach (XmlElement segment in segments.GetElementsByTagName("Segment"))
            {
                if (segment["Icon"] is { } icon)
                {
                    icon.InnerText = "";
                }
            }
        }

        return document.OuterXml;
    }
}

/// <summary>
/// One therun.gg component's runtime part (CollectorComponent's fields and handlers), kept on its
/// LayoutComponent.RuntimeState. The handlers read the timer when called, then send in the
/// background; an earlier live post still sending is cancelled by the next.
/// </summary>
public sealed class TheRunCollector(TheRunClient client)
{
    private CancellationTokenSource? _liveCts;
    private string _game = "";
    private string _category = "";
    private bool _timerPaused;
    private bool _wasJustResumed;
    private TimeSpan _currentPausedTime = TimeSpan.Zero;
    private TimeSpan _timePausedBeforeResume = TimeSpan.Zero;

    public DateTime? LastSyncTime { get; private set; }

    private void SetGameAndCategory(LiveSplitState state)
    {
        _game = state.Run.GameName;
        _category = state.Run.CategoryName;
    }

    private bool AreSplitsValid(string uploadKey) => _game != "" && _category != "" && uploadKey.Length == 36;

    /// <summary>HandleSplit (start, split, skip, undo, undo all pauses): the live run; the splits when the run is over.</summary>
    public async Task HandleSplitAsync(LiveSplitState state, TheRunComponentSettings settings, string uploadKey)
    {
        SetGameAndCategory(state);
        if (!AreSplitsValid(uploadKey) || !settings.IsLiveTrackingEnabled)
        {
            return;
        }

        try
        {
            await UpdateSplitsStateAsync(state, uploadKey);
            if (state.CurrentSplitIndex == state.Run.Count)
            {
                await UploadSplitsAsync(state, settings, uploadKey);
            }
        }
        catch (Exception)
        {
            // Windows ignores any failure here (catch { }).
        }

        _wasJustResumed = false;
    }

    public Task HandlePauseAsync(LiveSplitState state, TheRunComponentSettings settings, string uploadKey)
    {
        _timerPaused = true;
        return HandleSplitAsync(state, settings, uploadKey);
    }

    public Task HandleResumeAsync(LiveSplitState state, TheRunComponentSettings settings, string uploadKey)
    {
        TimeSpan pauseTime = state.PauseTime ?? TimeSpan.Zero;
        _timePausedBeforeResume = pauseTime - _currentPausedTime;
        _currentPausedTime = pauseTime;
        _timerPaused = false;
        _wasJustResumed = true;
        return HandleSplitAsync(state, settings, uploadKey);
    }

    /// <summary>HandleReset: the live run (now reset), then the splits when Sync on Reset is on.</summary>
    public async Task HandleResetAsync(LiveSplitState state, TheRunComponentSettings settings, string uploadKey)
    {
        SetGameAndCategory(state);
        if (!AreSplitsValid(uploadKey))
        {
            return;
        }

        try
        {
            if (settings.IsLiveTrackingEnabled)
            {
                await UpdateSplitsStateAsync(state, uploadKey);
            }

            if (settings.IsUploadOnResetEnabled)
            {
                await UploadSplitsAsync(state, settings, uploadKey);
            }
        }
        catch (Exception)
        {
            // catch { }
        }
    }

    /// <summary>
    /// Dispose (the component removed, or LiveSplit closing): with Stats Sync on and Sync on Reset
    /// off, the splits are uploaded, waiting at most 5 seconds.
    /// </summary>
    public void Dispose(LiveSplitState state, TheRunComponentSettings settings, string uploadKey)
    {
        _liveCts?.Cancel();
        try
        {
            SetGameAndCategory(state);
            if (AreSplitsValid(uploadKey) && settings.IsStatsUploadingEnabled && !settings.IsUploadOnResetEnabled)
            {
                string lss = TheRunLogic.SplitsForUpload(state.Run, settings.IsLayoutPathUploadEnabled);
                string game = _game, category = _category;
                Task upload = Task.Run(async () =>
                    await client.PutSplitsAsync(await client.GetUploadUrlAsync(game, category, uploadKey), lss));
                if (upload.Wait(TimeSpan.FromSeconds(5)))
                {
                    LastSyncTime = DateTime.Now;
                }
            }
        }
        catch (Exception)
        {
            // catch { }
        }
    }

    private async Task UpdateSplitsStateAsync(LiveSplitState state, string uploadKey)
    {
        _liveCts?.Cancel();
        _liveCts?.Dispose();
        _liveCts = new CancellationTokenSource();
        CancellationToken token = _liveCts.Token;
        string json = TheRunLogic.LiveRunJson(
            state, _game, _category, uploadKey, _timerPaused, _timePausedBeforeResume, _wasJustResumed);
        await client.PostLiveAsync(json, token);
    }

    private async Task UploadSplitsAsync(LiveSplitState state, TheRunComponentSettings settings, string uploadKey)
    {
        if (!settings.IsStatsUploadingEnabled)
        {
            return;
        }

        // The splits are read once the upload URL is there, as XmlRunAsString runs after it.
        string url = await client.GetUploadUrlAsync(_game, _category, uploadKey);
        string lss = TheRunLogic.SplitsForUpload(state.Run, settings.IsLayoutPathUploadEnabled);
        await client.PutSplitsAsync(url, lss);
        LastSyncTime = DateTime.Now;
    }
}
