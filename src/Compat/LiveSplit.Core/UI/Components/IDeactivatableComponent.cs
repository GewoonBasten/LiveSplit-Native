// Port of LiveSplit.Core UI/Components/IDeactivatableComponent.cs @ 93a11b85 (MIT), unchanged.
namespace LiveSplit.UI.Components;

public interface IDeactivatableComponent : IComponent
{
    bool Activated { get; set; }
}
