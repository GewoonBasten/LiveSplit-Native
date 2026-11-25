using System.Text;
using LiveSplitNative.Core.Localization;

namespace LiveSplitNative.Core.Web;

/// <summary>A piece of styled text from <see cref="SpeedrunComRules.ParseBBCode"/>.</summary>
public sealed record BBCodeSpan(
    string Text,
    bool Bold = false,
    bool Italic = false,
    bool Underline = false,
    bool Strikeout = false,
    double Size = BBCodeSpan.NormalSize,
    bool Hidden = false,
    bool Shaded = false)
{
    /// <summary>RichTextBoxExtensions' sizes: 8.25 pt, [small] 7 pt, [big] 12 pt.</summary>
    public const double NormalSize = 8.25;
}

/// <summary>
/// The Splits Editor's Rules box (MetadataControl.refreshRules, 93a11b85): a bold sentence about
/// how the game's runs are timed and whether they need a video, then the category's rules with
/// speedrun.com's BBCode (RichTextBoxExtensions.AppendBBCode).
/// </summary>
public static class SpeedrunComRules
{
    /// <summary>
    /// "Runs of this game are timed with the Game Time and require video proof." (only the parts
    /// that apply; null when neither does).
    /// </summary>
    public static string? Intro(SrcGame game)
    {
        var rules = new List<string>();
        if (game.DefaultTimingMethod != SrcTimingMethod.RealTime)
        {
            rules.Add(game.DefaultTimingMethod == SrcTimingMethod.RealTimeWithoutLoads
                ? UiText.T("are timed without the loading times")
                : UiText.T("are timed with the Game Time"));
        }

        if (game.RequiresVideo)
        {
            rules.Add(UiText.T("require video proof"));
        }

        if (rules.Count == 0)
        {
            return null;
        }

        string first = rules[0];
        string last = rules[^1];
        var text = new StringBuilder(UiText.T("Runs of this game "));
        foreach (string rule in rules)
        {
            if (rule != first)
            {
                text.Append(rule != last ? ", " : " and ");
            }

            text.Append(rule);
        }

        text.Append('.');
        return text.ToString();
    }

    /// <summary>
    /// AppendBBCode: [b] [i] [u] [s], [small] / [big], [center], [spoiler] (hidden until
    /// selected), [quote] / [quote=name] (shaded), [list] and [*] (bullets), [section=title],
    /// [url] and [url=address] (the address after the text). Any other code stays as written.
    /// </summary>
    public static IReadOnlyList<BBCodeSpan> ParseBBCode(string text)
    {
        var spans = new List<BBCodeSpan>();
        bool bold = false, italic = false, underline = false, strikeout = false, hidden = false, shaded = false;
        double size = BBCodeSpan.NormalSize;
        string url = "";

        void Add(string value, bool b, bool i, bool u, bool s, double z, bool h, bool q)
        {
            if (value.Length > 0)
            {
                spans.Add(new BBCodeSpan(value, b, i, u, s, z, h, q));
            }
        }

        foreach (string token in text.Split('['))
        {
            string actualText = token;
            int closing = token.IndexOf(']');
            if (closing >= 0)
            {
                string code = token[..closing];
                actualText = token[(closing + 1)..];
                switch (code)
                {
                    case "b": bold = true; break;
                    case "/b": bold = false; break;
                    case "i": italic = true; break;
                    case "/i": italic = false; break;
                    case "u": underline = true; break;
                    case "/u": underline = false; break;
                    case "s": strikeout = true; break;
                    case "/s": strikeout = false; break;
                    case "small": size = 7; break;
                    case "big": size = 12; break;
                    case "/small" or "/big": size = BBCodeSpan.NormalSize; break;
                    case "center" or "centre": break;
                    case "/center" or "/centre":
                        Add(Environment.NewLine, false, false, false, false, BBCodeSpan.NormalSize, false, false);
                        break;
                    case "spoiler":
                        Add(Environment.NewLine, false, false, false, false, BBCodeSpan.NormalSize, false, false);
                        Add("Spoiler: ", true, false, false, false, BBCodeSpan.NormalSize, false, true);
                        hidden = true;
                        break;
                    case "/spoiler":
                        hidden = false;
                        actualText = Environment.NewLine + actualText;
                        break;
                    case "/quote":
                        shaded = false;
                        actualText = Environment.NewLine + actualText;
                        break;
                    case "list" or "/list" or "/section" or "url": break;
                    case "*": actualText = "    ●  " + actualText; break;
                    case "/url":
                        if (url.Length > 0)
                        {
                            actualText = ": " + url + actualText;
                            url = "";
                        }

                        break;
                    default:
                        if (code == "quote" || code.StartsWith("quote=", StringComparison.Ordinal))
                        {
                            string quote = code.StartsWith("quote=", StringComparison.Ordinal)
                                ? string.Format("Originally posted by {0}", code[6..])
                                : "Quote";
                            Add(Environment.NewLine, false, false, false, false, BBCodeSpan.NormalSize, false, false);
                            Add(quote, false, true, true, false, BBCodeSpan.NormalSize, false, true);
                            shaded = true;
                            actualText = ": " + actualText;
                        }
                        else if (code.StartsWith("section=", StringComparison.Ordinal))
                        {
                            Add(Environment.NewLine, false, false, false, false, BBCodeSpan.NormalSize, false, false);
                            Add(code[8..] + Environment.NewLine, true, false, false, false, 10, false, false);
                        }
                        else if (code.StartsWith("url=", StringComparison.Ordinal))
                        {
                            url = code[4..];
                        }
                        else
                        {
                            actualText = "[" + token;
                        }

                        break;
                }
            }

            Add(actualText, bold, italic, underline, strikeout, size, hidden, shaded);
        }

        return spans;
    }
}
