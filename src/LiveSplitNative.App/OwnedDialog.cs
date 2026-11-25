using Avalonia.Controls;
using LiveSplitNative.App.Platform;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App;

/// <summary>
/// Shows a modal child above its immediate owner without letting an Always-on-Top parent cover
/// it, and without lowering the parent (lowered, the timer fell behind other apps' windows).
/// This is deliberately shared by the complete dialog chain.
/// </summary>
internal static class OwnedDialog
{
    public static async Task<T> ShowAsync<T>(Window owner, Window child)
    {
        OwnedDialogTopmostState state = Begin(owner, child);
        child.Opened += OnChildOpened;
        try
        {
            return await child.ShowDialog<T>(owner);
        }
        finally
        {
            child.Opened -= OnChildOpened;
            Restore(owner, child, state);
        }

        void OnChildOpened(object? sender, EventArgs e) => Opened(owner, child);
    }

    public static async Task ShowAsync(Window owner, Window child)
    {
        OwnedDialogTopmostState state = Begin(owner, child);
        child.Opened += OnChildOpened;
        try
        {
            await child.ShowDialog(owner);
        }
        finally
        {
            child.Opened -= OnChildOpened;
            Restore(owner, child, state);
        }

        void OnChildOpened(object? sender, EventArgs e) => Opened(owner, child);
    }

    /// <summary>The child goes above its owner by window level; the owner keeps its own.</summary>
    private static void Opened(Window owner, Window child)
    {
        NativeWindowFrameFactory.Current.PlaceAbove(child, owner);
        Activate(child);
    }

    private static OwnedDialogTopmostState Begin(Window owner, Window child)
    {
        var state = new OwnedDialogTopmostState(owner.Topmost, child.Topmost);
        owner.Topmost = state.OwnerWhileOpen;
        child.Topmost = state.ChildWhileOpen;
        return state;
    }

    private static void Restore(
        Window owner,
        Window child,
        OwnedDialogTopmostState state)
    {
        child.Topmost = state.ChildBefore;
        owner.Topmost = state.OwnerBefore;
        Activate(owner);
    }

    private static void Activate(Window window)
    {
        MacOsAppActivation.ActivateIgnoringOtherApps();
        window.Activate();
    }
}
