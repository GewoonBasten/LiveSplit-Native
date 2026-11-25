using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using LiveSplitNative.Core.Formatting;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// Settings for Run Prediction, Delta, Comparison Time, Current Comparison, Total Playtime and
/// Counter, in the order of their Windows settings controls (93a11b85). Each builder reads the
/// component's settings, adds the fields and sets <see cref="_applyMore"/> to write them back.
/// </summary>
public sealed partial class ComponentSettingsEditor
{
    private static readonly string[] GradientLabels = ["Plain", "Vertical", "Horizontal"];

    /// <summary>Writes the fields of a component built here back into its settings XML.</summary>
    private Action? _applyMore;

    private bool BuildMoreFields()
    {
        switch (_path)
        {
            case StandardLayoutFactory.RunPredictionPath:
                BuildRunPredictionFields();
                return true;
            case StandardLayoutFactory.DeltaPath:
                BuildDeltaFields();
                return true;
            case StandardLayoutFactory.ComparisonTimePath:
                BuildComparisonTimeFields();
                return true;
            case StandardLayoutFactory.CurrentComparisonPath:
                BuildCurrentComparisonFields();
                return true;
            case StandardLayoutFactory.TotalPlaytimePath:
                BuildTotalPlaytimeFields();
                return true;
            case StandardLayoutFactory.CounterPath:
                BuildCounterFields();
                return true;
            case StandardLayoutFactory.GraphPath:
                BuildGraphFields();
                return true;
            case StandardLayoutFactory.HotkeyIndicatorPath:
                BuildHotkeyIndicatorFields();
                return true;
            case StandardLayoutFactory.ManualGameTimePath:
                BuildManualGameTimeFields();
                return true;
            case StandardLayoutFactory.WorldRecordPath:
                BuildWorldRecordFields();
                return true;
            case StandardLayoutFactory.SoundPath:
                BuildSoundFields();
                return true;
            case StandardLayoutFactory.TheRunPath:
                BuildTheRunFields();
                return true;
            case StandardLayoutFactory.AutoSplitIntegrationPath:
                BuildAutoSplitIntegrationFields();
                return true;
            case StandardLayoutFactory.AutoSplittingRuntimePath:
                BuildAutoSplittingRuntimeFields();
                return true;
            default:
                return false;
        }
    }

