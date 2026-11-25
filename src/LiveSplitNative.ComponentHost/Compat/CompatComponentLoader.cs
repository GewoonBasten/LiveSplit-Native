using System.Reflection;
using System.Runtime.Loader;

namespace LiveSplitNative.ComponentHost.Compat;

/// <summary>
/// Loads Windows LiveSplit component DLLs unchanged (ComponentManager.LoadAllFactories): every
/// component of a folder goes into one AssemblyLoadContext, where a component's references to
/// LiveSplit.Core, System.Windows.Forms, System.Drawing and UpdateManager are answered with the
/// look-alikes from <c>compat/</c> next to the app; everything else (the framework, the app's own
/// assemblies) is shared with the app. A DLL that fails to load is skipped and reported.
/// </summary>
public sealed class CompatComponentLoader
{
    /// <summary>The assemblies a component gets the look-alikes of.</summary>
    public static readonly IReadOnlyList<string> LookAlikes = ["LiveSplit.Core", "System.Windows.Forms", "System.Drawing", "UpdateManager", "WinFormsColor", "LiveSplit.Subsplits"];

    private readonly ComponentContext _context;

    public CompatComponentLoader(string compatDirectory)
    {
        CompatDirectory = compatDirectory;
        _context = new ComponentContext(compatDirectory);
    }

    /// <summary>The folder with the look-alike assemblies (the app's <c>compat/</c>).</summary>
    public string CompatDirectory { get; }

    /// <summary>The folder next to the app's own assemblies holding the look-alikes.</summary>
    public static string DefaultCompatDirectory => Path.Combine(AppContext.BaseDirectory, "compat");

    public static bool LookAlikesPresent(string compatDirectory) =>
        LookAlikes.All(name => File.Exists(Path.Combine(compatDirectory, name + ".dll")));

    /// <summary>The look-alike LiveSplit.Core's runtime.</summary>
    public ICompatRuntime Runtime
    {
        get
        {
            Assembly core = _context.LoadFromAssemblyName(new AssemblyName("LiveSplit.Core"));
            Type type = core.GetType("LiveSplitNative.Compat.CompatRuntime", throwOnError: true)!;
            return (ICompatRuntime)Activator.CreateInstance(type)!;
        }
    }

    /// <summary>The factories of every component DLL in <paramref name="componentsDirectory"/>.</summary>
    public ComponentLoadReport LoadDirectory(string componentsDirectory)
    {
        var factories = new List<IHostedComponentFactory>();
        var failures = new List<(string Path, string Error)>();
        if (!Directory.Exists(componentsDirectory))
        {
            return new ComponentLoadReport(factories, failures);
        }

        _context.ComponentsDirectories.Add(componentsDirectory);
        ICompatRuntime runtime = Runtime;
        foreach (string path in Directory.EnumerateFiles(componentsDirectory, "*.dll").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (LookAlikes.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Assembly assembly = _context.LoadFromAssemblyPath(Path.GetFullPath(path));
                factories.AddRange(runtime.GetFactories(assembly));
            }
            catch (BadImageFormatException)
            {
                // A native library next to the components (loaded by a component itself, if at all).
            }
            catch (Exception ex)
            {
                failures.Add((path, ex.GetBaseException().Message));
            }
        }

        return new ComponentLoadReport(factories, failures);
    }

    private sealed class ComponentContext(string compatDirectory) : AssemblyLoadContext("livesplit-components", isCollectible: false)
    {
        public List<string> ComponentsDirectories { get; } = [];

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            string name = assemblyName.Name ?? "";
            if (LookAlikes.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return LoadFromAssemblyPath(Path.Combine(compatDirectory, name + ".dll"));
            }

            // The app's own assemblies and the framework are shared with the app.
            if (Default.Assemblies.Any(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            // A component's own dependencies sit next to it.
            foreach (string directory in ComponentsDirectories)
            {
                string candidate = Path.Combine(directory, name + ".dll");
                if (File.Exists(candidate))
                {
                    return LoadFromAssemblyPath(Path.GetFullPath(candidate));
                }
            }

            return null;
        }
    }
}

public sealed record ComponentLoadReport(
    IReadOnlyList<IHostedComponentFactory> Factories,
    IReadOnlyList<(string Path, string Error)> Failures);
