namespace LiveSplitNative.Core.Model.Comparisons;

/// <summary>
/// The comparison bookkeeping Windows does in TimerForm (LiveSplit.View @ 93a11b85), kept in Core
/// so the app stays thin and tests can run it: switching comparisons, applying Choose Active
/// Comparisons, and carrying the generators over when a new run is set.
/// </summary>
public static class ComparisonSwitching
{
    /// <summary>The setting value every upstream component uses for "follow the timer's comparison".</summary>
    public const string CurrentComparisonSetting = "Current Comparison";

    /// <summary>
    /// The comparison a component setting means, as every upstream component resolves it:
    /// "Current Comparison" (or nothing) is the timer's comparison, and a comparison the run does
    /// not have, such as a generator switched off in Choose Active Comparisons, falls back to it.
    /// </summary>
    public static string ResolveComponentComparison(LiveSplitState state, string? settingsComparison)
    {
        string comparison = string.IsNullOrWhiteSpace(settingsComparison) || settingsComparison == CurrentComparisonSetting
            ? state.CurrentComparison
            : settingsComparison;
        return state.Run.HasComparison(comparison) ? comparison : state.CurrentComparison;
    }

    /// <summary>TimerForm.SwitchComparison: a name the run does not have falls back to Personal Best.</summary>
    public static void SwitchComparison(LiveSplitState state, string name)
    {
        if (!state.Run.Comparisons.Contains(name, StringComparer.Ordinal))
        {
            name = Run.PersonalBestComparisonName;
        }

        state.CurrentComparison = name;
    }

    /// <summary>
    /// TimerForm.SwitchComparisonGenerators: rebuilds the run's generator list from the Choose
    /// Active Comparisons states (in upstream order), keeps the current comparison when it still
    /// exists, then regenerates.
    /// </summary>
    public static void SwitchComparisonGenerators(
        LiveSplitState state,
        IReadOnlyDictionary<string, bool> generatorStates,
        ComparisonGeneratorSettings settings)
    {
        Run run = state.Run;
        foreach (IComparisonGenerator generator in StandardComparisonGeneratorsFactory.GetAllGenerators(run))
        {
            IComparisonGenerator? inRun = run.ComparisonGenerators.FirstOrDefault(x => x.Name == generator.Name);
            if (inRun is not null)
            {
                run.ComparisonGenerators.Remove(inRun);
            }

            if (generatorStates.TryGetValue(generator.Name, out bool enabled) && enabled)
            {
                run.ComparisonGenerators.Add(generator);
            }
        }

        SwitchComparison(state, state.CurrentComparison);
        run.RegenerateComparisons(settings);
    }

    /// <summary>
    /// The comparison part of TimerForm.SetRun: the new run takes over the current run's
    /// generators, is fixed up (FixSplits), the state switches to it, comparisons are regenerated
    /// and the current comparison is kept when the new run has it (else Personal Best).
    /// </summary>
    public static void SetRun(LiveSplitState state, Run run, ComparisonGeneratorSettings settings)
    {
        List<IComparisonGenerator> current = [.. state.Run.ComparisonGenerators];
        Dictionary<string, IComparisonGenerator> available = StandardComparisonGeneratorsFactory
            .GetAllGenerators(run)
            .ToDictionary(g => g.Name, StringComparer.Ordinal);
        run.ComparisonGenerators.Clear();
        foreach (IComparisonGenerator old in current)
        {
            // A plugin's generator (racetime.gg's "[Race] name") moves over as it is, as Windows
            // copies the whole list.
            run.ComparisonGenerators.Add(available.TryGetValue(old.Name, out IComparisonGenerator? generator) ? generator : old);
        }

        run.FixSplits();
        state.Run = run;
        run.RegenerateComparisons(settings);
        SwitchComparison(state, state.CurrentComparison);
    }
}
