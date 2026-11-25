using LiveSplitNative.ComponentHost;

namespace LiveSplitNative.Core.Tests;

public class ComponentHostSafetyTests
{
    [Fact]
    public void Probe_Missing_Directory_Does_Not_Throw()
    {
        var probe = new ComponentAssemblyProbe();
        IReadOnlyList<ComponentLoadResult> results = probe.ProbeDirectory("/tmp/definitely-missing-components-dir-xyz");
        Assert.Empty(results);
    }

    [Fact]
    public void Probe_Garbage_File_Does_Not_Throw()
    {
        string path = Path.Combine(Path.GetTempPath(), "lsn-garbage-" + Guid.NewGuid().ToString("N") + ".dll");
        File.WriteAllText(path, "not a real assembly");
        try
        {
            var probe = new ComponentAssemblyProbe();
            ComponentLoadResult result = probe.ProbeFile(path);
            Assert.False(result.TypesInspectable);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
