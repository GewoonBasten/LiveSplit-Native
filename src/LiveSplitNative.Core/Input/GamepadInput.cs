namespace LiveSplitNative.Core.Input;

/// <summary>
/// LiveSplit's GamepadHook (93a11b85) for the values a Mac's HID devices report: a button or an
/// axis pushed to its end becomes a hotkey named as DirectInput names it ("Buttons0", "X+",
/// "RotationZ-", "POV_0_Up"; on a mouse "Middle", "Buttons3", "Scroll_Up"), followed by the
/// device ("Buttons0 Wireless Controller 054C-09CC", KeyOrButton.ToString). Windows names a
/// device by its DirectInput instance; here it is the product name with its vendor and product
/// IDs. A hotkey fires when the element becomes pressed (a scroll on every step).
/// </summary>
public sealed class GamepadInput
{
    // HID usage pages and usages (HID Usage Tables 1.4).
    public const int GenericDesktopPage = 0x01;
    public const int SimulationPage = 0x02;
    public const int ButtonPage = 0x09;
    public const int MouseUsage = 0x02;
    public const int JoystickUsage = 0x04;
    public const int GamePadUsage = 0x05;
    public const int MultiAxisControllerUsage = 0x08;
    private const int Wheel = 0x38;
    private const int HatSwitch = 0x39;

    // GamepadHook.IsPressed: an axis (0 … 65535) counts at either end.
    private const int ShortMaskMax = 0xFF00;
    private const int ShortMaskMin = 0xFF;

    private readonly Dictionary<(string Device, uint Element), string?> _pressed = [];

    /// <summary>The device part of a hotkey: the product name (no commas, as KeyOrButton reads them) and its IDs.</summary>
    public static string DeviceName(string? product, int vendorId, int productId)
    {
        string name = string.Join(' ', (product ?? "").Replace(',', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return $"{(name.Length > 0 ? name : "Gamepad")} {vendorId:X4}-{productId:X4}";
    }

    /// <summary>
    /// KeyOrButton(string): a gamepad button has a space and no ", " (a key with modifiers in
    /// Windows' Keys names has ", "; a chord has none of either).
    /// </summary>
    public static bool IsGamepadButton(string? hotkey) =>
        hotkey is not null && hotkey.Contains(' ', StringComparison.Ordinal) && !hotkey.Contains(", ", StringComparison.Ordinal);

    /// <summary>
    /// A value from an element of a device: the hotkey it presses, or null. <paramref name="device"/>
    /// tells devices apart (the same name may be connected twice); <paramref name="element"/> the element.
    /// </summary>
    public string? Value(
        string device,
        string deviceName,
        uint element,
        int usagePage,
        int usage,
        long value,
        long logicalMin,
        long logicalMax,
        bool isMouse)
    {
        string? name = Name(usagePage, usage, value, logicalMin, logicalMax, isMouse, out bool everyStep);
        if (everyStep)
        {
            return name is null ? null : name + " " + deviceName;
        }

        (string, uint) key = (device, element);
        _pressed.TryGetValue(key, out string? before);
        _pressed[key] = name;
        return name is not null && name != before ? name + " " + deviceName : null;
    }

    /// <summary>The element's pressed name for this value (null: not pressed, or not an element LiveSplit uses).</summary>
    private static string? Name(int page, int usage, long value, long min, long max, bool isMouse, out bool everyStep)
    {
        everyStep = false;
        if (page == ButtonPage)
        {
            if (usage < 1 || value == 0)
            {
                return null;
            }

            // A mouse's left and right buttons are never hotkeys; its third is Middle.
            return isMouse
                ? usage switch
                {
                    1 or 2 => null,
                    3 => "Middle",
                    _ => "Buttons" + (usage - 1),
                }
                : "Buttons" + (usage - 1);
        }

        if (page == GenericDesktopPage && usage == Wheel)
        {
            everyStep = true;
            return value == 0 ? null : value > 0 ? "Scroll_Up" : "Scroll_Down";
        }

        if (page == GenericDesktopPage && usage == HatSwitch)
        {
            return Pov(value, min, max);
        }

        string? axis = (page, usage) switch
        {
            (GenericDesktopPage, 0x30) => "X",
            (GenericDesktopPage, 0x31) => "Y",
            (GenericDesktopPage, 0x32) => "Z",
            (GenericDesktopPage, 0x33) => "RotationX",
            (GenericDesktopPage, 0x34) => "RotationY",
            (GenericDesktopPage, 0x35) => "RotationZ",
            (GenericDesktopPage, 0x36) => "Sliders0",
            (GenericDesktopPage, 0x37) => "Sliders1",
            // Triggers some controllers report on the Simulation page (brake, accelerator).
            (SimulationPage, 0xC5) => "Z",
            (SimulationPage, 0xC4) => "RotationZ",
            _ => null,
        };
        if (axis is null || max <= min || (isMouse && axis is "X" or "Y"))
        {
            return null;
        }

        long scaled = Math.Clamp((value - min) * 65535 / (max - min), 0, 65535);
        return scaled >= ShortMaskMax ? axis + "+" : scaled <= ShortMaskMin ? axis + "-" : null;
    }

    /// <summary>
    /// A hat switch as a DirectInput point of view: the direction in hundredths of degrees
    /// (clockwise from up), named by GamepadHook.ToString's ranges; none when centered.
    /// </summary>
    private static string? Pov(long value, long min, long max)
    {
        if (value < min || value > max || max < min)
        {
            return null;
        }

        long steps = max - min + 1;
        long angle = (value - min) * 36000 / steps;
        string direction = angle switch
        {
            < 2250 => "Up",
            < 6750 => "UpRight",
            < 11250 => "Right",
            < 15750 => "DownRight",
            < 20250 => "Down",
            < 24750 => "DownLeft",
            < 29250 => "Left",
            < 33750 => "UpLeft",
            _ => "Up",
        };
        return "POV_0_" + direction;
    }
}
