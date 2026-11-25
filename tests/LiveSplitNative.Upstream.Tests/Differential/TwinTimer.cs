using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LiveSplit.Model.Comparisons;
using N = LiveSplitNative.Core.Model;
using NC = LiveSplitNative.Core.Model.Comparisons;
using NT = LiveSplitNative.Core.Timing;
using U = LiveSplit.Model;
using UC = LiveSplit.Model.Comparisons;

namespace LiveSplitNative.Upstream.Tests.Differential;

/// <summary>
/// One run driven twice: by upstream LiveSplit's TimerModel and by LiveSplitNative's,
/// on the same manual clock. Every command goes to both sides; the snapshots render each
/// side's observable state as comparable lines. Each side also holds every standard comparison
/// generator, which <see cref="RegenerateComparisons"/> runs as Windows TimerForm does after a reset.
/// </summary>
internal sealed class TwinTimer
{
    public const string VariableName = "Deaths";

    public TwinTimer(int segmentCount, TimeSpan offset, bool gameTimeMethod)
    {
        URun = new U.Run(new StandardComparisonGeneratorsFactory());
        NRun = new N.Run();
        for (int i = 0; i < segmentCount; i++)
        {
            string name = "S" + i.ToString(CultureInfo.InvariantCulture);
            U.RunExtensions.AddSegment(URun, name);
            NRun.AddSegment(name);
        }

        URun.Offset = offset;
        NRun.Offset = offset;
        URun.Metadata.RunID = "run-id";
        NRun.Metadata.RunId = "run-id";

        UState = new U.LiveSplitState(
            URun,
            new System.Windows.Forms.Form(),
            new LiveSplit.UI.ShimLayout(),
            new LiveSplit.Options.LayoutSettings(),
            new LiveSplit.Options.ShimSettings());
        NState = new N.LiveSplitState(NRun);

        UState.CurrentComparison = U.Run.PersonalBestComparisonName;
        SetTimingMethod(gameTimeMethod);

        UModel = new U.TimerModel { CurrentState = UState };
        NModel = new N.TimerModel { CurrentState = NState };

        UGenerators = GeneratorComparer.UpstreamGenerators(URun);
        NGenerators = GeneratorComparer.NativeGenerators(NRun);

        UModel.OnStart += (_, _) => UEvents.Add("Start");
        UModel.OnSplit += (_, _) => UEvents.Add("Split");
        UModel.OnSkipSplit += (_, _) => UEvents.Add("Skip");
        UModel.OnUndoSplit += (_, _) => UEvents.Add("Undo");
        UModel.OnPause += (_, _) => UEvents.Add("Pause");
        UModel.OnResume += (_, _) => UEvents.Add("Resume");
        UModel.OnUndoAllPauses += (_, _) => UEvents.Add("UndoAllPauses");
        UModel.OnReset += (_, phase) => UEvents.Add("Reset:" + phase);
        UModel.OnSwitchComparisonNext += (_, _) => UEvents.Add("ComparisonNext");
        UModel.OnSwitchComparisonPrevious += (_, _) => UEvents.Add("ComparisonPrevious");

        NModel.OnStart += (_, _) => NEvents.Add("Start");
        NModel.OnSplit += (_, _) => NEvents.Add("Split");
        NModel.OnSkipSplit += (_, _) => NEvents.Add("Skip");
        NModel.OnUndoSplit += (_, _) => NEvents.Add("Undo");
        NModel.OnPause += (_, _) => NEvents.Add("Pause");
        NModel.OnResume += (_, _) => NEvents.Add("Resume");
        NModel.OnUndoAllPauses += (_, _) => NEvents.Add("UndoAllPauses");
        NModel.OnReset += (_, phase) => NEvents.Add("Reset:" + phase);
        NModel.OnSwitchComparisonNext += (_, _) => NEvents.Add("ComparisonNext");
        NModel.OnSwitchComparisonPrevious += (_, _) => NEvents.Add("ComparisonPrevious");
    }

    public U.Run URun { get; }

    public U.LiveSplitState UState { get; }

    public U.TimerModel UModel { get; }

    public N.Run NRun { get; }

    public N.LiveSplitState NState { get; }

    public N.TimerModel NModel { get; }

    public List<UC.IComparisonGenerator> UGenerators { get; }

    public List<NC.IComparisonGenerator> NGenerators { get; }

    public LiveSplit.Options.ShimSettings UGeneratorSettings { get; } = new();

    public NC.ComparisonGeneratorSettings NGeneratorSettings { get; } = new();

    private string _uRegenerateThrew;

    private string _nRegenerateThrew;

    public List<string> UEvents { get; } = new();

