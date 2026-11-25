using System.Diagnostics;
using System.Runtime.Loader;
using LiveSplitNative.Core.Serialization;

namespace LiveSplitNative.App;

/// <summary>
/// Keeps a downloaded component from taking LiveSplit Native down for good (not in Windows
/// LiveSplit, whose components run on the system they were made for).
/// <list type="bullet">
/// <item>An exception a component throws on the UI thread (in its settings page's handlers, say)
/// is logged and the app keeps running (<see cref="IsFromComponent"/>).</item>
/// <item>A crash that no exception handler sees (the process is gone): a file next to
/// settings.cfg says the app is running, and is removed when it quits normally. Found at the next
/// start, the downloaded components (the Components folder) are left off for that session and
/// the user is asked whether to turn them back on (<see cref="SkippedAfterCrash"/>).</item>
/// </list>
/// Only the app itself does this (not tests or screenshot runs).
/// </summary>
internal static class ComponentCrashGuard
{
    public const string MarkerName = "running.marker";

    /// <summary>The Components folder's DLLs left off because the last session did not end normally.</summary>
    public static IReadOnlyList<string> SkippedAfterCrash { get; private set; } = [];

    private static string? _marker;

    /// <summary>
    /// At start, before the layout's components are made: a marker left from the last session
    /// turns the downloaded components off (when there are any); then this session's marker.
    /// </summary>
    public static void Start(string settingsPath, string componentsDirectory)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(settingsPath));
        if (directory is null)
        {
            return;
        }

        _marker = Path.Combine(directory, MarkerName);
        SkippedAfterCrash = Check(_marker, componentsDirectory);
        if (SkippedAfterCrash.Count > 0)
        {
            ExternalComponents.UserComponentsOff = true;
        }

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(_marker, $"{DateTime.UtcNow:O} {Environment.ProcessId}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _marker = null;
        }
    }

    /// <summary>The downloaded component DLLs to leave off: none unless the marker was left behind.</summary>
    internal static IReadOnlyList<string> Check(string marker, string componentsDirectory)
    {
        if (!File.Exists(marker) || !Directory.Exists(componentsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(componentsDirectory, "*.dll")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The app quit normally.</summary>
    public static void Stop()
    {
        if (_marker is null)
        {
            return;
        }

        try
        {
            File.Delete(_marker);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Next start asks once for nothing; no harm.
        }

        _marker = null;
    }

    /// <summary>
    /// The exception came from a component DLL or the look-alikes it runs on: some frame of it
    /// (or of an inner exception) is code loaded outside the app's own load context.
    /// </summary>
    public static bool IsFromComponent(Exception exception)
    {
        for (Exception? ex = exception; ex is not null; ex = ex.InnerException)
        {
            foreach (StackFrame frame in new StackTrace(ex, false).GetFrames())
            {
                if (frame.GetMethod()?.DeclaringType?.Assembly is { } assembly
                    && AssemblyLoadContext.GetLoadContext(assembly) is { } context
                    && context != AssemblyLoadContext.Default)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The settings.cfg the app uses (the --settings one, or the default).</summary>
    public static string SettingsPath => AppCli.SettingsPath ?? XmlAppSettingsSerializer.DefaultPath;
}
