using System.Diagnostics;
using System.Xml;

namespace LiveSplitNative.Core.UI;

/// <summary>
/// LiveSplit.AutoSplitIntegration's settings (version 1.8), as in Basten's Pilgrim build of it
/// (pilgrim-autosplitter-v1.1.3/livesplit-desktop-integration).
/// </summary>
public sealed class AutoSplitIntegrationSettings
{
    /// <summary>The autosplitter to start (Pilgrim Autosplitter, or AutoSplit): an app or a program.</summary>
    public string AutoSplitPath { get; set; } = "";

    /// <summary>The autosplitter's settings or profile file, sent to it once it runs.</summary>
    public string SettingsPath { get; set; } = "";

    /// <summary>Pause Game Time only: the autosplitter's pause toggles game time instead of the timer.</summary>
    public bool GameTimePausing { get; set; }

    public static AutoSplitIntegrationSettings FromInnerXml(string? innerXml)
    {
        XmlElement root = ComponentSettingsXml.LoadRoot(innerXml);
        if (root.ChildNodes.Count == 0)
        {
            return new AutoSplitIntegrationSettings();
        }

        return new AutoSplitIntegrationSettings
        {
            AutoSplitPath = ComponentSettingsXml.String(root, "AutoSplitPath", ""),
            SettingsPath = ComponentSettingsXml.String(root, "SettingsPath", ""),
            GameTimePausing = ComponentSettingsXml.Bool(root, "GameTimePausing", false),
        };
    }

    public string ToInnerXml() => ComponentSettingsXml.ToInnerXml((doc, parent) =>
    {
        ComponentSettingsXml.SetText(doc, parent, "Version", "1.8");
        ComponentSettingsXml.SetText(doc, parent, "AutoSplitPath", AutoSplitPath);
        ComponentSettingsXml.SetText(doc, parent, "SettingsPath", SettingsPath);
        ComponentSettingsXml.SetText(doc, parent, "GameTimePausing", GameTimePausing.ToString());
    });
}

/// <summary>Commands: the lines LiveSplit and the autosplitter send each other.</summary>
public static class AutoSplitCommands
{
    public const string Start = "start";
    public const string Split = "split";
    public const string Reset = "reset";
    public const string KillMe = "killme";
    public const string Pause = "pause";
    public const string Skip = "skip";
    public const string Undo = "undo";
    public const string Kill = "kill";

    public static string Settings(string path) => "settings|" + path;
}

/// <summary>
/// One AutoSplit Integration component's autosplitter (AutoSplitProcess plus the component's
/// start/kill/ignore parts), kept on its LayoutComponent.RuntimeState. The autosplitter runs with
/// --auto-controlled; it prints its version and process ID, then commands, one per line, and reads
/// LiveSplit's commands from its input. Lines are handed over through <c>post</c> (the UI thread).
/// </summary>
public sealed class AutoSplitIntegrationRuntime(Action<Action> post)
{
    public const string NotAvailable = "N/A";

    private readonly object _lock = new();
    private readonly HashSet<string> _ignoreNext = [];
    private Process? _startup;
    private Process? _main;
    private int _initLine;

    /// <summary>Set once the component started its autosplitter (SetSettings' hasStarted).</summary>
    public bool HasStarted { get; set; }

    public bool IsRunning { get; private set; }

    public string Version { get; private set; } = NotAvailable;

    /// <summary>Running state or version changed (the settings page follows it).</summary>
    public event Action? Changed;

    /// <summary>A command from the autosplitter (start, split, reset, pause), on the UI thread.</summary>
    public Action<string>? CommandReceived { get; set; }