    public List<string> NEvents { get; } = new();

    public void SetTimingMethod(bool gameTime)
    {
        UState.CurrentTimingMethod = gameTime ? U.TimingMethod.GameTime : U.TimingMethod.RealTime;
        NState.CurrentTimingMethod = gameTime ? N.TimingMethod.GameTime : N.TimingMethod.RealTime;
    }

    public void SetVariable(string value)
    {
        URun.Metadata.CustomVariables[VariableName] = new U.CustomVariable(value, isPermanent: true);
        NRun.Metadata.PermanentCustomVariables[VariableName] = value;
    }

    /// <summary>The settings.cfg values the generators read, set the same on both sides.</summary>
    public void ConfigureGenerators(bool simpleSumOfBest, int hcpHistorySize, int hcpNBestRuns)
    {
        UGeneratorSettings.SimpleSumOfBest = simpleSumOfBest;
        UGeneratorSettings.HcpHistorySize = hcpHistorySize;
        UGeneratorSettings.HcpNBestRuns = hcpNBestRuns;
        NGeneratorSettings.SimpleSumOfBest = simpleSumOfBest;
        NGeneratorSettings.HcpHistorySize = hcpHistorySize;
        NGeneratorSettings.HcpNBestRuns = hcpNBestRuns;
    }

    /// <summary>Runs every generator on both sides; an exception on either side shows up in the snapshots.</summary>
    public void RegenerateComparisons()
    {
        _uRegenerateThrew = Try(() =>
        {
            foreach (UC.IComparisonGenerator generator in UGenerators)
            {
                generator.Generate(UGeneratorSettings);
            }
        });
        _nRegenerateThrew = Try(() =>
        {
            foreach (NC.IComparisonGenerator generator in NGenerators)
            {
                generator.Generate(NGeneratorSettings);
            }
        });
    }

    /// <summary>Every generated comparison on every segment; null when both sides agree.</summary>
    public string CompareGenerated(out string category) =>
        GeneratorComparer.Compare(URun, NRun, NGenerators.Select(g => g.Name), out category);

    /// <summary>Runs one command on both sides; returns each side's exception type, or null.</summary>
    public (string Upstream, string Native) Apply(Action upstream, Action native) => (Try(upstream), Try(native));

