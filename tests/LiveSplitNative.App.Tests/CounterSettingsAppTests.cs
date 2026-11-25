using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using LiveSplitNative.Core.UI;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// The Counter's hotkey boxes (CounterComponentSettings.SetHotkeyHandlers): the next key pressed
/// becomes the hotkey, written as Windows writes it; Escape sets None; Command is not a Windows
/// modifier, so a Command combination is ignored.
/// </summary>
public class CounterSettingsAppTests
{
    private static TextBox HotkeyBox(ComponentSettingsEditor editor, string label) =>
        editor.GetLogicalDescendants().OfType<TextBox>().Single(box =>
            box.IsReadOnly
            && box.GetLogicalParent() is Panel row
            && row.Children.OfType<TextBlock>().Any(t => LabelText.Is(t, label)));

    private static void Press(TextBox box, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    private static CounterComponentSettings Saved(ComponentSettingsEditor editor) =>
        CounterComponentSettings.FromInnerXml(editor.Apply().SettingsInnerXml);

    [AvaloniaFact]
    public void A_Key_Press_Sets_The_Hotkey_As_Windows_Names_It()
    {
        var editor = new ComponentSettingsEditor(StandardLayoutFactory.CreateBuiltin(StandardLayoutFactory.CounterPath));
        int notified = 0;
        editor.SettingsChanged = () => notified++;
        TextBox increment = HotkeyBox(editor, "Increment Counter:");
        TextBox reset = HotkeyBox(editor, "Reset Counter:");
        Assert.Equal("Add", increment.Text);

        Press(increment, Key.F5, KeyModifiers.Control | KeyModifiers.Shift);
        Assert.Equal("F5, Shift, Control", increment.Text);
        Assert.Equal("F5, Shift, Control", Saved(editor).IncrementKey);
        Assert.True(notified > 0);

        // A modifier alone waits for the key; Command combinations are ignored.
        Press(increment, Key.LeftCtrl, KeyModifiers.Control);
        Press(increment, Key.A, KeyModifiers.Meta);
        Assert.Equal("F5, Shift, Control", Saved(editor).IncrementKey);

        Press(reset, Key.Escape);
        Assert.Equal("None", reset.Text);
        Assert.Equal("", Saved(editor).ResetKey);
        Assert.Equal("Subtract", Saved(editor).DecrementKey);
    }
}
