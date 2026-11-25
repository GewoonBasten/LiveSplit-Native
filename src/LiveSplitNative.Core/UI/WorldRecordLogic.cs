using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Timing;
using LiveSplitNative.Core.Web;
using static LiveSplitNative.Core.Localization.UiText;

namespace LiveSplitNative.Core.UI;

/// <summary>What a World Record lookup found: the game, the category and the record (all ties).</summary>
public sealed record WorldRecordResult(SrcGame Game, SrcCategory Category, IReadOnlyList<SrcRecord> Ties);

/// <summary>What the World Record component shows; Centered is its Centered Text look (one centered line).</summary>
public sealed record WorldRecordView(InfoComponentView Info, bool Centered);

/// <summary>
/// The run and settings a World Record lookup needs (taken on the UI thread when it starts):
/// the speedrun.com names from the splits and the leaderboard filters.
/// </summary>
public sealed record WorldRecordQuery(
    string GameName,
    string CategoryName,
    string? PlatformName,
    string? RegionName,
    bool? UsesEmulator,
    IReadOnlyDictionary<string, string> VariableValueNames,
    bool FilterVariables,
    bool FilterSubcategories,
    SrcTimingMethod? Timing);

/// <summary>LiveSplit.WorldRecord WorldRecordComponent (93a11b85): RefreshWorldRecord and ShowWorldRecord.</summary>
public static class WorldRecordLogic
{
    /// <summary>ComponentName: T("World Record").</summary>
    public static string ComponentName => T("World Record");

    /// <summary>GetTimingMethodOverride: the leaderboard timing the settings ask for (none: the leaderboard's own).</summary>
    public static SrcTimingMethod? TimingOverride(string setting) => setting switch
    {
        "Real Time" => SrcTimingMethod.RealTime,
        "Real Time Without Loads" => SrcTimingMethod.RealTimeWithoutLoads,
        "Game Time" => SrcTimingMethod.GameTime,
        _ => null,
    };

    /// <summary>SpeedrunCom.ToLiveSplitTimingMethod: without loads is Game Time in LiveSplit.</summary>
    public static TimingMethod ToLiveSplit(SrcTimingMethod method) =>
        method == SrcTimingMethod.RealTime ? TimingMethod.RealTime : TimingMethod.GameTime;

    public static WorldRecordQuery Query(Run run, WorldRecordComponentSettings settings) => new(
        run.GameName,
        run.CategoryName,
        settings.FilterPlatform ? run.Metadata.PlatformName : null,
        settings.FilterRegion ? run.Metadata.RegionName : null,
        settings.FilterPlatform ? run.Metadata.UsesEmulator : null,
        new Dictionary<string, string>(run.Metadata.VariableValueNames, StringComparer.Ordinal),
        settings.FilterVariables,
        settings.FilterSubcategories,
        TimingOverride(settings.TimingMethod));

    /// <summary>
    /// WorldRecordComponent.Update's cache: a lookup starts again when one of these changes
    /// (game, category, the filters and what they filter by, timing method, precision).
    /// </summary>
    public static string LookupKey(Run run, WorldRecordComponentSettings settings) => string.Join(
        "\u001f",
        run.GameName,
        run.CategoryName,
        settings.FilterPlatform ? run.Metadata.PlatformName : "-",
        settings.FilterRegion ? run.Metadata.RegionName : "-",
        settings.FilterPlatform ? run.Metadata.UsesEmulator.ToString() : "-",
        settings.FilterVariables || settings.FilterSubcategories ? string.Join(",", run.Metadata.VariableValueNames.Values) : "-",
        settings.FilterVariables.ToString(),
        settings.FilterSubcategories.ToString(),
        settings.TimingMethod,
        settings.PrecisionType.ToString());

    /// <summary>
    /// RefreshWorldRecord: the game by its exact name, its per-game category by name, then the
    /// leaderboard's top (with ties) for the filters. Null when the game or category is not on
    /// speedrun.com.
    /// </summary>
    public static async Task<WorldRecordResult?> FetchAsync(SpeedrunComClient client, WorldRecordQuery query, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(query.GameName) || string.IsNullOrEmpty(query.CategoryName))
        {
            return null;
        }

        SrcGame? game = await client.FindGameAsync(query.GameName, token).ConfigureAwait(false);
        if (game is null)
        {
            return null;
        }

        SrcCategory? category = (await client.GetCategoriesAsync(game.Id, token).ConfigureAwait(false))
            .FirstOrDefault(c => c.IsPerGame && c.Name == query.CategoryName);
        if (category is null)
        {
            return null;
        }

        var variableFilter = new List<(string, string)>();
        if (query.FilterVariables || query.FilterSubcategories)
        {
            // RunMetadata.VariableValues: the full-game variables of this category, by name, and
            // the value with the saved label.
            foreach (SrcVariable variable in (await client.GetVariablesAsync(game.Id, token).ConfigureAwait(false))
                .Where(v => v.IsFullGame && (v.CategoryId is null || v.CategoryId == category.Id)))
            {
                if (!query.VariableValueNames.TryGetValue(variable.Name, out string? label)
                    || (variable.IsSubcategory ? !query.FilterSubcategories : !query.FilterVariables))
                {
                    continue;
                }

                string? valueId = variable.Values.FirstOrDefault(v => v.Value == label).Key;
                if (valueId is not null)
                {
                    variableFilter.Add((variable.Id, valueId));
                }
            }
        }

