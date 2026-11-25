using LiveSplitNative.Core.Localization;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// Layout component entry as stored in .lsl — path + opaque settings XML.
/// Built-in paths (Title/Splits/Timer/PreviousSegment) render natively; others stay listed.
/// </summary>
public sealed class LayoutComponent
{
    public LayoutComponent(string path, string? settingsInnerXml = null)
    {
        Path = path;
        SettingsInnerXml = settingsInnerXml;
    }

    public string Path { get; set; }

    /// <summary>InnerXml of &lt;Settings&gt; preserved for round-trip.</summary>
    public string? SettingsInnerXml { get; set; }

    /// <summary>
    /// Other elements of the &lt;Component&gt; (such as Windows' &lt;FontOverrides&gt;, which this app
    /// does not use: Basten 18 Oct), kept as they were so saving does not drop them.
    /// </summary>
    public List<string> UnknownOuterXml { get; } = [];

    /// <summary>
    /// What the component keeps while the layout is open, such as a Counter's count (upstream keeps
    /// it in the component object). Never saved; a clone shares it, so the Layout Editor's working
    /// copies keep the same count.
    /// </summary>
    public object? RuntimeState { get; set; }

    public string DisplayName => Path switch
    {
        "LiveSplit.Title.dll" => "Title",
        "LiveSplit.Splits.dll" => "Splits",
        "LiveSplit.Subsplits.dll" => "Subsplits",
        "LiveSplit.Timer.dll" => "Timer",
        "LiveSplit.PreviousSegment.dll" => UiText.T("Previous Segment"),
        "LiveSplit.DetailedTimer.dll" => "Detailed Timer",
        "LiveSplit.SumOfBest.dll" => "Sum of Best",
        "LiveSplit.PossibleTimeSave.dll" => "Possible Time Save",
        "LiveSplit.Count.dll" or "LiveSplit.Counter.dll" => "Counter",
        "LiveSplit.Text.dll" => "Text",
        // ComponentName of the components named after their settings.
        StandardLayoutFactory.RunPredictionPath => InfoComponentLogic.RunPredictionName(
            RunPredictionComponentSettings.FromInnerXml(SettingsInnerXml).Comparison),
        StandardLayoutFactory.DeltaPath => InfoComponentLogic.DeltaName(
            DeltaComponentSettings.FromInnerXml(SettingsInnerXml).Comparison),
        StandardLayoutFactory.ComparisonTimePath => InfoComponentLogic.ComparisonTimeName(
            ComparisonTimeComponentSettings.FromInnerXml(SettingsInnerXml)),
        StandardLayoutFactory.CurrentComparisonPath => UiText.T("Current Comparison"),
        StandardLayoutFactory.TotalPlaytimePath => "Total Playtime",
        StandardLayoutFactory.GraphPath => GraphLogic.Name(GraphComponentSettings.FromInnerXml(SettingsInnerXml).Comparison),
        StandardLayoutFactory.HotkeyIndicatorPath => "Hotkey Indicator",
        StandardLayoutFactory.ManualGameTimePath => "Manual Game Time",
        StandardLayoutFactory.WorldRecordPath => WorldRecordLogic.ComponentName,
        StandardLayoutFactory.SoundPath => "Sound Effects",
        StandardLayoutFactory.AutoSplitIntegrationPath => "AutoSplit Integration",
        StandardLayoutFactory.AutoSplittingRuntimePath => "Auto Splitting Runtime",
        StandardLayoutFactory.ContextualSlideshowPath => "Contextual Slideshow",
        StandardLayoutFactory.TheRunPath => TheRunLogic.Name((RuntimeState as TheRunCollector)?.LastSyncTime, DateTime.Now),
        "LiveSplit.BlankSpace.dll" => "Blank Space",
        // SeparatorComponent.ComponentName: the Layout Editor lists a separator as a line.
        "" => SeparatorName,
        // A component DLL: its ComponentName.
        _ when RuntimeState is IExternalComponent external => external.ComponentName,
        _ when Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            => System.IO.Path.GetFileNameWithoutExtension(Path).Replace("LiveSplit.", "", StringComparison.Ordinal),
        _ => Path,
    };

    public LayoutComponent Clone()
    {
        var clone = new LayoutComponent(Path, SettingsInnerXml) { RuntimeState = RuntimeState };
        clone.UnknownOuterXml.AddRange(UnknownOuterXml);
        return clone;
    }

    /// <summary>SeparatorComponent.ComponentName.</summary>
    public const string SeparatorName =
        "----------------------------------------------------------------------------";

    /// <summary>The Separator and Contextual Slideshow have no settings (GetSettingsControl returns null).</summary>
    public bool HasSettings => Path != StandardLayoutFactory.SeparatorPath && Path != StandardLayoutFactory.ContextualSlideshowPath;
}
