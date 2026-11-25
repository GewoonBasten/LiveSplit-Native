namespace LiveSplitNative.App.Platform;

/// <summary>
/// Platform-agnostic context-menu tree. Shared by AppKit NSMenu (macOS) and the
/// Avalonia fallback — no colors, fonts, or padding here.
/// </summary>
public sealed class NativeMenuModel
{
    public required IReadOnlyList<NativeMenuEntry> Items { get; init; }
}

public abstract class NativeMenuEntry;

public sealed class NativeMenuSeparator : NativeMenuEntry;

public sealed class NativeMenuAction : NativeMenuEntry
{
    public required string Header { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsChecked { get; init; }
    public Action? OnClick { get; init; }
    public Func<Task>? OnClickAsync { get; init; }
    public IReadOnlyList<NativeMenuEntry>? Children { get; init; }

    public void Invoke()
    {
        if (OnClickAsync is not null)
        {
            _ = OnClickAsync();
            return;
        }

        OnClick?.Invoke();
    }
}
