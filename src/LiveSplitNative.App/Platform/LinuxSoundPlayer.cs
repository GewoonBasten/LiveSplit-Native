using System.Diagnostics;
using LiveSplitNative.Core.UI;

namespace LiveSplitNative.App.Platform;

/// <summary>
/// Sound Effects' player on Linux (Windows: NAudio WaveOut + AudioFileReader): the desktop's own
/// command-line player, one sound at a time, on the default output. PipeWire's pw-play and
/// PulseAudio's paplay play WAV, OGG and FLAC (and MP3 with a recent libsndfile); for MP3 an
/// installed ffplay or mpv is preferred.
/// </summary>
internal sealed class LinuxSoundPlayer : ISoundPlayer
{
    private readonly object _lock = new();
    private Process? _playing;

    /// <summary>Whether a program exists on the PATH (tests replace it).</summary>
    internal static Func<string, bool> HasProgram { get; set; } = Exists;

    public void Stop()
    {
        lock (_lock)
        {
            Kill();
        }
    }

    public void Play(string path, float volume, int outputDevice)
    {
        if (!OperatingSystem.IsLinux() || Command(path, volume) is not { } command)
        {
            if (OperatingSystem.IsLinux())
            {
                Console.Error.WriteLine("Sound Effects: no player found (install PipeWire's pw-play, PulseAudio's paplay, ffplay or mpv).");
            }

            return;
        }

        lock (_lock)
        {
            Kill();
            try
            {
                var start = new ProcessStartInfo(command.Program) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string argument in command.Arguments)
                {
                    start.ArgumentList.Add(argument);
                }

                _playing = Process.Start(start);
                _playing?.StandardOutput.ReadToEndAsync();
                _playing?.StandardError.ReadToEndAsync();
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Console.Error.WriteLine("Sound Effects: could not play " + path + " (" + ex.Message + ").");
            }
        }
    }

    /// <summary>The program and arguments that play <paramref name="path"/> at <paramref name="volume"/> (0-1), or null.</summary>
    internal static (string Program, string[] Arguments)? Command(string path, float volume)
    {
        float v = Math.Clamp(volume, 0f, 1f);
        string percent = ((int)Math.Round(v * 100)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        bool mp3 = path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);
        var candidates = new List<(string, string[])>();
        if (mp3)
        {
            candidates.Add(("ffplay", ["-nodisp", "-autoexit", "-loglevel", "quiet", "-volume", percent, path]));
            candidates.Add(("mpv", ["--no-video", "--really-quiet", "--volume=" + percent, path]));
        }

        candidates.Add(("pw-play", ["--volume", v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), path]));
        candidates.Add(("paplay", ["--volume=" + ((int)Math.Round(v * 65536)).ToString(System.Globalization.CultureInfo.InvariantCulture), path]));
        if (!mp3)
        {
            candidates.Add(("ffplay", ["-nodisp", "-autoexit", "-loglevel", "quiet", "-volume", percent, path]));
            candidates.Add(("mpv", ["--no-video", "--really-quiet", "--volume=" + percent, path]));
        }

        foreach ((string program, string[] arguments) in candidates)
        {
            if (HasProgram(program))
            {
                return (program, arguments);
            }
        }

        return null;
    }

    private void Kill()
    {
        try
        {
            if (_playing is { HasExited: false })
            {
                _playing.Kill();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }

        _playing?.Dispose();
        _playing = null;
    }

    private static bool Exists(string program) =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => File.Exists(Path.Combine(directory, program)));
}
