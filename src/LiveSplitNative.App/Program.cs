using Avalonia;
using System.Runtime.InteropServices;
using LiveSplitNative.App;
using LiveSplitNative.ComponentHost;
using LiveSplitNative.Core.Model;
using LiveSplitNative.Core.Serialization;
using LiveSplitNative.Core.Timing;

// Shared CLI flags for GUI / headless / screenshot demo.
AppCli.Parse(args);

// Leave a trail when the process dies from managed exceptions (macOS often
// produces empty stderr / no DiagnosticReports .ips for Avalonia/Skia faults).
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine("UnhandledException: " + e.ExceptionObject);
    GeometryTrail.Dump(Console.Error);
    try { Console.Error.Flush(); } catch { /* ignore */ }
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Console.Error.WriteLine("UnobservedTaskException: " + e.Exception);
    e.SetObserved();
    try { Console.Error.Flush(); } catch { /* ignore */ }
};

if (args.Contains("--ntp-check", StringComparer.OrdinalIgnoreCase))
{
    // Diagnostics: the atomic clock as the timer reads it (Ntp.Now, time.windows.com), three
    // times, against this Mac's clock; and the drift correction's first round (ten readings).
    for (int i = 0; i < 3; i++)
    {
        try
        {
            DateTime ntp = Ntp.Now();
            Console.WriteLine($"NTP {ntp:O} system {DateTime.UtcNow:O} offset {(DateTime.UtcNow - ntp).TotalMilliseconds:0.0} ms");
        }
        catch (Exception e)
        {
            Console.WriteLine("NTP failed: " + e.Message);
        }
    }

    var check = new AtomicClock();
    var refresher = new DriftRefresher(check, () => TimeStamp.RawNow, () => TimeStamp.Now.Value, Ntp.Now, Thread.Sleep);
    Console.WriteLine($"First round: next in {refresher.Step()}, synced={check.IsSynced}, clock source {TimeStamp.Source.Name}");
    AtomicDateTime synced = check.CurrentDateTime(TimeStamp.Now.Value);
    Console.WriteLine($"Synced time {synced.Time:O} (synced={synced.SyncedWithAtomicClock}) system {DateTime.UtcNow:O}");
    return 0;
}

if (args.Contains("--gamepad-check", StringComparer.OrdinalIgnoreCase))
{
    // Diagnostics: the controllers the gamepad hotkeys see, and the buttons pressed for 20 s.
    var gamepads = new LiveSplitNative.App.Platform.MacGamepadHook();
    gamepads.ButtonPressed += button => Console.WriteLine("Pressed: " + button);
    using (gamepads.Acquire())
    {
        Thread.Sleep(TimeSpan.FromSeconds(20));
    }

    Console.WriteLine("Gamepad check done.");
    return 0;
}

if (AppCli.Headless)
{
    return RunHeadless(args);
}

BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

return 0;

static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        // Mac agent sessions: prefer Software, then Metal (OpenGL can be flaky headless/remote).
        .With(new AvaloniaNativePlatformOptions
        {
            RenderingMode =
            [
                AvaloniaNativeRenderingMode.Software,
                AvaloniaNativeRenderingMode.Metal,
            ],
        })
        .WithInterFont()
        .LogToTrace();


static int RunHeadless(string[] args)
{
    Console.WriteLine("LiveSplit Native — headless smoke");
    Console.WriteLine($"OS: {RuntimeInformation.OSDescription} RID={RuntimeInformation.RuntimeIdentifier}");
    Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}");

    var run = new Run { GameName = "Demo", CategoryName = "Any%" };
    run.AddSegment("Segment 1");
    run.AddSegment("Segment 2");
    var state = new LiveSplitState(run);
    var model = new TimerModel { CurrentState = state };

    model.Start();
    Thread.Sleep(20);
    model.Split();
    Thread.Sleep(10);
    model.Pause();
    Console.WriteLine($"Paused at {state.CurrentTime.RealTime}");
    model.Pause(); // resume
    model.UndoSplit();
    model.SkipSplit();
    model.Reset();
    Console.WriteLine($"Reset phase={state.CurrentPhase} attempts={run.AttemptCount}");

    if (AppCli.LssPath is { } lss)
    {
        Run loaded = XmlRunSerializer.LoadFile(lss);
        using var ms = new MemoryStream();
        XmlRunSerializer.Save(loaded, ms);
        ms.Position = 0;
        Run again = XmlRunSerializer.Load(ms);
        Console.WriteLine($"LSS round-trip: {again.GameName} / {again.CategoryName} segments={again.Count}");
    }

    if (AppCli.ComponentsDir is { } components)
    {
        var probe = new ComponentAssemblyProbe();
        IReadOnlyList<ComponentLoadResult> results = probe.ProbeDirectory(components);
        Console.WriteLine($"Component probe: {results.Count} entries (safe)");
        foreach (ComponentLoadResult r in results.Take(5))
        {
            Console.WriteLine($"  {Path.GetFileName(r.Path)} => {r.Status}");
        }
    }

    _ = new ComponentAssemblyProbe().ProbeDirectory("/nonexistent/path/for/safety");
    Console.WriteLine("OK");
    return 0;
}
