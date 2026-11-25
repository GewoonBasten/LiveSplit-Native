using Avalonia.Controls;
using Avalonia.Interactivity;
using LiveSplitNative.Core.Model.Comparisons;

namespace LiveSplitNative.App;

/// <summary>What Choose Active Comparisons returns on OK.</summary>
public sealed record ChooseComparisonsResult(
    IReadOnlyDictionary<string, bool> GeneratorStates,
    int HcpHistorySize,
    int HcpNBestRuns);

/// <summary>
/// Port of LiveSplit.View ChooseComparisonsDialog @ 93a11b85. Works on a copy of the states;
/// OK returns them, Cancel and closing return null. History Size and N Best Runs are 1–50, and
/// N Best Runs never exceeds History Size (each spinner pushes the other, as on Windows).
/// </summary>
public partial class ChooseComparisonsWindow : Window
{
    /// <summary>The dialog's list, in Windows order.</summary>
    public static readonly string[] GeneratorNames =
    [
        BestSegmentsComparisonGenerator.ComparisonName,
        BestSplitTimesComparisonGenerator.ComparisonName,
        AverageSegmentsComparisonGenerator.ComparisonName,
        MedianSegmentsComparisonGenerator.ComparisonName,
        WorstSegmentsComparisonGenerator.ComparisonName,
        PercentileComparisonGenerator.ComparisonName,
        LatestRunComparisonGenerator.ComparisonName,
        HCPComparisonGenerator.ComparisonName,
        NoneComparisonGenerator.ComparisonName,
    ];

    private const int HcpMinimum = 1;
    private const int HcpMaximum = 50;

    private readonly Dictionary<string, bool> _states;
    private readonly Dictionary<string, CheckBox> _boxes = new(StringComparer.Ordinal);
    private readonly NumericUpDown _historySizeBox;
    private readonly NumericUpDown _nBestRunsBox;
    private bool _syncingSpinners;

    public ChooseComparisonsWindow()
        : this(new Dictionary<string, bool>(), 20, 8)
    {
    }

    public ChooseComparisonsWindow(IReadOnlyDictionary<string, bool> states, int hcpHistorySize, int hcpNBestRuns)
    {
        InitializeComponent();
        DialogChrome.ConfigureDialog(this);
        DialogChrome.PreferFitToContentHeight(this);

        _states = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (string name in GeneratorNames)
        {
            _states[name] = states.TryGetValue(name, out bool enabled) && enabled;
            var box = new CheckBox { Content = name, IsChecked = _states[name] };
            string captured = name;
            box.IsCheckedChanged += (_, _) =>
            {
                _states[captured] = box.IsChecked == true;
                if (captured == HCPComparisonGenerator.ComparisonName)
                {
                    UpdateHcpSettingsVisibility();
                }
            };
            _boxes[name] = box;
            ComparisonsList.Children.Add(box);
        }

        _historySizeBox = ComponentSettingsEditor.CreateSettingsNumericUpDown(hcpHistorySize, HcpMinimum, HcpMaximum);
        _nBestRunsBox = ComponentSettingsEditor.CreateSettingsNumericUpDown(hcpNBestRuns, HcpMinimum, HcpMaximum);
        HcpGrid.Children.Add(new SettingsField { Label = "History Size:", Field = _historySizeBox });
        HcpGrid.Children.Add(new SettingsField { Label = "N Best Runs to Use:", Field = _nBestRunsBox });
        _historySizeBox.ValueChanged += (_, _) => OnHistorySizeChanged();
        _nBestRunsBox.ValueChanged += (_, _) => OnNBestRunsChanged();

        UpdateHcpSettingsVisibility();
    }

    /// <summary>Checks or unchecks one generator, as a click would (proof harness and tests).</summary>
    public void SetGeneratorChecked(string name, bool isChecked)
    {
        if (_boxes.TryGetValue(name, out CheckBox? box))
        {
            box.IsChecked = isChecked;
        }
    }

    private void UpdateHcpSettingsVisibility() =>
        HcpGroup.IsVisible = _states[HCPComparisonGenerator.ComparisonName];

    // Windows numericUpDownHcpHistorySize_ValueChanged: a smaller history pulls N Best Runs down.
    private void OnHistorySizeChanged()
    {
        if (_syncingSpinners || _historySizeBox.Value is not { } history || _nBestRunsBox.Value is not { } best)
        {
            return;
        }

        if (history < best)
        {
            _syncingSpinners = true;
            _nBestRunsBox.Value = history;
            _syncingSpinners = false;
        }
    }

    // Windows numericUpDownHcpNBestRuns_ValueChanged: more best runs than history pushes history up.
    private void OnNBestRunsChanged()
    {
        if (_syncingSpinners || _historySizeBox.Value is not { } history || _nBestRunsBox.Value is not { } best)
        {
            return;
        }

        if (best > history)
        {
            _syncingSpinners = true;
            _historySizeBox.Value = best;
            _syncingSpinners = false;
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        // Validate on commit: an empty or partial spinner keeps its last valid value.
        int history = (int)Math.Clamp(_historySizeBox.Value ?? HcpMinimum, HcpMinimum, HcpMaximum);
        int best = (int)Math.Clamp(_nBestRunsBox.Value ?? HcpMinimum, HcpMinimum, HcpMaximum);
        if (best > history)
        {
            history = best;
        }

        Close(new ChooseComparisonsResult(new Dictionary<string, bool>(_states, StringComparer.Ordinal), history, best));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
