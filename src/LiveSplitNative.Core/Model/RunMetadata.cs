namespace LiveSplitNative.Core.Model;

public sealed class RunMetadata
{
    public string? RunId { get; set; }
    public string PlatformName { get; set; } = "";
    public bool UsesEmulator { get; set; }
    public string RegionName { get; set; } = "";
    public Dictionary<string, string> VariableValueNames { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> PermanentCustomVariables { get; } = new(StringComparer.Ordinal);

    /// <summary>Custom variables set while LiveSplit runs (the server's setcustomvariable); never saved.</summary>
    public Dictionary<string, string> TemporaryCustomVariables { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// RunMetadata.SetCustomVariable: a permanent variable keeps its new value in the run (true:
    /// the run changed); any other name is a temporary one.
    /// </summary>
    public bool SetCustomVariable(string name, string value)
    {
        if (PermanentCustomVariables.ContainsKey(name))
        {
            PermanentCustomVariables[name] = value;
            return true;
        }

        TemporaryCustomVariables[name] = value;
        return false;
    }

    /// <summary>
    /// Value for LiveSplit.Text CustomVariable mode — permanent custom vars, then category variables.
    /// </summary>
    public string? CustomVariableValue(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (PermanentCustomVariables.TryGetValue(name, out string? permanent))
        {
            return permanent;
        }

        if (TemporaryCustomVariables.TryGetValue(name, out string? temporary))
        {
            return temporary;
        }

        return VariableValueNames.TryGetValue(name, out string? category) ? category : null;
    }
}
