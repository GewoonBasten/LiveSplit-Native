using LiveSplitNative.Core.Input;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.Core.Tests;

/// <summary>Gamepad hotkeys named and pressed as LiveSplit's GamepadHook (DirectInput) does it.</summary>
public class GamepadInputTests
{
    private const string Pad = "Wireless Controller 054C-09CC";

    private static string? Press(GamepadInput input, int page, int usage, long value, long min = 0, long max = 1, bool mouse = false, uint element = 1) =>
        input.Value("dev", Pad, element, page, usage, value, min, max, mouse);

    [Fact]
    public void Buttons_Are_Numbered_From_Zero_And_Fire_When_Pressed()
    {
        var input = new GamepadInput();
        Assert.Equal("Buttons0 " + Pad, Press(input, 9, 1, 1));
        Assert.Null(Press(input, 9, 1, 1)); // still held
        Assert.Null(Press(input, 9, 1, 0)); // released
        Assert.Equal("Buttons0 " + Pad, Press(input, 9, 1, 1));
        Assert.Equal("Buttons11 " + Pad, Press(input, 9, 12, 1, element: 12));
    }

    [Fact]
    public void Axes_Count_At_Either_End()
    {
        var input = new GamepadInput();
        Assert.Null(Press(input, 1, 0x30, 128, 0, 255));
        Assert.Equal("X+ " + Pad, Press(input, 1, 0x30, 255, 0, 255));
        Assert.Null(Press(input, 1, 0x30, 254, 0, 255)); // 0xFEFE: still at the end
        Assert.Equal("X- " + Pad, Press(input, 1, 0x30, 0, 0, 255));
        Assert.Equal("RotationZ+ " + Pad, Press(input, 1, 0x35, 32767, -32768, 32767, element: 2));
        Assert.Equal("Z+ " + Pad, Press(input, 2, 0xC5, 1023, 0, 1023, element: 3));
    }

    [Fact]
    public void A_Hat_Switch_Is_A_Point_Of_View()
    {
        var input = new GamepadInput();
        Assert.Equal("POV_0_Up " + Pad, Press(input, 1, 0x39, 0, 0, 7));
        Assert.Equal("POV_0_Right " + Pad, Press(input, 1, 0x39, 2, 0, 7));
        Assert.Null(Press(input, 1, 0x39, 8, 0, 7)); // centered
        Assert.Equal("POV_0_DownLeft " + Pad, Press(input, 1, 0x39, 5, 0, 7));
        Assert.Equal("POV_0_Left " + Pad, Press(input, 1, 0x39, 4, 1, 4, element: 2));
    }

    [Fact]
    public void A_Mouse_Has_Middle_Side_Buttons_And_The_Wheel()
    {
        var input = new GamepadInput();
        Assert.Null(Press(input, 9, 1, 1, mouse: true));
        Assert.Null(Press(input, 9, 2, 1, mouse: true, element: 2));
        Assert.Equal("Middle " + Pad, Press(input, 9, 3, 1, mouse: true, element: 3));
        Assert.Equal("Buttons3 " + Pad, Press(input, 9, 4, 1, mouse: true, element: 4));
        Assert.Null(Press(input, 1, 0x30, 127, -127, 127, mouse: true, element: 5));
        Assert.Equal("Scroll_Up " + Pad, Press(input, 1, 0x38, 1, -127, 127, mouse: true, element: 6));
        Assert.Equal("Scroll_Up " + Pad, Press(input, 1, 0x38, 1, -127, 127, mouse: true, element: 6));
        Assert.Equal("Scroll_Down " + Pad, Press(input, 1, 0x38, -2, -127, 127, mouse: true, element: 6));
    }

    [Fact]
    public void Device_Names_Have_No_Commas_And_Read_As_Gamepad_Buttons()
    {
        Assert.Equal("Pro Controller 057E-2009", GamepadInput.DeviceName("Pro  Controller", 0x057E, 0x2009));
        Assert.Equal("Pad 1 Left 0001-0002", GamepadInput.DeviceName("Pad 1, Left", 1, 2));
        Assert.Equal("Gamepad 0000-0000", GamepadInput.DeviceName(null, 0, 0));
        Assert.True(GamepadInput.IsGamepadButton("Buttons0 " + Pad));
        Assert.False(GamepadInput.IsGamepadButton("Ctrl+Shift+Add"));
        Assert.False(GamepadInput.IsGamepadButton("D1, Control"));
        // Saved as it is (KeyOrButton.ToString), and read back the same.
        Assert.Equal("Buttons0 " + Pad, WindowsKeys.FromChord("Buttons0 " + Pad));
    }
}
