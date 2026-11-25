using Avalonia.Input;

namespace LiveSplitNative.App;

/// <summary>
/// A key with its modifiers in the form the hotkey profile stores ("Ctrl+Alt+Shift+Meta+Key", the
/// key as Avalonia names it). The timer window's key presses and the global keyboard hook both
/// produce it, so a binding matches the same way whichever one saw the key.
/// </summary>
internal static class HotkeyChord
{
    public static string Format(Key key, KeyModifiers modifiers)
    {
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return "";
        }

        var parts = new List<string>(5);
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Meta");
        }

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
