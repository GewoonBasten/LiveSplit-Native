using System.Reflection;
using System.Runtime.Loader;

namespace LiveSplitNative.ComponentHost;

/// <summary>
/// Safe ComponentManager-style probe. Failures never throw to callers —
/// bad components must not crash the timer host.
/// </summary>
public sealed class ComponentAssemblyProbe
{
    public IReadOnlyList<ComponentLoadResult> ProbeDirectory(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return [];
            }

            var results = new List<ComponentLoadResult>();
            foreach (string path in Directory.EnumerateFiles(directory, "*.dll").OrderBy(Path.GetFileName))
            {
                results.Add(ProbeFile(path, directory));
            }

            return results;
        }
        catch (Exception ex)
        {
            return
            [
                new ComponentLoadResult(directory, false, false, ComponentLoadStatus.FailedOther,
                    "ProbeDirectory: " + ex.Message)
            ];
        }
    }

    public ComponentLoadResult ProbeFile(string path, string? siblingDirectory = null)
    {
        try
        {
            siblingDirectory ??= Path.GetDirectoryName(path);
            var alc = new AssemblyLoadContext("component-probe-" + Guid.NewGuid().ToString("N"), isCollectible: true);
            try
            {
                alc.Resolving += (_, name) =>
                {
                    try
                    {
                        if (siblingDirectory is null)
                        {
                            return null;
                        }

                        string candidate = Path.Combine(siblingDirectory, name.Name + ".dll");
                        return File.Exists(candidate) ? alc.LoadFromAssemblyPath(candidate) : null;
                    }
                    catch
                    {
                        return null;
                    }
                };

                Assembly asm;
                try
                {
                    asm = alc.LoadFromAssemblyPath(Path.GetFullPath(path));
                }
                catch (BadImageFormatException ex)
                {
                    return new ComponentLoadResult(path, false, false, ComponentLoadStatus.SkippedNative, ex.Message);
                }
                catch (Exception ex)
                {
                    return new ComponentLoadResult(path, false, false, ComponentLoadStatus.FailedOther, ex.Message);
                }

                try
                {
                    _ = asm.GetTypes();
                    return new ComponentLoadResult(path, true, true, ComponentLoadStatus.TypesInspectable, "GetTypes OK");
                }
                catch (ReflectionTypeLoadException ex)
                {
                    string joined = string.Join(" | ", ex.LoaderExceptions.Take(3).Select(e => e?.Message ?? ""));
                    return new ComponentLoadResult(path, true, false, Classify(joined), joined);
                }
                catch (Exception ex)
                {
                    return new ComponentLoadResult(path, true, false, Classify(ex.Message), ex.Message);
                }
            }
            finally
            {
                try
                {
                    alc.Unload();
                }
                catch
                {
                    // ignore unload failures
                }
            }
        }
        catch (Exception ex)
        {
            return new ComponentLoadResult(path, false, false, ComponentLoadStatus.FailedOther, ex.Message);
        }
    }

    private static ComponentLoadStatus Classify(string message)
    {
        if (message.Contains("System.Windows.Forms", StringComparison.Ordinal) ||
            message.Contains("System.Drawing", StringComparison.Ordinal))
        {
            return ComponentLoadStatus.FailedWinFormsOrDrawing;
        }

        if (message.Contains("BadImageFormat", StringComparison.Ordinal))
        {
            return ComponentLoadStatus.FailedNativeOrBadImage;
        }

        if (message.Contains("Could not load file or assembly", StringComparison.Ordinal))
        {
            return ComponentLoadStatus.FailedMissingDependency;
        }

        return ComponentLoadStatus.FailedOther;
    }
}
