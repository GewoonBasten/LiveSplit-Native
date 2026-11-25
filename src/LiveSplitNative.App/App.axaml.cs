using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls;
using LiveSplitNative.App.Platform;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace LiveSplitNative.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        FluentTemplateSizes.Register();
        LayoutFontCollection.EnsureRegistered();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Linux: every window shows the app icon (taskbar, Alt-Tab). macOS takes it from the .app.
        string icon = Path.Combine(AppContext.BaseDirectory, "livesplit.png");
        if (OperatingSystem.IsLinux() && File.Exists(icon))
        {
            Window.IconProperty.OverrideDefaultValue<Window>(new WindowIcon(icon));
        }

        // Every window (timer, settings, dialogs) draws its colors as sRGB.
        Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) =>
        {
            string? colorSpace = NativeWindowFrameFactory.Current.UseSrgbColorSpace(window);
            if (colorSpace is not null && window is MainWindow)
            {
                Console.WriteLine($"Timer window color space: {colorSpace}");
            }

            // UiLocalizer.Apply when a form is shown.
            UiLocalizer.Apply(window);
        });

        // TimerForm.PerSecondTimer_Elapsed: every open window again each second, so texts set
        // after it opened are translated too.
        var localize = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        localize.Tick += (_, _) =>
        {
            if (UiLocalizer.Current.RequiresLocalization && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime app)
            {
                foreach (Window open in app.Windows)
                {
                    UiLocalizer.Apply(open);
                }
            }
        };
        localize.Start();

        // A component's exception on the UI thread is logged; the app keeps running.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            if (ComponentCrashGuard.IsFromComponent(e.Exception))
            {
                Console.Error.WriteLine("[component] " + e.Exception);
                e.Handled = true;
            }
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // A crash last time leaves the downloaded components off (asked about once open).
            ComponentCrashGuard.Start(ComponentCrashGuard.SettingsPath, ExternalComponents.UserComponentsDirectory);
            desktop.Exit += (_, _) => ComponentCrashGuard.Stop();

            // A downloaded update takes this app's place once it has quit.
            desktop.Exit += (_, _) => AppUpdater.OnExit();
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>The app menu's About LiveSplit: the same About window as the timer's menu.</summary>
    private void OnAboutClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: MainWindow main })
        {
            _ = main.ShowAboutAsync();
        }
    }
}
