using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace LiveSplitNative.App.Tests;

/// <summary>
/// A settings label as it reads: a SettingsField shows its Windows label without the trailing
/// colon and its unit in a grey run, so labels are compared on what they say.
/// </summary>
internal static class LabelText
{
    public static string Of(TextBlock text) =>
        Norm(text.Inlines is { Count: > 0 } inlines ? string.Concat(inlines.OfType<Run>().Select(r => r.Text)) : text.Text);

    public static string Norm(string? label)
    {
        string text = (label ?? "").Trim();
        if (text.EndsWith(':') || text.EndsWith('：'))
        {
            text = text[..^1].TrimEnd();
        }

        return text;
    }

    public static bool Is(TextBlock text, string label) => Of(text) == Norm(label);
}
