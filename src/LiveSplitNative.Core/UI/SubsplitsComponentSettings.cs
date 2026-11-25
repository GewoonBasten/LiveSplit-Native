using System.Xml;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// Port of LiveSplit.Subsplits SplitsSettings (93a11b85): its settings, defaults and XML.
/// Subsplit marker: leading '-' on the segment name; optional section name via '{Name} …' on
/// the section's last split.
/// </summary>
public sealed class SubsplitsComponentSettings
{
    public enum SubsplitVisibility
    {
        AlwaysHide = 0,
        Contextual = 1,
        AlwaysShow = 2,
    }

    // Defaults: SplitsSettings' constructor (LiveSplit.Subsplits, 93a11b85).
    public int VisualSplitCount { get; set; } = 8;
    public int SplitPreviewCount { get; set; } = 1;
    public int MinimumMajorSplits { get; set; }
    public bool ShowThinSeparators { get; set; }
    public bool AlwaysShowLastSplit { get; set; } = true;
    public bool SeparatorLastSplit { get; set; } = true;
    public bool LockLastSplit { get; set; } = true;
    public bool Display2Rows { get; set; }
    public bool DropDecimals { get; set; } = true;
    public bool DisplayIcons { get; set; } = true;
    public bool IconShadows { get; set; } = true;
    public bool IndentBlankIcons { get; set; } = true;
    public float IconSize { get; set; } = 24f;
    public float SplitWidth { get; set; } = 20f;
    public float SplitHeight { get; set; } = 3.6f;
    public bool ShowColumnLabels { get; set; }
    public ArgbColor LabelsColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool IndentSubsplits { get; set; } = true;
    /// <summary>Windows ShowHeader.</summary>
    public bool ShowSectionHeader { get; set; } = true;
    /// <summary>Windows CurrentSectionOnly.</summary>
    public bool OnlyCurrentSection { get; set; }
    public bool AutomaticAbbreviation { get; set; }
    /// <summary>Windows' three radio buttons: HideSubsplits, neither (Contextual), ShowSubsplits.</summary>
    public SubsplitVisibility Visibility { get; set; } = SubsplitVisibility.Contextual;
    public bool HideSubsplits => Visibility == SubsplitVisibility.AlwaysHide;
    public bool ShowSubsplits => Visibility == SubsplitVisibility.AlwaysShow;
    public TimeAccuracy SplitTimesAccuracy { get; set; } = TimeAccuracy.Seconds;
    public TimeAccuracy DeltasAccuracy { get; set; } = TimeAccuracy.Tenths;
    public TimeAccuracy HeaderAccuracy { get; set; } = TimeAccuracy.Tenths;
    public TimeAccuracy SectionTimerAccuracy { get; set; } = TimeAccuracy.Tenths;

    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(1, 255, 255, 255);
    public string BackgroundGradient { get; set; } = "Alternating";

    public ArgbColor CurrentSplitTopColor { get; set; } = ArgbColor.FromArgb(255, 51, 115, 244);
    public ArgbColor CurrentSplitBottomColor { get; set; } = ArgbColor.FromArgb(255, 21, 53, 116);
    public string CurrentSplitGradient { get; set; } = "Vertical";
    /// <summary>Not a Subsplits setting on Windows (version 1.7 has only HeaderComparison); kept for the column defaults.</summary>
    public string Comparison { get; set; } = "Current Comparison";

