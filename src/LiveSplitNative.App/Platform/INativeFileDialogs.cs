using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Thin seam over OS file dialogs. Today Avalonia <see cref="IStorageProvider"/> already
/// maps to native macOS panels; Linux will need XDG portal / GTK awareness documented in
/// <c>docs/linux-readiness.md</c>. Callers should use this type instead of reaching for
/// platform-specific dialog chrome.
/// </summary>
public interface INativeFileDialogs
{
    string BackendName { get; }

    Task<IReadOnlyList<IStorageFile>> OpenFilesAsync(Window owner, FilePickerOpenOptions options);

    Task<IStorageFile?> SaveFileAsync(Window owner, FilePickerSaveOptions options);
}

public sealed class AvaloniaStorageFileDialogs : INativeFileDialogs
{
    public string BackendName =>
        OperatingSystem.IsMacOS()
            ? "Avalonia.StorageProvider→AppKit"
            : OperatingSystem.IsLinux()
                ? "Avalonia.StorageProvider→Linux(XDG/GTK TBD)"
                : "Avalonia.StorageProvider";

    public Task<IReadOnlyList<IStorageFile>> OpenFilesAsync(Window owner, FilePickerOpenOptions options) =>
        owner.StorageProvider.OpenFilePickerAsync(options);

    public Task<IStorageFile?> SaveFileAsync(Window owner, FilePickerSaveOptions options) =>
        owner.StorageProvider.SaveFilePickerAsync(options);
}

public static class NativeFileDialogs
{
    public static INativeFileDialogs Current { get; } = new AvaloniaStorageFileDialogs();
}
