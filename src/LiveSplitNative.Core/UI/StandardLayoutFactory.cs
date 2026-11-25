namespace LiveSplitNative.Core.UI;

/// <summary>
/// Default layout matching Windows LiveSplit StandardLayoutFactory / DefaultLayout.lsl
/// component order: Title, Splits, Timer, Previous Segment.
/// </summary>
public static class StandardLayoutFactory
{
    public const string TitlePath = "LiveSplit.Title.dll";
    public const string SplitsPath = "LiveSplit.Splits.dll";
    public const string SubsplitsPath = "LiveSplit.Subsplits.dll";
    public const string TimerPath = "LiveSplit.Timer.dll";
    public const string DetailedTimerPath = "LiveSplit.DetailedTimer.dll";
    public const string PreviousSegmentPath = "LiveSplit.PreviousSegment.dll";
    public const string SumOfBestPath = "LiveSplit.SumOfBest.dll";
    public const string BlankSpacePath = "LiveSplit.BlankSpace.dll";
    public const string PossibleTimeSavePath = "LiveSplit.PossibleTimeSave.dll";
    public const string TextPath = "LiveSplit.Text.dll";
    public const string RunPredictionPath = "LiveSplit.RunPrediction.dll";
    public const string DeltaPath = "LiveSplit.Delta.dll";
    public const string ComparisonTimePath = "LiveSplit.ComparisonTime.dll";
    public const string CurrentComparisonPath = "LiveSplit.CurrentComparison.dll";
    public const string TotalPlaytimePath = "LiveSplit.TotalPlaytime.dll";
    public const string CounterPath = "LiveSplit.Counter.dll";
    public const string HotkeyIndicatorPath = "LiveSplit.HotkeyIndicator.dll";
    public const string GraphPath = "LiveSplit.Graph.dll";
    public const string ManualGameTimePath = "LiveSplit.ManualGameTime.dll";
    public const string WorldRecordPath = "LiveSplit.WorldRecord.dll";
    public const string SoundPath = "LiveSplit.Sound.dll";
    public const string TheRunPath = "LiveSplit.TheRun.dll";
    public const string AutoSplitIntegrationPath = "LiveSplit.AutoSplitIntegration.dll";
    public const string AutoSplittingRuntimePath = "LiveSplit.AutoSplittingRuntime.dll";
    public const string ContextualSlideshowPath = "LiveSplit.ContextualSlideshow.dll";

    /// <summary>
    /// The Separator (SeparatorFactory / SeparatorComponent in LiveSplit.Core): a layout
    /// component with an empty path, saved as &lt;Path&gt;&lt;/Path&gt; with empty settings.
    /// </summary>
    public const string SeparatorPath = "";

    /// <summary>Windows <c>ComponentCategory</c> order for Layout Editor Add menu.</summary>
    public enum ComponentCategory
    {
        Timer = 0,
        List = 1,
        Information = 2,
        Media = 3,
        Control = 4,
        Other = 5,
    }

    public sealed record BuiltinComponentInfo(
        string Path,
        string DisplayName,
        string Description,
        ComponentCategory Category,
        bool NativeRendered);

