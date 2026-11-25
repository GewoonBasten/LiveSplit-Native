namespace LiveSplitNative.Core.UI;

/// <summary>
/// Pure state transition used by every owned dialog. While a child is open it is topmost and
/// its owner keeps its own state; both are restored when that child closes. The owner used to
/// be demoted (Windows EditLayout sets TopMost = false), but on macOS a window dropped to the
/// normal level falls behind other apps' windows: the timer vanished while Layout Settings was
/// open (Basten, 12 Oct 2026). The child is kept above its owner by its window level instead
/// (INativeWindowFrame.PlaceAbove).
/// </summary>
public readonly record struct OwnedDialogTopmostState(
    bool OwnerBefore,
    bool ChildBefore)
{
    public bool OwnerWhileOpen => OwnerBefore;

    public bool ChildWhileOpen => true;
}
