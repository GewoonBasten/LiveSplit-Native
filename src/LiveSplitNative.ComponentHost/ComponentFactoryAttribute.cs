namespace LiveSplitNative.ComponentHost;

/// <summary>
/// Mirror of LiveSplit.UI.Components.ComponentFactoryAttribute so a future
/// adapter host can recognize upstream factories when assemblies load.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class ComponentFactoryAttribute : Attribute
{
    public ComponentFactoryAttribute(Type componentFactoryClassType)
    {
        ComponentFactoryClassType = componentFactoryClassType;
    }

    public Type ComponentFactoryClassType { get; }
}