    /// <summary>RunPredictionSettings: Background, Comparison, Display 2 Rows, Text Color, Time (Color, Accuracy).</summary>
    private void BuildRunPredictionFields()
    {
        RunPredictionComponentSettings settings = RunPredictionComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        BackgroundRows background = AddBackground(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2);
        ComboBox comparison = AddCombo(
            "Comparison:", ComparisonChoices.RunPrediction(RunComparisons(), settings.Comparison), settings.Comparison);
        CheckBox twoRows = AddTwoRows(_fields, settings.Display2Rows);
        ColorOverride text = AddOverrideColor(_fields, "Text Color", settings.OverrideTextColor, settings.TextColor);
        SettingsCard time = BeginGroup("Time");
        ColorOverride timeColor = AddOverrideColor(time, "Color", settings.OverrideTimeColor, settings.TimeColor, nested: true);
        ComboBox accuracy = AddAccuracyRadios(BeginGroup("Accuracy", nested: true, host: time), settings.Accuracy);

        _applyMore = () =>
        {
            (settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2) = background.Read();
            settings.Comparison = comparison.SelectedItem?.ToString() ?? settings.Comparison;
            settings.Display2Rows = ReadTwoRows(twoRows, settings.Display2Rows);
            (settings.OverrideTextColor, settings.TextColor) = text.Read();
            (settings.OverrideTimeColor, settings.TimeColor) = timeColor.Read();
            settings.Accuracy = ReadAccuracy(accuracy, settings.Accuracy);
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>
    /// DeltaSettings: Background, Comparison, Display 2 Rows, Delta Accuracy (Drop Decimals,
    /// accuracy), Text (Color, Override Default Label, Different Label When Ahead, the labels).
    /// </summary>
    private void BuildDeltaFields()
    {
        DeltaComponentSettings settings = DeltaComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        BackgroundRows background = AddBackground(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2);
        ComboBox comparison = AddCombo(
            "Comparison:", ComparisonChoices.Delta(RunComparisons(), settings.Comparison), settings.Comparison);
        CheckBox twoRows = AddTwoRows(_fields, settings.Display2Rows);
        SettingsCard deltaAccuracy = BeginGroup("Delta Accuracy");
        CheckBox dropDecimals = AddCheckTo(deltaAccuracy, "Drop Decimals When More Than 1 Minute", settings.DropDecimals);
        ComboBox accuracy = AddAccuracyRadios(deltaAccuracy, settings.Accuracy);

        SettingsCard textGroup = BeginGroup("Text");
        ColorOverride text = AddOverrideColor(textGroup, "Color", settings.OverrideTextColor, settings.TextColor, nested: true);
        CheckBox overrideText = AddCheckTo(textGroup, "Override Default Label", settings.OverrideText);
        CheckBox differential = AddCheckTo(textGroup, "Different Label When Ahead", settings.DifferentialText);
        (TextBox customText, SettingsField customLabel) = AddTextTo(textGroup, "Label:", settings.CustomText);
        (TextBox customAhead, SettingsField aheadLabel) = AddTextTo(textGroup, "Label When Ahead:", settings.CustomTextAhead);

        // DeltaSettings.UpdateCustomTextFields.
        void UpdateLabels()
        {
            bool custom = overrideText.IsChecked == true;
            bool ahead = custom && differential.IsChecked == true;
            differential.IsEnabled = customText.IsEnabled = customLabel.IsEnabled = custom;
            customAhead.IsEnabled = aheadLabel.IsEnabled = ahead;
            customLabel.Label = differential.IsChecked == true ? "Label When Behind:" : "Label:";
        }

        overrideText.IsCheckedChanged += (_, _) => UpdateLabels();
        differential.IsCheckedChanged += (_, _) => UpdateLabels();
        UpdateLabels();

        _applyMore = () =>
        {
            (settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2) = background.Read();
            settings.Comparison = comparison.SelectedItem?.ToString() ?? settings.Comparison;
            settings.Display2Rows = ReadTwoRows(twoRows, settings.Display2Rows);
            settings.DropDecimals = dropDecimals.IsChecked == true;
            settings.Accuracy = ReadAccuracy(accuracy, settings.Accuracy);
            (settings.OverrideTextColor, settings.TextColor) = text.Read();
            settings.OverrideText = overrideText.IsChecked == true;
            settings.DifferentialText = differential.IsChecked == true;
            settings.CustomText = customText.Text ?? "";
            settings.CustomTextAhead = customAhead.Text ?? "";
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>
    /// ComparisonTimeSettings: Background, Comparison, Timing Method, Display 2 Rows, Text Color,
    /// Time (Color, Accuracy, Type).
    /// </summary>
    private void BuildComparisonTimeFields()
    {
        ComparisonTimeComponentSettings settings = ComparisonTimeComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        BackgroundRows background = AddBackground(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2);
        ComboBox comparison = AddCombo(
            "Comparison:", ComparisonChoices.RunPrediction(RunComparisons(), settings.Comparison), settings.Comparison);
        ComboBox method = AddCombo("Timing Method:", TimingMethodLabels, settings.TimingMethod);
        CheckBox twoRows = AddTwoRows(_fields, settings.Display2Rows);
        ColorOverride text = AddOverrideColor(_fields, "Text Color", settings.OverrideTextColor, settings.TextColor);
        SettingsCard time = BeginGroup("Time");
        ColorOverride timeColor = AddOverrideColor(time, "Color", settings.OverrideTimeColor, settings.TimeColor, nested: true);
        ComboBox accuracy = AddAccuracyRadios(BeginGroup("Accuracy", nested: true, host: time), settings.Accuracy);
        SettingsCard typeGroup = BeginGroup("Type", nested: true, host: time);
        var typeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        typeGroup.Children.Add(typeRow);
        string group = Guid.NewGuid().ToString("N");
        var types = new List<(RadioButton Radio, ComparisonTimeType Type)>();
        foreach ((string label, ComparisonTimeType type) in new[]
        {
            ("Final Time", ComparisonTimeType.FinalTime),
            ("Split Time", ComparisonTimeType.SplitTime),
            ("Segment Time", ComparisonTimeType.SegmentTime),
        })
        {
            var radio = new RadioButton { Content = label, GroupName = group, IsChecked = settings.Type == type };
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked == true)
                {
                    NotifyPreview();
                }
            };
            typeRow.Children.Add(radio);
            types.Add((radio, type));
        }

        _applyMore = () =>
        {
            (settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2) = background.Read();
            settings.Comparison = comparison.SelectedItem?.ToString() ?? settings.Comparison;
            settings.TimingMethod = method.SelectedItem?.ToString() ?? settings.TimingMethod;
            settings.Display2Rows = ReadTwoRows(twoRows, settings.Display2Rows);
            (settings.OverrideTextColor, settings.TextColor) = text.Read();
            (settings.OverrideTimeColor, settings.TimeColor) = timeColor.Read();
            settings.Accuracy = ReadAccuracy(accuracy, settings.Accuracy);
            settings.Type = types.FirstOrDefault(t => t.Radio.IsChecked == true) is { Radio: not null } chosen
                ? chosen.Type
                : settings.Type;
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>
    /// CurrentComparisonSettings: Background, Display 2 Rows, "Comparing Against Label" color and
    /// "Comparison Text" color.
    /// </summary>
    private void BuildCurrentComparisonFields()
    {
        CurrentComparisonComponentSettings settings = CurrentComparisonComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        BackgroundRows background = AddBackground(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2);
        CheckBox twoRows = AddTwoRows(_fields, settings.Display2Rows);
        ColorOverride label = AddOverrideColor(
            BeginGroup("Comparing Against Label"), "Color", settings.OverrideTextColor, settings.TextColor, nested: true);
        ColorOverride value = AddOverrideColor(
            BeginGroup("Comparison Text"), "Color", settings.OverrideTimeColor, settings.TimeColor, nested: true);

        _applyMore = () =>
        {
            (settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2) = background.Read();
            settings.Display2Rows = ReadTwoRows(twoRows, settings.Display2Rows);
            (settings.OverrideTextColor, settings.TextColor) = label.Read();
            (settings.OverrideTimeColor, settings.TimeColor) = value.Read();
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>TotalPlaytimeSettings: Background, Display 2 Rows and Show Total Hours, Text Color, Time Color.</summary>
    private void BuildTotalPlaytimeFields()
    {
        TotalPlaytimeComponentSettings settings = TotalPlaytimeComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        BackgroundRows background = AddBackground(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2);
        Control checks = _fields;
        CheckBox twoRows = AddTwoRows(checks, settings.Display2Rows);
        CheckBox totalHours = AddCheckTo(checks, "Show Total Hours", settings.ShowTotalHours);
        ColorOverride text = AddOverrideColor(_fields, "Text Color", settings.OverrideTextColor, settings.TextColor);
        ColorOverride time = AddOverrideColor(_fields, "Time Color", settings.OverrideTimeColor, settings.TimeColor);

        _applyMore = () =>
        {
            (settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2) = background.Read();
            settings.Display2Rows = ReadTwoRows(twoRows, settings.Display2Rows);
            settings.ShowTotalHours = totalHours.IsChecked == true;
            (settings.OverrideTextColor, settings.TextColor) = text.Read();
            (settings.OverrideTimeColor, settings.TimeColor) = time.Read();
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>
    /// CounterComponentSettings: Background, Counter Colors (Override Layout Settings, Text Color,
    /// Value Color), Counter (Counter Text, Initial Value, Increment Value, Global Counter
    /// Hotkeys, Hotkeys). A hotkey box takes the next key pressed in it; Escape sets None.
    /// </summary>
    private void BuildCounterFields()
    {
        CounterComponentSettings settings = CounterComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        BackgroundRows background = AddBackground(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2);
        SettingsCard colors = BeginGroup("Counter Colors");
        CheckBox overrideColors = AddCheckTo(colors, "Override Layout Settings", settings.OverrideTextColor);
        ColorSwatchButton textColor = AddColorTo(colors, "Text Color:", settings.CounterTextColor);
        ColorSwatchButton valueColor = AddColorTo(colors, "Value Color:", settings.CounterValueColor);
        WireEnable(overrideColors, Row(textColor), Row(valueColor));

        SettingsCard counter = BeginGroup("Counter");
        (TextBox counterText, _) = AddTextTo(counter, "Counter Text:", settings.CounterText);
        NumericUpDown initial = AddNumberTo(counter, "Initial Value:", settings.InitialValue, int.MinValue, int.MaxValue);
        NumericUpDown increment = AddNumberTo(counter, "Increment Value:", settings.Increment, 0, int.MaxValue);
        CheckBox global = AddCheckTo(counter, "Global Counter Hotkeys", settings.GlobalHotkeysEnabled);
        SettingsCard hotkeys = BeginGroup("Hotkeys", nested: true, host: counter);
        string incrementKey = settings.IncrementKey, decrementKey = settings.DecrementKey, resetKey = settings.ResetKey;
        AddHotkeyBox(hotkeys, "Increment Counter:", () => incrementKey, key => incrementKey = key);
        AddHotkeyBox(hotkeys, "Decrement Counter:", () => decrementKey, key => decrementKey = key);
        AddHotkeyBox(hotkeys, "Reset Counter:", () => resetKey, key => resetKey = key);

        _applyMore = () =>
        {
            (settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2) = background.Read();
            settings.OverrideTextColor = overrideColors.IsChecked == true;
            settings.CounterTextColor = textColor.ArgbColor;
            settings.CounterValueColor = valueColor.ArgbColor;
            settings.CounterText = counterText.Text ?? "";
            settings.InitialValue = (int)Math.Round(initial.Value ?? settings.InitialValue);
            settings.Increment = (int)Math.Round(increment.Value ?? settings.Increment);
            settings.GlobalHotkeysEnabled = global.IsChecked == true;
            settings.IncrementKey = incrementKey;
            settings.DecrementKey = decrementKey;
            settings.ResetKey = resetKey;
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>
    /// GraphSettings: Comparison, Height (Width in a horizontal layout; Windows' slider 3-40 in
    /// steps of 5 / 10), the colors in Windows' two columns, Show Best Segments with its color,
    /// Live Graph and Flip Graph.
    /// </summary>
    private void BuildGraphFields()
    {
        GraphComponentSettings settings = GraphComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        bool horizontal = _layoutMode == LayoutMode.Horizontal;
        ComboBox comparison = AddCombo(
            "Comparison:", ComparisonChoices.Graph(RunComparisons(), settings.Comparison), settings.Comparison);
        NumericUpDown size = horizontal
            ? AddNumber("Width:", (decimal)settings.GraphWidth, 30, 400)
            : AddNumber("Height:", (decimal)settings.GraphHeight, 15, 200);
        size.Increment = horizontal ? 10 : 5;

        // Windows' table: two columns of colors, then the check boxes beside them.
        var table = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,32,Auto"),
            RowDefinitions = new RowDefinitions("32,32,32,32,32,32,32"),
        };
        Rows(_fields).Add(table);
        T Place<T>(T control, int row, int column)
            where T : Control
        {
            Control cell = control is ColorSwatchButton ? Row(control) : control;
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column * 2);
            return control;
        }

        ColorSwatchButton behind = Place(AddColorTo(table, "Behind Background Color:", settings.BehindGraphColor), 0, 0);
        ColorSwatchButton partialBehind = Place(AddColorTo(table, "Partial Fill Color (Behind):", settings.PartialFillColorBehind), 0, 1);
        ColorSwatchButton ahead = Place(AddColorTo(table, "Ahead Background Color:", settings.AheadGraphColor), 1, 0);
        ColorSwatchButton completeBehind = Place(AddColorTo(table, "Complete Fill Color (Behind):", settings.CompleteFillColorBehind), 1, 1);
        ColorSwatchButton gridlines = Place(AddColorTo(table, "Gridlines Color:", settings.GridlinesColor), 2, 0);
        ColorSwatchButton partialAhead = Place(AddColorTo(table, "Partial Fill Color (Ahead):", settings.PartialFillColorAhead), 2, 1);
        ColorSwatchButton graph = Place(AddColorTo(table, "Main Graph Color:", settings.GraphColor), 3, 0);
        ColorSwatchButton completeAhead = Place(AddColorTo(table, "Complete Fill Color (Ahead):", settings.CompleteFillColorAhead), 3, 1);
        ColorSwatchButton shadows = Place(AddColorTo(table, "Shadows Color:", settings.ShadowsColor), 4, 0);
        ColorSwatchButton lines = Place(AddColorTo(table, "Separators Color:", settings.GraphLinesColor), 4, 1);
        CheckBox bestSegments = Place(AddCheckTo(table, "Show Best Segments", settings.ShowBestSegments), 5, 0);
        ColorSwatchButton gold = Place(AddColorTo(table, "Best Segment Color:", settings.GraphGoldColor), 5, 1);
        WireEnable(bestSegments, Row(gold));
        CheckBox liveGraph = Place(AddCheckTo(table, "Live Graph", settings.IsLiveGraph), 6, 0);
        CheckBox flip = Place(AddCheckTo(table, "Flip Graph", settings.FlipGraph), 6, 1);
        foreach (Control check in new Control[] { bestSegments, liveGraph, flip })
        {
            check.VerticalAlignment = VerticalAlignment.Center;
        }

        _applyMore = () =>
        {
            settings.Comparison = comparison.SelectedItem?.ToString() ?? settings.Comparison;
            if (size.Value is { } value)
            {
                if (horizontal)
                {
                    settings.GraphWidth = (float)value;
                }
                else
                {
                    settings.GraphHeight = (float)value;
                }
            }

            settings.BehindGraphColor = behind.ArgbColor;
            settings.AheadGraphColor = ahead.ArgbColor;
            settings.GridlinesColor = gridlines.ArgbColor;
            settings.GraphColor = graph.ArgbColor;
            settings.ShadowsColor = shadows.ArgbColor;
            settings.PartialFillColorBehind = partialBehind.ArgbColor;
            settings.CompleteFillColorBehind = completeBehind.ArgbColor;
            settings.PartialFillColorAhead = partialAhead.ArgbColor;
            settings.CompleteFillColorAhead = completeAhead.ArgbColor;
            settings.GraphLinesColor = lines.ArgbColor;
            settings.ShowBestSegments = bestSegments.IsChecked == true;
            settings.GraphGoldColor = gold.ArgbColor;
            settings.IsLiveGraph = liveGraph.IsChecked == true;
            settings.FlipGraph = flip.IsChecked == true;
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>HotkeyIndicatorSettings: the two colors, then Height (Width in a horizontal layout), 1-50.</summary>
    private void BuildHotkeyIndicatorFields()
    {
        HotkeyIndicatorComponentSettings settings = HotkeyIndicatorComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        bool horizontal = _layoutMode == LayoutMode.Horizontal;
        ColorSwatchButton on = AddColor("Global Hotkeys On Color:", settings.HotkeysOnColor);
        ColorSwatchButton off = AddColor("Global Hotkeys Off Color:", settings.HotkeysOffColor);
        NumericUpDown size = horizontal
            ? AddNumber("Width:", (decimal)settings.IndicatorWidth, 1, 50)
            : AddNumber("Height:", (decimal)settings.IndicatorHeight, 1, 50);

        _applyMore = () =>
        {
            settings.HotkeysOnColor = on.ArgbColor;
            settings.HotkeysOffColor = off.ArgbColor;
            if (size.Value is { } value)
            {
                if (horizontal)
                {
                    settings.IndicatorWidth = (float)value;
                }
                else
                {
                    settings.IndicatorHeight = (float)value;
                }
            }

            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>ManualGameTimeSettings: Game Time Input, Segment Times or Split Times.</summary>
    private void BuildManualGameTimeFields()
    {
        ManualGameTimeComponentSettings settings = ManualGameTimeComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        SettingsCard group = BeginGroup("Game Time Input");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        group.Children.Add(row);
        string name = Guid.NewGuid().ToString("N");
        var segmentTimes = new RadioButton { Content = "Segment Times", GroupName = name, IsChecked = settings.UseSegmentTimes };
        var splitTimes = new RadioButton { Content = "Split Times", GroupName = name, IsChecked = !settings.UseSegmentTimes };
        foreach (RadioButton radio in new[] { segmentTimes, splitTimes })
        {
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked == true)
                {
                    NotifyPreview();
                }
            };
            row.Children.Add(radio);
        }

        _applyMore = () =>
        {
            settings.UseSegmentTimes = segmentTimes.IsChecked == true;
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>
    /// WorldRecordSettings: Background, Display 2 Rows and Centered Text (off and greyed with two
    /// rows), Text Color, Time Color, Filter Leaderboard By (Region, Platform, Variables,
    /// Subcategories, Timing Method) and Precision.
    /// </summary>
    private void BuildWorldRecordFields()
    {
        WorldRecordComponentSettings settings = WorldRecordComponentSettings.FromInnerXml(_component.SettingsInnerXml);
        BackgroundRows background = AddBackground(settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2);
        Control rows = _fields;
        CheckBox twoRows = AddTwoRows(rows, settings.Display2Rows);
        CheckBox centered = AddCheckTo(rows, "Centered Text", settings.CenteredText);

        // chkTwoRows_CheckedChanged: with two rows Centered Text is unchecked and greyed (the
        // setting itself stays).
        void TwoRowsChanged()
        {
            bool two = twoRows.IsChecked == true;
            centered.IsEnabled = !two;
            if (two)
            {
                centered.IsChecked = false;
            }
            else
            {
                centered.IsChecked = settings.CenteredText;
            }
        }

        twoRows.IsCheckedChanged += (_, _) => TwoRowsChanged();
        TwoRowsChanged();

        // LiveSplitNative only (Basten 17 Oct, Separate Time): "World Record by inkstar" on the left and the time
        // on the right; for the name / value row, so greyed while Centered Text is on.
        CheckBox separateTime = AddCheckTo(rows, "Separate Time", settings.SeparateTime);
        void CenteredChanged() => separateTime.IsEnabled = centered.IsChecked != true;
        centered.IsCheckedChanged += (_, _) => CenteredChanged();
        CenteredChanged();

        ColorOverride text = AddOverrideColor(_fields, "Text Color", settings.OverrideTextColor, settings.TextColor);
        ColorOverride time = AddOverrideColor(_fields, "Time Color", settings.OverrideTimeColor, settings.TimeColor);
        SettingsCard filters = BeginGroup("Filter Leaderboard By");
        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        filters.Children.Add(filterRow);
        CheckBox region = AddCheckTo(filterRow, "Region", settings.FilterRegion);
        CheckBox platform = AddCheckTo(filterRow, "Platform", settings.FilterPlatform);
        CheckBox variables = AddCheckTo(filterRow, "Variables", settings.FilterVariables);
        CheckBox subcategories = AddCheckTo(filterRow, "Subcategories", settings.FilterSubcategories);
        ComboBox timing = AddComboTo(
            filters,
            "Timing Method:",
            ["Default for Leaderboard", "Real Time", "Real Time Without Loads", "Game Time"],
            settings.TimingMethod);

        SettingsCard precisionGroup = BeginGroup("Precision");
        var precisionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        precisionGroup.Children.Add(precisionRow);
        string group = Guid.NewGuid().ToString("N");
        var precisions = new List<(RadioButton Radio, WorldRecordPrecisionType Type)>();
        foreach ((string label, WorldRecordPrecisionType type) in new[]
        {
            ("From Leaderboard", WorldRecordPrecisionType.FromLeaderboard),
            ("Seconds", WorldRecordPrecisionType.Seconds),
            ("Milliseconds", WorldRecordPrecisionType.Milliseconds),
        })
        {
            var radio = new RadioButton { Content = label, GroupName = group, IsChecked = settings.PrecisionType == type };
            radio.IsCheckedChanged += (_, _) =>
            {
                if (radio.IsChecked == true)
                {
                    NotifyPreview();
                }
            };
            precisionRow.Children.Add(radio);
            precisions.Add((radio, type));
        }

        _applyMore = () =>
        {
            (settings.BackgroundGradient, settings.BackgroundColor, settings.BackgroundColor2) = background.Read();
            settings.Display2Rows = ReadTwoRows(twoRows, settings.Display2Rows);
            if (centered.IsEnabled)
            {
                settings.CenteredText = centered.IsChecked == true;
            }

            settings.SeparateTime = separateTime.IsChecked == true;
            (settings.OverrideTextColor, settings.TextColor) = text.Read();
            (settings.OverrideTimeColor, settings.TimeColor) = time.Read();
            settings.FilterRegion = region.IsChecked == true;
            settings.FilterPlatform = platform.IsChecked == true;
            settings.FilterVariables = variables.IsChecked == true;
            settings.FilterSubcategories = subcategories.IsChecked == true;
            settings.TimingMethod = timing.SelectedItem?.ToString() ?? settings.TimingMethod;
            settings.PrecisionType = precisions.FirstOrDefault(p => p.Radio.IsChecked == true) is { Radio: not null } chosen
                ? chosen.Type
                : settings.PrecisionType;
            _component.SettingsInnerXml = settings.ToInnerXml();
        };
    }

    /// <summary>The background row of every component: gradient, then one color, or two with a gradient.</summary>
    private sealed record BackgroundRows(ComboBox Gradient, ColorSwatchButton First, ColorSwatchButton Second)
    {
        public (string Gradient, ArgbColor First, ArgbColor Second) Read() =>
            (Gradient.SelectedItem?.ToString() ?? "Plain", First.ArgbColor, Second.ArgbColor);
    }

    /// <summary>
    /// "Background Color:" with Plain / Vertical / Horizontal: Plain shows one color, a gradient
    /// both (Windows hides btnColor1 for Plain).
    /// </summary>
    private BackgroundRows AddBackground(string gradient, ArgbColor first, ArgbColor second)
    {
        ComboBox combo = AddCombo("Background Gradient", GradientLabels, gradient);
        ColorSwatchButton firstSwatch = AddColor("Background Color", first);
        ColorSwatchButton secondSwatch = AddColor("Background Color 2", second);
        void ShowSecond() => Row(secondSwatch).IsVisible = combo.SelectedItem?.ToString() != "Plain";
        combo.SelectionChanged += (_, _) => ShowSecond();
        ShowSecond();
        return new BackgroundRows(combo, firstSwatch, secondSwatch);
    }

    /// <summary>A color group: Override Layout Settings and the color it enables.</summary>
    private sealed record ColorOverride(CheckBox Override, ColorSwatchButton Color)
    {
        public (bool Override, ArgbColor Color) Read() => (Override.IsChecked == true, Color.ArgbColor);
    }

    private ColorOverride AddOverrideColor(Control host, string title, bool overrideColor, ArgbColor color, bool nested = false)
    {
        SettingsCard group = BeginGroup(title, nested, host);
        CheckBox check = AddCheckTo(group, "Override Layout Settings", overrideColor);
        ColorSwatchButton swatch = AddColorTo(group, "Color:", color);
        WireEnable(check, Row(swatch));
        return new ColorOverride(check, swatch);
    }

    /// <summary>Display 2 Rows: in a horizontal layout always two rows (checked, disabled, the setting kept).</summary>
    private CheckBox AddTwoRows(Control host, bool value)
    {
        bool horizontal = _layoutMode == LayoutMode.Horizontal;
        CheckBox box = AddCheckTo(host, "Display 2 Rows", horizontal || value);
        box.IsEnabled = !horizontal;
        return box;
    }

    private bool ReadTwoRows(CheckBox box, bool saved) =>
        _layoutMode == LayoutMode.Horizontal ? saved : box.IsChecked == true;

    private static TimeAccuracy ReadAccuracy(ComboBox combo, TimeAccuracy saved) =>
        Enum.TryParse(combo.SelectedItem?.ToString(), out TimeAccuracy accuracy) ? accuracy : saved;

    /// <summary>The label + control row a field sits in (to grey out or hide the whole row).</summary>
    private static Control Row(Control field) => field.Parent as Control ?? field;

    private (TextBox Box, SettingsField Row) AddTextTo(Control host, string label, string value)
    {
        var box = new TextBox { Text = value, Width = 220 };
        // Live while typing (DataSourceUpdateMode.OnPropertyChanged).
        box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                NotifyPreview();
            }
        };
        SettingsField row = Labeled(label, box);
        Rows(host).Add(row);
        return (box, row);
    }

    /// <summary>
    /// CounterComponentSettings.SetHotkeyHandlers: the box shows the key as Windows names it
    /// ("Add", "Add, Control"); clicking it shows "Set Hotkey..." and the next key pressed
    /// becomes the hotkey (Escape: None). Command combinations have no Windows form and are ignored.
    /// </summary>
    private void AddHotkeyBox(Control host, string label, Func<string> get, Action<string> set)
    {
        static string Show(string key) => string.IsNullOrEmpty(key) ? "None" : key;
        var box = new TextBox { Text = Show(get()), Width = 220, IsReadOnly = true };
        box.GotFocus += (_, _) => box.Text = "Set Hotkey...";
        box.LostFocus += (_, _) => box.Text = Show(get());
        box.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is Key.Tab)
            {
                return;
            }

            e.Handled = true;
            if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.None)
            {
                return;
            }

            string? key = e.Key == Key.Escape ? "" : WindowsKeys.FromChord(Chord(e));
            if (key is null)
            {
                return;
            }

            set(key);
            box.Text = Show(key);
            NotifyPreview();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Rows(host).Add(Labeled(label, box));
    }

    /// <summary>The pressed keys as MainWindow names a hotkey chord ("Ctrl+Alt+Shift+Meta+Key").</summary>
    private static string Chord(KeyEventArgs e)
    {
        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Meta");
        }

        parts.Add(e.Key.ToString());
        return string.Join("+", parts);
    }
}
