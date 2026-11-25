using System.Xml;
using LiveSplitNative.Core.Formatting;

namespace LiveSplitNative.Core.UI;

/// <summary>Portable subset of LiveSplit.Splits SplitsSettings used by the native renderer.</summary>
public sealed class SplitsComponentSettings
{
    public int VisualSplitCount { get; set; } = 16;
    public int SplitPreviewCount { get; set; } = 1;
    public bool ShowThinSeparators { get; set; } = true;
    public bool AlwaysShowLastSplit { get; set; } = true;
    public bool ShowBlankSplits { get; set; } = true;
    public bool ShowSplitTimes { get; set; } = true;
    public bool LockLastSplit { get; set; } = true;
    public bool SeparatorLastSplit { get; set; } = true;
    public bool DropDecimals { get; set; } = true;
    public bool DisplayIcons { get; set; } = true;
    public bool IconShadows { get; set; } = true;
    public bool Display2Rows { get; set; }
    public bool AutomaticAbbreviations { get; set; }
    /// <summary>Windows SplitsSettings.IconSize — square draw size; does not set row height.</summary>
    public float IconSize { get; set; } = 24f;
    /// <summary>
    /// Windows SplitsSettings.SplitHeight. Vertical row height = SplitHeight + 25
    /// (single-row mode). Icons are centered inside that height and never inflate it.
    /// </summary>
    public float SplitHeight { get; set; } = 3.6f;
    public bool ShowColumnLabels { get; set; }
    public ArgbColor LabelsColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public TimeAccuracy SplitTimesAccuracy { get; set; } = TimeAccuracy.Seconds;
    public TimeAccuracy DeltasAccuracy { get; set; } = TimeAccuracy.Tenths;
    public bool OverrideDeltasColor { get; set; }
    public ArgbColor DeltasColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTextColor { get; set; }
    public ArgbColor BeforeNamesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor CurrentNamesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor AfterNamesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public bool OverrideTimesColor { get; set; }
    public ArgbColor BeforeTimesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor CurrentTimesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor AfterTimesColor { get; set; } = ArgbColor.FromArgb(255, 255, 255, 255);
    public ArgbColor BackgroundColor { get; set; } = ArgbColor.FromArgb(0, 0, 0, 0);
    public ArgbColor BackgroundColor2 { get; set; } = ArgbColor.FromArgb(1, 255, 255, 255);
    public string BackgroundGradient { get; set; } = "Alternating";
    public ArgbColor CurrentSplitTopColor { get; set; } = ArgbColor.FromArgb(255, 51, 115, 244);
    public ArgbColor CurrentSplitBottomColor { get; set; } = ArgbColor.FromArgb(255, 21, 53, 116);
    /// <summary>Windows SplitsSettings.CurrentSplitGradient: Plain / Vertical / Horizontal.</summary>
    public string CurrentSplitGradient { get; set; } = "Vertical";
    public string Comparison { get; set; } = "Current Comparison";
    public List<ColumnData> Columns { get; } = [];

