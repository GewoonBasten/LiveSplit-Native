// Port of LiveSplit.Core UI/Components/ComponentFactoryAttribute.cs @ 93a11b85 (MIT).
using System;

namespace LiveSplit.UI.Components;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ComponentFactoryAttribute : Attribute
{
    public Type ComponentFactoryClassType { get; private set; }

    public ComponentFactoryAttribute(Type componentFactoryClassType)
    {
        ComponentFactoryClassType = componentFactoryClassType;
    }
}
