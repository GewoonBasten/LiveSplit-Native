using System.Text;

namespace LiveSplitNative.Core.Model;

/// <summary>
/// Ports of upstream IRun.GetExtendedName / GetExtendedFileName / GetExtendedCategoryName (93a11b85).
/// This app has no speedrun.com data, so these follow upstream's offline path (no game or
/// category loaded): every variable in the splits file counts, and region and platform come
/// from the file's own names.
/// </summary>
public static class RunNames
{
    /// <summary>"Game - Category (variables)", as Windows uses for window titles and file names.</summary>
    public static string GetExtendedName(this Run run, bool useExtendedCategoryName = true)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(run.GameName))
        {
            builder.Append(run.GameName);
        }

        string categoryName = useExtendedCategoryName ? run.GetExtendedCategoryName() : run.CategoryName;
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            if (builder.Length > 0)
            {
                builder.Append(" - ");
            }

            builder.Append(categoryName);
        }

        return builder.ToString();
    }

    /// <summary><see cref="GetExtendedName"/> without the characters Windows does not allow in file names.</summary>
    public static string GetExtendedFileName(this Run run, bool useExtendedCategoryName = true)
    {
        var builder = new StringBuilder();
        foreach (char c in run.GetExtendedName(useExtendedCategoryName))
        {
            if (c is not '\\' and not '/' and not ':' and not '*' and not '?' and not '"' and not '<' and not '>' and not '|')
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The category with its parenthesised extras: what was already in parentheses, then the
    /// variables ("yes" adds the name, "no" adds "No name", anything else the value), then region
    /// and platform when asked. "Any% (No ACE)" for Any% with Arbitrary Code Execution = No ACE.
    /// </summary>
    public static string GetExtendedCategoryName(
        this Run run,
        bool showRegion = false,
        bool showPlatform = false,
        bool showVariables = true)
    {
        var list = new List<string>();
        string categoryName = run.CategoryName;
        string afterParentheses = "";

        if (string.IsNullOrEmpty(categoryName))
        {
            return string.Empty;
        }

        int indexStart = categoryName.IndexOf('(');
        int indexEnd = categoryName.IndexOf(')', indexStart + 1);
        if (indexStart >= 0 && indexEnd >= 0)
        {
            string inside = categoryName.Substring(indexStart + 1, indexEnd - indexStart - 1);
            list.Add(inside);
            afterParentheses = categoryName[(indexEnd + 1)..].Trim();
            categoryName = categoryName[..indexStart].Trim();
        }

        if (showVariables)
        {
            foreach ((string variable, string variableValue) in run.Metadata.VariableValueNames)
            {
                string name = variable.TrimEnd('?');
                string valueLower = variableValue.ToLowerInvariant();
                if (valueLower == "yes")
                {
                    list.Add(name);
                }
                else if (valueLower == "no")
                {
                    list.Add($"No {name}");
                }
                else
                {
                    list.Add(variableValue);
                }
            }
        }

        if (showRegion && !string.IsNullOrEmpty(run.Metadata.RegionName))
        {
            list.Add(run.Metadata.RegionName);
        }

        if (showPlatform)
        {
            if (!string.IsNullOrEmpty(run.Metadata.PlatformName))
            {
                list.Add(run.Metadata.UsesEmulator ? $"{run.Metadata.PlatformName} Emulator" : run.Metadata.PlatformName);
            }
            else if (run.Metadata.UsesEmulator)
            {
                list.Add("Emulator");
            }
        }

        if (list.Count > 0)
        {
            categoryName = $"{categoryName} ({string.Join(", ", list)}) {afterParentheses}";
        }

        return categoryName.Trim();
    }
}