    public static string Try(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name;
        }
    }

    private static string Read(Func<string> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            return "throws " + ex.GetType().Name;
        }
    }

    public static string T(TimeSpan? time) =>
        time.HasValue ? time.Value.ToString("c", CultureInfo.InvariantCulture) : "null";

    private static string T(U.Time time) => T(time.RealTime) + " | " + T(time.GameTime);

    private static string T(N.SplitTime time) => T(time.RealTime) + " | " + T(time.GameTime);

    private static readonly bool[] Bools = { false, true };

    public List<string> UpstreamSnapshot()
    {
        List<string> lines = UpstreamStateLines(UState);
        lines.Add("Events=" + string.Join(",", UEvents));
        lines.Add("Generators=" + string.Join(",", UGenerators.Select(g => g.Name)));
        lines.Add("RegenerateThrows=" + (_uRegenerateThrew ?? "nothing"));
        return lines;
    }

    /// <summary>Everything observable about one state and its run (also used for reset copies).</summary>
    public static List<string> UpstreamStateLines(U.LiveSplitState s)
    {
        var lines = new List<string>
        {
            "Phase=" + s.CurrentPhase,
            "CurrentSplitIndex=" + s.CurrentSplitIndex,
            "CurrentTime=" + Read(() => T(s.CurrentTime)),
            "PauseTime=" + Read(() => T(s.PauseTime)),
            "TimePausedAt=" + T(s.TimePausedAt),
            "IsGameTimeInitialized=" + s.IsGameTimeInitialized,
            "IsGameTimePaused=" + s.IsGameTimePaused,
            "LoadingTimes=" + T(s.LoadingTimes),
            "GameTimePauseTime=" + T(s.GameTimePauseTime),
            "AttemptCount=" + s.Run.AttemptCount,
            "HasChanged=" + s.Run.HasChanged,
            "RunId=" + (s.Run.Metadata.RunID ?? "null"),
            "Comparisons=" + string.Join(",", s.Run.Comparisons),
            "CurrentComparison=" + s.CurrentComparison,
        };

        for (int i = 0; i < s.Run.Count; i++)
        {
            U.ISegment seg = s.Run[i];
            lines.Add($"Segment[{i}].SplitTime=" + T(seg.SplitTime));
            lines.Add($"Segment[{i}].PersonalBest=" + T(seg.PersonalBestSplitTime));
            lines.Add($"Segment[{i}].BestSegment=" + T(seg.BestSegmentTime));
            lines.Add($"Segment[{i}].History=" + string.Join(
                "; ",
                seg.SegmentHistory.OrderBy(p => p.Key).Select(p => p.Key.ToString(CultureInfo.InvariantCulture) + ": " + T(p.Value))));
            lines.Add($"Segment[{i}].Variables=" + string.Join(
                ",",
                seg.CustomVariableValues.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value)));
        }

        foreach (U.Attempt attempt in s.Run.AttemptHistory)
        {
            lines.Add($"Attempt[{attempt.Index}]=" + T(attempt.Time) + " pause=" + T(attempt.PauseTime)
                + " started=" + attempt.Started.HasValue + " ended=" + attempt.Ended.HasValue);
        }

        foreach (bool simple in Bools)
        {
            foreach (bool useCurrentRun in Bools)
            {
                foreach (U.TimingMethod method in new[] { U.TimingMethod.RealTime, U.TimingMethod.GameTime })
                {
                    lines.Add($"SumOfBest(simple={simple}, currentRun={useCurrentRun}, {method})="
                        + Read(() => T(U.SumOfBest.CalculateSumOfBest(s.Run, simple, useCurrentRun, method))));
                }
            }
        }

        return lines;
    }

    public List<string> NativeSnapshot()
    {
        List<string> lines = NativeStateLines(NState);
        lines.Add("Events=" + string.Join(",", NEvents));
        lines.Add("Generators=" + string.Join(",", NGenerators.Select(g => g.Name)));
        lines.Add("RegenerateThrows=" + (_nRegenerateThrew ?? "nothing"));
        return lines;
    }

    /// <summary>Everything observable about one state and its run (also used for reset copies).</summary>
    public static List<string> NativeStateLines(N.LiveSplitState s)
    {
        var lines = new List<string>
        {
            "Phase=" + s.CurrentPhase,
            "CurrentSplitIndex=" + s.CurrentSplitIndex,
            "CurrentTime=" + Read(() => T(s.CurrentTime)),
            "PauseTime=" + Read(() => T(s.PauseTime)),
            "TimePausedAt=" + T(s.TimePausedAt),
            "IsGameTimeInitialized=" + s.IsGameTimeInitialized,
            "IsGameTimePaused=" + s.IsGameTimePaused,
            "LoadingTimes=" + T(s.LoadingTimes),
            "GameTimePauseTime=" + T(s.GameTimePauseTime),
            "AttemptCount=" + s.Run.AttemptCount,
            "HasChanged=" + s.Run.HasChanged,
            "RunId=" + (s.Run.Metadata.RunId ?? "null"),
            "Comparisons=" + string.Join(",", s.Run.Comparisons),
            "CurrentComparison=" + s.CurrentComparison,
        };

        for (int i = 0; i < s.Run.Count; i++)
        {
            N.Segment seg = s.Run[i];
            lines.Add($"Segment[{i}].SplitTime=" + T(seg.SplitTime));
            lines.Add($"Segment[{i}].PersonalBest=" + T(seg.PersonalBestSplitTime));
            lines.Add($"Segment[{i}].BestSegment=" + T(seg.BestSegmentTime));
            lines.Add($"Segment[{i}].History=" + string.Join(
                "; ",
                seg.SegmentHistory.OrderBy(p => p.Key).Select(p => p.Key.ToString(CultureInfo.InvariantCulture) + ": " + T(p.Value))));
            lines.Add($"Segment[{i}].Variables=" + string.Join(
                ",",
                seg.CustomVariableValues.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value)));
        }

        foreach (N.Attempt attempt in s.Run.AttemptHistory)
        {
            lines.Add($"Attempt[{attempt.Index}]=" + T(attempt.Time) + " pause=" + T(attempt.PauseTime)
                + " started=" + attempt.Started.HasValue + " ended=" + attempt.Ended.HasValue);
        }

        foreach (bool simple in Bools)
        {
            foreach (bool useCurrentRun in Bools)
            {
                foreach (N.TimingMethod method in new[] { N.TimingMethod.RealTime, N.TimingMethod.GameTime })
                {
                    lines.Add($"SumOfBest(simple={simple}, currentRun={useCurrentRun}, {method})="
                        + Read(() => T(N.SumOfBest.CalculateSumOfBest(s.Run, simple, useCurrentRun, method))));
                }
            }
        }

        return lines;
    }

    /// <summary>Phase names must line up for the snapshots to compare; checked once here.</summary>
    public static bool PhasesLineUp() =>
        Enum.GetNames(typeof(U.TimerPhase)).SequenceEqual(Enum.GetNames(typeof(NT.TimerPhase)));
}
