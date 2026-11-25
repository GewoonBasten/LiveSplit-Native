using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.Options;

namespace LiveSplitNative.App;

/// <summary>
/// App-level Appearance (System/Light/Dark) for settings chrome + native menus.
/// Does not touch layout/.lsl timer paint.
/// </summary>
internal static class AppThemeController
{
    private static readonly INativeAppearance Native = NativeAppearanceFactory.Current;
    private static AppAppearanceMode _mode = AppAppearanceMode.System;

    public static AppAppearanceMode CurrentMode => _mode;

    public static event Action? Changed;

    public static void Apply(AppAppearanceMode mode)
    {
        _mode = mode;
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = mode switch
            {
                AppAppearanceMode.Light => ThemeVariant.Light,
                AppAppearanceMode.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default, // follow OS
            };
        }

        Native.Apply(mode);
        ApplyToOpenDialogs();
        Changed?.Invoke();
    }

    /// <summary>Dialogs follow app appearance; timer MainWindow keeps its own variant.</summary>
    public static void ConfigureDialog(Window window)
    {
        // Never force Light — hardcoded light faces + Dark text caused invisible Settings labels.
        window.RequestedThemeVariant = null; // inherit Application
        window.FontSize = AppFonts.ControlFontSize;
        window.FontFamily = new FontFamily(AppFonts.ChromeFamily);
        // Clear local overrides so DialogStyles semantic brushes / inheritance apply.
        window.ClearValue(Window.BackgroundProperty);
        window.ClearValue(Window.ForegroundProperty);
    }

    /// <summary>Resolve a semantic dialog brush for the current Appearance theme.</summary>
    public static IBrush? GetDialogBrush(string resourceKey, Control? relativeTo = null)
    {
        ThemeVariant? variant = relativeTo?.ActualThemeVariant
            ?? Application.Current?.ActualThemeVariant
            ?? ThemeVariant.Default;
        if (Application.Current?.TryGetResource(resourceKey, variant, out object? value) == true
            && value is IBrush brush)
        {
            return brush;
        }

        return null;
    }

    private static void ApplyToOpenDialogs()
    {
        if (Application.Current?.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        foreach (Window window in desktop.Windows)
        {
            if (window is MainWindow)
            {
                // Timer surface: leave RequestedThemeVariant alone (layout colors from .lsl).
                continue;
            }

            window.RequestedThemeVariant = null;
            window.ClearValue(Window.BackgroundProperty);
            window.ClearValue(Window.ForegroundProperty);
        }
    }
}
