namespace LiveSplitNative.Core.UI;

/// <summary>
/// What every upstream one-line information component (the ones drawn by InfoTimeComponent or
/// InfoTextComponent) has in its settings besides its own fields: the background and
/// Display 2 Rows.
/// </summary>
public interface IInfoComponentSettings
{
    ArgbColor BackgroundColor { get; }
    ArgbColor BackgroundColor2 { get; }
    string BackgroundGradient { get; }
    bool Display2Rows { get; }
}

/// <summary>
/// What an information component shows: its name (with the shorter forms used when it does not
/// fit), the value text and the two colors. <see cref="ValueInTextFont"/> is InfoTextComponent's
/// value (drawn in the Text font rather than the Times font).
/// </summary>
public sealed record InfoComponentView(
    string Name,
    IReadOnlyList<string> AlternateNames,
    string Value,
    ArgbColor NameColor,
    ArgbColor ValueColor,
    bool ValueInTextFont = false);