    /// <summary>Built-in catalog for Layout Editor Add (Windows ComponentCategory groups).</summary>
    public static readonly BuiltinComponentInfo[] BuiltinCatalog =
    [
        new(TimerPath, "Timer", "Main run timer", ComponentCategory.Timer, NativeRendered: true),
        new(DetailedTimerPath, "Detailed Timer",
            "Run timer, segment timer, and segment times for up to two comparisons",
            ComponentCategory.Timer, NativeRendered: true),
        new(SplitsPath, "Splits", "Segment list with comparison columns", ComponentCategory.List, NativeRendered: true),
        new(SubsplitsPath, "Subsplits",
            "Segment list with hierarchical subsplits (leading '-' / {Section} names)",
            ComponentCategory.List, NativeRendered: true),
        new(TitlePath, "Title", "Game / category / attempt counter", ComponentCategory.Information, NativeRendered: true),
        new(PreviousSegmentPath, "Previous Segment", "Delta for the last completed segment", ComponentCategory.Information, NativeRendered: true),
        new(SumOfBestPath, "Sum of Best", "Sum of best segment times", ComponentCategory.Information, NativeRendered: true),
        new(PossibleTimeSavePath, "Possible Time Save", "Time save vs comparison / golds", ComponentCategory.Information, NativeRendered: true),
        new(TextPath, "Text", "Custom label / value or custom variable", ComponentCategory.Information, NativeRendered: true),
        new(RunPredictionPath, "Run Prediction",
            "Displays what the final run time would be if the run continues at the same pace as a set comparison.",
            ComponentCategory.Information, NativeRendered: true),
        new(DeltaPath, "Delta", "Displays the current delta to a comparison.", ComponentCategory.Information, NativeRendered: true),
        new(ComparisonTimePath, "Comparison Time",
            "Displays the final time (or a split time/segment time) from a particular comparison.",
            ComponentCategory.Information, NativeRendered: true),
        new(CurrentComparisonPath, "Current Comparison", "Shows which comparison you are currently comparing to.",
            ComponentCategory.Information, NativeRendered: true),
        new(TotalPlaytimePath, "Total Playtime", "Shows the total playtime for running with these splits.",
            ComponentCategory.Information, NativeRendered: true),
        new(BlankSpacePath, "Blank Space", "Vertical/horizontal spacer", ComponentCategory.Other, NativeRendered: true),
        new(WorldRecordPath, "World Record", "Shows the World Record for the run",
            ComponentCategory.Information, NativeRendered: true),
        new(GraphPath, "Graph", "Shows a graph of the current run in relation to a comparison.",
            ComponentCategory.Media, NativeRendered: true),
        new(SoundPath, "Sound Effects", "Plays sound effects for different situations.",
            ComponentCategory.Media, NativeRendered: true),
        new(ContextualSlideshowPath, "Contextual Slideshow",
            "Creates a Slideshow of multiple Components that always shows the most relevant one.",
            ComponentCategory.Media, NativeRendered: true),
        new(AutoSplitIntegrationPath, "AutoSplit Integration", "Directly connects AutoSplit with LiveSplit.",
            ComponentCategory.Control, NativeRendered: true),
        new(AutoSplittingRuntimePath, "Auto Splitting Runtime",
            "Allows auto splitters provided as WebAssembly modules to define the splitting behaviour.",
            ComponentCategory.Control, NativeRendered: true),
        new(ManualGameTimePath, "Manual Game Time", "Allows manually entering segment times as game time.",
            ComponentCategory.Control, NativeRendered: true),
        new(HotkeyIndicatorPath, "Hotkey Indicator",
            "Shows whether global hotkeys are on or off. Green indicates that global hotkeys are on, while red indicates off.",
            ComponentCategory.Other, NativeRendered: true),
        new(CounterPath, "Counter",
            "A labelled counter, allowing an initial value to be incremented/decremented by a specified amount.",
            ComponentCategory.Other, NativeRendered: true),
        new(TheRunPath, "therun.gg", "Syncs your runs and live data to therun.gg", ComponentCategory.Other, NativeRendered: true),
        new(SeparatorPath, "Separator", "Shows a line to separate components.", ComponentCategory.Other, NativeRendered: true),
    ];

    /// <summary>Category headers always shown (Windows LayoutEditorDialog groups), even if empty.</summary>
    public static readonly ComponentCategory[] AddMenuCategories =
    [
        ComponentCategory.Timer,
        ComponentCategory.List,
        ComponentCategory.Information,
        ComponentCategory.Media,
        ComponentCategory.Control,
        ComponentCategory.Other,
    ];

    public static readonly string[] BuiltinAddablePaths =
        BuiltinCatalog.Select(c => c.Path).ToArray();

