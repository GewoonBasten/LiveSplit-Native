namespace LiveSplitNative.Core.UI;

/// <summary>Port of LiveSplit.UI.Layout (structure + settings; native render subset).</summary>
public sealed class Layout
{
    public LayoutMode Mode { get; set; } = LayoutMode.Vertical;
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int VerticalWidth { get; set; } = 286;
    public int VerticalHeight { get; set; } = 360;
    public int HorizontalWidth { get; set; } = -1;
    public int HorizontalHeight { get; set; } = -1;
    public string? Version { get; set; } = "1.3";
    public LayoutSettings Settings { get; set; } = LayoutSettings.CreateStandard();
    public List<LayoutComponent> Components { get; } = [];

    /// <summary>
    /// Elements of &lt;Layout&gt; that are not LiveSplit's (Windows drops them; kept here so a
    /// layout from a newer or other program keeps them).
    /// </summary>
    public List<string> UnknownOuterXml { get; } = [];

    public bool HasChanged { get; set; }

    public Layout Clone()
    {
        var clone = new Layout
        {
            Mode = Mode,
            X = X,
            Y = Y,
            VerticalWidth = VerticalWidth,
            VerticalHeight = VerticalHeight,
            HorizontalWidth = HorizontalWidth,
            HorizontalHeight = HorizontalHeight,
            Version = Version,
            Settings = Settings.Clone(),
            HasChanged = HasChanged,
        };
        foreach (LayoutComponent c in Components)
        {
            clone.Components.Add(c.Clone());
        }

        clone.UnknownOuterXml.AddRange(UnknownOuterXml);

        return clone;
    }

    /// <summary>Replace this layout's fields/components from <paramref name="other"/> (in-place).</summary>
    public void Assign(Layout other)
    {
        Mode = other.Mode;
        X = other.X;
        Y = other.Y;
        VerticalWidth = other.VerticalWidth;
        VerticalHeight = other.VerticalHeight;
        HorizontalWidth = other.HorizontalWidth;
        HorizontalHeight = other.HorizontalHeight;
        Version = other.Version;
        HasChanged = other.HasChanged;
        Settings.Assign(other.Settings);
        Components.Clear();
        foreach (LayoutComponent c in other.Components)
        {
            Components.Add(c.Clone());
        }

        List<string> unknown = [.. other.UnknownOuterXml];
        UnknownOuterXml.Clear();
        UnknownOuterXml.AddRange(unknown);
    }
}
