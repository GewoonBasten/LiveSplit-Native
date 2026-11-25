using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(LiveSplitNative.App.Tests.TestAppBuilder))]

namespace LiveSplitNative.App.Tests;

/// <summary>The app's own headless configuration (Program.BuildAvaloniaAppHeadless), for tests.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<LiveSplitNative.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont();
}
