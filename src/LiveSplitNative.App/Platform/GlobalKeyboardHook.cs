namespace LiveSplitNative.App.Platform;

/// <summary>
/// CompositeHook's keyboard part: every key press, whichever app has the keyboard, as a hotkey
/// chord (macOS: an event tap; Linux: X11's RECORD extension).
/// </summary>
internal interface IGlobalKeyboardHook : IDisposable
{
    /// <summary>A key chord was pressed (raised on the hook's own thread).</summary>
    event Action<string>? KeyPressed;

    /// <summary>Whether a chord is kept from the app that has the keyboard (asked on the hook's thread).</summary>
    Func<string, bool>? ShouldConsume { get; set; }

    /// <summary>Whether key presses anywhere are seen.</summary>
    bool IsActive { get; }

    /// <summary>Starts (or restarts) the hook; <paramref name="consume"/> asks to keep keys from other programs.</summary>
    void Start(bool consume);

    void Stop();
}

internal static class GlobalKeyboardHooks
{
    /// <summary>The hook for this system, or null where there is none (Wayland without X11, Windows).</summary>
    public static IGlobalKeyboardHook? Create()
    {
        if (MacGlobalKeyboardHook.IsSupported)
        {
            return new MacGlobalKeyboardHook();
        }

        if (X11GlobalKeyboardHook.IsSupported)
        {
            return new X11GlobalKeyboardHook();
        }

        return null;
    }
}
