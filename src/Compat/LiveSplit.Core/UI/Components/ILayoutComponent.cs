// Port of LiveSplit.Core UI/Components/ILayoutComponent.cs @ 93a11b85 (MIT).
namespace LiveSplit.UI.Components;

public interface ILayoutComponent
{
    string Path { get; set; }
    IComponent Component { get; set; }
}
