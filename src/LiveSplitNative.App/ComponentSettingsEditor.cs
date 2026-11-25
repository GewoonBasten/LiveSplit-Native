using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

public sealed partial class ComponentSettingsEditor : UserControl
{
    /// <summary>Windows Subsplits/Splits ColumnSettings combo labels (spaces → enum).</summary>
    private static readonly string[] ColumnTypeLabels =
    [
        "Delta",
        "Split Time",
        "Delta or Split Time",
        "Segment Delta",
        "Segment Time",
        "Segment Delta or Segment Time",
        "Custom Variable",
    ];

    private static readonly string[] TimingMethodLabels =
    [
        "Current Timing Method",
        "Real Time",
        "Game Time",
    ];

    private static readonly string[] AccuracyLabels =
        ["Seconds", "Tenths", "Hundredths", "Milliseconds"];

    private readonly LayoutComponent _component;
    private readonly string _path;
    private readonly LayoutMode _layoutMode;
    private TitleComponentSettings? _title;
    private SplitsComponentSettings? _splits;
    private SubsplitsComponentSettings? _subsplits;
    private TimerComponentSettings? _timer;
    private DetailedTimerComponentSettings? _detailedTimer;
    private PreviousSegmentComponentSettings? _previous;
    private SumOfBestComponentSettings? _sumOfBest;
    private PossibleTimeSaveComponentSettings? _pts;
    private BlankSpaceComponentSettings? _blank;
    private TextComponentSettings? _text;

    private CheckBox? _chkGameName, _chkCategoryName, _chkAttemptCount, _chkFinishedRuns, _chkSingleLine, _chkDisplayIcon;
    private CheckBox? _chkShowRegion, _chkShowPlatform, _chkShowVariables, _chkTitleOverrideColor;
    private ComboBox? _cmbTitleGradient, _cmbTitleAlignment;
    private NumericUpDown? _numVisualSplits, _numPreview, _numSplitSize;
    private CheckBox? _chkThinSep, _chkAlwaysLast, _chkSepLast, _chkShowSplitTimes, _chkDropDecimalsSplits, _chkDisplayIcons, _chkShowColumnLabels;
    private CheckBox? _chkIndentSubsplits, _chkShowSectionHeader, _chkOnlyCurrentSection;
    private CheckBox? _chkLockLast, _chkDisplay2Rows, _chkAutoAbbrev, _chkIconShadows, _chkIndentBlankIcons;
    private CheckBox? _chkOverrideNamesColor, _chkOverrideTimesColor, _chkOverrideDeltasColor, _chkOverrideSubsplitColor;
    private CheckBox? _chkIndentSectionSplit, _chkShowIconSectionSplit, _chkShowSectionIcon;
    private CheckBox? _chkOverrideHeaderColor, _chkHeaderText, _chkHeaderTimes, _chkSectionTimer, _chkSectionTimerGradient;
    private ComboBox? _cmbSubsplitVisibility, _cmbSplitAccuracy, _cmbDeltaAccuracy;
    private ComboBox? _cmbBgGradient, _cmbCurrentSplitGradient, _cmbSubsplitGradient, _cmbHeaderGradient;
    private ComboBox? _cmbHeaderAccuracy, _cmbSectionTimerAccuracy, _cmbHeaderComparison, _cmbHeaderTiming;
    private NumericUpDown? _numIconSize, _numMinMajorSplits;
    private ColorSwatchButton? _swLabelsColor, _swBg1, _swBg2;
    private ColorSwatchButton? _swBeforeNames, _swCurrentNames, _swAfterNames;
    private ColorSwatchButton? _swBeforeTimes, _swCurrentTimes, _swAfterTimes, _swDeltasColor;
    private ColorSwatchButton? _swSubsplitTop, _swSubsplitBottom;
    private ColorSwatchButton? _swHeaderTop, _swHeaderBottom, _swHeaderText, _swHeaderTimes, _swSectionTimerColor;
    private SettingsCard? _columnsHost;
    private readonly List<ColumnData> _editColumns = [];
    private NumericUpDown? _numTimerHeight, _numTimerWidth, _numDecimalsSize, _numDetailedHeight, _numSegmentRatio;
    private NumericUpDown? _numDetailedWidth, _numDetailedIconSize, _numDetailedDecimals, _numDetailedSegDecimals;
    private CheckBox? _chkShowGradient, _chkCenterTimer, _chkOverrideColors;
    private CheckBox? _chkDetailedShowSplitName, _chkDetailedHideComparison;
    private CheckBox? _chkDetailedTimerGradient, _chkDetailedSegGradient, _chkDetailedOverrideColors, _chkDetailedDisplayIcon;
    private ComboBox? _cmbTimerAccuracy, _cmbTimerMethod, _cmbTimerDigits, _cmbTimerGradient;
    private ComboBox? _cmbDetailedDigits, _cmbDetailedAccuracy, _cmbDetailedSegDigits, _cmbDetailedSegAccuracy;
    private ComboBox? _cmbDetailedTimesAccuracy, _cmbDetailedGradient, _cmbDetailedComparison, _cmbDetailedComparison2, _cmbDetailedMethod;
    // Detailed Timer fonts as its font dialog edits them (DetailedTimerSettings btn*Font_Click).
    private LayoutFontChoice _detailedLabelsFont, _detailedTimesFont, _detailedSplitFont;
    private Control? _rowDetailedSplitFont;
    private ComboBox? _cmbPrevDeltaAccuracy, _cmbSobAccuracy, _cmbSobGradient, _cmbBlankGradient, _cmbPtsAccuracy;
    private ComboBox? _cmbPrevTimeSaveAccuracy, _cmbPrevGradient, _cmbPtsGradient;
    private CheckBox? _chkPrevDropDecimals, _chkPrevTwoRows, _chkPrevShowPts, _chkPrevOverrideText;
    private CheckBox? _chkSobTwoRows, _chkSobOverrideText, _chkSobOverrideTime;
    private CheckBox? _chkPtsDropDecimals, _chkPtsTwoRows, _chkPtsTotal, _chkPtsOverrideText, _chkPtsOverrideTime;
    private ComboBox? _cmbPrevComparison, _cmbPtsComparison;
    private NumericUpDown? _numBlankHeight, _numBlankWidth;
    private TextBox? _txtText1, _txtText2;
    private CheckBox? _chkTextCustomVar, _chkTextTwoRows, _chkTextOverrideText, _chkTextOverrideTime;
    private ComboBox? _cmbTextGradient;

    private ColorSwatchButton? _swTitleColor, _swTitleBg1, _swTitleBg2;
    private ColorSwatchButton? _swSplitTop, _swSplitBottom;
    private ColorSwatchButton? _swTimerColor, _swTimerBg1, _swTimerBg2;
    private ColorSwatchButton? _swDetailedTimerColor, _swDetailedSegmentColor;
    private ColorSwatchButton? _swDetailedLabelsColor, _swDetailedTimesColor, _swDetailedSplitColor;
    private ColorSwatchButton? _swDetailedBg1, _swDetailedBg2;
    private ColorSwatchButton? _swPrevBg1, _swPrevBg2, _swPrevText;
    private ColorSwatchButton? _swSobText, _swSobTime, _swSobBg1, _swSobBg2;
    private ColorSwatchButton? _swPtsText, _swPtsTime, _swPtsBg1, _swPtsBg2;
    private ColorSwatchButton? _swBlankBg1, _swBlankBg2;
    private ColorSwatchButton? _swTextColor, _swTextTime, _swTextBg1, _swTextBg2;

    /// <summary>The page: its cards (and their headings), SettingsLayout.SectionGap apart.</summary>
    private readonly StackPanel _fields = new()
    {
        Spacing = SettingsLayout.SectionGap,
    };

    /// <summary>The page's padding (inside the window, around the cards).</summary>
    internal static readonly Avalonia.Thickness PagePadding = new(24, 20, 24, 20);

    /// <summary>A card of the page's rows outside any group (Tag).</summary>
    private static readonly object LooseCard = new();

    /// <summary>
    /// The open run's comparisons (Run.Comparisons), which the Comparison dropdowns list as the
    /// upstream settings controls do from CurrentState. The main window points this at its run;
    /// without one it is a new run's (Personal Best, Best Segments, Average Segments).
    /// </summary>
    internal static Func<IReadOnlyList<string>> RunComparisons { get; set; } =
        () => ["Personal Best", "Best Segments", "Average Segments"];

    public ComponentSettingsEditor(
        LayoutComponent component,
        LayoutMode layoutMode = LayoutMode.Vertical)
    {
        _component = component.Clone();
        _path = _component.Path;
        _layoutMode = layoutMode;
        SettingsLayout.SetLabelWidth(this, SettingsLabelColumnWidth);
        Content = new Border
        {
            Padding = PagePadding,
            Child = _fields,
        };
        BuildFields();
    }

    /// <summary>Fired after a control change is applied to the working component (live preview).</summary>
    public Action? SettingsChanged { get; set; }

    public string TabHeader => _component.DisplayName;

    /// <summary>The width of a component DLL's settings page (0 for the app's own pages).</summary>
    public double HostedPageWidth { get; private set; }

    private void NotifyPreview()
    {
        ApplyToComponent();
        SettingsChanged?.Invoke();
    }

    /// <summary>Measured height of all settings fields (for dialog fit — avoids ScrollViewer under-measure).</summary>
    public double MeasureContentHeight(double availableWidth)
    {
        double width = Math.Max(200, availableWidth);
        // The page's padding — previously omitted → short window / clipped last control.
        double total = PagePadding.Top + PagePadding.Bottom + _fields.Margin.Top + _fields.Margin.Bottom;
        double innerW = Math.Max(160, width - PagePadding.Left - PagePadding.Right);
        foreach (Control child in _fields.Children.OfType<Control>())
        {
            child.Measure(new Avalonia.Size(innerW, double.PositiveInfinity));
            total += child.DesiredSize.Height + _fields.Spacing;
        }

        if (_fields.Children.Count > 0)
        {
            total -= _fields.Spacing;
        }

        return Math.Max(total, DesiredSize.Height);
    }

    public LayoutComponent Apply()
    {
        ApplyToComponent();
        return _component;
    }

