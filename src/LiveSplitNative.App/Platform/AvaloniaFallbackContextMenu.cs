using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Non-macOS / headless fallback. Uses Avalonia <see cref="ContextMenu"/> — not OS-native.
/// Linux should replace this with GTK/Qt/XDG when that platform ships.
/// </summary>
internal sealed class AvaloniaFallbackContextMenu : INativeContextMenu
{
    public bool IsSystemNative => false;
    public string BackendName => "Avalonia.ContextMenu";

    public void Show(Window owner, PixelPoint screenPoint, NativeMenuModel model)
    {
        var menu = new ContextMenu();
        foreach (Control item in ToAvaloniaItems(model.Items))
        {
            menu.Items.Add(item);
        }

        MenuChrome.PrepareFullHeight(menu, owner);

        // Open relative to owner; Avalonia places near the pointer when possible.
        void Open()
        {
            menu.Open(owner);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Open();
        }
        else
        {
            Dispatcher.UIThread.Post(Open);
        }

        _ = screenPoint; // reserved for a future PlacementRect path
    }

    internal static List<Control> ToAvaloniaItems(IReadOnlyList<NativeMenuEntry> entries)
    {
        var list = new List<Control>(entries.Count);
        foreach (NativeMenuEntry entry in entries)
        {
            list.Add(ToAvalonia(entry));
        }

        return list;
    }

    private static Control ToAvalonia(NativeMenuEntry entry)
    {
        if (entry is NativeMenuSeparator)
        {
            return new Separator();
        }

        var action = (NativeMenuAction)entry;
        var item = new MenuItem
        {
            Header = action.Header,
            IsEnabled = action.IsEnabled,
        };

        if (action.IsChecked)
        {
            item.ToggleType = MenuItemToggleType.CheckBox;
            item.IsChecked = true;
        }

        if (action.Children is not null)
        {
            foreach (Control child in ToAvaloniaItems(action.Children))
            {
                item.Items.Add(child);
            }
        }
        else
        {
            item.Click += (_, _) => action.Invoke();
        }

        return item;
    }
}
