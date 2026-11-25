namespace LiveSplitNative.App;

/// <summary>Parsed process arguments shared by headless and GUI entry points.</summary>
public static class AppCli
{
    public static bool Headless { get; private set; }
    public static string? LssPath { get; private set; }
    public static string? ComponentsDir { get; private set; }
    public static string? SettingsPath { get; private set; }

    /// <summary>
    /// <c>--legacy-window-drag</c>: move and resize through Avalonia Width / Height / Position as
    /// before F2, instead of one AppKit frame per pointer event. For comparing the two.
    /// </summary>
    public static bool LegacyWindowDrag { get; private set; }

    public static void Parse(string[] args)
    {
        Headless = args.Contains("--headless", StringComparer.OrdinalIgnoreCase);
        LssPath = ValueAfter(args, "--lss");
        ComponentsDir = ValueAfter(args, "--components");
        SettingsPath = ValueAfter(args, "--settings");
        LegacyWindowDrag = args.Contains("--legacy-window-drag", StringComparer.OrdinalIgnoreCase);
    }

    private static string? ValueAfter(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