    private void BuildFields()
    {
        _fields.Children.Clear();
        if (_component.RuntimeState is HostedLayoutComponent hosted)
        {
            BuildHostedFields(hosted);
        }
        else if (_path == StandardLayoutFactory.TitlePath)
        {
            BuildTitleFields();
        }
        else if (_path == StandardLayoutFactory.SplitsPath)
        {
            BuildSplitsFields();
        }
        else if (_path == StandardLayoutFactory.SubsplitsPath)
        {
            BuildSubsplitsFields();
        }
        else if (_path == StandardLayoutFactory.TimerPath)
        {
            BuildTimerFields();
        }
        else if (_path == StandardLayoutFactory.DetailedTimerPath)
        {
            BuildDetailedTimerFields();
        }
        else if (_path == StandardLayoutFactory.PreviousSegmentPath)
        {
            BuildPreviousSegmentFields();
        }
        else if (_path == StandardLayoutFactory.SumOfBestPath)
        {
            BuildSumOfBestFields();
        }
        else if (_path == StandardLayoutFactory.PossibleTimeSavePath)
        {
            BuildPossibleTimeSaveFields();
        }
        else if (_path == StandardLayoutFactory.BlankSpacePath)
        {
            BuildBlankSpaceFields();
        }
        else if (_path == StandardLayoutFactory.TextPath)
        {
            BuildTextFields();
        }
        else if (!_component.HasSettings)
        {
            // No settings (Separator, Contextual Slideshow: GetSettingsControl returns null).
        }
        else if (!BuildMoreFields())
        {
            _fields.Children.Add(new TextBlock
            {
                Text = "No native settings panel for this component path yet. Settings XML is preserved on save.",
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    private void BuildTitleFields()
    {
        _title = TitleComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        _chkGameName = AddCheck("Show Game Name", _title.ShowGameName);
        _chkCategoryName = AddCheck("Show Category Name", _title.ShowCategoryName);
        _chkAttemptCount = AddCheck("Show Attempt Count", _title.ShowAttemptCount);
        _chkFinishedRuns = AddCheck("Show Finished Runs Count", _title.ShowFinishedRunsCount);
        _chkSingleLine = AddCheck("Display Game and Category in Single Line", _title.SingleLine);
        _chkDisplayIcon = AddCheck("Display Game Icon", _title.DisplayGameIcon);

        SettingsCard titleColor = BeginGroup("Title Color");
        _chkTitleOverrideColor = AddCheckTo(titleColor, "Override Layout Settings", _title.OverrideTitleColor);
        _swTitleColor = AddColorTo(titleColor, "Color:", _title.TitleColor);
        WireEnable(_chkTitleOverrideColor, _swTitleColor);

        _swTitleBg1 = AddColor("Background Color", _title.BackgroundColor);
        _swTitleBg2 = AddColor("Background Color 2", _title.BackgroundColor2);
        _cmbTitleGradient = AddCombo("Background Gradient", ["Plain", "Vertical", "Horizontal"], _title.BackgroundGradient);
        string align = _title.TextAlignment == 1 ? "Center" : _title.TextAlignment == 2 ? "Right" : "Left";
        _cmbTitleAlignment = AddCombo("Text Alignment", ["Left", "Center", "Right"], align);

        SettingsCard extra = BeginGroup("Additional Category Info");
        _chkShowRegion = AddCheckTo(extra, "Show Region", _title.ShowRegion);
        _chkShowPlatform = AddCheckTo(extra, "Show Platform", _title.ShowPlatform);
        _chkShowVariables = AddCheckTo(extra, "Show Variables", _title.ShowVariables);
    }

    private void BuildSplitsFields()
    {
        _splits = SplitsComponentSettings.FromInnerXml(_component.SettingsInnerXml);

        _swBg1 = AddColor("Background Color", _splits.BackgroundColor);
        _swBg2 = AddColor("Background Color 2", _splits.BackgroundColor2);
        _cmbBgGradient = AddCombo("Background Gradient",
            ["Plain", "Vertical", "Horizontal", "Alternating"],
            _splits.BackgroundGradient);
        _numVisualSplits = AddNumber("Total Splits", _splits.VisualSplitCount, 1, 40);
        _numPreview = AddNumber("Upcoming Splits", _splits.SplitPreviewCount, 0, 20);
        if (ComponentSettingsSizeAxisPolicy.For(_layoutMode) != ComponentSettingsSizeAxis.Width)
        {
            _numSplitSize = AddSplitHeight(_splits.SplitHeight);
        }
        _chkThinSep = AddCheck("Show Thin Separators", _splits.ShowThinSeparators);
        _chkSepLast = AddCheck("Show Separator Before Last Split", _splits.SeparatorLastSplit);
        _chkAlwaysLast = AddCheck("Always Show Last Split", _splits.AlwaysShowLastSplit);
        _chkLockLast = AddCheck("Lock Last Split to Bottom", _splits.LockLastSplit);
        _chkDisplay2Rows = AddCheck("Display 2 Rows", _splits.Display2Rows);
        _chkShowSplitTimes = AddCheck("Show Split Times", _splits.ShowSplitTimes);
        _swSplitTop = AddColor("Current Split Top Color", _splits.CurrentSplitTopColor);
        _swSplitBottom = AddColor("Current Split Bottom Color", _splits.CurrentSplitBottomColor);
        _cmbCurrentSplitGradient = AddCombo("Current Split Gradient",
            ["Plain", "Vertical", "Horizontal"],
            _splits.CurrentSplitGradient);

        SettingsCard names = BeginGroup("Split Names");
        _chkAutoAbbrev = AddCheckTo(names, "Automatically Abbreviate Names If Necessary", _splits.AutomaticAbbreviations);
        SettingsCard namesColor = BeginGroup("Color", nested: true, host: names);
        _chkOverrideNamesColor = AddCheckTo(namesColor, "Override Layout Settings", _splits.OverrideTextColor);
        _swBeforeNames = AddColorTo(namesColor, "Color (Before Current Split):", _splits.BeforeNamesColor);
        _swCurrentNames = AddColorTo(namesColor, "Color (Current Split):", _splits.CurrentNamesColor);
        _swAfterNames = AddColorTo(namesColor, "Color (After Current Split):", _splits.AfterNamesColor);
        WireEnable(_chkOverrideNamesColor, _swBeforeNames, _swCurrentNames, _swAfterNames);

        SettingsCard icons = BeginGroup("Icons");
        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _chkDisplayIcons = AddCheckTo(iconRow, "Display Icons", _splits.DisplayIcons);
        _chkIconShadows = AddCheckTo(iconRow, "Show Icon Shadows", _splits.IconShadows);
        icons.Children.Add(iconRow);
        _numIconSize = AddNumberTo(icons, "Size:", (decimal)_splits.IconSize, 8, 64);
        WireEnable(_chkDisplayIcons, _chkIconShadows, _numIconSize);

        SettingsCard deltas = BeginGroup("Deltas");
        SettingsCard deltaAcc = BeginGroup("Accuracy", nested: true, host: deltas);
        _chkDropDecimalsSplits = AddCheckTo(deltaAcc, "Drop Decimals When More Than 1 Minute", _splits.DropDecimals);
        _cmbDeltaAccuracy = AddAccuracyRadios(deltaAcc, _splits.DeltasAccuracy);
        SettingsCard liveDelta = BeginGroup("Live Delta Color", nested: true, host: deltas);
        _chkOverrideDeltasColor = AddCheckTo(liveDelta, "Override Layout Settings", _splits.OverrideDeltasColor);
        _swDeltasColor = AddColorTo(liveDelta, "Color:", _splits.DeltasColor);
        WireEnable(_chkOverrideDeltasColor, _swDeltasColor);

        SettingsCard times = BeginGroup("Split Times");
        SettingsCard timesColor = BeginGroup("Color", nested: true, host: times);
        _chkOverrideTimesColor = AddCheckTo(timesColor, "Override Layout Settings", _splits.OverrideTimesColor);
        _swBeforeTimes = AddColorTo(timesColor, "Color (Before Current Split):", _splits.BeforeTimesColor);
        _swCurrentTimes = AddColorTo(timesColor, "Color (Current Split):", _splits.CurrentTimesColor);
        _swAfterTimes = AddColorTo(timesColor, "Color (After Current Split):", _splits.AfterTimesColor);
        WireEnable(_chkOverrideTimesColor, _swBeforeTimes, _swCurrentTimes, _swAfterTimes);
        SettingsCard timesAcc = BeginGroup("Accuracy", nested: true, host: times);
        _cmbSplitAccuracy = AddAccuracyRadios(timesAcc, _splits.SplitTimesAccuracy);

        BuildColumnDesigner(_splits, includeLabelsColor: true, labelsColor: _splits.LabelsColor);
    }

    /// <summary>
    /// Windows Subsplits settings groups (authoritative: media/windows-reference/subsplits-settings-windows.png).
    /// Bordered GroupBoxes with nested Override Layout Settings panels — not a flat checkbox dump.
    /// </summary>
    private void BuildSubsplitsFields()
    {
        _subsplits = SubsplitsComponentSettings.FromInnerXml(_component.SettingsInnerXml);

        _swBg1 = AddColor("Background Color", _subsplits.BackgroundColor);
        _swBg2 = AddColor("Background Color 2", _subsplits.BackgroundColor2);
        _cmbBgGradient = AddCombo("Background Gradient",
            ["Plain", "Vertical", "Horizontal", "Alternating"],
            _subsplits.BackgroundGradient);
        _numVisualSplits = AddNumber("Total Splits", _subsplits.VisualSplitCount, 1, 40);
        _numPreview = AddNumber("Upcoming Splits", _subsplits.SplitPreviewCount, 0, 20);
        // SplitsSettings trkSize: Split Height 0-25 (the trackbar's 0-250 / 10), or in
        // horizontal mode Split Width 5-120.
        _numSplitSize = ComponentSettingsSizeAxisPolicy.For(_layoutMode) == ComponentSettingsSizeAxis.Width
            ? AddNumber("Split Width", (decimal)_subsplits.SplitWidth, 5, 120)
            : AddSplitHeight(_subsplits.SplitHeight);
        _chkThinSep = AddCheck("Show Thin Separators", _subsplits.ShowThinSeparators);
        _chkSepLast = AddCheck("Show Separator Before Last Split", _subsplits.SeparatorLastSplit);
        _chkAlwaysLast = AddCheck("Always Show Last Split", _subsplits.AlwaysShowLastSplit);
        _chkLockLast = AddCheck("Lock Last Split to Bottom", _subsplits.LockLastSplit);
        _chkDisplay2Rows = AddCheck("Display 2 Rows", _subsplits.Display2Rows);
        _swSplitTop = AddColor("Current Split Top Color", _subsplits.CurrentSplitTopColor);
        _swSplitBottom = AddColor("Current Split Bottom Color", _subsplits.CurrentSplitBottomColor);
        _cmbCurrentSplitGradient = AddCombo("Current Split Gradient",
            ["Plain", "Vertical", "Horizontal"],
            _subsplits.CurrentSplitGradient);

        SettingsCard subs = BeginGroup("Subsplits");
        subs.Children.Add(new TextBlock
        {
            Text = "To set a split as a subsplit, add a - (dash) to the beginning of the split name. Optional: Add a section name with {}. Ex: '{Section Name} Split Name'",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DodgerBlue,
            FontSize = 11,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
        });
        _cmbSubsplitVisibility = AddComboTo(subs,
            "Subsplit Visibility",
            ["AlwaysHide", "Contextual", "AlwaysShow"],
            _subsplits.Visibility.ToString());
        _chkIndentSubsplits = AddCheckTo(subs, "Indent Subsplits", _subsplits.IndentSubsplits);
        _chkOnlyCurrentSection = AddCheckTo(subs, "Only Current Section", _subsplits.OnlyCurrentSection);
        _numMinMajorSplits = AddNumberTo(subs, "Minimum Number of Sections Before Current Section", _subsplits.MinimumMajorSplits, 0, 40);
        SettingsCard subColor = BeginGroup("Subsplit Color", nested: true, host: subs);
        _chkOverrideSubsplitColor = AddCheckTo(subColor, "Override Layout Settings", _subsplits.OverrideSubsplitColor);
        _swSubsplitTop = AddColorTo(subColor, "Color:", _subsplits.SubsplitTopColor);
        _swSubsplitBottom = AddColorTo(subColor, "Color 2:", _subsplits.SubsplitBottomColor);
        _cmbSubsplitGradient = AddComboTo(subColor, "Gradient", ["Plain", "Vertical", "Horizontal"], _subsplits.SubsplitGradient);
        WireEnable(_chkOverrideSubsplitColor, _swSubsplitTop, _swSubsplitBottom, _cmbSubsplitGradient);

        SettingsCard header = BeginGroup("Section Header");
        _chkShowSectionHeader = AddCheckTo(header, "Show Section Header", _subsplits.ShowSectionHeader);
        _chkShowSectionIcon = AddCheckTo(header, "Show Icon", _subsplits.ShowSectionIcon);
        _cmbHeaderComparison = AddComboTo(
            header, "Comparison", ComparisonChoices.SubsplitsHeader(RunComparisons(), _subsplits.HeaderComparison), _subsplits.HeaderComparison);
        _cmbHeaderTiming = AddComboTo(header, "Timing Method", TimingMethodLabels, _subsplits.HeaderTimingMethod);
        _chkIndentSectionSplit = AddCheckTo(header, "Indent Last Split Of Section", _subsplits.IndentSectionSplit);
        _chkShowIconSectionSplit = AddCheckTo(header, "Show Icon of Last Split of Section", _subsplits.ShowIconSectionSplit);
        _swHeaderTop = AddColorTo(header, "Header Background Color", _subsplits.HeaderTopColor);
        _swHeaderBottom = AddColorTo(header, "Header Background Color 2", _subsplits.HeaderBottomColor);
        _cmbHeaderGradient = AddComboTo(header, "Header Gradient", ["Plain", "Vertical", "Horizontal"], _subsplits.HeaderGradient);
        SettingsCard headerColor = BeginGroup("Section Header Color", nested: true, host: header);
        _chkOverrideHeaderColor = AddCheckTo(headerColor, "Override Layout Text Colors", _subsplits.OverrideHeaderColor);
        _swHeaderText = AddColorTo(headerColor, "Name Color", _subsplits.HeaderTextColor);
        _chkHeaderText = AddCheckTo(headerColor, "Show Split Name", _subsplits.HeaderText);
        _swHeaderTimes = AddColorTo(headerColor, "Time Color", _subsplits.HeaderTimesColor);
        _chkHeaderTimes = AddCheckTo(headerColor, "Show Total Section Time", _subsplits.HeaderTimes);
        SettingsCard headerAcc = BeginGroup("Time Accuracy", nested: true, host: header);
        _cmbHeaderAccuracy = AddAccuracyRadios(headerAcc, _subsplits.HeaderAccuracy);
        WireEnable(_chkOverrideHeaderColor, _swHeaderText, _swHeaderTimes);
        WireEnable(_chkShowSectionHeader,
            _chkShowSectionIcon, _cmbHeaderComparison, _cmbHeaderTiming,
            _chkIndentSectionSplit, _chkShowIconSectionSplit,
            _swHeaderTop, _swHeaderBottom, _cmbHeaderGradient,
            _chkOverrideHeaderColor, _swHeaderText, _chkHeaderText, _swHeaderTimes, _chkHeaderTimes, _cmbHeaderAccuracy);

        SettingsCard sectionTimer = BeginGroup("Section Timer");
        _chkSectionTimer = AddCheckTo(sectionTimer, "Show Section Timer", _subsplits.SectionTimer);
        _swSectionTimerColor = AddColorTo(sectionTimer, "Color", _subsplits.SectionTimerColor);
        _chkSectionTimerGradient = AddCheckTo(sectionTimer, "Show Gradient", _subsplits.SectionTimerGradient);
        SettingsCard timerAcc = BeginGroup("Timer Accuracy", nested: true, host: sectionTimer);
        _cmbSectionTimerAccuracy = AddAccuracyRadios(timerAcc, _subsplits.SectionTimerAccuracy);
        WireEnable(_chkSectionTimer, _swSectionTimerColor, _chkSectionTimerGradient, _cmbSectionTimerAccuracy);

        // —— Density match for Windows screenshot region ——
        SettingsCard names = BeginGroup("Split Names");
        _chkAutoAbbrev = AddCheckTo(names, "Automatically Abbreviate Names If Necessary", _subsplits.AutomaticAbbreviation);
        SettingsCard namesColor = BeginGroup("Color", nested: true, host: names);
        _chkOverrideNamesColor = AddCheckTo(namesColor, "Override Layout Settings", _subsplits.OverrideTextColor);
        _swBeforeNames = AddColorTo(namesColor, "Color (Before Current Split):", _subsplits.BeforeNamesColor);
        _swCurrentNames = AddColorTo(namesColor, "Color (Current Split):", _subsplits.CurrentNamesColor);
        _swAfterNames = AddColorTo(namesColor, "Color (After Current Split):", _subsplits.AfterNamesColor);
        WireEnable(_chkOverrideNamesColor, _swBeforeNames, _swCurrentNames, _swAfterNames);

        SettingsCard icons = BeginGroup("Icons");
        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        _chkDisplayIcons = AddCheckTo(iconRow, "Display Icons", _subsplits.DisplayIcons);
        _chkIconShadows = AddCheckTo(iconRow, "Show Icon Shadows", _subsplits.IconShadows);
        _chkIndentBlankIcons = AddCheckTo(iconRow, "Indent Blank Icons", _subsplits.IndentBlankIcons);
        icons.Children.Add(iconRow);
        _numIconSize = AddNumberTo(icons, "Size:", (decimal)_subsplits.IconSize, 8, 64);
        WireEnable(_chkDisplayIcons, _chkIconShadows, _chkIndentBlankIcons, _numIconSize);

        SettingsCard deltas = BeginGroup("Deltas");
        SettingsCard deltaAcc = BeginGroup("Accuracy", nested: true, host: deltas);
        _chkDropDecimalsSplits = AddCheckTo(deltaAcc, "Drop Decimals When More Than 1 Minute", _subsplits.DropDecimals);
        _cmbDeltaAccuracy = AddAccuracyRadios(deltaAcc, _subsplits.DeltasAccuracy);
        SettingsCard liveDelta = BeginGroup("Live Delta Color", nested: true, host: deltas);
        _chkOverrideDeltasColor = AddCheckTo(liveDelta, "Override Layout Settings", _subsplits.OverrideDeltasColor);
        _swDeltasColor = AddColorTo(liveDelta, "Color:", _subsplits.DeltasColor);
        WireEnable(_chkOverrideDeltasColor, _swDeltasColor);

        SettingsCard times = BeginGroup("Split Times");
        SettingsCard timesColor = BeginGroup("Color", nested: true, host: times);
        _chkOverrideTimesColor = AddCheckTo(timesColor, "Override Layout Settings", _subsplits.OverrideTimesColor);
        _swBeforeTimes = AddColorTo(timesColor, "Color (Before Current Split):", _subsplits.BeforeTimesColor);
        _swCurrentTimes = AddColorTo(timesColor, "Color (Current Split):", _subsplits.CurrentTimesColor);
        _swAfterTimes = AddColorTo(timesColor, "Color (After Current Split):", _subsplits.AfterTimesColor);
        WireEnable(_chkOverrideTimesColor, _swBeforeTimes, _swCurrentTimes, _swAfterTimes);
        SettingsCard timesAcc = BeginGroup("Accuracy", nested: true, host: times);
        _cmbSplitAccuracy = AddAccuracyRadios(timesAcc, _subsplits.SplitTimesAccuracy);

        BuildColumnDesigner(_subsplits.AsSplitsSettings(), includeLabelsColor: true, labelsColor: _subsplits.LabelsColor);
    }

    private void BuildTimerFields()
    {
        _timer = TimerComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        if (ComponentSettingsSizeAxisPolicy.For(_layoutMode) == ComponentSettingsSizeAxis.Width)
        {
            _numTimerWidth = AddNumber("Width:", (decimal)_timer.TimerWidth, 50, 500);
        }
        else
        {
            _numTimerHeight = AddNumber("Height:", (decimal)_timer.TimerHeight, 20, 200);
        }

        _chkShowGradient = AddCheck("Show Gradient", _timer.ShowGradient);
        _chkCenterTimer = AddCheck("Align to Center", _timer.CenterTimer);

        SettingsCard timerColor = BeginGroup("Timer Color");
        _chkOverrideColors = AddCheckTo(timerColor, "Override Layout Settings", _timer.OverrideSplitColors);
        _swTimerColor = AddColorTo(timerColor, "Color:", _timer.TimerColor);
        WireEnable(_chkOverrideColors, _swTimerColor);

        _swTimerBg1 = AddColor("Background Color", _timer.BackgroundColor);
        _swTimerBg2 = AddColor("Background Color 2", _timer.BackgroundColor2);
        _cmbTimerGradient = AddCombo("Background Gradient", ["Plain", "Vertical", "Horizontal"], _timer.BackgroundGradient);
        _cmbTimerDigits = AddCombo("Timer Format", ["1", "00:01", "0:00:01", "00:00:01"], _timer.DigitsFormat);
        _cmbTimerAccuracy = AddCombo("Accuracy", AccuracyLabels, _timer.TimerAccuracy.ToString());
        _numDecimalsSize = AddNumber("Decimals Font Size:", (decimal)_timer.DecimalsSize, 10, 80);
        _cmbTimerMethod = AddCombo("Timing Method", TimingMethodLabels, _timer.TimingMethod);
    }

    private void BuildPreviousSegmentFields()
    {
        _previous = PreviousSegmentComponentSettings.FromInnerXml(_component.SettingsInnerXml);

        SettingsCard deltaAcc = BeginGroup("Delta Accuracy");
        _chkPrevDropDecimals = AddCheckTo(deltaAcc, "Drop Decimals When More Than 1 Minute", _previous.DropDecimals);
        SettingsCard acc = BeginGroup("Accuracy", nested: true, host: deltaAcc);
        _cmbPrevDeltaAccuracy = AddAccuracyRadios(acc, _previous.DeltaAccuracy);

        SettingsCard textColor = BeginGroup("Text Color");
        _chkPrevOverrideText = AddCheckTo(textColor, "Override Layout Settings", _previous.OverrideTextColor);
        _swPrevText = AddColorTo(textColor, "Color:", _previous.TextColor);
        WireEnable(_chkPrevOverrideText, _swPrevText);

        _cmbPrevComparison = AddCombo(
            "Comparison:", ComparisonChoices.PreviousSegment(RunComparisons(), _previous.Comparison), _previous.Comparison);
        _chkPrevTwoRows = AddCheck("Display 2 Rows", _previous.Display2Rows);
        _swPrevBg1 = AddColor("Background Color", _previous.BackgroundColor);
        _swPrevBg2 = AddColor("Background Color 2", _previous.BackgroundColor2);
        _cmbPrevGradient = AddCombo("Background Gradient", ["Plain", "Vertical", "Horizontal"], _previous.BackgroundGradient);

        SettingsCard pts = BeginGroup("Possible Time Save");
        _chkPrevShowPts = AddCheckTo(pts, "Show Possible Time Save", _previous.ShowPossibleTimeSave);
        SettingsCard ptsAcc = BeginGroup("Accuracy", nested: true, host: pts);
        _cmbPrevTimeSaveAccuracy = AddAccuracyRadios(ptsAcc, _previous.TimeSaveAccuracy);
        WireEnable(_chkPrevShowPts, _cmbPrevTimeSaveAccuracy);
    }

    private void BuildSumOfBestFields()
    {
        _sumOfBest = SumOfBestComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        _cmbSobAccuracy = AddCombo("Accuracy", ["Seconds", "Tenths", "Hundredths"], _sumOfBest.Accuracy.ToString());
        _chkSobTwoRows = AddCheck("Display 2 Rows", _sumOfBest.Display2Rows);

        SettingsCard textColor = BeginGroup("Text Color");
        _chkSobOverrideText = AddCheckTo(textColor, "Override Layout Settings", _sumOfBest.OverrideTextColor);
        _swSobText = AddColorTo(textColor, "Color:", _sumOfBest.TextColor);
        WireEnable(_chkSobOverrideText, _swSobText);

        SettingsCard timeColor = BeginGroup("Time Color");
        _chkSobOverrideTime = AddCheckTo(timeColor, "Override Layout Settings", _sumOfBest.OverrideTimeColor);
        _swSobTime = AddColorTo(timeColor, "Color:", _sumOfBest.TimeColor);
        WireEnable(_chkSobOverrideTime, _swSobTime);

        _cmbSobGradient = AddCombo("Background Gradient", ["Plain", "Vertical", "Horizontal"], _sumOfBest.BackgroundGradient);
        _swSobBg1 = AddColor("Background Color", _sumOfBest.BackgroundColor);
        _swSobBg2 = AddColor("Background Color 2", _sumOfBest.BackgroundColor2);
    }

    private void BuildPossibleTimeSaveFields()
    {
        _pts = PossibleTimeSaveComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        _cmbPtsAccuracy = AddCombo("Accuracy", ["Seconds", "Tenths", "Hundredths"], _pts.Accuracy.ToString());
        _chkPtsDropDecimals = AddCheck("Drop Decimals When More Than 1 Minute", _pts.DropDecimals);
        _chkPtsTwoRows = AddCheck("Display 2 Rows", _pts.Display2Rows);
        _chkPtsTotal = AddCheck("Total Possible Time Save", _pts.TotalTimeSave);
        _cmbPtsComparison = AddCombo(
            "Comparison", ComparisonChoices.PossibleTimeSave(RunComparisons(), _pts.Comparison), _pts.Comparison);

        SettingsCard textColor = BeginGroup("Text Color");
        _chkPtsOverrideText = AddCheckTo(textColor, "Override Layout Settings", _pts.OverrideTextColor);
        _swPtsText = AddColorTo(textColor, "Color:", _pts.TextColor);
        WireEnable(_chkPtsOverrideText, _swPtsText);

        SettingsCard timeColor = BeginGroup("Time Color");
        _chkPtsOverrideTime = AddCheckTo(timeColor, "Override Layout Settings", _pts.OverrideTimeColor);
        _swPtsTime = AddColorTo(timeColor, "Color:", _pts.TimeColor);
        WireEnable(_chkPtsOverrideTime, _swPtsTime);

        _cmbPtsGradient = AddCombo("Background Gradient", ["Plain", "Vertical", "Horizontal"], _pts.BackgroundGradient);
        _swPtsBg1 = AddColor("Background Color", _pts.BackgroundColor);
        _swPtsBg2 = AddColor("Background Color 2", _pts.BackgroundColor2);
    }

    private void BuildBlankSpaceFields()
    {
        _blank = BlankSpaceComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        _numBlankHeight = AddNumber("Space Height", (decimal)_blank.SpaceHeight, 1, 1000);
        _numBlankWidth = AddNumber("Space Width", (decimal)_blank.SpaceWidth, 1, 1000);
        _cmbBlankGradient = AddCombo("Background Gradient", ["Plain", "Vertical", "Horizontal"], _blank.BackgroundGradient);
        _swBlankBg1 = AddColor("Background Color", _blank.BackgroundColor);
        _swBlankBg2 = AddColor("Background Color 2", _blank.BackgroundColor2);
    }

    private void BuildTextFields()
    {
        _text = TextComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        _txtText1 = AddText("Text / Label", _text.Text1);
        _txtText2 = AddText("Text2 / Variable Name", _text.Text2);
        _chkTextCustomVar = AddCheck("Custom Variable (Text2 is variable name)", _text.CustomVariable);
        _chkTextTwoRows = AddCheck("Display 2 Rows", _text.Display2Rows);

        SettingsCard textColor = BeginGroup("Text Color");
        _chkTextOverrideText = AddCheckTo(textColor, "Override Layout Settings", _text.OverrideTextColor);
        _swTextColor = AddColorTo(textColor, "Color:", _text.TextColor);
        WireEnable(_chkTextOverrideText, _swTextColor);

        SettingsCard timeColor = BeginGroup("Time Color");
        _chkTextOverrideTime = AddCheckTo(timeColor, "Override Layout Settings", _text.OverrideTimeColor);
        _swTextTime = AddColorTo(timeColor, "Color:", _text.TimeColor);
        WireEnable(_chkTextOverrideTime, _swTextTime);

        _cmbTextGradient = AddCombo("Background Gradient", ["Plain", "Vertical", "Horizontal"], _text.BackgroundGradient);
        _swTextBg1 = AddColor("Background Color", _text.BackgroundColor);
        _swTextBg2 = AddColor("Background Color 2", _text.BackgroundColor2);
    }

    /// <summary>
    /// Windows Columns group: Show Column Labels + Labels Color + Add Column + per-column GroupBox
    /// (Name / Column Type / Comparison / Timing Method / Move Up / Move Down / Remove Column).
    /// </summary>
    private void BuildColumnDesigner(SplitsComponentSettings splits, bool includeLabelsColor = false, ArgbColor? labelsColor = null)
    {
        _editColumns.Clear();
        _editColumns.AddRange((splits.Columns.Count > 0 ? splits.Columns : ColumnData.CreateDefault())
            .Select(c => c.Clone()));

        SettingsCard columnsGroup = BeginGroup("Columns");

        _chkShowColumnLabels = new CheckBox { Content = "Show Column Labels", IsChecked = splits.ShowColumnLabels };
        _chkShowColumnLabels.IsCheckedChanged += (_, _) => NotifyPreview();
        var addColumn = MakeDialogButton("Add Column", minWidth: 110);

        var labelsRow = new DockPanel { LastChildFill = false };
        labelsRow.Children.Add(_chkShowColumnLabels);
        DockPanel.SetDock(_chkShowColumnLabels, Dock.Left);
        if (includeLabelsColor)
        {
            _swLabelsColor = new ColorSwatchButton { ArgbColor = labelsColor ?? ArgbColor.FromArgb(255, 255, 255, 255) };
            _swLabelsColor.LivePreview = _ => NotifyPreview();
            var labelsColorRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(16, 0, 0, 0),
                Children =
                {
                    new TextBlock { Text = "Labels Color:", VerticalAlignment = VerticalAlignment.Center },
                    _swLabelsColor,
                },
            };
            labelsRow.Children.Add(labelsColorRow);
            DockPanel.SetDock(labelsColorRow, Dock.Left);
            WireEnable(_chkShowColumnLabels, _swLabelsColor);
        }

        DockPanel.SetDock(addColumn, Dock.Right);
        labelsRow.Children.Add(addColumn);
        columnsGroup.Children.Add(labelsRow);

        // Each column is a part of the Columns card (heading row, then its rows), like a
        // nested group box.
        _columnsHost = new SettingsCard { Classes = { "nestedCard" }, CornerRadiusValue = 0 };
        SettingsCard.SetFullBleed(_columnsHost, true);
        columnsGroup.Children.Add(_columnsHost);

        addColumn.Click += (_, _) =>
        {
            _editColumns.Add(new ColumnData("+/-", ColumnType.Delta, "Current Comparison", "Current Timing Method"));
            RebuildColumnCards();
            NotifyPreview();
        };

        RebuildColumnCards();
    }

    private void RebuildColumnCards()
    {
        if (_columnsHost is null)
        {
            return;
        }

        _columnsHost.Children.Clear();
        for (int i = 0; i < _editColumns.Count; i++)
        {
            _columnsHost.Children.Add(CreateColumnCard(i));
        }
    }

    private SettingsCard CreateColumnCard(int index)
    {
        ColumnData col = _editColumns[index];
        var title = new TextBlock
        {
            Text = string.Format(UiLocalizer.T("Column: {0}"), col.Name),
            Classes = { "subsectionTitle" },
            VerticalAlignment = VerticalAlignment.Center,
        };

        var nameBox = new TextBox { Text = col.Name, MinWidth = 220, Width = 280 };
        var typeBox = new ComboBox
        {
            ItemsSource = ColumnTypeLabels.ToList(),
            SelectedItem = ColumnTypeToLabel(col.Type),
            MinWidth = 220,
            Width = 280,
            Height = 26,
            MinHeight = 26,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Classes = { "settingsCombo" },
        };
        // Upstream ColumnSettings: the list depends on the column type (segment columns have no
        // Best Split Times) and is refilled when the type changes.
        string savedComparison = col.Comparison;
        var cmpBox = new ComboBox
        {
            ItemsSource = ComparisonChoices.Column(RunComparisons(), col.Type, ref savedComparison),
            MinWidth = 220,
            Width = 280,
            Height = 26,
            MinHeight = 26,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Classes = { "settingsCombo" },
        };
        cmpBox.SelectedItem = savedComparison;
        var timingBox = new ComboBox
        {
            ItemsSource = TimingMethodLabels.ToList(),
            SelectedItem = TimingMethodLabels.Contains(col.TimingMethod) ? col.TimingMethod : TimingMethodLabels[0],
            MinWidth = 220,
            Width = 280,
            Height = 26,
            MinHeight = 26,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            Classes = { "settingsCombo" },
        };

        void SyncTitle() => title.Text = string.Format(UiLocalizer.T("Column: {0}"), nameBox.Text ?? col.Name);

        void Persist()
        {
            col.Name = nameBox.Text?.Trim() ?? col.Name;
            col.Type = ParseColumnTypeLabel(typeBox.SelectedItem?.ToString());
            col.Comparison = cmpBox.SelectedItem?.ToString() ?? "Current Comparison";
            col.TimingMethod = timingBox.SelectedItem?.ToString() ?? "Current Timing Method";
            SyncTitle();
            NotifyPreview();
        }

        // Upstream ColumnSettings binds the name OnPropertyChanged: the column header follows typing.
        nameBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                Persist();
            }
        };
        typeBox.SelectionChanged += (_, _) =>
        {
            string comparison = cmpBox.SelectedItem?.ToString() ?? col.Comparison;
            cmpBox.ItemsSource = ComparisonChoices.Column(
                RunComparisons(), ParseColumnTypeLabel(typeBox.SelectedItem?.ToString()), ref comparison);
            cmpBox.SelectedItem = comparison;
            Persist();
        };
        cmpBox.SelectionChanged += (_, _) => Persist();
        timingBox.SelectionChanged += (_, _) => Persist();

        var moveUp = MakeDialogButton("Move Up", minWidth: 80, enabled: index > 0);
        var moveDown = MakeDialogButton("Move Down", minWidth: 90, enabled: index < _editColumns.Count - 1);
        var remove = MakeDialogButton("Remove Column", minWidth: 110, enabled: _editColumns.Count > 1);
        moveUp.Click += (_, _) =>
        {
            Persist();
            MoveColumnAt(index, -1);
        };
        moveDown.Click += (_, _) =>
        {
            Persist();
            MoveColumnAt(index, 1);
        };
        remove.Click += (_, _) =>
        {
            if (_editColumns.Count <= 1)
            {
                return;
            }

            _editColumns.RemoveAt(index);
            RebuildColumnCards();
            NotifyPreview();
        };

        var part = new SettingsCard
        {
            Classes = { "nestedCard" },
            CornerRadiusValue = 0,
            Children =
            {
                title,
                Labeled("Name:", nameBox),
                Labeled("Column Type:", typeBox),
                Labeled("Comparison:", cmpBox),
                Labeled("Timing Method:", timingBox),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { moveUp, moveDown, remove },
                },
            },
        };
        SettingsCard.SetFullBleed(part, true);
        return part;
    }