    public static Layout Create()
    {
        var layout = new Layout
        {
            Mode = LayoutMode.Vertical,
            VerticalWidth = 286,
            VerticalHeight = 519,
            Settings = LayoutSettings.CreateStandard(),
        };
        layout.Components.Add(new LayoutComponent(TitlePath, DefaultTitleSettings));
        layout.Components.Add(new LayoutComponent(SplitsPath, DefaultSplitsSettings));
        layout.Components.Add(new LayoutComponent(TimerPath, DefaultTimerSettings));
        layout.Components.Add(new LayoutComponent(PreviousSegmentPath, DefaultPreviousSegmentSettings));
        return layout;
    }

    /// <summary>
    /// Windows TimerOnlyLayoutFactory: a new Timer component alone, 252 × 50 at (100, 100), with the
    /// standard layout settings. Used when there are no splits (first start, Close Splits).
    /// </summary>
    public static Layout CreateTimerOnly()
    {
        var layout = new Layout
        {
            Mode = LayoutMode.Vertical,
            VerticalWidth = 252,
            VerticalHeight = 50,
            HorizontalWidth = 252,
            HorizontalHeight = 50,
            X = 100,
            Y = 100,
            Settings = LayoutSettings.CreateStandard(),
        };
        layout.Components.Add(CreateBuiltin(TimerPath));
        return layout;
    }

    /// <summary>Windows SetInTimerOnlyMode's test: the layout is a single Timer component.</summary>
    public static bool IsTimerOnly(Layout layout) =>
        layout.Components.Count == 1 && layout.Components[0].Path == TimerPath;

    public static LayoutComponent CreateBuiltin(string path) =>
        new(path, DefaultSettingsFor(path));

    public static string? DefaultSettingsFor(string path) => path switch
    {
        TitlePath => DefaultTitleSettings,
        SplitsPath => DefaultSplitsSettings,
        // A new Subsplits component has SplitsSettings' constructor defaults.
        SubsplitsPath => new SubsplitsComponentSettings().ToInnerXml(),
        TimerPath => DefaultTimerSettings,
        DetailedTimerPath => DefaultDetailedTimerSettings,
        PreviousSegmentPath => DefaultPreviousSegmentSettings,
        SumOfBestPath => DefaultSumOfBestSettings,
        BlankSpacePath => DefaultBlankSpaceSettings,
        PossibleTimeSavePath => DefaultPossibleTimeSaveSettings,
        TextPath => DefaultTextSettings,
        // The settings controls' constructor defaults.
        RunPredictionPath => new RunPredictionComponentSettings().ToInnerXml(),
        DeltaPath => new DeltaComponentSettings().ToInnerXml(),
        ComparisonTimePath => new ComparisonTimeComponentSettings().ToInnerXml(),
        CurrentComparisonPath => new CurrentComparisonComponentSettings().ToInnerXml(),
        TotalPlaytimePath => new TotalPlaytimeComponentSettings().ToInnerXml(),
        CounterPath => new CounterComponentSettings().ToInnerXml(),
        HotkeyIndicatorPath => new HotkeyIndicatorComponentSettings().ToInnerXml(),
        GraphPath => new GraphComponentSettings().ToInnerXml(),
        ManualGameTimePath => new ManualGameTimeComponentSettings().ToInnerXml(),
        WorldRecordPath => new WorldRecordComponentSettings().ToInnerXml(),
        SoundPath => new SoundComponentSettings().ToInnerXml(),
        TheRunPath => new TheRunComponentSettings().ToInnerXml(),
        AutoSplitIntegrationPath => new AutoSplitIntegrationSettings().ToInnerXml(),
        AutoSplittingRuntimePath => new AutoSplittingRuntimeSettings().ToInnerXml(),
        ContextualSlideshowPath => new ContextualSlideshowComponentSettings().ToInnerXml(),
        _ => null,
    };

