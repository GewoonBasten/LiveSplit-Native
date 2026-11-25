using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LiveSplitNative.Core.Timing;
using Xunit;

namespace LiveSplitNative.Upstream.Tests.Differential;

/// <summary>
/// Random command sequences sent to upstream LiveSplit's timer and to ours on one manual
/// clock. After every command the full observable state must be identical: phase, split
/// index, current and pause time, game time state, every segment's split, PB, gold, history
/// and custom variables, the attempt history, the fired events and Sum of Best in all eight
/// variants. After every reset both sides regenerate all nine standard comparisons (as Windows
/// TimerForm does), and every generated split must match too; see <see cref="GeneratorComparer"/>
/// for the two that allow a .NET Framework rounding tolerance. Seeds are fixed, so a failure
/// always reproduces.
/// </summary>
public class TimerDifferentialTests
{
    private const int Seeds = 400;
    private const int StepsPerSeed = 300;

    private enum Op
    {
        Start,
        Split,
        SkipSplit,
        UndoSplit,
        Pause,
        UndoAllPauses,
        Reset,
        ResetWithoutSaving,
        ResetAndSetAttemptAsPb,
        Advance,
        AdvanceRound,
        AdvanceZeroOrOneTick,
        InitializeGameTime,
        UninitializeGameTime,
        PauseGameTime,
        ResumeGameTime,
        SetLoadingTimes,
        SetVariable,
        SwitchTimingMethod,
        SwitchComparisonNext,
        SwitchComparisonPrevious,
    }

    private static readonly (Op Op, int Weight)[] Weights =
    {
        (Op.Start, 6),
        (Op.Split, 20),
        (Op.SkipSplit, 3),
        (Op.UndoSplit, 4),
        (Op.Pause, 5),
        (Op.UndoAllPauses, 2),
        (Op.Reset, 4),
        (Op.ResetWithoutSaving, 1),
        (Op.ResetAndSetAttemptAsPb, 1),
        (Op.Advance, 18),
        (Op.AdvanceRound, 12),
        (Op.AdvanceZeroOrOneTick, 3),
        (Op.InitializeGameTime, 3),
        (Op.UninitializeGameTime, 1),
        (Op.PauseGameTime, 3),
        (Op.ResumeGameTime, 3),
        (Op.SetLoadingTimes, 2),
        (Op.SetVariable, 2),
        (Op.SwitchTimingMethod, 1),
        (Op.SwitchComparisonNext, 1),
        (Op.SwitchComparisonPrevious, 1),
    };

    private static readonly TimeSpan[] Offsets =
    {
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(-1500),
        TimeSpan.FromMilliseconds(-250),
        TimeSpan.FromSeconds(2),
    };

    [Fact]
    public void Phase_Names_Line_Up()
    {
        Assert.True(TwinTimer.PhasesLineUp());
    }

    /// <summary>
    /// Windows TimerForm.SaveSplits saves a reset copy of the state (state.Clone(), then Reset on
    /// a fresh TimerModel) so the attempt lands in the file while the timer keeps running. After
    /// every command both sides take such a copy: the copies must match, and the live timers must
    /// still match each other (so a copy never reaches back into the live run).
    /// </summary>
    [Fact]
    public void Reset_Copies_For_Saving_Match_Upstream()
    {
        for (int seed = 0; seed < ResetCopySeeds; seed++)
        {
            string divergence = RunSeed(seed, out string category, checkResetCopies: true);
            Assert.True(divergence is null, $"seed {seed}, {category}:\n{divergence}");
        }
    }

    private const int ResetCopySeeds = 150;