    /// <summary>
    /// The program to start for a chosen path. A Mac app is run from inside its bundle: the
    /// program it bundles under its own name in Resources (Pilgrim's Platypus app only re-opens
    /// that program, which would lose the connection), otherwise its main program.
    /// </summary>
    public static string? Program(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))
        {
            string name = Path.GetFileNameWithoutExtension(path.TrimEnd('/'));
            string bundled = Path.Combine(path, "Contents", "Resources", name);
            if (File.Exists(bundled))
            {
                return bundled;
            }

            string macOs = Path.Combine(path, "Contents", "MacOS");
            string main = Path.Combine(macOs, name);
            if (File.Exists(main))
            {
                return main;
            }

            return Directory.Exists(macOs) ? Directory.EnumerateFiles(macOs).FirstOrDefault() : null;
        }

        return File.Exists(path) ? path : null;
    }

    /// <summary>StartAutoSplit: a new autosplitter (the old one asked to quit), then its settings file.</summary>
    public void Start(AutoSplitIntegrationSettings settings)
    {
        HasStarted = true;
        if (Program(settings.AutoSplitPath) is { } program)
        {
            Close();
            Launch(program);
        }

        LoadSettings(settings);
    }

    private void Launch(string program)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = program,
                    Arguments = "--auto-controlled",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(program) ?? "",
                },
                EnableRaisingEvents = true,
            };
            process.OutputDataReceived += (_, e) => OnOutput(process, e.Data);
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Console.WriteLine("[AutoSplit] Error: " + e.Data);
                }
            };
            process.Exited += (_, _) => OnExited(process);
            lock (_lock)
            {
                _startup = process;
                _main = null;
                _initLine = 0;
            }

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            SetRunning(true, NotAvailable);
        }
        catch (Exception e)
        {
            Console.WriteLine("Error while starting AutoSplit: " + e.Message);
        }
    }

    private void OnOutput(Process process, string? line)
    {
        if (string.IsNullOrEmpty(line) || !ReferenceEquals(process, _startup))
        {
            return;
        }

        // The first two lines: the version, then the main process's ID.
        if (_initLine <= 1)
        {
            if (_initLine == 0)
            {
                post(() => SetRunning(IsRunning, line));
            }
            else if (int.TryParse(line, out int pid))
            {
                try
                {
                    Process main = Process.GetProcessById(pid);
                    main.EnableRaisingEvents = true;
                    main.Exited += (_, _) => OnExited(process);
                    _main = main;
                }
                catch (Exception)
                {
                    // The startup process stands in for it.
                }
            }

            _initLine++;
            return;
        }

        if (line == AutoSplitCommands.KillMe)
        {
            Send(AutoSplitCommands.Kill);
            return;
        }

        post(() => CommandReceived?.Invoke(line));
    }

    private void OnExited(Process process)
    {
        if (ReferenceEquals(process, _startup))
        {
            post(() => SetRunning(false, NotAvailable));
        }
    }

    private void SetRunning(bool running, string version)
    {
        IsRunning = running;
        Version = version;
        Changed?.Invoke();
    }

    /// <summary>LoadAutoSplitSettings: the settings file, when there is one.</summary>
    public void LoadSettings(AutoSplitIntegrationSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.SettingsPath) && File.Exists(settings.SettingsPath))
        {
            Send(AutoSplitCommands.Settings(settings.SettingsPath));
        }
    }

    /// <summary>Sends a command to the autosplitter while it runs.</summary>
    public void Send(string command)
    {
        Process? process = _startup;
        if (!IsRunning || process is null)
        {
            return;
        }

        try
        {
            process.StandardInput.WriteLine(command);
            process.StandardInput.Flush();
        }
        catch (Exception)
        {
            // It is quitting.
        }
    }

    /// <summary>KillAutoSplit: ends the autosplitter's main process.</summary>
    public void Kill()
    {
        if (!IsRunning)
        {
            return;
        }

        try
        {
            (_main ?? _startup)?.Kill();
        }
        catch (Exception)
        {
            // Already gone.
        }
    }

    /// <summary>Close (the component disposed, or a new autosplitter): asks it to quit.</summary>
    public void Close()
    {
        if (IsRunning)
        {
            Send(AutoSplitCommands.Kill);
        }
    }

    /// <summary>IgnoreNext: the timer event a command from the autosplitter causes is not sent back to it.</summary>
    public void IgnoreNext(string command) => _ignoreNext.Add(command);

    public bool ConsumeIgnore(string command) => _ignoreNext.Remove(command);
}
