namespace LiveSplitNative.Core.UI;

/// <summary>LiveSplit.Counter Counter: a count that starts at the initial value and steps by the increment.</summary>
public sealed class CounterValue
{
    public CounterValue(int initialValue = 0, int increment = 1)
    {
        InitialValue = initialValue;
        IncrementValue = increment;
        Count = initialValue;
    }

    public int Count { get; private set; }
    public int InitialValue { get; }
    public int IncrementValue { get; private set; }

    /// <summary>Counter.Increment: stops at int.MaxValue.</summary>
    public bool Increment()
    {
        if (Count == int.MaxValue)
        {
            return false;
        }

        long next = (long)Count + IncrementValue;
        Count = (int)Math.Min(next, int.MaxValue);
        return next <= int.MaxValue;
    }

    /// <summary>Counter.Decrement: stops at int.MinValue.</summary>
    public bool Decrement()
    {
        if (Count == int.MinValue)
        {
            return false;
        }

        long next = (long)Count - IncrementValue;
        Count = (int)Math.Max(next, int.MinValue);
        return next >= int.MinValue;
    }

    public void Reset() => Count = InitialValue;

    public void SetIncrement(int increment) => IncrementValue = increment;
}

/// <summary>
/// CounterComponent: the counter a Counter component keeps while its layout is open, and its
/// hotkeys. A new counter starts when the component is loaded or its Initial Value changes
/// (Settings_CounterReinitialiseRequired); a new Increment Value applies to the running count
/// (Settings_IncrementUpdateRequired).
/// </summary>
public static class CounterLogic
{
    public static CounterValue For(LayoutComponent component, CounterComponentSettings settings)
    {
        if (component.RuntimeState is not CounterValue counter || counter.InitialValue != settings.InitialValue)
        {
            counter = new CounterValue(settings.InitialValue, settings.Increment);
            component.RuntimeState = counter;
        }
        else if (counter.IncrementValue != settings.Increment)
        {
            counter.SetIncrement(settings.Increment);
        }

        return counter;
    }

    /// <summary>Whether a Counter in the layout has Global Hotkeys on (the hook must run).</summary>
    public static bool HasGlobalHotkeys(Layout layout) =>
        layout.Components.Any(c => c.Path == StandardLayoutFactory.CounterPath
            && CounterComponentSettings.FromInnerXml(c.SettingsInnerXml).GlobalHotkeysEnabled);

    /// <summary>
    /// CounterComponent.hook_KeyOrButtonPressed: a key chord (this app's "Ctrl+Alt+Shift+Key" form)
    /// increments, decrements or resets every Counter whose hotkey it is. True when one did.
    /// </summary>
    public static bool HandleHotkey(Layout layout, string chord) => HandleHotkey(layout, chord, timerFocused: true);

    /// <summary>
    /// The same from the global keyboard hook: a Counter takes the key while the timer window has
    /// the keyboard, or anywhere with its Global Hotkeys on.
    /// </summary>
    public static bool HandleHotkey(Layout layout, string chord, bool timerFocused)
    {
        if (string.IsNullOrEmpty(chord))
        {
            return false;
        }

        bool handled = false;
        foreach (LayoutComponent component in layout.Components)
        {
            if (component.Path != StandardLayoutFactory.CounterPath)
            {
                continue;
            }

            CounterComponentSettings settings = CounterComponentSettings.FromInnerXml(component.SettingsInnerXml);
            if (!timerFocused && !settings.GlobalHotkeysEnabled)
            {
                continue;
            }

            CounterValue counter = For(component, settings);
            if (WindowsKeys.Matches(chord, settings.IncrementKey))
            {
                counter.Increment();
                handled = true;
            }

            if (WindowsKeys.Matches(chord, settings.DecrementKey))
            {
                counter.Decrement();
                handled = true;
            }

            if (WindowsKeys.Matches(chord, settings.ResetKey))
            {
                counter.Reset();
                handled = true;
            }
        }

        return handled;
    }
}

/// <summary>
/// Hotkeys as Windows writes them in a layout (System.Windows.Forms.Keys names: "Add",
/// "D1, Control", "F5, Shift, Control, Alt") and as this app writes a pressed chord
/// ("Ctrl+Alt+Shift+Key"). Command (Meta) has no Windows modifier, so a chord with it has no
/// Windows form.
/// </summary>
public static class WindowsKeys
{
    /// <summary>"Ctrl+Shift+Add" → "Add, Shift, Control" (Keys flags in Windows' order); null with Meta.</summary>
    public static string? FromChord(string chord)
    {
        // A gamepad button (KeyOrButton "Buttons0 <gamepad>") is written as it is.
        if (Input.GamepadInput.IsGamepadButton(chord))
        {
            return chord;
        }

        string[] parts = chord.Split('+', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        string key = parts[^1];
        bool ctrl = false, alt = false, shift = false;
        foreach (string modifier in parts[..^1])
        {
            switch (modifier)
            {
                case "Ctrl":
                    ctrl = true;
                    break;
                case "Alt":
                    alt = true;
                    break;
                case "Shift":
                    shift = true;
                    break;
                default:
                    return null;
            }
        }

        var names = new List<string> { key };
        if (shift)
        {
            names.Add("Shift");
        }

        if (ctrl)
        {
            names.Add("Control");
        }

        if (alt)
        {
            names.Add("Alt");
        }

        return string.Join(", ", names);
    }

    /// <summary>"Add, Shift, Control" → "Ctrl+Shift+Add"; empty for none.</summary>
    public static string ToChord(string? windowsKeys)
    {
        if (string.IsNullOrWhiteSpace(windowsKeys))
        {
            return "";
        }

        string[] names = windowsKeys.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool Has(string modifier) => names.Any(n => string.Equals(n, modifier, StringComparison.OrdinalIgnoreCase));
        string key = names.FirstOrDefault(n => n is not ("Control" or "Shift" or "Alt")) ?? "";
        var parts = new List<string>();
        if (Has("Control"))
        {
            parts.Add("Ctrl");
        }

        if (Has("Alt"))
        {
            parts.Add("Alt");
        }

        if (Has("Shift"))
        {
            parts.Add("Shift");
        }

        parts.Add(key);
        return string.Join("+", parts);
    }

    /// <summary>KeyOrButton ==: the same key and modifiers (key names compared without case, as Windows parses them).</summary>
    public static bool Matches(string chord, string? windowsKeys)
    {
        string binding = ToChord(windowsKeys);
        return binding.Length > 0 && string.Equals(chord, binding, StringComparison.OrdinalIgnoreCase);
    }
}