    public static SplitsComponentSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        var settings = new SplitsComponentSettings
        {
            VisualSplitCount = ComponentSettingsXml.Int(root, "VisualSplitCount", 16),
            SplitPreviewCount = ComponentSettingsXml.Int(root, "SplitPreviewCount", 1),
            ShowThinSeparators = ComponentSettingsXml.Bool(root, "ShowThinSeparators", true),
            AlwaysShowLastSplit = ComponentSettingsXml.Bool(root, "AlwaysShowLastSplit", true),
            ShowBlankSplits = ComponentSettingsXml.Bool(root, "ShowBlankSplits", true),
            ShowSplitTimes = ComponentSettingsXml.Bool(root, "ShowSplitTimes", true),
            LockLastSplit = ComponentSettingsXml.Bool(root, "LockLastSplit", true),
            SeparatorLastSplit = ComponentSettingsXml.Bool(root, "SeparatorLastSplit", true),
            DropDecimals = ComponentSettingsXml.Bool(root, "DropDecimals", true),
            DisplayIcons = ComponentSettingsXml.Bool(root, "DisplayIcons", true),
            IconShadows = ComponentSettingsXml.Bool(root, "IconShadows", true),
            Display2Rows = ComponentSettingsXml.Bool(root, "Display2Rows", false),
            AutomaticAbbreviations = ComponentSettingsXml.Bool(root, "AutomaticAbbreviations", false),
            IconSize = ComponentSettingsXml.Float(root, "IconSize", 24f),
            // Windows ParseFloat default is 6 when the node is missing; ctor default is 3.6.
            SplitHeight = ComponentSettingsXml.Float(root, "SplitHeight", 3.6f),
            ShowColumnLabels = ComponentSettingsXml.Bool(root, "ShowColumnLabels", false),
            LabelsColor = ComponentSettingsXml.Color(root, "LabelsColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            SplitTimesAccuracy = ComponentSettingsXml.Accuracy(root, "SplitTimesAccuracy", TimeAccuracy.Seconds),
            DeltasAccuracy = ComponentSettingsXml.Accuracy(root, "DeltasAccuracy", TimeAccuracy.Tenths),
            OverrideDeltasColor = ComponentSettingsXml.Bool(root, "OverrideDeltasColor", false),
            DeltasColor = ComponentSettingsXml.Color(root, "DeltasColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTextColor = ComponentSettingsXml.Bool(root, "OverrideTextColor", false),
            BeforeNamesColor = ComponentSettingsXml.Color(root, "BeforeNamesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            CurrentNamesColor = ComponentSettingsXml.Color(root, "CurrentNamesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            AfterNamesColor = ComponentSettingsXml.Color(root, "AfterNamesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            OverrideTimesColor = ComponentSettingsXml.Bool(root, "OverrideTimesColor", false),
            BeforeTimesColor = ComponentSettingsXml.Color(root, "BeforeTimesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            CurrentTimesColor = ComponentSettingsXml.Color(root, "CurrentTimesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            AfterTimesColor = ComponentSettingsXml.Color(root, "AfterTimesColor", ArgbColor.FromArgb(255, 255, 255, 255)),
            BackgroundColor = ComponentSettingsXml.Color(root, "BackgroundColor", ArgbColor.FromArgb(0, 0, 0, 0)),
            BackgroundColor2 = ComponentSettingsXml.Color(root, "BackgroundColor2", ArgbColor.FromArgb(1, 255, 255, 255)),
            BackgroundGradient = ComponentSettingsXml.String(root, "BackgroundGradient", "Alternating"),
            CurrentSplitTopColor = ComponentSettingsXml.Color(root, "CurrentSplitTopColor", ArgbColor.FromArgb(255, 51, 115, 244)),
            CurrentSplitBottomColor = ComponentSettingsXml.Color(root, "CurrentSplitBottomColor", ArgbColor.FromArgb(255, 21, 53, 116)),
            CurrentSplitGradient = ComponentSettingsXml.String(root, "CurrentSplitGradient", "Vertical"),
            Comparison = ComponentSettingsXml.String(root, "Comparison", "Current Comparison"),
        };

        if (root["Columns"] is { } columns)
        {
            foreach (XmlElement child in columns.SelectNodes("Settings")!.OfType<XmlElement>())
            {
                settings.Columns.Add(ColumnData.FromXml(child));
            }
        }

        if (settings.Columns.Count == 0)
        {
            // Legacy ShowSplitTimes / Comparison → default +/- and Time columns.
            string comparison = settings.Comparison;
            if (settings.ShowSplitTimes)
            {
                settings.Columns.Add(new ColumnData("+/-", ColumnType.Delta, comparison, "Current Timing Method"));
                settings.Columns.Add(new ColumnData("Time", ColumnType.SplitTime, comparison, "Current Timing Method"));
            }
            else
            {
                settings.Columns.Add(new ColumnData("+/-", ColumnType.DeltaorSplitTime, comparison, "Current Timing Method"));
            }
        }

        return settings;
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.6");
        ComponentSettingsXml.SetText(doc, parent, "VisualSplitCount", VisualSplitCount.ToString());
        ComponentSettingsXml.SetText(doc, parent, "SplitPreviewCount", SplitPreviewCount.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowThinSeparators", ShowThinSeparators.ToString());
        ComponentSettingsXml.SetText(doc, parent, "AlwaysShowLastSplit", AlwaysShowLastSplit.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowBlankSplits", ShowBlankSplits.ToString());
        ComponentSettingsXml.SetText(doc, parent, "ShowSplitTimes", ShowSplitTimes.ToString());
        ComponentSettingsXml.SetText(doc, parent, "LockLastSplit", LockLastSplit.ToString());
        ComponentSettingsXml.SetText(doc, parent, "SeparatorLastSplit", SeparatorLastSplit.ToString());
        ComponentSettingsXml.SetText(doc, parent, "DropDecimals", DropDecimals.ToString());
        ComponentSettingsXml.SetText(doc, parent, "DisplayIcons", DisplayIcons.ToString());
        ComponentSettingsXml.SetText(doc, parent, "IconShadows", IconShadows.ToString());
        ComponentSettingsXml.SetText(doc, parent, "Display2Rows", Display2Rows.ToString());
        ComponentSettingsXml.SetText(doc, parent, "AutomaticAbbreviations", AutomaticAbbreviations.ToString());
        ComponentSettingsXml.SetText(doc, parent, "IconSize", IconSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "SplitHeight", SplitHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ComponentSettingsXml.SetText(doc, parent, "ShowColumnLabels", ShowColumnLabels.ToString());
        ComponentSettingsXml.SetText(doc, parent, "LabelsColor", LabelsColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "SplitTimesAccuracy", SplitTimesAccuracy.ToString());
        ComponentSettingsXml.SetText(doc, parent, "DeltasAccuracy", DeltasAccuracy.ToString());
        ComponentSettingsXml.SetText(doc, parent, "OverrideDeltasColor", OverrideDeltasColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "DeltasColor", DeltasColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTextColor", OverrideTextColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "BeforeNamesColor", BeforeNamesColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "CurrentNamesColor", CurrentNamesColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "AfterNamesColor", AfterNamesColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "OverrideTimesColor", OverrideTimesColor.ToString());
        ComponentSettingsXml.SetText(doc, parent, "BeforeTimesColor", BeforeTimesColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "CurrentTimesColor", CurrentTimesColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "AfterTimesColor", AfterTimesColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor", BackgroundColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundColor2", BackgroundColor2.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "BackgroundGradient", BackgroundGradient);
        ComponentSettingsXml.SetText(doc, parent, "CurrentSplitTopColor", CurrentSplitTopColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "CurrentSplitBottomColor", CurrentSplitBottomColor.ToHex());
        ComponentSettingsXml.SetText(doc, parent, "CurrentSplitGradient", CurrentSplitGradient);
        ComponentSettingsXml.SetText(doc, parent, "Comparison", Comparison);

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

    /// <summary>
    /// If no columns are configured, apply Windows default: +/- + Time bound to
    /// <c>Current Comparison</c>.
    /// </summary>
    public void EnsureDefaultColumnsIfEmpty()
    {
        if (Columns.Count == 0)
        {
            Columns.AddRange(ColumnData.CreateDefault());
        }
    }

    /// <summary>
    /// Path B: stop synthesizing columns from available comparisons, and collapse layouts that
    /// look auto-expanded (one SplitTime column per custom comparison, Name==Comparison —
    /// the old <see cref="EnsureCustomComparisonColumns"/> fingerprint). Clearly intentional
    /// multi-columns (custom names/types, or only a subset of comparisons) are preserved.
    /// Returns true when Columns were mutated.
    /// </summary>
    public bool NormalizeComparisonColumns(IEnumerable<string> comparisons)
    {
        EnsureDefaultColumnsIfEmpty();
        var nonPb = comparisons
            .Where(c => !string.IsNullOrWhiteSpace(c)
                && !string.Equals(c, "Personal Best", StringComparison.Ordinal)
                && !string.Equals(c, "Current Comparison", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (nonPb.Count == 0)
        {
            return false;
        }

        // Auto-fingerprint: exact shape of the old EnsureCustomComparisonColumns adder.
        bool IsAutoFingerprint(ColumnData c) =>
            c.Type == ColumnType.SplitTime
            && string.Equals(c.Name, c.Comparison, StringComparison.Ordinal)
            && (string.IsNullOrEmpty(c.TimingMethod)
                || string.Equals(c.TimingMethod, "Current Timing Method", StringComparison.OrdinalIgnoreCase))
            && nonPb.Any(n => string.Equals(n, c.Comparison, StringComparison.Ordinal));

        var auto = Columns.Where(IsAutoFingerprint).ToList();
        if (auto.Count == 0)
        {
            return false;
        }

        // Collapse only when every available non-PB comparison has an auto column
        // (SMS / EnsureCustom pattern). Partial sets look intentional — keep them.
        bool coversAll = nonPb.All(n =>
            auto.Any(c => string.Equals(c.Comparison, n, StringComparison.Ordinal)));
        if (!coversAll)
        {
            return false;
        }

        foreach (ColumnData c in auto)
        {
            Columns.Remove(c);
        }

        EnsureDefaultColumnsIfEmpty();
        // Hotkeys switch CurrentComparison — keep default pair on Current Comparison.
        foreach (ColumnData c in Columns)
        {
            bool isDefaultName = string.Equals(c.Name, "+/-", StringComparison.Ordinal)
                || string.Equals(c.Name, "Time", StringComparison.Ordinal)
                || string.Equals(c.Name, Localization.UiText.T("Time"), StringComparison.Ordinal);
            if (!isDefaultName
                || c.Type is not (ColumnType.SplitTime or ColumnType.Delta or ColumnType.DeltaorSplitTime))
            {
                continue;
            }

            if (!string.Equals(c.Comparison, "Current Comparison", StringComparison.OrdinalIgnoreCase)
                && nonPb.Contains(c.Comparison))
            {
                c.Comparison = "Current Comparison";
            }
        }

        return true;
    }

    /// <summary>
    /// Obsolete: previously synthesized one SplitTime column per custom comparison.
    /// Now normalizes (path B collapse). Prefer <see cref="NormalizeComparisonColumns"/>.
    /// </summary>
    [Obsolete("Do not map available comparisons into columns. Use NormalizeComparisonColumns.")]
    public void EnsureCustomComparisonColumns(IEnumerable<string> comparisons) =>
        NormalizeComparisonColumns(comparisons);
}
