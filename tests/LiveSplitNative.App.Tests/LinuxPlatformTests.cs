using Avalonia.Input;
using LiveSplitNative.App.Platform;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Linux's parts that do not need Linux to check: key names, controller buttons, sound commands.</summary>
public class LinuxPlatformTests
{
    /// <summary>X11 keysyms name the keys as Avalonia does, so hotkeys set in the window match the global ones.</summary>
    [Theory]
    [InlineData(0x61UL, Key.A)]
    [InlineData(0x5AUL, Key.Z)]
    [InlineData(0x31UL, Key.D1)]
    [InlineData(0xFFBEUL, Key.F1)]
    [InlineData(0xFFC9UL, Key.F12)]
    [InlineData(0xFFB0UL, Key.NumPad0)]
    [InlineData(0xFFB9UL, Key.NumPad9)]
    [InlineData(0xFF9EUL, Key.Insert)]
    [InlineData(0xFFAEUL, Key.Decimal)]
    [InlineData(0xFFABUL, Key.Add)]
    [InlineData(0xFF8DUL, Key.Return)]
    [InlineData(0x20UL, Key.Space)]
    [InlineData(0xFF51UL, Key.Left)]
    [InlineData(0xFF55UL, Key.PageUp)]
    [InlineData(0x2DUL, Key.OemMinus)]
    [InlineData(0xFFE1UL, Key.None)]
    [InlineData(0xFFE9UL, Key.None)]
    public void Keysyms_Are_Avalonias_Keys(ulong keysym, Key key) =>
        Assert.Equal(key, X11GlobalKeyboardHook.KeyForKeysym(keysym));

    [Fact]
    public void X11_Modifiers_Are_The_Chords_Modifiers()
    {
        Assert.Equal("Ctrl+Shift+F1", HotkeyChord.Format(Key.F1, X11GlobalKeyboardHook.Modifiers(0x01 | 0x04)));
        Assert.Equal("Alt+Meta+A", HotkeyChord.Format(Key.A, X11GlobalKeyboardHook.Modifiers(0x08 | 0x40)));
        // Num Lock (Mod2) and Caps Lock are not part of a chord.
        Assert.Equal("NumPad1", HotkeyChord.Format(Key.NumPad1, X11GlobalKeyboardHook.Modifiers(0x10 | 0x02)));
    }

    [Theory]
    [InlineData(0x130, 0)]
    [InlineData(0x131, 1)]
    [InlineData(0x13B, 11)]
    [InlineData(0x120, 0)]
    [InlineData(0x12F, 15)]
    [InlineData(0x2C0, 16)]
    public void Controller_Buttons_Are_Numbered(int code, int index) =>
        Assert.Equal(index, LinuxGamepadHook.ButtonIndex(code));

    [Fact]
    public void Keyboard_And_Mouse_Buttons_Are_Not_Controller_Buttons()
    {
        Assert.Null(LinuxGamepadHook.ButtonIndex(0x110)); // BTN_LEFT
        Assert.Null(LinuxGamepadHook.ButtonIndex(30)); // KEY_A
    }

    /// <summary>The desktop's player plays the sound at its volume; MP3 prefers ffplay or mpv.</summary>
    [Fact]
    public void Sounds_Play_With_The_Desktops_Player()
    {
        Func<string, bool> before = LinuxSoundPlayer.HasProgram;
        try
        {
            LinuxSoundPlayer.HasProgram = p => p is "pw-play" or "paplay";
            (string program, string[] arguments) = LinuxSoundPlayer.Command("/s/split.wav", 0.5f)!.Value;
            Assert.Equal("pw-play", program);
            Assert.Equal(["--volume", "0.5", "/s/split.wav"], arguments);

            LinuxSoundPlayer.HasProgram = p => p is "paplay";
            Assert.Equal(["--volume=32768", "/s/split.wav"], LinuxSoundPlayer.Command("/s/split.wav", 0.5f)!.Value.Arguments);

            LinuxSoundPlayer.HasProgram = p => p is "pw-play" or "ffplay";
            Assert.Equal("ffplay", LinuxSoundPlayer.Command("/s/pb.MP3", 1f)!.Value.Program);

            LinuxSoundPlayer.HasProgram = _ => false;
            Assert.Null(LinuxSoundPlayer.Command("/s/split.wav", 1f));
        }
        finally
        {
            LinuxSoundPlayer.HasProgram = before;
        }
    }
}