    private static string ColumnTypeToLabel(ColumnType type) => type switch
    {
        ColumnType.SplitTime => "Split Time",
        ColumnType.Delta => "Delta",
        ColumnType.DeltaorSplitTime => "Delta or Split Time",
        ColumnType.SegmentTime => "Segment Time",
        ColumnType.SegmentDelta => "Segment Delta",
        ColumnType.SegmentDeltaorSegmentTime => "Segment Delta or Segment Time",
        ColumnType.CustomVariable => "Custom Variable",
        _ => "Split Time",
    };

    private static ColumnType ParseColumnTypeLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return ColumnType.SplitTime;
        }

        string compact = label.Replace(" ", "", StringComparison.Ordinal);
        return Enum.TryParse(compact, ignoreCase: true, out ColumnType type) ? type : ColumnType.SplitTime;
    }

    /// <summary>
    /// Windows DetailedTimerSettings control names/options (practical Avalonia equivalents).
    /// Fonts: portable family/size/bold; Windows BF blobs partially mapped via WindowsFontBlobParser.
    /// </summary>
    private void BuildDetailedTimerFields()
    {
        _detailedTimer = DetailedTimerComponentSettings.FromInnerXml(_component.SettingsInnerXml);

        SettingsCard size = BeginGroup("Size");
        if (ComponentSettingsSizeAxisPolicy.For(_layoutMode) == ComponentSettingsSizeAxis.Width)
        {
            _numDetailedWidth = AddNumberTo(size, "Width:", (decimal)_detailedTimer.Width, 50, 500);
        }
        else
        {
            _numDetailedHeight = AddNumberTo(size, "Height:", (decimal)_detailedTimer.Height, 20, 150);
        }

        _numSegmentRatio = AddNumberTo(size, "Segment Timer Size:", (decimal)_detailedTimer.SegmentTimerSizeRatio, 10, 90);

        SettingsCard bg = BeginGroup("Background");
        _cmbDetailedGradient = AddComboTo(bg, "Background Color:",
            ["Plain", "Vertical", "Horizontal", "Plain With Delta Color", "Vertical With Delta Color", "Horizontal With Delta Color"],
            NormalizeGradientLabel(_detailedTimer.BackgroundGradient));
        _swDetailedBg1 = AddColorTo(bg, "Color 1:", _detailedTimer.BackgroundColor);
        _swDetailedBg2 = AddColorTo(bg, "Color 2:", _detailedTimer.BackgroundColor2);

        SettingsCard icon = BeginGroup("Icon");
        _chkDetailedDisplayIcon = AddCheckTo(icon, "Display Icon", _detailedTimer.DisplayIcon);
        _numDetailedIconSize = AddNumberTo(icon, "Size:", (decimal)_detailedTimer.IconSize, 8, 120);
        WireEnable(_chkDetailedDisplayIcon, _numDetailedIconSize);

        SettingsCard timer = BeginGroup("Timer");
        _cmbDetailedDigits = AddComboTo(timer, "Format:", ["1", "00:01", "0:00:01", "00:00:01"], _detailedTimer.DigitsFormat);
        _cmbDetailedAccuracy = AddComboTo(timer, "Accuracy:", ["", ".2", ".23", ".234"], DetailedTimerLogic.AccuracyToken(_detailedTimer.Accuracy));
        _numDetailedDecimals = AddNumberTo(timer, "Decimals Font Size:", (decimal)_detailedTimer.DecimalsSize, 10, 100);
        _chkDetailedTimerGradient = AddCheckTo(timer, "Show Gradient", _detailedTimer.TimerShowGradient);
        _chkDetailedOverrideColors = AddCheckTo(timer, "Override Layout Settings", _detailedTimer.OverrideTimerColors);
        _swDetailedTimerColor = AddColorTo(timer, "Color:", _detailedTimer.TimerColor);
        WireEnable(_chkDetailedOverrideColors, _swDetailedTimerColor);

        SettingsCard segTimer = BeginGroup("Segment Timer");
        _cmbDetailedSegDigits = AddComboTo(segTimer, "Format:", ["1", "00:01", "0:00:01", "00:00:01"], _detailedTimer.SegmentDigitsFormat);
        _cmbDetailedSegAccuracy = AddComboTo(segTimer, "Accuracy:", ["", ".2", ".23", ".234"], DetailedTimerLogic.AccuracyToken(_detailedTimer.SegmentAccuracy));
        _numDetailedSegDecimals = AddNumberTo(segTimer, "Decimals Font Size:", (decimal)_detailedTimer.SegmentTimerDecimalsSize, 10, 100);
        _chkDetailedSegGradient = AddCheckTo(segTimer, "Show Gradient", _detailedTimer.SegmentTimerShowGradient);
        _swDetailedSegmentColor = AddColorTo(segTimer, "Color:", _detailedTimer.SegmentTimerColor);

        _detailedLabelsFont = new(_detailedTimer.SegmentLabelsFontFamily, _detailedTimer.SegmentLabelsFontSizePx,
            _detailedTimer.SegmentLabelsFontBold, _detailedTimer.SegmentLabelsFontItalic);
        _detailedTimesFont = new(_detailedTimer.SegmentTimesFontFamily, _detailedTimer.SegmentTimesFontSizePx,
            _detailedTimer.SegmentTimesFontBold, _detailedTimer.SegmentTimesFontItalic);
        _detailedSplitFont = new(_detailedTimer.SplitNameFontFamily, _detailedTimer.SplitNameFontSizePx,
            _detailedTimer.SplitNameFontBold, _detailedTimer.SplitNameFontItalic);

        SettingsCard labels = BeginGroup("Segment Labels");
        _swDetailedLabelsColor = AddColorTo(labels, "Color:", _detailedTimer.SegmentLabelsColor);
        AddFontTo(labels, () => _detailedLabelsFont, font => _detailedLabelsFont = font);

        SettingsCard times = BeginGroup("Segment Times");
        _swDetailedTimesColor = AddColorTo(times, "Color:", _detailedTimer.SegmentTimesColor);
        AddFontTo(times, () => _detailedTimesFont, font => _detailedTimesFont = font);
        _cmbDetailedTimesAccuracy = AddComboTo(times, "Accuracy:",
            ["Seconds", "Tenths", "Hundredths", "Milliseconds"],
            _detailedTimer.SegmentTimesAccuracy.ToString());

        SettingsCard splitName = BeginGroup("Split Name");
        _chkDetailedShowSplitName = AddCheckTo(splitName, "Show Split Name", _detailedTimer.ShowSplitName);
        _swDetailedSplitColor = AddColorTo(splitName, "Split Name Color:", _detailedTimer.SplitNameColor);
        _rowDetailedSplitFont = AddFontTo(splitName, () => _detailedSplitFont, font => _detailedSplitFont = font);
        WireEnable(
            _chkDetailedShowSplitName,
            _swDetailedSplitColor,
            _rowDetailedSplitFont);

        SettingsCard comparisons = BeginGroup("Segment Time Comparisons");
        _cmbDetailedComparison = AddComboTo(
            comparisons,
            "Comparison 1:",
            ComparisonChoices.DetailedTimer(RunComparisons(), _detailedTimer.Comparison),
            _detailedTimer.Comparison);
        _cmbDetailedComparison2 = AddComboTo(
            comparisons,
            "Comparison 2:",
            ComparisonChoices.DetailedTimer(RunComparisons(), _detailedTimer.Comparison2),
            _detailedTimer.Comparison2);
        _chkDetailedHideComparison = AddCheckTo(comparisons, "Hide Comparison 2", _detailedTimer.HideComparison);
        WireEnableInverted(_chkDetailedHideComparison, _cmbDetailedComparison2);
        _cmbDetailedMethod = AddComboTo(comparisons, "Timing Method:",
            ["Current Timing Method", "Real Time", "Game Time"],
            _detailedTimer.TimingMethod);

        // Portable font fallback is silent — no developer/BinaryFormatter notes in the UI.
    }

    /// <summary>
    /// A group box of the page: a card under its heading (SettingsSection). A nested group is a
    /// part of its parent's card: its heading row, then its rows.
    /// </summary>
    private SettingsCard BeginGroup(string title, bool nested = false, Control? host = null)
    {
        if (nested && host is SettingsCard parent)
        {
            var part = new SettingsCard { Classes = { "nestedCard" }, CornerRadiusValue = 0 };
            SettingsCard.SetFullBleed(part, true);
            part.Children.Add(new TextBlock { Text = title, Classes = { "subsectionTitle" }, VerticalAlignment = VerticalAlignment.Center });
            parent.Children.Add(part);
            return part;
        }

        var card = new SettingsCard();
        var section = new SettingsSection { Header = title };
        section.Children.Add(card);
        (host is Panel panel ? panel.Children : _fields.Children).Add(section);
        return card;
    }

    /// <summary>
    /// Where a row goes: a group's card, or for the page itself the card of the rows outside any
    /// group (a new one after a group, so the order stays).
    /// </summary>
    private Controls Rows(Control? host)
    {
        switch (host)
        {
            case SettingsCard card:
                return card.Children;
            case null:
            case not null when ReferenceEquals(host, _fields):
                if (_fields.Children.LastOrDefault() is SettingsCard { Tag: { } tag } loose && ReferenceEquals(tag, LooseCard))
                {
                    return loose.Children;
                }

                var next = new SettingsCard { Tag = LooseCard };
                _fields.Children.Add(next);
                return next.Children;
            case Panel panel:
                return panel.Children;
            default:
                throw new ArgumentException("Not a row host: " + host.GetType().Name, nameof(host));
        }
    }

    private ComboBox AddAccuracyRadios(Control host, TimeAccuracy selected)
    {
        // ComboBox stores selection for Apply(); radios drive it for WinForms-like density.
        // Tag = wrapper so WireEnable greys out the whole radio row.
        var wrap = new StackPanel { Spacing = 2 };
        var combo = new ComboBox
        {
            ItemsSource = AccuracyLabels.ToList(),
            SelectedItem = AccuracyLabels.Contains(selected.ToString()) ? selected.ToString() : "Tenths",
            Width = 140,
            IsVisible = false,
            Height = 0,
            Tag = wrap,
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        string group = Guid.NewGuid().ToString("N");
        foreach (string label in AccuracyLabels)
        {
            var radio = new RadioButton
            {
                Content = label,
                GroupName = group,
                IsChecked = string.Equals(combo.SelectedItem?.ToString(), label, StringComparison.Ordinal),
            };
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked == true)
                {
                    combo.SelectedItem = label;
                    // The combo is hidden and has no handler of its own: preview here.
                    NotifyPreview();
                }
            };
            row.Children.Add(radio);
        }

        wrap.Children.Add(combo);
        wrap.Children.Add(row);
        Rows(host).Add(wrap);
        return combo;
    }

    private static string NormalizeGradientLabel(string value) =>
        value switch
        {
            "PlainWithDeltaColor" => "Plain With Delta Color",
            "VerticalWithDeltaColor" => "Vertical With Delta Color",
            "HorizontalWithDeltaColor" => "Horizontal With Delta Color",
            _ => string.IsNullOrWhiteSpace(value) ? "Plain" : value,
        };

    private static string GradientStorage(string? label) =>
        (label ?? "Plain").Replace(" ", "", StringComparison.Ordinal);

    /// <summary>
    /// Shared dialog button chrome (<c>Button.dialog</c> / WinFormsButtonTheme).
    /// Theme brushes only — no hardcoded light faces (Dark Appearance must stay readable).
    /// Padding: optical vertical center (more top) so Helvetica/Segoe caps aren’t high.
    /// </summary>
    private static Button MakeDialogButton(string content, double minWidth = 75, bool enabled = true)
    {
        return new Button
        {
            Content = content,
            MinWidth = minWidth,
            MinHeight = 28,
            Padding = new Avalonia.Thickness(10, 5, 10, 1),
            FontSize = 12,
            IsEnabled = enabled,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new Avalonia.CornerRadius(6),
            Margin = new Avalonia.Thickness(0, 2, 0, 2),
            Classes = { "dialog" },
        };
    }

    private static void WireEnable(CheckBox? gate, params Control?[] targets)
    {
        if (gate is null)
        {
            return;
        }

        void Apply()
        {
            bool on = gate.IsChecked == true;
            foreach (Control? t in targets)
            {
                SetEnabled(t, on);
            }
        }

        gate.IsCheckedChanged += (_, _) => Apply();
        Apply();
    }

    private static void SetEnabled(Control? control, bool on)
    {
        if (control is null)
        {
            return;
        }

        if (control is ComboBox { Tag: Control wrap })
        {
            wrap.IsEnabled = on;
        }

        control.IsEnabled = on;
    }

    private static void WireEnableInverted(CheckBox? gate, params Control?[] targets)
    {
        if (gate is null)
        {
            return;
        }

        void Apply()
        {
            bool on = gate.IsChecked != true;
            foreach (Control? t in targets)
            {
                SetEnabled(t, on);
            }
        }

        gate.IsCheckedChanged += (_, _) => Apply();
        Apply();
    }

    private void MoveColumnAt(int index, int delta)
    {
        int j = index + delta;
        if (index < 0 || j < 0 || j >= _editColumns.Count)
        {
            return;
        }

        (_editColumns[index], _editColumns[j]) = (_editColumns[j], _editColumns[index]);
        RebuildColumnCards();
        NotifyPreview();
    }

    private CheckBox AddCheck(string label, bool value) => AddCheckTo(_fields, label, value);

    private CheckBox AddCheckTo(Control host, string label, bool value)
    {
        var box = new CheckBox { Content = label, IsChecked = value };
        box.IsCheckedChanged += (_, _) => NotifyPreview();
        Rows(host).Add(box);
        return box;
    }

    private ComboBox AddCombo(string label, IEnumerable<string> items, string selected) =>
        AddComboTo(_fields, label, items, selected);

    private ComboBox AddComboTo(Control host, string label, IEnumerable<string> items, string selected)
    {
        var list = items.ToList();
        if (!string.IsNullOrEmpty(selected) && !list.Contains(selected))
        {
            list.Insert(0, selected);
        }

        var combo = new ComboBox
        {
            ItemsSource = list,
            Width = 220,
            Height = 26,
            MinHeight = 26,
            MaxHeight = 26,
            Padding = new Avalonia.Thickness(8, 0, 4, 0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "settingsCombo" },
        };
        combo.SelectedItem = list.Contains(selected) ? selected : list.FirstOrDefault();
        combo.SelectionChanged += (_, _) => NotifyPreview();
        Rows(host).Add(Labeled(label, combo));
        return combo;
    }

    private NumericUpDown AddNumber(string label, decimal value, decimal min, decimal max) =>
        AddNumberTo(_fields, label, value, min, max);

    /// <summary>
    /// Shared numeric row: fixed label column + fixed-width Avalonia NumericUpDown
    /// (same vertical axis for Height / Width / Segment Timer Size — no per-field offsets).
    /// </summary>
    /// <summary>
    /// SplitsSettings "Split Height:" (vertical mode): the space added to each 25 high row, 0-25
    /// in steps of 0.1 (Windows' trackbar 0-250 over ScaledSplitHeight = SplitHeight x 10).
    /// </summary>
    private NumericUpDown AddSplitHeight(float value)
    {
        NumericUpDown num = AddNumber("Split Height", Math.Round((decimal)value, 1), 0, 25);
        num.Increment = 0.1m;
        num.FormatString = "0.0";
        return num;
    }

    private NumericUpDown AddNumberTo(Control host, string label, decimal value, decimal min, decimal max)
    {
        var num = CreateSettingsNumericUpDown(value, min, max);
        // Preview only on valid values — ignore mid-typing empty/partial.
        num.ValueChanged += (_, _) =>
        {
            if (num.Value is not null)
            {
                NotifyPreview();
            }
        };
        Rows(host).Add(Labeled(label, num));
        return num;
    }

    /// <summary>
    /// Dialog NumericUpDown — SettingsNumericUpDownTheme (one compact spinner unit).
    /// Visual chrome lives in Themes/WinFormsControls.axaml; do not “fix” look via layout only.
    /// Always editable text (click/type/select/paste/undo) — never arrow-only unless caller sets IsReadOnly.
    /// </summary>
    /// <summary>
    /// A component DLL: its own settings page (IComponent.GetSettingsControl), shown with the
    /// app's controls; a component without one shows nothing (Windows shows no tab).
    /// </summary>
    private void BuildHostedFields(HostedLayoutComponent hosted)
    {
        if (hosted.Component.GetSettingsControl(_layoutMode) is not { } page)
        {
            return;
        }

        // The page fills the window's 600 less the page padding, as the app's own cards do.
        HostedSettingsView view;
        try
        {
            view = new HostedSettingsView(page, 600 - PagePadding.Left - PagePadding.Right) { HorizontalAlignment = HorizontalAlignment.Left };
        }
        catch (Exception ex)
        {
            // A component whose page fails to show (an error in its code): said so, instead of
            // closing the app.
            Console.Error.WriteLine("[component] settings page: " + ex);
            _fields.Children.Add(new TextBlock
            {
                Text = "This component's settings could not be shown: " + ex.GetBaseException().Message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 600 - PagePadding.Left - PagePadding.Right,
            });
            return;
        }

        // Without the page's own margins: the window's padding is the margin.
        view.Margin = new Avalonia.Thickness(-view.Inset.Left, -view.Inset.Top, -view.Inset.Right, 0);
        view.Edited += NotifyPreview;
        _fields.Children.Add(view);
        HostedPageWidth = view.Width - view.Inset.Left - view.Inset.Right;
    }

    internal static NumericUpDown CreateSettingsNumericUpDown(decimal value, decimal min, decimal max, decimal increment = 1)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            Increment = increment,
            // Fixed outer size so steppers share one vertical axis across rows.
            Width = SettingsNumericWidth,
            MinWidth = SettingsNumericWidth,
            MaxWidth = SettingsNumericWidth,
            Height = SettingsNumericHeight,
            MinHeight = SettingsNumericHeight,
            MaxHeight = SettingsNumericHeight,
            Padding = new Avalonia.Thickness(8, 0, 6, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Center,
            ShowButtonSpinner = true,
            AllowSpin = true,
            // Clip on commit/spin — do not block intermediate empty/partial typing.
            ClipValueToMinMax = true,
            IsReadOnly = false,
            Focusable = true,
            FormatString = "0",
            Classes = { "settingsNumeric" },
        };
    }

    // Wide enough for "Segment Timer Size:" / "Decimals Font Size:"; Grid pins control column.
    private const double SettingsLabelColumnWidth = 212;
    private const double SettingsNumericWidth = 104;
    private const double SettingsNumericHeight = 26;

    private TextBox AddText(string label, string value)
    {
        var box = new TextBox { Text = value, Width = 220 };
        // Live while typing, as upstream's text settings (DataSourceUpdateMode.OnPropertyChanged).
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                NotifyPreview();
            }
        };
        Rows(_fields).Add(Labeled(label, box));
        return box;
    }

    private ColorSwatchButton AddColor(string label, ArgbColor value) => AddColorTo(_fields, label, value);

    /// <summary>
    /// DetailedTimerSettings' font rows: the font's name and style, and Choose... opening the
    /// font dialog (SettingsHelper.GetFontDialog(font, 7, 26): every installed font, sizes 7-26,
    /// Bold / Italic). Changes preview on the timer; the dialog's Cancel puts the font back.
    /// </summary>
    private Control AddFontTo(Control host, Func<LayoutFontChoice> get, Action<LayoutFontChoice> set)
    {
        var name = new TextBlock
        {
            Text = get().Describe(),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Width = 220,
        };
        Button choose = MakeDialogButton("Choose...");
        choose.Margin = new Avalonia.Thickness(8, 2, 0, 2);
        choose.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            var dialog = new FontDialogWindow(get(), 7, 26);
            dialog.FontChanged += font =>
            {
                set(font);
                name.Text = font.Describe();
                NotifyPreview();
            };
            await OwnedDialog.ShowAsync<bool?>(owner, dialog);
            name.Text = get().Describe();
        };
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { name, choose },
        };
        Rows(host).Add(Labeled("Font:", row));
        return row;
    }

    private ColorSwatchButton AddColorTo(Control host, string label, ArgbColor value)
    {
        var swatch = new ColorSwatchButton { ArgbColor = value };
        swatch.LivePreview = _ => NotifyPreview();
        Rows(host).Add(Labeled(label, swatch));
        return swatch;
    }

    /// <summary>A labelled row: the label in the page's label column, then the control.</summary>
    private static SettingsField Labeled(string label, Control control) =>
        new() { Label = label, Field = control };

    private void ApplyToComponent()
    {
        // A component DLL's settings page changes the component's settings itself.
        if (_component.RuntimeState is HostedLayoutComponent hosted)
        {
            _component.SettingsInnerXml = hosted.ReadSettings();
            return;
        }

        if (_title is not null)
        {
            _title.ShowGameName = _chkGameName?.IsChecked == true;
            _title.ShowCategoryName = _chkCategoryName?.IsChecked == true;
            _title.ShowAttemptCount = _chkAttemptCount?.IsChecked == true;
            _title.ShowFinishedRunsCount = _chkFinishedRuns?.IsChecked == true;
            _title.SingleLine = _chkSingleLine?.IsChecked == true;
            _title.DisplayGameIcon = _chkDisplayIcon?.IsChecked == true;
            _title.ShowRegion = _chkShowRegion?.IsChecked == true;
            _title.ShowPlatform = _chkShowPlatform?.IsChecked == true;
            _title.ShowVariables = _chkShowVariables?.IsChecked == true;
            _title.OverrideTitleColor = _chkTitleOverrideColor?.IsChecked == true;
            _title.BackgroundGradient = _cmbTitleGradient?.SelectedItem?.ToString() ?? "Vertical";
            _title.TextAlignment = (_cmbTitleAlignment?.SelectedItem?.ToString()) switch
            {
                "Center" => 1,
                "Right" => 2,
                _ => 0,
            };
            if (_swTitleColor is not null) _title.TitleColor = _swTitleColor.ArgbColor;
            if (_swTitleBg1 is not null) _title.BackgroundColor = _swTitleBg1.ArgbColor;
            if (_swTitleBg2 is not null) _title.BackgroundColor2 = _swTitleBg2.ArgbColor;
            _component.SettingsInnerXml = _title.ToInnerXml();
        }
        else if (_splits is not null)
        {
            _splits.VisualSplitCount = (int)(_numVisualSplits?.Value ?? 16);
            _splits.SplitPreviewCount = (int)(_numPreview?.Value ?? 1);
            if (_numSplitSize?.Value is { } splitsHeight)
            {
                _splits.SplitHeight = (float)splitsHeight;
            }
            _splits.ShowThinSeparators = _chkThinSep?.IsChecked == true;
            _splits.SeparatorLastSplit = _chkSepLast?.IsChecked == true;
            _splits.AlwaysShowLastSplit = _chkAlwaysLast?.IsChecked == true;
            _splits.LockLastSplit = _chkLockLast?.IsChecked == true;
            _splits.Display2Rows = _chkDisplay2Rows?.IsChecked == true;
            _splits.ShowSplitTimes = _chkShowSplitTimes?.IsChecked == true;
            _splits.DropDecimals = _chkDropDecimalsSplits?.IsChecked == true;
            _splits.DisplayIcons = _chkDisplayIcons?.IsChecked == true;
            _splits.IconShadows = _chkIconShadows?.IsChecked == true;
            _splits.AutomaticAbbreviations = _chkAutoAbbrev?.IsChecked == true;
            _splits.IconSize = (float)(_numIconSize?.Value ?? 24);
            _splits.ShowColumnLabels = _chkShowColumnLabels?.IsChecked == true;
            if (_swLabelsColor is not null) _splits.LabelsColor = _swLabelsColor.ArgbColor;
            if (Enum.TryParse(_cmbSplitAccuracy?.SelectedItem?.ToString(), out TimeAccuracy sa))
            {
                _splits.SplitTimesAccuracy = sa;
            }

            if (Enum.TryParse(_cmbDeltaAccuracy?.SelectedItem?.ToString(), out TimeAccuracy da))
            {
                _splits.DeltasAccuracy = da;
            }

            _splits.OverrideTextColor = _chkOverrideNamesColor?.IsChecked == true;
            if (_swBeforeNames is not null) _splits.BeforeNamesColor = _swBeforeNames.ArgbColor;
            if (_swCurrentNames is not null) _splits.CurrentNamesColor = _swCurrentNames.ArgbColor;
            if (_swAfterNames is not null) _splits.AfterNamesColor = _swAfterNames.ArgbColor;
            _splits.OverrideTimesColor = _chkOverrideTimesColor?.IsChecked == true;
            if (_swBeforeTimes is not null) _splits.BeforeTimesColor = _swBeforeTimes.ArgbColor;
            if (_swCurrentTimes is not null) _splits.CurrentTimesColor = _swCurrentTimes.ArgbColor;
            if (_swAfterTimes is not null) _splits.AfterTimesColor = _swAfterTimes.ArgbColor;
            _splits.OverrideDeltasColor = _chkOverrideDeltasColor?.IsChecked == true;
            if (_swDeltasColor is not null) _splits.DeltasColor = _swDeltasColor.ArgbColor;
            if (_swBg1 is not null) _splits.BackgroundColor = _swBg1.ArgbColor;
            if (_swBg2 is not null) _splits.BackgroundColor2 = _swBg2.ArgbColor;
            _splits.BackgroundGradient = _cmbBgGradient?.SelectedItem?.ToString() ?? "Alternating";
            if (_swSplitTop is not null) _splits.CurrentSplitTopColor = _swSplitTop.ArgbColor;
            if (_swSplitBottom is not null) _splits.CurrentSplitBottomColor = _swSplitBottom.ArgbColor;
            _splits.CurrentSplitGradient = _cmbCurrentSplitGradient?.SelectedItem?.ToString() ?? "Vertical";
            _splits.Columns.Clear();
            _splits.Columns.AddRange(_editColumns.Select(c => c.Clone()));
            _component.SettingsInnerXml = _splits.ToInnerXml();
        }
        else if (_subsplits is not null)
        {
            _subsplits.VisualSplitCount = (int)(_numVisualSplits?.Value ?? 16);
            _subsplits.SplitPreviewCount = (int)(_numPreview?.Value ?? 1);
            if (_numSplitSize?.Value is { } subsplitsSize)
            {
                if (ComponentSettingsSizeAxisPolicy.For(_layoutMode) == ComponentSettingsSizeAxis.Width)
                {
                    _subsplits.SplitWidth = (float)subsplitsSize;
                }
                else
                {
                    _subsplits.SplitHeight = (float)subsplitsSize;
                }
            }
            _subsplits.MinimumMajorSplits = (int)(_numMinMajorSplits?.Value ?? 0);
            _subsplits.ShowThinSeparators = _chkThinSep?.IsChecked == true;
            _subsplits.SeparatorLastSplit = _chkSepLast?.IsChecked == true;
            _subsplits.AlwaysShowLastSplit = _chkAlwaysLast?.IsChecked == true;
            _subsplits.LockLastSplit = _chkLockLast?.IsChecked == true;
            _subsplits.Display2Rows = _chkDisplay2Rows?.IsChecked == true;
            _subsplits.DropDecimals = _chkDropDecimalsSplits?.IsChecked == true;
            _subsplits.DisplayIcons = _chkDisplayIcons?.IsChecked == true;
            _subsplits.IconShadows = _chkIconShadows?.IsChecked == true;
            _subsplits.IndentBlankIcons = _chkIndentBlankIcons?.IsChecked == true;
            _subsplits.IconSize = (float)(_numIconSize?.Value ?? 24);
            _subsplits.ShowColumnLabels = _chkShowColumnLabels?.IsChecked == true;
            if (_swLabelsColor is not null) _subsplits.LabelsColor = _swLabelsColor.ArgbColor;
            _subsplits.IndentSubsplits = _chkIndentSubsplits?.IsChecked == true;
            _subsplits.ShowSectionHeader = _chkShowSectionHeader?.IsChecked == true;
            _subsplits.OnlyCurrentSection = _chkOnlyCurrentSection?.IsChecked == true;
            _subsplits.AutomaticAbbreviation = _chkAutoAbbrev?.IsChecked == true;
            if (Enum.TryParse(_cmbSubsplitVisibility?.SelectedItem?.ToString(), out SubsplitsComponentSettings.SubsplitVisibility vis))
            {
                _subsplits.Visibility = vis;
            }

            if (Enum.TryParse(_cmbSplitAccuracy?.SelectedItem?.ToString(), out TimeAccuracy sa))
            {
                _subsplits.SplitTimesAccuracy = sa;
            }

            if (Enum.TryParse(_cmbDeltaAccuracy?.SelectedItem?.ToString(), out TimeAccuracy da))
            {
                _subsplits.DeltasAccuracy = da;
            }

            if (Enum.TryParse(_cmbHeaderAccuracy?.SelectedItem?.ToString(), out TimeAccuracy ha))
            {
                _subsplits.HeaderAccuracy = ha;
            }

            if (Enum.TryParse(_cmbSectionTimerAccuracy?.SelectedItem?.ToString(), out TimeAccuracy sta))
            {
                _subsplits.SectionTimerAccuracy = sta;
            }

            if (_swBg1 is not null) _subsplits.BackgroundColor = _swBg1.ArgbColor;
            if (_swBg2 is not null) _subsplits.BackgroundColor2 = _swBg2.ArgbColor;
            _subsplits.BackgroundGradient = _cmbBgGradient?.SelectedItem?.ToString() ?? "Alternating";
            if (_swSplitTop is not null) _subsplits.CurrentSplitTopColor = _swSplitTop.ArgbColor;
            if (_swSplitBottom is not null) _subsplits.CurrentSplitBottomColor = _swSplitBottom.ArgbColor;
            _subsplits.CurrentSplitGradient = _cmbCurrentSplitGradient?.SelectedItem?.ToString() ?? "Vertical";
            _subsplits.OverrideTextColor = _chkOverrideNamesColor?.IsChecked == true;
            if (_swBeforeNames is not null) _subsplits.BeforeNamesColor = _swBeforeNames.ArgbColor;
            if (_swCurrentNames is not null) _subsplits.CurrentNamesColor = _swCurrentNames.ArgbColor;
            if (_swAfterNames is not null) _subsplits.AfterNamesColor = _swAfterNames.ArgbColor;
            _subsplits.OverrideTimesColor = _chkOverrideTimesColor?.IsChecked == true;
            if (_swBeforeTimes is not null) _subsplits.BeforeTimesColor = _swBeforeTimes.ArgbColor;
            if (_swCurrentTimes is not null) _subsplits.CurrentTimesColor = _swCurrentTimes.ArgbColor;
            if (_swAfterTimes is not null) _subsplits.AfterTimesColor = _swAfterTimes.ArgbColor;
            _subsplits.OverrideDeltasColor = _chkOverrideDeltasColor?.IsChecked == true;
            if (_swDeltasColor is not null) _subsplits.DeltasColor = _swDeltasColor.ArgbColor;
            _subsplits.OverrideSubsplitColor = _chkOverrideSubsplitColor?.IsChecked == true;
            if (_swSubsplitTop is not null) _subsplits.SubsplitTopColor = _swSubsplitTop.ArgbColor;
            if (_swSubsplitBottom is not null) _subsplits.SubsplitBottomColor = _swSubsplitBottom.ArgbColor;
            _subsplits.SubsplitGradient = _cmbSubsplitGradient?.SelectedItem?.ToString() ?? "Plain";
            _subsplits.IndentSectionSplit = _chkIndentSectionSplit?.IsChecked == true;
            _subsplits.ShowIconSectionSplit = _chkShowIconSectionSplit?.IsChecked == true;
            _subsplits.ShowSectionIcon = _chkShowSectionIcon?.IsChecked == true;
            _subsplits.HeaderComparison = _cmbHeaderComparison?.SelectedItem?.ToString() ?? "Current Comparison";
            _subsplits.HeaderTimingMethod = _cmbHeaderTiming?.SelectedItem?.ToString() ?? "Current Timing Method";
            if (_swHeaderTop is not null) _subsplits.HeaderTopColor = _swHeaderTop.ArgbColor;
            if (_swHeaderBottom is not null) _subsplits.HeaderBottomColor = _swHeaderBottom.ArgbColor;
            _subsplits.HeaderGradient = _cmbHeaderGradient?.SelectedItem?.ToString() ?? "Vertical";
            _subsplits.OverrideHeaderColor = _chkOverrideHeaderColor?.IsChecked == true;
            if (_swHeaderText is not null) _subsplits.HeaderTextColor = _swHeaderText.ArgbColor;
            _subsplits.HeaderText = _chkHeaderText?.IsChecked == true;
            if (_swHeaderTimes is not null) _subsplits.HeaderTimesColor = _swHeaderTimes.ArgbColor;
            _subsplits.HeaderTimes = _chkHeaderTimes?.IsChecked == true;
            _subsplits.SectionTimer = _chkSectionTimer?.IsChecked == true;
            if (_swSectionTimerColor is not null) _subsplits.SectionTimerColor = _swSectionTimerColor.ArgbColor;
            _subsplits.SectionTimerGradient = _chkSectionTimerGradient?.IsChecked == true;
            _subsplits.Columns.Clear();
            _subsplits.Columns.AddRange(_editColumns.Select(c => c.Clone()));
            _component.SettingsInnerXml = _subsplits.ToInnerXml();
        }
        else if (_timer is not null)
        {
            if (_numTimerHeight?.Value is { } timerHeight)
            {
                _timer.TimerHeight = (float)timerHeight;
            }

            if (_numTimerWidth?.Value is { } timerWidth)
            {
                _timer.TimerWidth = (float)timerWidth;
            }

            _timer.DecimalsSize = (float)(_numDecimalsSize?.Value ?? 35);
            _timer.ShowGradient = _chkShowGradient?.IsChecked == true;
            _timer.CenterTimer = _chkCenterTimer?.IsChecked == true;
            _timer.OverrideSplitColors = _chkOverrideColors?.IsChecked == true;
            if (_swTimerColor is not null) _timer.TimerColor = _swTimerColor.ArgbColor;
            if (_swTimerBg1 is not null) _timer.BackgroundColor = _swTimerBg1.ArgbColor;
            if (_swTimerBg2 is not null) _timer.BackgroundColor2 = _swTimerBg2.ArgbColor;
            _timer.BackgroundGradient = _cmbTimerGradient?.SelectedItem?.ToString() ?? "Plain";
            _timer.DigitsFormat = _cmbTimerDigits?.SelectedItem?.ToString() ?? "1";
            if (Enum.TryParse(_cmbTimerAccuracy?.SelectedItem?.ToString(), out TimeAccuracy ta))
            {
                _timer.TimerAccuracy = ta;
            }

            _timer.TimingMethod = _cmbTimerMethod?.SelectedItem?.ToString() ?? "Current Timing Method";
            _component.SettingsInnerXml = _timer.ToInnerXml();
        }
        else if (_detailedTimer is not null)
        {
            if (_numDetailedHeight?.Value is { } detailedHeight)
            {
                _detailedTimer.Height = (float)detailedHeight;
            }

            if (_numDetailedWidth?.Value is { } detailedWidth)
            {
                _detailedTimer.Width = (float)detailedWidth;
            }

            _detailedTimer.SegmentTimerSizeRatio = (float)(_numSegmentRatio?.Value ?? 40);
            _detailedTimer.BackgroundGradient = GradientStorage(_cmbDetailedGradient?.SelectedItem?.ToString());
            if (_swDetailedBg1 is not null) _detailedTimer.BackgroundColor = _swDetailedBg1.ArgbColor;
            if (_swDetailedBg2 is not null) _detailedTimer.BackgroundColor2 = _swDetailedBg2.ArgbColor;
            _detailedTimer.DisplayIcon = _chkDetailedDisplayIcon?.IsChecked == true;
            _detailedTimer.IconSize = (float)(_numDetailedIconSize?.Value ?? 40);
            _detailedTimer.DigitsFormat = _cmbDetailedDigits?.SelectedItem?.ToString() ?? "1";
            _detailedTimer.Accuracy = DetailedTimerLogic.ParseAccuracyToken(_cmbDetailedAccuracy?.SelectedItem?.ToString());
            _detailedTimer.DecimalsSize = (float)(_numDetailedDecimals?.Value ?? 35);
            _detailedTimer.TimerShowGradient = _chkDetailedTimerGradient?.IsChecked == true;
            _detailedTimer.OverrideTimerColors = _chkDetailedOverrideColors?.IsChecked == true;
            if (_swDetailedTimerColor is not null) _detailedTimer.TimerColor = _swDetailedTimerColor.ArgbColor;
            _detailedTimer.SegmentDigitsFormat = _cmbDetailedSegDigits?.SelectedItem?.ToString() ?? "1";
            _detailedTimer.SegmentAccuracy = DetailedTimerLogic.ParseAccuracyToken(_cmbDetailedSegAccuracy?.SelectedItem?.ToString());
            _detailedTimer.SegmentTimerDecimalsSize = (float)(_numDetailedSegDecimals?.Value ?? 35);
            _detailedTimer.SegmentTimerShowGradient = _chkDetailedSegGradient?.IsChecked == true;
            if (_swDetailedSegmentColor is not null) _detailedTimer.SegmentTimerColor = _swDetailedSegmentColor.ArgbColor;
            if (_swDetailedLabelsColor is not null) _detailedTimer.SegmentLabelsColor = _swDetailedLabelsColor.ArgbColor;
            if (_swDetailedTimesColor is not null) _detailedTimer.SegmentTimesColor = _swDetailedTimesColor.ArgbColor;
            if (Enum.TryParse(_cmbDetailedTimesAccuracy?.SelectedItem?.ToString(), out TimeAccuracy sta))
            {
                _detailedTimer.SegmentTimesAccuracy = sta;
            }

            _detailedTimer.ShowSplitName = _chkDetailedShowSplitName?.IsChecked == true;
            if (_swDetailedSplitColor is not null) _detailedTimer.SplitNameColor = _swDetailedSplitColor.ArgbColor;
            (_detailedTimer.SegmentLabelsFontFamily, _detailedTimer.SegmentLabelsFontSizePx,
                _detailedTimer.SegmentLabelsFontBold, _detailedTimer.SegmentLabelsFontItalic) =
                (_detailedLabelsFont.Family, _detailedLabelsFont.Size, _detailedLabelsFont.Bold, _detailedLabelsFont.Italic);
            (_detailedTimer.SegmentTimesFontFamily, _detailedTimer.SegmentTimesFontSizePx,
                _detailedTimer.SegmentTimesFontBold, _detailedTimer.SegmentTimesFontItalic) =
                (_detailedTimesFont.Family, _detailedTimesFont.Size, _detailedTimesFont.Bold, _detailedTimesFont.Italic);
            (_detailedTimer.SplitNameFontFamily, _detailedTimer.SplitNameFontSizePx,
                _detailedTimer.SplitNameFontBold, _detailedTimer.SplitNameFontItalic) =
                (_detailedSplitFont.Family, _detailedSplitFont.Size, _detailedSplitFont.Bold, _detailedSplitFont.Italic);
            _detailedTimer.Comparison = _cmbDetailedComparison?.SelectedItem?.ToString() ?? "Current Comparison";
            _detailedTimer.Comparison2 = _cmbDetailedComparison2?.SelectedItem?.ToString() ?? "Best Segments";
            _detailedTimer.HideComparison = _chkDetailedHideComparison?.IsChecked == true;
            _detailedTimer.TimingMethod = _cmbDetailedMethod?.SelectedItem?.ToString() ?? "Current Timing Method";
            _component.SettingsInnerXml = _detailedTimer.ToInnerXml();
        }
        else if (_previous is not null)
        {
            if (Enum.TryParse(_cmbPrevDeltaAccuracy?.SelectedItem?.ToString(), out TimeAccuracy pa))
            {
                _previous.DeltaAccuracy = pa;
            }

            _previous.DropDecimals = _chkPrevDropDecimals?.IsChecked == true;
            _previous.Display2Rows = _chkPrevTwoRows?.IsChecked == true;
            _previous.ShowPossibleTimeSave = _chkPrevShowPts?.IsChecked == true;
            if (Enum.TryParse(_cmbPrevTimeSaveAccuracy?.SelectedItem?.ToString(), out TimeAccuracy tsa))
            {
                _previous.TimeSaveAccuracy = tsa;
            }

            _previous.Comparison = _cmbPrevComparison?.SelectedItem?.ToString() ?? "Current Comparison";
            _previous.OverrideTextColor = _chkPrevOverrideText?.IsChecked == true;
            if (_swPrevText is not null) _previous.TextColor = _swPrevText.ArgbColor;
            if (_swPrevBg1 is not null) _previous.BackgroundColor = _swPrevBg1.ArgbColor;
            if (_swPrevBg2 is not null) _previous.BackgroundColor2 = _swPrevBg2.ArgbColor;
            _previous.BackgroundGradient = _cmbPrevGradient?.SelectedItem?.ToString() ?? "Vertical";
            _component.SettingsInnerXml = _previous.ToInnerXml();
        }
        else if (_sumOfBest is not null)
        {
            if (Enum.TryParse(_cmbSobAccuracy?.SelectedItem?.ToString(), out TimeAccuracy sa))
            {
                _sumOfBest.Accuracy = sa;
            }

            _sumOfBest.Display2Rows = _chkSobTwoRows?.IsChecked == true;
            _sumOfBest.OverrideTextColor = _chkSobOverrideText?.IsChecked == true;
            _sumOfBest.OverrideTimeColor = _chkSobOverrideTime?.IsChecked == true;
            if (_swSobText is not null) _sumOfBest.TextColor = _swSobText.ArgbColor;
            if (_swSobTime is not null) _sumOfBest.TimeColor = _swSobTime.ArgbColor;
            _sumOfBest.BackgroundGradient = _cmbSobGradient?.SelectedItem?.ToString() ?? "Plain";
            if (_swSobBg1 is not null) _sumOfBest.BackgroundColor = _swSobBg1.ArgbColor;
            if (_swSobBg2 is not null) _sumOfBest.BackgroundColor2 = _swSobBg2.ArgbColor;
            _component.SettingsInnerXml = _sumOfBest.ToInnerXml();
        }
        else if (_pts is not null)
        {
            if (Enum.TryParse(_cmbPtsAccuracy?.SelectedItem?.ToString(), out TimeAccuracy pa))
            {
                _pts.Accuracy = pa;
            }

            _pts.DropDecimals = _chkPtsDropDecimals?.IsChecked == true;
            _pts.Display2Rows = _chkPtsTwoRows?.IsChecked == true;
            _pts.TotalTimeSave = _chkPtsTotal?.IsChecked == true;
            _pts.Comparison = _cmbPtsComparison?.SelectedItem?.ToString() ?? "Current Comparison";
            _pts.OverrideTextColor = _chkPtsOverrideText?.IsChecked == true;
            _pts.OverrideTimeColor = _chkPtsOverrideTime?.IsChecked == true;
            if (_swPtsText is not null) _pts.TextColor = _swPtsText.ArgbColor;
            if (_swPtsTime is not null) _pts.TimeColor = _swPtsTime.ArgbColor;
            _pts.BackgroundGradient = _cmbPtsGradient?.SelectedItem?.ToString() ?? "Plain";
            if (_swPtsBg1 is not null) _pts.BackgroundColor = _swPtsBg1.ArgbColor;
            if (_swPtsBg2 is not null) _pts.BackgroundColor2 = _swPtsBg2.ArgbColor;
            _component.SettingsInnerXml = _pts.ToInnerXml();
        }
        else if (_blank is not null)
        {
            _blank.SpaceHeight = (float)(_numBlankHeight?.Value ?? 24);
            _blank.SpaceWidth = (float)(_numBlankWidth?.Value ?? 24);
            _blank.BackgroundGradient = _cmbBlankGradient?.SelectedItem?.ToString() ?? "Plain";
            if (_swBlankBg1 is not null) _blank.BackgroundColor = _swBlankBg1.ArgbColor;
            if (_swBlankBg2 is not null) _blank.BackgroundColor2 = _swBlankBg2.ArgbColor;
            _component.SettingsInnerXml = _blank.ToInnerXml();
        }
        else if (_text is not null)
        {
            _text.Text1 = _txtText1?.Text ?? "Text";
            _text.Text2 = _txtText2?.Text ?? "";
            _text.CustomVariable = _chkTextCustomVar?.IsChecked == true;
            _text.Display2Rows = _chkTextTwoRows?.IsChecked == true;
            _text.OverrideTextColor = _chkTextOverrideText?.IsChecked == true;
            _text.OverrideTimeColor = _chkTextOverrideTime?.IsChecked == true;
            if (_swTextColor is not null) _text.TextColor = _swTextColor.ArgbColor;
            if (_swTextTime is not null) _text.TimeColor = _swTextTime.ArgbColor;
            _text.BackgroundGradient = _cmbTextGradient?.SelectedItem?.ToString() ?? "Plain";
            if (_swTextBg1 is not null) _text.BackgroundColor = _swTextBg1.ArgbColor;
            if (_swTextBg2 is not null) _text.BackgroundColor2 = _swTextBg2.ArgbColor;
            _component.SettingsInnerXml = _text.ToInnerXml();
        }
        else
        {
            _applyMore?.Invoke();
        }
    }
}