    [Fact]
    public void Random_Command_Sequences_Match_Upstream()
    {
        var divergences = new Dictionary<string, string>(StringComparer.Ordinal);
        int divergingSeeds = 0;

        for (int seed = 0; seed < Seeds; seed++)
        {
            string divergence = RunSeed(seed, out string category);
            if (divergence is null)
            {
                continue;
            }

            divergingSeeds++;
            if (!divergences.ContainsKey(category))
            {
                divergences[category] = divergence;
            }
        }

        if (divergences.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine(CultureInfo.InvariantCulture, $"{divergingSeeds} of {Seeds} seeds diverge from upstream, in {divergences.Count} distinct ways. First example of each:");
        foreach (KeyValuePair<string, string> pair in divergences.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            message.AppendLine();
            message.AppendLine("=== " + pair.Key);
            message.AppendLine(pair.Value);
        }

        Assert.Fail(message.ToString());
    }

    /// <summary>Returns null when the seed matches upstream throughout, else a readable report.</summary>
    private static string RunSeed(int seed, out string category, bool checkResetCopies = false)
    {
        category = null;
        var rng = new Random(seed);
        using ManualTimeSourceScope clock = ManualTimeSource.Install(TimeSpan.FromHours(1 + rng.Next(100)));

        int segmentCount = 1 + rng.Next(6);
        TimeSpan offset = Offsets[rng.Next(Offsets.Length)];
        bool gameTimeMethod = rng.Next(4) == 0;
        var twin = new TwinTimer(segmentCount, offset, gameTimeMethod);

        // Generator settings come from their own generator, so the command stream per seed stays
        // the same as before generators were added.
        var settingsRng = new Random(1_000_000 + seed);
        bool simpleSumOfBest = settingsRng.Next(2) == 0;
        int hcpHistorySize = 1 + settingsRng.Next(25);
        int hcpNBestRuns = 1 + settingsRng.Next(10);
        twin.ConfigureGenerators(simpleSumOfBest, hcpHistorySize, hcpNBestRuns);
        twin.RegenerateComparisons();

        var trace = new List<string>
        {
            $"seed {seed}: {segmentCount} segments, offset {TwinTimer.T(offset)}, timing method {(gameTimeMethod ? "GameTime" : "RealTime")}, "
                + $"SimpleSumOfBest {simpleSumOfBest}, HcpHistorySize {hcpHistorySize}, HcpNBestRuns {hcpNBestRuns}",
        };

        string firstCompare = Compare(twin, trace, null, null, out category);
        if (firstCompare is not null)
        {
            return firstCompare;
        }

        int totalWeight = Weights.Sum(w => w.Weight);
        bool gameTime = gameTimeMethod;
        for (int step = 0; step < StepsPerSeed; step++)
        {
            int pick = rng.Next(totalWeight);
            Op op = Weights.First(w => (pick -= w.Weight) < 0).Op;
            string label = op.ToString();
            (string Upstream, string Native) thrown;

            switch (op)
            {
                case Op.Start:
                    thrown = twin.Apply(twin.UModel.Start, twin.NModel.Start);
                    break;
                case Op.Split:
                    thrown = twin.Apply(twin.UModel.Split, twin.NModel.Split);
                    break;
                case Op.SkipSplit:
                    thrown = twin.Apply(twin.UModel.SkipSplit, twin.NModel.SkipSplit);
                    break;
                case Op.UndoSplit:
                    thrown = twin.Apply(twin.UModel.UndoSplit, twin.NModel.UndoSplit);
                    break;
                case Op.Pause:
                    thrown = twin.Apply(twin.UModel.Pause, twin.NModel.Pause);
                    break;
                case Op.UndoAllPauses:
                    thrown = twin.Apply(twin.UModel.UndoAllPauses, twin.NModel.UndoAllPauses);
                    break;
                case Op.Reset:
                    thrown = twin.Apply(() => twin.UModel.Reset(true), () => twin.NModel.Reset(true));
                    twin.RegenerateComparisons();
                    break;
                case Op.ResetWithoutSaving:
                    thrown = twin.Apply(() => twin.UModel.Reset(false), () => twin.NModel.Reset(false));
                    twin.RegenerateComparisons();
                    break;
                case Op.ResetAndSetAttemptAsPb:
                    thrown = twin.Apply(twin.UModel.ResetAndSetAttemptAsPB, twin.NModel.ResetAndSetAttemptAsPb);
                    twin.RegenerateComparisons();
                    break;
                case Op.Advance:
                {
                    var by = TimeSpan.FromTicks(1 + rng.Next(80_000_000));
                    label = "Advance " + TwinTimer.T(by);
                    clock.Advance(by);
                    thrown = (null, null);
                    break;
                }

                case Op.AdvanceRound:
                {
                    // Whole half-seconds make equal segment times across attempts, which is
                    // what the duplicate-history cleanup in FixSplits acts on.
                    var by = TimeSpan.FromMilliseconds(500 * (1 + rng.Next(12)));
                    label = "Advance " + TwinTimer.T(by);
                    clock.Advance(by);
                    thrown = (null, null);
                    break;
                }

                case Op.AdvanceZeroOrOneTick:
                {
                    var by = TimeSpan.FromTicks(rng.Next(2));
                    label = "Advance " + TwinTimer.T(by);
                    clock.Advance(by);
                    thrown = (null, null);
                    break;
                }

                case Op.InitializeGameTime:
                    thrown = twin.Apply(
                        () => twin.UState.IsGameTimeInitialized = true,
                        () => twin.NState.IsGameTimeInitialized = true);
                    break;
                case Op.UninitializeGameTime:
                    thrown = twin.Apply(
                        () => twin.UState.IsGameTimeInitialized = false,
                        () => twin.NState.IsGameTimeInitialized = false);
                    break;
                case Op.PauseGameTime:
                    thrown = twin.Apply(
                        () => twin.UState.IsGameTimePaused = true,
                        () => twin.NState.IsGameTimePaused = true);
                    break;
                case Op.ResumeGameTime:
                    thrown = twin.Apply(
                        () => twin.UState.IsGameTimePaused = false,
                        () => twin.NState.IsGameTimePaused = false);
                    break;
                case Op.SetLoadingTimes:
                {
                    var loading = TimeSpan.FromMilliseconds(rng.Next(6000));
                    label = "LoadingTimes = " + TwinTimer.T(loading);
                    thrown = twin.Apply(
                        () => twin.UState.LoadingTimes = loading,
                        () => twin.NState.LoadingTimes = loading);
                    break;
                }

                case Op.SetVariable:
                {
                    string value = rng.Next(10).ToString(CultureInfo.InvariantCulture);
                    label = $"{TwinTimer.VariableName} = {value}";
                    thrown = twin.Apply(() => twin.SetVariable(value), () => { });
                    break;
                }

                case Op.SwitchTimingMethod:
                    gameTime = !gameTime;
                    label = "TimingMethod = " + (gameTime ? "GameTime" : "RealTime");
                    twin.SetTimingMethod(gameTime);
                    thrown = (null, null);
                    break;
                case Op.SwitchComparisonNext:
                    thrown = twin.Apply(twin.UModel.SwitchComparisonNext, twin.NModel.SwitchComparisonNext);
                    break;
                case Op.SwitchComparisonPrevious:
                    thrown = twin.Apply(twin.UModel.SwitchComparisonPrevious, twin.NModel.SwitchComparisonPrevious);
                    break;
                default:
                    throw new InvalidOperationException(op.ToString());
            }

            trace.Add($"  {step,3}: {label}");
            string report = Compare(twin, trace, thrown.Upstream, thrown.Native, out category);
            if (report is null && checkResetCopies)
            {
                report = CompareResetCopies(twin, trace, out category);
            }

            if (report is not null)
            {
                category = op + " -> " + category;
                return report;
            }
        }

        return null;
    }

    private static readonly Regex Indexes = new(@"\[-?\d+\]", RegexOptions.Compiled);

    private static string CompareResetCopies(TwinTimer twin, List<string> trace, out string category)
    {
        List<string> nativeBefore = twin.NativeSnapshot();
        var upstreamCopy = (LiveSplit.Model.LiveSplitState)twin.UState.Clone();
        new LiveSplit.Model.TimerModel { CurrentState = upstreamCopy }.Reset();
        LiveSplitNative.Core.Model.LiveSplitState nativeCopy = twin.NState.Clone();
        new LiveSplitNative.Core.Model.TimerModel { CurrentState = nativeCopy }.Reset();

        List<string> upstream = TwinTimer.UpstreamStateLines(upstreamCopy);
        List<string> native = TwinTimer.NativeStateLines(nativeCopy);
        upstream.Insert(0, "ResetCopy");
        native.Insert(0, "ResetCopy");
        string report = CompareLines(upstream, native, trace, out category);
        if (report is not null)
        {
            category = "reset copy " + category;
            return report;
        }

        nativeBefore.Insert(0, "LiveBeforeCopy");
        List<string> nativeAfter = twin.NativeSnapshot();
        nativeAfter.Insert(0, "LiveBeforeCopy");
        report = CompareLines(nativeBefore, nativeAfter, trace, out category);
        if (report is not null)
        {
            category = "copy changed the live timer " + category;
            return report.Replace("upstream: ", "before:   ").Replace("native:   ", "after:    ");
        }

        report = Compare(twin, trace, null, null, out category);
        if (report is not null)
        {
            category = "live after copy " + category;
        }

        return report;
    }

    private static string Compare(TwinTimer twin, List<string> trace, string upstreamThrew, string nativeThrew, out string category)
    {
        List<string> upstream = twin.UpstreamSnapshot();
        List<string> native = twin.NativeSnapshot();
        upstream.Insert(0, "Throws=" + (upstreamThrew ?? "nothing"));
        native.Insert(0, "Throws=" + (nativeThrew ?? "nothing"));
        string report = CompareLines(upstream, native, trace, out category);
        if (report is not null)
        {
            return report;
        }

        string generated = twin.CompareGenerated(out category);
        if (generated is not null)
        {
            var generatedReport = new StringBuilder();
            generatedReport.AppendLine(generated);
            generatedReport.AppendLine("commands (last 25):");
            foreach (string line in trace.Take(1).Concat(trace.Skip(Math.Max(1, trace.Count - 25))))
            {
                generatedReport.AppendLine(line);
            }

            return generatedReport.ToString();
        }

        category = null;
        return null;
    }

    private static string CompareLines(List<string> upstream, List<string> native, List<string> trace, out string category)
    {
        int count = Math.Max(upstream.Count, native.Count);
        for (int i = 0; i < count; i++)
        {
            string u = i < upstream.Count ? upstream[i] : "(missing)";
            string n = i < native.Count ? native[i] : "(missing)";
            if (u == n)
            {
                continue;
            }

            string key = u.Contains('=') ? u[..u.IndexOf('=')] : u;
            category = Indexes.Replace(key, "[#]");
            var report = new StringBuilder();
            report.AppendLine("upstream: " + u);
            report.AppendLine("native:   " + n);
            report.AppendLine("commands (last 25):");
            foreach (string line in trace.Take(1).Concat(trace.Skip(Math.Max(1, trace.Count - 25))))
            {
                report.AppendLine(line);
            }

            return report.ToString();
        }

        category = null;
        return null;
    }
}
