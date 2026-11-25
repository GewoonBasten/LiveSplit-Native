using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;

namespace LiveSplitNative.App;

/// <summary>
/// The last window size and scale changes, written to stderr with every open window's size when
/// the process dies (Basten, 12 Oct 2026: after a switch back to vertical, rendering failed with
/// "Unable to create pixel format shim surface", which Avalonia raises for a framebuffer it cannot
/// allocate, such as one with a zero or huge size). Cheap enough to always run.
/// </summary>
internal static class GeometryTrail
{
    private const int Capacity = 48;
    private static readonly Queue<string> Entries = new();
    private static readonly object Gate = new();

    public static void Add(string entry)
    {
        string line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " " + entry;
        lock (Gate)
        {
            Entries.Enqueue(line);
            while (Entries.Count > Capacity)
            {
                Entries.Dequeue();
            }
        }
    }

    /// <summary>The trail and every window's size, scale and position; never throws.</summary>
    public static void Dump(TextWriter writer)
    {
        try
        {
            writer.WriteLine("== geometry trail (oldest first)");
            lock (Gate)
            {
                foreach (string line in Entries)
                {
                    writer.WriteLine("   " + line);
                }
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                writer.WriteLine("== windows");
                foreach (Window window in desktop.Windows)
                {
                    writer.WriteLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"   {window.GetType().Name} visible={window.IsVisible} size={window.Width:0.#}x{window.Height:0.#} " +
                        $"client={window.ClientSize.Width:0.#}x{window.ClientSize.Height:0.#} scaling={window.RenderScaling:0.##} " +
                        $"position={window.Position} min={window.MinWidth:0.#}x{window.MinHeight:0.#} max={window.MaxWidth:0.#}x{window.MaxHeight:0.#}"));
                }

                Window? first = desktop.Windows.FirstOrDefault();
                if (first?.Screens is { } screens)
                {
                    foreach (Screen screen in screens.All)
                    {
                        writer.WriteLine($"   screen bounds={screen.Bounds} work={screen.WorkingArea} scaling={screen.Scaling}");
                    }
                }
            }

            writer.Flush();
        }
        catch (Exception ex)
        {
            try
            {
                writer.WriteLine("(geometry dump failed: " + ex.Message + ")");
            }
            catch
            {
                // Nothing left to report to.
            }
        }
    }
}
