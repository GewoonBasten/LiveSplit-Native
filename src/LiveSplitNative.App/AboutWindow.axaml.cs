using Avalonia.Controls;
using Avalonia.Interactivity;
using LiveSplitNative.App.Platform;

namespace LiveSplitNative.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        // The bundle's version (the release tag without its "v"); a build run outside a bundle has none.
        if (AppUpdater.CurrentVersion is { } version)
        {
            VersionText.Text = "Version " + MainWindow.VersionText(version);
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