        string? platformId = query.PlatformName is { } platform ? game.Platforms.FirstOrDefault(p => p.Name == platform)?.Id : null;
        string? regionId = query.RegionName is { } region ? game.Regions.FirstOrDefault(r => r.Name == region)?.Id : null;
        IReadOnlyList<SrcRecord> records = await client.GetLeaderboardAsync(
            game.Id,
            category.Id,
            1,
            platformId,
            regionId,
            query.UsesEmulator,
            query.Timing,
            variableFilter,
            token).ConfigureAwait(false);
        return new WorldRecordResult(game, category, records);
    }

    /// <summary>ShowWorldRecord and PrepareDraw.</summary>
    public static WorldRecordView View(
        LiveSplitState state,
        WorldRecordComponentSettings settings,
        LayoutSettings layout,
        WorldRecordResult? result,
        bool isLoading,
        bool horizontal)
    {
        bool centered = settings.CenteredText && !settings.Display2Rows && !horizontal;
        ArgbColor nameColor = settings.OverrideTextColor ? settings.TextColor : layout.TextColor;
        ArgbColor valueColor = settings.OverrideTimeColor ? settings.TimeColor : layout.TextColor;
        string name = ComponentName;
        IReadOnlyList<string> alternates = ["WR"];
        string value;
        if (result is { Ties.Count: > 0 })
        {
            SrcRecord record = result.Ties[0];
            SrcTimingMethod? timingOverride = TimingOverride(settings.TimingMethod);
            TimeSpan? wrTime = timingOverride switch
            {
                SrcTimingMethod.RealTime => record.Times.RealTime,
                SrcTimingMethod.RealTimeWithoutLoads => record.Times.RealTimeWithoutLoads,
                SrcTimingMethod.GameTime => record.Times.GameTime,
                _ => record.Times.Primary,
            };
            TimingMethod method = ToLiveSplit(timingOverride ?? result.Game.DefaultTimingMethod);
            bool milliseconds = CheckPrecisionMillis(settings.PrecisionType, wrTime);
            bool fromLeaderboard = settings.PrecisionType == WorldRecordPrecisionType.FromLeaderboard;

            // AutomaticPrecisionTimeFormatter (milliseconds, trailing zeros dropped with From
            // Leaderboard) and RegularTimeFormatter for the PB at the leaderboard's precision.
            var wrFormatter = new GeneralTimeFormatter
            {
                NullFormat = NullFormat.ZeroWithAccuracy,
                DigitsFormat = DigitsFormat.SingleDigitMinutes,
                AutomaticPrecision = fromLeaderboard,
                Accuracy = fromLeaderboard || milliseconds ? TimeAccuracy.Milliseconds : TimeAccuracy.Seconds,
            };
            var pbFormatter = new GeneralTimeFormatter
            {
                NullFormat = NullFormat.ZeroWithAccuracy,
                DigitsFormat = DigitsFormat.SingleDigitMinutes,
                AutomaticPrecision = fromLeaderboard,
                Accuracy = milliseconds ? TimeAccuracy.Milliseconds : TimeAccuracy.Seconds,
            };

            string formatted = wrFormatter.Format(wrTime);
            string runners = string.Join(", ", result.Ties.Select(t => string.Join(" & ", t.Players)));
            int tieCount = result.Ties.Count;
            TimeSpan? pbTime = PersonalBest(state, method);
            if (IsPbTimeLower(settings.PrecisionType, pbTime, wrTime))
            {
                formatted = pbFormatter.Format(pbTime);
                runners = result.Category.Players > 1 ? "us" : "me";
                tieCount = 1;
            }

            if (centered)
            {
                var texts = new List<string>
                {
                    string.Format(T("World Record is {0} by {1}"), formatted, runners),
                    string.Format(T("World Record: {0} by {1}"), formatted, runners),
                    string.Format(T("WR: {0} by {1}"), formatted, runners),
                    string.Format(T("WR is {0} by {1}"), formatted, runners),
                };
                if (tieCount > 1)
                {
                    texts.Add(string.Format(T("World Record is {0} ({1}-way tie)"), formatted, tieCount));
                    texts.Add(string.Format(T("World Record: {0} ({1}-way tie)"), formatted, tieCount));
                    texts.Add(string.Format(T("WR: {0} ({1}-way tie)"), formatted, tieCount));
                    texts.Add(string.Format(T("WR is {0} ({1}-way tie)"), formatted, tieCount));
                }

                return new WorldRecordView(new InfoComponentView(texts[0], texts, "", nameColor, valueColor, ValueInTextFont: true), true);
            }

            if (tieCount <= 1 && settings.SeparateTime)
            {
                // LiveSplitNative's Separate Time (Basten 17 Oct, as his LiveSplit One text):
                // "World Record by inkstar" on the left, only the time on the right.
                name = $"{ComponentName} by {runners}";
                alternates = [$"WR by {runners}", ComponentName, "WR"];
                value = formatted;
            }
            else
            {
                value = tieCount > 1
                    ? string.Format(T("{0} ({1}-way tie)"), formatted, tieCount)
                    : string.Format(T("{0} by {1}"), formatted, runners);
            }
        }
        else if (isLoading)
        {
            if (centered)
            {
                return new WorldRecordView(
                    new InfoComponentView(T("Loading World Record..."), [T("Loading WR...")], "", nameColor, valueColor, ValueInTextFont: true),
                    true);
            }

            value = T("Loading...");
        }
        else
        {
            if (centered)
            {
                return new WorldRecordView(
                    new InfoComponentView(T("Unknown World Record"), [T("Unknown WR")], "", nameColor, valueColor, ValueInTextFont: true),
                    true);
            }

            value = TimeFormatConstants.Dash;
        }

        // Separate Time: the value in the Times font, as the times above it.
        return new WorldRecordView(new InfoComponentView(name, alternates, value, nameColor, valueColor, ValueInTextFont: !settings.SeparateTime), false);
    }

    /// <summary>CheckPrecisionMillis: From Leaderboard uses milliseconds when the record has them.</summary>
    public static bool CheckPrecisionMillis(WorldRecordPrecisionType precision, TimeSpan? recordTime) =>
        precision == WorldRecordPrecisionType.FromLeaderboard
            ? recordTime is not { } time || time.Milliseconds != 0
            : precision == WorldRecordPrecisionType.Milliseconds;

    /// <summary>IsPBTimeLower: a PB at or under the record (at the record's precision) is shown instead.</summary>
    public static bool IsPbTimeLower(WorldRecordPrecisionType precision, TimeSpan? pbTime, TimeSpan? wrTime)
    {
        if (pbTime is not { } pb || wrTime is not { } wr)
        {
            return false;
        }

        return CheckPrecisionMillis(precision, wrTime)
            ? (int)pb.TotalMilliseconds <= (int)wr.TotalMilliseconds
            : (int)pb.TotalSeconds <= (int)wr.TotalSeconds;
    }

    /// <summary>GetPBTime: the PB's final time, or this run's when it just finished faster.</summary>
    public static TimeSpan? PersonalBest(LiveSplitState state, TimingMethod method)
    {
        if (state.Run.Count == 0)
        {
            return null;
        }

        Segment last = state.Run[^1];
        TimeSpan? pb = last.PersonalBestSplitTime[method];
        TimeSpan? split = last.SplitTime[method];
        return state.CurrentPhase == TimerPhase.Ended && split < pb ? split : pb;
    }
}

