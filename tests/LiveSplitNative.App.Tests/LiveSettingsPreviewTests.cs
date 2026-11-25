using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// Layout Settings live preview, for every control Windows updates live: changing it must
/// change the component's settings and tell the dialog (SettingsChanged), so the timer redraws
/// while the dialog is open, as Windows' settings controls do (DataSourceUpdateMode
/// OnPropertyChanged).
/// </summary>
public class LiveSettingsPreviewTests
{
    public static TheoryData<string> ComponentPaths() =>
    [
        StandardLayoutFactory.TitlePath,
        StandardLayoutFactory.SplitsPath,
        StandardLayoutFactory.SubsplitsPath,
        StandardLayoutFactory.TimerPath,
        StandardLayoutFactory.DetailedTimerPath,
        StandardLayoutFactory.PreviousSegmentPath,
        StandardLayoutFactory.SumOfBestPath,
        StandardLayoutFactory.PossibleTimeSavePath,
        StandardLayoutFactory.BlankSpacePath,
        StandardLayoutFactory.TextPath,
        StandardLayoutFactory.RunPredictionPath,
        StandardLayoutFactory.DeltaPath,
        StandardLayoutFactory.ComparisonTimePath,
        StandardLayoutFactory.CurrentComparisonPath,
        StandardLayoutFactory.TotalPlaytimePath,
        StandardLayoutFactory.CounterPath,
        StandardLayoutFactory.GraphPath,
        StandardLayoutFactory.HotkeyIndicatorPath,
        StandardLayoutFactory.ManualGameTimePath,
        StandardLayoutFactory.WorldRecordPath,
        StandardLayoutFactory.SoundPath,
        StandardLayoutFactory.TheRunPath,
        StandardLayoutFactory.AutoSplitIntegrationPath,
        StandardLayoutFactory.AutoSplittingRuntimePath,
    ];

    [AvaloniaTheory]
    [MemberData(nameof(ComponentPaths))]
    public void Every_Component_Control_Previews_Live(string path)
    {
        var problems = new List<string>();
        foreach (LayoutMode mode in new[] { LayoutMode.Vertical, LayoutMode.Horizontal })
        {
            var editor = new ComponentSettingsEditor(StandardLayoutFactory.CreateBuiltin(path), mode);
            int notified = 0;
            editor.SettingsChanged = () => notified++;
            List<Control> controls = editor.GetLogicalDescendants().OfType<Control>().ToList();
            foreach (Control control in controls)
            {
                string before = editor.Apply().SettingsInnerXml;
                int notifiedBefore = notified;
                string? what = Change(control);
                if (what is null)
                {
                    continue;
                }

                string after = editor.Apply().SettingsInnerXml;
                bool changed = after != before;
                bool told = notified > notifiedBefore;
                if (!told)
                {
                    problems.Add($"{mode} {what}: {(changed ? "applied only on OK (no live preview)" : "not applied at all")}");
                }
            }
        }

        Assert.True(problems.Count == 0, path + ":\n" + string.Join("\n", problems));
    }

    [AvaloniaFact]
    public void Every_Layout_Tab_Control_Previews_Live()
    {
        var dialog = new LayoutSettingsWindow(StandardLayoutFactory.Create());
        int notified = 0;
        dialog.LiveLayoutPreview = _ => notified++;
        var problems = new List<string>();
        Control layoutTab = (Control)((TabItem)dialog.SettingsTabs.Items[0]!).Content!;
        foreach (Control control in layoutTab.GetLogicalDescendants().OfType<Control>().ToList())
        {
            int before = notified;
            string? what = control is Slider slider
                ? SetSlider(slider)
                : Change(control);
            if (what is not null && notified == before)
            {
                problems.Add(what + ": no live preview");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    private static string SetSlider(Slider slider)
    {
        slider.Value = slider.Value - 0.1 >= slider.Minimum ? slider.Value - 0.1 : slider.Value + 0.1;
        return "slider";
    }

    /// <summary>Changes one user control as a user would; returns its description, or null to skip.</summary>
    private static string? Change(Control control)
    {
        switch (control)
        {
            case RadioButton radio when radio.IsChecked != true && radio.IsEffectivelyVisible:
                radio.IsChecked = true;
                return $"radio '{radio.Content}'";
            case CheckBox box when box.IsEffectivelyVisible:
                box.IsChecked = box.IsChecked != true;
                return $"check '{Describe(box.Content)}'";
            case ComboBox combo when combo.IsVisible && combo.ItemCount > 1:
                combo.SelectedIndex = combo.SelectedIndex == 0 ? 1 : 0;
                return $"combo '{Label(combo)}'";
            case NumericUpDown number:
                decimal value = number.Value ?? 0;
                number.Value = value + 1 <= number.Maximum ? value + 1 : value - 1;
                return $"number '{Label(number)}'";
            // A read-only box (the Counter's hotkeys) takes a key press, not typing.
            case TextBox text when !text.IsReadOnly && text.FindLogicalAncestorOfType<NumericUpDown>() is null:
                text.Text = (text.Text ?? "") + "x";
                return $"text '{Label(text)}'";
            default:
                return null;
        }
    }

    private static string Describe(object? content) => content switch
    {
        TextBlock t => t.Text ?? "",
        null => "",
        _ => content.ToString() ?? "",
    };

    /// <summary>The label TextBlock beside a control in the editor's label + control rows.</summary>
    private static string Label(Control control) =>
        control.GetLogicalParent() is Panel row
            ? row.Children.OfType<TextBlock>().FirstOrDefault() is { } label ? LabelText.Of(label) : control.GetType().Name
            : control.GetType().Name;
}