    public bool OverrideTextColor { get; set; }
    public ArgbColor BeforeNamesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor CurrentNamesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor AfterNamesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);

    public bool OverrideTimesColor { get; set; }
    public ArgbColor BeforeTimesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor CurrentTimesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor AfterTimesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);

    public bool OverrideDeltasColor { get; set; }
    public ArgbColor DeltasColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);

    public bool OverrideSubsplitColor { get; set; }
    public ArgbColor SubsplitTopColor { get; set; } = ArgbColor.FromArgb(0x8D, 0x00, 0x00, 0x00);
    public ArgbColor SubsplitBottomColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public string SubsplitGradient { get; set; } = "Plain";

    public bool IndentSectionSplit { get; set; } = true;
    public bool ShowIconSectionSplit { get; set; } = true;
    public bool ShowSectionIcon { get; set; } = true;
    public string HeaderComparison { get; set; } = "Current Comparison";
    public string HeaderTimingMethod { get; set; } = "Current Timing Method";
    public ArgbColor HeaderTopColor { get; set; } = ArgbColor.FromArgb(0x2B, 0xFF, 0xFF, 0xFF);
    public ArgbColor HeaderBottomColor { get; set; } = ArgbColor.FromArgb(0xD8, 0x00, 0x00, 0x00);
    public string HeaderGradient { get; set; } = "Vertical";
    public bool OverrideHeaderColor { get; set; }
    public ArgbColor HeaderTextColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool HeaderText { get; set; } = true;
    public ArgbColor HeaderTimesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool HeaderTimes { get; set; } = true;

    public bool SectionTimer { get; set; } = true;
    public ArgbColor SectionTimerColor { get; set; } = ArgbColor.FromArgb(255, 0x77, 0x77, 0x77);
    public bool SectionTimerGradient { get; set; } = true;

    public List<ColumnData> Columns { get; } = [];

    /// <summary>SplitsSettings.SetSettings (LiveSplit.Subsplits, 93a11b85), with the same defaults.</summary>
    public static SubsplitsComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        Version version = Version.TryParse(root["Version"]?.InnerText, out Version? parsed) ? parsed : new Version(1, 0, 0, 0);

        // HideSubsplits / ShowSubsplits as Windows writes them; SubsplitVisibility only in
        // layouts saved by earlier builds of this app that had neither.
        SubsplitVisibility visibility = SubsplitVisibility.Contextual;
        if (root["HideSubsplits"] is not null || root["ShowSubsplits"] is not null)
        {
            visibility = ComponentSettingsXml.Bool(root, "HideSubsplits", false) ? SubsplitVisibility.AlwaysHide
                : ComponentSettingsXml.Bool(root, "ShowSubsplits", false) ? SubsplitVisibility.AlwaysShow
                : SubsplitVisibility.Contextual;
        }
        else if (Enum.TryParse(ComponentSettingsXml.String(root, "SubsplitVisibility", "Contextual"), ignoreCase: true, out SubsplitVisibility legacy))
        {
            visibility = legacy;
        }

        var settings = new SubsplitsComponentSettings
        {
            AutomaticAbbreviation = ComponentSettingsXml.Bool(root, "AutomaticAbbreviation", false),
            CurrentSplitTopColor = ComponentSettingsXml.Color(root, "CurrentSplitTopColor", ArgbColor.FromArgb(255, 51, 115, 244)),
            CurrentSplitBottomColor = ComponentSettingsXml.Color(root, "CurrentSplitBottomColor", ArgbColor.FromArgb(255, 21, 53, 116)),
            VisualSplitCount = ComponentSettingsXml.Int(root, "VisualSplitCount", 8),
            SplitPreviewCount = ComponentSettingsXml.Int(root, "SplitPreviewCount", 1),
            MinimumMajorSplits = ComponentSettingsXml.Int(root, "MinimumMajorSplits", 0),
            DisplayIcons = ComponentSettingsXml.Bool(root, "DisplayIcons", true),
            ShowThinSeparators = ComponentSettingsXml.Bool(root, "ShowThinSeparators", false),
            AlwaysShowLastSplit = ComponentSettingsXml.Bool(root, "AlwaysShowLastSplit", true),
            SplitWidth = ComponentSettingsXml.Float(root, "SplitWidth", 20f),
            IndentBlankIcons = ComponentSettingsXml.Bool(root, "IndentBlankIcons", true),
            IndentSubsplits = ComponentSettingsXml.Bool(root, "IndentSubsplits", true),
            Visibility = visibility,
            OnlyCurrentSection = ComponentSettingsXml.Bool(root, "CurrentSectionOnly",
                ComponentSettingsXml.Bool(root, "OnlyCurrentSection", false)),
            OverrideSubsplitColor = ComponentSettingsXml.Bool(root, "OverrideSubsplitColor", false),
            SubsplitTopColor = ComponentSettingsXml.Color(root, "SubsplitTopColor", ArgbColor.FromArgb(0x8D, 0x00, 0x00, 0x00)),
            SubsplitBottomColor = ComponentSettingsXml.Color(root, "SubsplitBottomColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            SubsplitGradient = ComponentSettingsXml.String(root, "SubsplitGradient", "Plain"),
            ShowSectionHeader = ComponentSettingsXml.Bool(root, "ShowHeader",
                ComponentSettingsXml.Bool(root, "ShowSectionHeader", true)),
            IndentSectionSplit = ComponentSettingsXml.Bool(root, "IndentSectionSplit", true),
            ShowIconSectionSplit = ComponentSettingsXml.Bool(root, "ShowIconSectionSplit", true),
            ShowSectionIcon = ComponentSettingsXml.Bool(root, "ShowSectionIcon", true),
            HeaderTopColor = ComponentSettingsXml.Color(root, "HeaderTopColor", ArgbColor.FromArgb(0x2B, 0xFF, 0xFF, 0xFF)),
            HeaderBottomColor = ComponentSettingsXml.Color(root, "HeaderBottomColor", ArgbColor.FromArgb(0xD8, 0x00, 0x00, 0x00)),
            HeaderGradient = ComponentSettingsXml.String(root, "HeaderGradient", "Vertical"),
            OverrideHeaderColor = ComponentSettingsXml.Bool(root, "OverrideHeaderColor", false),
            HeaderTextColor = ComponentSettingsXml.Color(root, "HeaderTextColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            HeaderText = ComponentSettingsXml.Bool(root, "HeaderText", true),
            HeaderTimesColor = ComponentSettingsXml.Color(root, "HeaderTimesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            HeaderTimes = ComponentSettingsXml.Bool(root, "HeaderTimes", true),
            HeaderAccuracy = ComponentSettingsXml.Accuracy(root, "HeaderAccuracy", TimeAccuracy.Tenths),
            SectionTimer = ComponentSettingsXml.Bool(root, "SectionTimer", true),
            SectionTimerColor = ComponentSettingsXml.Color(root, "SectionTimerColor", ArgbColor.FromArgb(255, 0x77, 0x77, 0x77)),
            SectionTimerGradient = ComponentSettingsXml.Bool(root, "SectionTimerGradient", true),
            SectionTimerAccuracy = ComponentSettingsXml.Accuracy(root, "SectionTimerAccuracy", TimeAccuracy.Tenths),
            OverrideTimesColor = ComponentSettingsXml.Bool(root, "OverrideTimesColor", false),
            BeforeTimesColor = ComponentSettingsXml.Color(root, "BeforeTimesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            CurrentTimesColor = ComponentSettingsXml.Color(root, "CurrentTimesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            AfterTimesColor = ComponentSettingsXml.Color(root, "AfterTimesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            SplitHeight = ComponentSettingsXml.Float(root, "SplitHeight", 3.6f),
            CurrentSplitGradient = ComponentSettingsXml.String(root, "CurrentSplitGradient", "Vertical"),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(1, 255, 255, 255)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Alternating"),
            SeparatorLastSplit = ComponentSettingsXml.Bool(root, "SeparatorLastSplit", true),
            DropDecimals = ComponentSettingsXml.Bool(root, "DropDecimals", true),
            DeltasAccuracy = ComponentSettingsXml.Accuracy(root, "DeltasAccuracy", TimeAccuracy.Tenths),
            OverrideDeltasColor = ComponentSettingsXml.Bool(root, "OverrideDeltasColor", false),
            DeltasColor = ComponentSettingsXml.Color(root, "DeltasColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
            SplitTimesAccuracy = ComponentSettingsXml.Accuracy(root, "SplitTimesAccuracy", TimeAccuracy.Seconds),
            LockLastSplit = ComponentSettingsXml.Bool(root, "LockLastSplit", true),
            IconSize = ComponentSettingsXml.Float(root, "IconSize", 24f),
            IconShadows = ComponentSettingsXml.Bool(root, "IconShadows", true),
            ShowColumnLabels = ComponentSettingsXml.Bool(root, "ShowColumnLabels", false),
            LabelsColor = ComponentSettingsXml.Color(root, "LabelsColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            Comparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison"),
        };

        if (root["Columns"] is { } columns)
        {
            settings.HeaderComparison = ComponentSettingsXml.String(root, "HeaderComparison", "Current Comparison");
            settings.HeaderTimingMethod = ComponentSettingsXml.String(root, "HeaderTimingMethod", "Current Timing Method");
            foreach (XmlElement child in columns.SelectNodes("Settings")!.OfType<XmlElement>())
            {
                settings.Columns.Add(ColumnData.FromXml(child));
            }
        }
        else if (version < new Version(1, 7))
        {
            // Before 1.7: Comparison was the header comparison, ShowSplitTimes chose the columns.
            settings.HeaderComparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison");
            settings.HeaderTimingMethod = "Current Timing Method";
            if (ComponentSettingsXml.Bool(root, "ShowSplitTimes", false))
            {
                settings.Columns.Add(new ColumnData("+/-", ColumnType.Delta, settings.HeaderComparison, "Current Timing Method"));
                settings.Columns.Add(new ColumnData("Time", ColumnType.SplitTime, settings.HeaderComparison, "Current Timing Method"));
            }
            else
            {
                settings.Columns.Add(new ColumnData("+/-", ColumnType.DeltaorSplitTime, settings.HeaderComparison, "Current Timing Method"));
            }
        }
        else
        {
            settings.HeaderComparison = ComponentSettingsXml.String(root, "HeaderComparison", "Current Comparison");
            settings.HeaderTimingMethod = ComponentSettingsXml.String(root, "HeaderTimingMethod", "Current Timing Method");
        }

        if (version < new Version(1, 3))
        {
            // Before 1.3: one SplitNamesColor (1.2) or white, and UseTextColor.
            ArgbColor names = version >= new Version(1, 2)
                ? ComponentSettingsXml.Color(root, "SplitNamesColor", ArgbColor.FromArgb(255, 255, 255, 255))
                : ArgbColor.FromArgb(255, 255, 255, 255);
            settings.BeforeNamesColor = settings.CurrentNamesColor = settings.AfterNamesColor = names;
            settings.OverrideTextColor = !ComponentSettingsXml.Bool(root, "UseTextColor", true);
        }
        else
        {
            settings.BeforeNamesColor = ComponentSettingsXml.Color(root, "BeforeNamesColor", ArgbColor.FromArgb(255, 255, 255, 255));
            settings.CurrentNamesColor = ComponentSettingsXml.Color(root, "CurrentNamesColor", ArgbColor.FromArgb(255, 255, 255, 255));
            settings.AfterNamesColor = ComponentSettingsXml.Color(root, "AfterNamesColor", ArgbColor.FromArgb(255, 255, 255, 255));
            settings.OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false);
        }

        if (settings.Columns.Count == 0)
        {
            settings.Columns.Add(new ColumnData("+/-", ColumnType.Delta, settings.Comparison, "Current Timing Method"));
            settings.Columns.Add(new ColumnData("Time", ColumnType.SplitTime, settings.Comparison, "Current Timing Method"));
        }

        return settings;
    }

    /// <summary>SplitsSettings.CreateSettingsNode: Windows' elements in Windows' order, version 1.7.</summary>
    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        void Set(string name, string value) => ComponentSettingsXml.SetText(doc, parent, name, value);
        string F(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Set("Version", "1.7");
        Set("AutomaticAbbreviation", AutomaticAbbreviation.ToString());
        Set("CurrentSplitTopColor", CurrentSplitTopColor.ToHex());
        Set("CurrentSplitBottomColor", CurrentSplitBottomColor.ToHex());
        Set("VisualSplitCount", VisualSplitCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("SplitPreviewCount", SplitPreviewCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("MinimumMajorSplits", MinimumMajorSplits.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set("DisplayIcons", DisplayIcons.ToString());
        Set("ShowThinSeparators", ShowThinSeparators.ToString());
        Set("AlwaysShowLastSplit", AlwaysShowLastSplit.ToString());
        Set("SplitWidth", F(SplitWidth));
        Set("SplitTimesAccuracy", SplitTimesAccuracy.ToString());
        Set("BeforeNamesColor", BeforeNamesColor.ToHex());
        Set("CurrentNamesColor", CurrentNamesColor.ToHex());
        Set("AfterNamesColor", AfterNamesColor.ToHex());
        Set("OverrideTextColor", OverrideTextColor.ToString());
        Set("BeforeTimesColor", BeforeTimesColor.ToHex());
        Set("CurrentTimesColor", CurrentTimesColor.ToHex());
        Set("AfterTimesColor", AfterTimesColor.ToHex());
        Set("OverrideTimesColor", OverrideTimesColor.ToString());
        Set("LockLastSplit", LockLastSplit.ToString());
        Set("IconSize", F(IconSize));
        Set("IconShadows", IconShadows.ToString());
        Set("SplitHeight", F(SplitHeight));
        Set("CurrentSplitGradient", CurrentSplitGradient);
        Set("BackgroundColor", BackgroundColor.ToHex());
        Set("BackgroundColor2", BackgroundColor2.ToHex());
        Set("BackgroundGradient", BackgroundGradient);
        Set("SeparatorLastSplit", SeparatorLastSplit.ToString());
        Set("DeltasAccuracy", DeltasAccuracy.ToString());
        Set("DropDecimals", DropDecimals.ToString());
        Set("OverrideDeltasColor", OverrideDeltasColor.ToString());
        Set("DeltasColor", DeltasColor.ToHex());
        Set("HeaderComparison", HeaderComparison);
        Set("HeaderTimingMethod", HeaderTimingMethod);
        Set("Display2Rows", Display2Rows.ToString());
        Set("IndentBlankIcons", IndentBlankIcons.ToString());
        Set("IndentSubsplits", IndentSubsplits.ToString());
        Set("HideSubsplits", HideSubsplits.ToString());
        Set("ShowSubsplits", ShowSubsplits.ToString());
        Set("CurrentSectionOnly", OnlyCurrentSection.ToString());
        Set("OverrideSubsplitColor", OverrideSubsplitColor.ToString());
        Set("SubsplitGradient", SubsplitGradient);
        Set("ShowHeader", ShowSectionHeader.ToString());
        Set("IndentSectionSplit", IndentSectionSplit.ToString());
        Set("ShowIconSectionSplit", ShowIconSectionSplit.ToString());
        Set("ShowSectionIcon", ShowSectionIcon.ToString());
        Set("HeaderGradient", HeaderGradient);
        Set("OverrideHeaderColor", OverrideHeaderColor.ToString());
        Set("HeaderText", HeaderText.ToString());
        Set("HeaderTimes", HeaderTimes.ToString());
        Set("HeaderAccuracy", HeaderAccuracy.ToString());
        Set("SectionTimer", SectionTimer.ToString());
        Set("SectionTimerGradient", SectionTimerGradient.ToString());
        Set("SectionTimerAccuracy", SectionTimerAccuracy.ToString());
        Set("SubsplitTopColor", SubsplitTopColor.ToHex());
        Set("SubsplitBottomColor", SubsplitBottomColor.ToHex());
        Set("HeaderTopColor", HeaderTopColor.ToHex());
        Set("HeaderBottomColor", HeaderBottomColor.ToHex());
        Set("HeaderTextColor", HeaderTextColor.ToHex());
        Set("HeaderTimesColor", HeaderTimesColor.ToHex());
        Set("SectionTimerColor", SectionTimerColor.ToHex());
        Set("ShowColumnLabels", ShowColumnLabels.ToString());
        Set("LabelsColor", LabelsColor.ToHex());

        XmlElement columnsEl = doc.CreateElement("Columns");
        parent.AppendChild(columnsEl);
        IEnumerable<ColumnData> columns = Columns.Count > 0 ? Columns : ColumnData.CreateDefault();
        foreach (ColumnData column in columns)
        {
            XmlElement settings = doc.CreateElement("Settings");
            columnsEl.AppendChild(settings);
            column.WriteXml(doc, settings);
        }
    });

    /// <summary>Project to SplitsComponentSettings for the shared row renderer.</summary>
    public SplitsComponentSettings AsSplitsSettings()
    {
        var splits = new SplitsComponentSettings
        {
            VisualSplitCount = VisualSplitCount,
            SplitPreviewCount = SplitPreviewCount,
            ShowThinSeparators = ShowThinSeparators,
            AlwaysShowLastSplit = AlwaysShowLastSplit,
            // Subsplits always has Total Splits rows (SplitsComponent.Prepare uses
            // Settings.VisualSplitCount; unused rows are blank), like Splits' Show Blank Splits.
            ShowBlankSplits = true,
            SeparatorLastSplit = SeparatorLastSplit,
            LockLastSplit = LockLastSplit,
            DropDecimals = DropDecimals,
            DisplayIcons = DisplayIcons,
            IconShadows = IconShadows,
            Display2Rows = Display2Rows,
            AutomaticAbbreviations = AutomaticAbbreviation,
            IconSize = IconSize,
            SplitHeight = SplitHeight,
            ShowColumnLabels = ShowColumnLabels,
            LabelsColor = LabelsColor,
            SplitTimesAccuracy = SplitTimesAccuracy,
            DeltasAccuracy = DeltasAccuracy,
            OverrideDeltasColor = OverrideDeltasColor,
            DeltasColor = DeltasColor,
            OverrideTextColor = OverrideTextColor,
            BeforeNamesColor = BeforeNamesColor,
            CurrentNamesColor = CurrentNamesColor,
            AfterNamesColor = AfterNamesColor,
            OverrideTimesColor = OverrideTimesColor,
            BeforeTimesColor = BeforeTimesColor,
            CurrentTimesColor = CurrentTimesColor,
            AfterTimesColor = AfterTimesColor,
            BackgroundColor = BackgroundColor,
            BackgroundColor2 = BackgroundColor2,
            BackgroundGradient = BackgroundGradient,
            CurrentSplitTopColor = CurrentSplitTopColor,
            CurrentSplitBottomColor = CurrentSplitBottomColor,
            CurrentSplitGradient = CurrentSplitGradient,
            Comparison = Comparison,
        };
        splits.Columns.Clear();
        splits.Columns.AddRange(Columns.Select(c => c.Clone()));
        return splits;
    }
}
