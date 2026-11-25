using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace LiveSplitNative.App;

/// <summary>
/// Size flyout menus to full content height. Fluent defaults often clip into a
/// short ScrollViewer; we raise MaxHeight to the screen working area so every
/// item is visible, with scroll only as a last resort for huge lists.
/// </summary>
internal static class MenuChrome
{
    public static void PrepareFullHeight(ContextMenu menu, Visual? relativeTo = null)
    {
        menu.MaxHeight = ResolveScreenMaxHeightDip(relativeTo);
    }

    public static void PrepareFullHeight(MenuFlyoutPresenter presenter, Visual? relativeTo = null)
    {
        presenter.MaxHeight = ResolveScreenMaxHeightDip(relativeTo);
    }

    /// <summary>Screen working-area height in DIPs (for dialog MaxHeight / menus).</summary>
    public static double ResolveScreenMaxHeightDipPublic(Visual? relativeTo) =>
        ResolveScreenMaxHeightDip(relativeTo);

    private static double ResolveScreenMaxHeightDip(Visual? relativeTo)
    {
        try
        {
            TopLevel? top = relativeTo is null ? null : TopLevel.GetTopLevel(relativeTo);
            Screen? screen = null;
            if (top?.Screens is { } screens)
            {
                screen = (relativeTo is not null ? screens.ScreenFromVisual(relativeTo) : null)
                    ?? screens.Primary
                    ?? screens.All.FirstOrDefault();
            }

            if (screen is not null)
            {
                // WorkingArea is device pixels; MaxHeight is DIPs.
                double dipH = screen.WorkingArea.Height / Math.Max(0.5, screen.Scaling);
                return Math.Max(240, dipH - 24);
            }
        }
        catch
        {
            // fall through
        }

        return 10000;
    }
}
