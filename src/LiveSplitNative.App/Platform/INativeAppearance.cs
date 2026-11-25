using LiveSplitNative.Core.Options;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// OS-native appearance for menus/dialogs (NSAppearance on macOS; GTK/Qt later on Linux).
/// Keep AppKit out of Core — App layer only.
/// </summary>
public interface INativeAppearance
{
    string BackendName { get; }

    /// <summary>Apply System / Light / Dark to the process native chrome.</summary>
    void Apply(AppAppearanceMode mode);
}

public static class NativeAppearanceFactory
{
    private static INativeAppearance? _cached;

    public static INativeAppearance Current => _cached ??= Create();

    public static INativeAppearance Create()
    {
        if (OperatingSystem.IsMacOS() && MacOsNativeAppearance.IsAvailable)
        {
            return new MacOsNativeAppearance();
        }

        return new NoOpNativeAppearance();
    }
}

internal sealed class NoOpNativeAppearance : INativeAppearance
{
    public string BackendName => "None";

    public void Apply(AppAppearanceMode mode)
    {
        _ = mode;
    }
}