/// <summary>
/// The World Record lookups of one component (kept on its LayoutComponent while the layout is
/// open): a new lookup when the game, category or filters change, and again every 5 minutes;
/// the previous result stays shown while a refresh runs.
/// </summary>
public sealed class WorldRecordFetcher(SpeedrunComClient client)
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly object _lock = new();
    private string? _key;
    private TimeStamp? _lastUpdate;
    private int _generation;
    private WorldRecordResult? _result;
    private bool _isLoading;

    public WorldRecordResult? Result
    {
        get
        {
            lock (_lock)
            {
                return _result;
            }
        }
    }

    public bool IsLoading
    {
        get
        {
            lock (_lock)
            {
                return _isLoading;
            }
        }
    }

    /// <summary>The fetcher kept on this component (a new one for a component loaded anew).</summary>
    public static WorldRecordFetcher For(LayoutComponent component, SpeedrunComClient client)
    {
        if (component.RuntimeState is not WorldRecordFetcher fetcher)
        {
            fetcher = new WorldRecordFetcher(client);
            component.RuntimeState = fetcher;
        }

        return fetcher;
    }

    /// <summary>WorldRecordComponent.Update's lookup part. Returns the lookup it started, if any (for tests).</summary>
    public Task? Update(Run run, WorldRecordComponentSettings settings)
    {
        string key = WorldRecordLogic.LookupKey(run, settings);
        lock (_lock)
        {
            if (key != _key)
            {
                _key = key;
                _result = null;
                _isLoading = true;
            }
            else if (_lastUpdate is null || TimeStamp.Now - _lastUpdate < RefreshInterval)
            {
                return null;
            }

            _lastUpdate = TimeStamp.Now;
            int generation = ++_generation;
            WorldRecordQuery query = WorldRecordLogic.Query(run, settings);
            return Task.Run(async () =>
            {
                WorldRecordResult? result = null;
                try
                {
                    result = await WorldRecordLogic.FetchAsync(client, query).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // RefreshWorldRecord logs the error and shows no record.
                }

                lock (_lock)
                {
                    if (generation == _generation)
                    {
                        _result = result;
                        _isLoading = false;
                    }
                }
            });
        }
    }
}
