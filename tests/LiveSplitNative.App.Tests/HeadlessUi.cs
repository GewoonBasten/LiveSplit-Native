using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>Small helpers for driving the real windows on Avalonia's headless platform.</summary>
internal static class HeadlessUi
{
    /// <summary>Runs queued UI work a few times so async continuations and dialogs settle.</summary>
    public static void Pump()
    {
        for (int i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>The one dialog the window has open, which must carry this title.</summary>
    public static Window Prompt(Window owner, string title)
    {
        Window prompt = Assert.Single(owner.OwnedWindows);
        Assert.Equal(title, prompt.Title);
        return prompt;
    }

    /// <summary>Presses the dialog button with this label and lets the result run.</summary>
    public static void Click(Window dialog, string label)
    {
        Button button = dialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, label));
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
    }

    public static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lsn-app-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static void Delete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: it is only a temp folder.
        }
    }
}
