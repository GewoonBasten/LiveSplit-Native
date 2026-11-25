using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LiveSplitNative.Core.Options;

namespace LiveSplitNative.App;

public partial class HotkeySettingsWindow : Window
{
    private readonly HotkeyProfileStub _profile;

    public HotkeySettingsWindow() : this(HotkeyProfileStub.CreateDefault())
    {
    }

    public HotkeySettingsWindow(HotkeyProfileStub profile)
    {
        InitializeComponent();
        _profile = new HotkeyProfileStub
        {
            SplitKey = profile.SplitKey,
            ResetKey = profile.ResetKey,
            SkipKey = profile.SkipKey,
            UndoKey = profile.UndoKey,
            PauseKey = profile.PauseKey,
            ToggleGlobalHotkeys = profile.ToggleGlobalHotkeys,
            SwitchComparisonPrevious = profile.SwitchComparisonPrevious,
            SwitchComparisonNext = profile.SwitchComparisonNext,
            GlobalHotkeysEnabled = profile.GlobalHotkeysEnabled,
            DoubleTapPrevention = profile.DoubleTapPrevention,
            HotkeyDelay = profile.HotkeyDelay,
        };
        GlobalHotkeysBox.IsChecked = _profile.GlobalHotkeysEnabled;
        DoubleTapBox.IsChecked = _profile.DoubleTapPrevention;
        AddCaptureRow("Split", () => _profile.SplitKey, v => _profile.SplitKey = v);
        AddCaptureRow("Reset", () => _profile.ResetKey, v => _profile.ResetKey = v);
        AddCaptureRow("Skip Split", () => _profile.SkipKey, v => _profile.SkipKey = v);
        AddCaptureRow("Undo Split", () => _profile.UndoKey, v => _profile.UndoKey = v);
        AddCaptureRow("Pause", () => _profile.PauseKey, v => _profile.PauseKey = v);
        AddCaptureRow("Toggle Global Hotkeys", () => _profile.ToggleGlobalHotkeys, v => _profile.ToggleGlobalHotkeys = v);
        AddCaptureRow("Previous Comparison", () => _profile.SwitchComparisonPrevious, v => _profile.SwitchComparisonPrevious = v);
        AddCaptureRow("Next Comparison", () => _profile.SwitchComparisonNext, v => _profile.SwitchComparisonNext = v);
    }

    public HotkeyProfileStub? Result { get; private set; }

    private void AddCaptureRow(string label, Func<string> get, Action<string> set)
    {
        var box = new TextBox
        {
            Text = string.IsNullOrEmpty(get()) ? "(unbound)" : get(),
            Width = 180,
            IsReadOnly = true,
            Watermark = "Click, then press a key",
        };
        // dialogCompact — not Button.dialog (MinWidth=75); match Settings Hotkeys Clear column.
        var clear = new Button
        {
            Content = "Clear",
            Width = 56,
            MinWidth = 56,
            Height = 24,
            MinHeight = 24,
        };
        clear.Classes.Add("dialogCompact");
        box.GotFocus += (_, _) => box.Text = "Press a key…";
        box.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Escape)
            {
                set("");
                box.Text = "(unbound)";
                e.Handled = true;
                return;
            }

            if (e.Key is Key.Tab or Key.None)
            {
                return;
            }

            string name = FormatKey(e);
            set(name);
            box.Text = name;
            e.Handled = true;
        };
        box.LostFocus += (_, _) =>
        {
            box.Text = string.IsNullOrEmpty(get()) ? "(unbound)" : get();
        };
        clear.Click += (_, _) =>
        {
            set("");
            box.Text = "(unbound)";
        };
        box.Width = double.NaN;
        box.Height = 26;
        clear.Width = 60;
        clear.MinWidth = 60;
        clear.Height = 26;
        clear.MinHeight = 26;
        clear.Margin = new Avalonia.Thickness(8, 0, 0, 0);
        Grid.SetColumn(clear, 1);
        FieldsPanel.Children.Add(new SettingsField
        {
            Label = label,
            Field = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { box, clear } },
        });
    }

    private static string FormatKey(KeyEventArgs e)
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

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _profile.GlobalHotkeysEnabled = GlobalHotkeysBox.IsChecked == true;
        _profile.DoubleTapPrevention = DoubleTapBox.IsChecked != false;
        Result = _profile;
        Close(Result);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
