// Port of LiveSplit.Core UI/Components/LayoutComponent.cs @ 93a11b85 (MIT), without
// FontOverrides: LiveSplit Native's components follow the layout's fonts (no per-component
// font overrides).
namespace LiveSplit.UI.Components;

public class LayoutComponent : ILayoutComponent
{
    public IComponent Component { get; set; }
    public string Path { get; set; }

    public LayoutComponent(string path, IComponent component)
    {
        Component = component;
        Path = path;
    }

    public override string ToString()
    {
        return Component.ComponentName;
    }

    public LayoutComponent Clone()
    {
        return new LayoutComponent(Path, Component);
    }
}