    private const string DefaultTitleSettings =
        """
        <Version>1.7.3</Version>
        <ShowAttemptCount>True</ShowAttemptCount>
        <DisplayGameIcon>True</DisplayGameIcon>
        <TextAlignment>0</TextAlignment>
        """;

    private const string DefaultSplitsSettings =
        """
        <Version>1.6</Version>
        <VisualSplitCount>16</VisualSplitCount>
        <ShowSplitTimes>True</ShowSplitTimes>
        <AlwaysShowLastSplit>True</AlwaysShowLastSplit>
        <ShowColumnLabels>False</ShowColumnLabels>
        <CurrentSplitTopColor>FF3373F4</CurrentSplitTopColor>
        <CurrentSplitBottomColor>FF153574</CurrentSplitBottomColor>
        <CurrentSplitGradient>Vertical</CurrentSplitGradient>
        <Columns>
          <Settings>
            <Version>1.5</Version>
            <Name>+/-</Name>
            <Type>Delta</Type>
            <Comparison>Current Comparison</Comparison>
            <TimingMethod>Current Timing Method</TimingMethod>
          </Settings>
          <Settings>
            <Version>1.5</Version>
            <Name>Time</Name>
            <Type>SplitTime</Type>
            <Comparison>Current Comparison</Comparison>
            <TimingMethod>Current Timing Method</TimingMethod>
          </Settings>
        </Columns>
        """;

    private const string DefaultTimerSettings =
        """
        <Version>1.3</Version>
        <TimerHeight>69</TimerHeight>
        <TimerAccuracy>Hundredths</TimerAccuracy>
        <CenterTimer>False</CenterTimer>
        """;

    private const string DefaultDetailedTimerSettings =
        """
        <Version>1.5</Version>
        <Height>75</Height>
        <Width>200</Width>
        <SegmentTimerSizeRatio>40</SegmentTimerSizeRatio>
        <TimerShowGradient>True</TimerShowGradient>
        <SegmentTimerShowGradient>True</SegmentTimerShowGradient>
        <ShowSplitName>False</ShowSplitName>
        <DecimalsSize>35</DecimalsSize>
        <SegmentTimerDecimalsSize>35</SegmentTimerDecimalsSize>
        <TimerAccuracy>Hundredths</TimerAccuracy>
        <SegmentTimerAccuracy>Hundredths</SegmentTimerAccuracy>
        <SegmentTimesAccuracy>Hundredths</SegmentTimesAccuracy>
        <Comparison>Current Comparison</Comparison>
        <Comparison2>Best Segments</Comparison2>
        <HideComparison>False</HideComparison>
        """;

    private const string DefaultPreviousSegmentSettings =
        """
        <Version>1.4</Version>
        <DeltaAccuracy>Tenths</DeltaAccuracy>
        <DropDecimals>True</DropDecimals>
        """;

    private const string DefaultSumOfBestSettings =
        """
        <Version>1.4</Version>
        <Accuracy>Seconds</Accuracy>
        """;

    private const string DefaultBlankSpaceSettings =
        """
        <Version>1.7</Version>
        <SpaceHeight>24</SpaceHeight>
        <SpaceWidth>24</SpaceWidth>
        <BackgroundGradient>Plain</BackgroundGradient>
        """;

    private const string DefaultPossibleTimeSaveSettings =
        """
        <Version>1.4</Version>
        <Accuracy>Hundredths</Accuracy>
        <DropDecimals>False</DropDecimals>
        <Comparison>Current Comparison</Comparison>
        <TotalTimeSave>False</TotalTimeSave>
        """;

    private const string DefaultTextSettings =
        """
        <Version>1.5</Version>
        <Text1>Text</Text1>
        <Text2></Text2>
        <BackgroundGradient>Plain</BackgroundGradient>
        <Display2Rows>False</Display2Rows>
        <CustomVariable>False</CustomVariable>
        """;
}
