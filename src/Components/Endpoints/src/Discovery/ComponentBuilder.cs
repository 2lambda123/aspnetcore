// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components;

public class ComponentBuilder : IEquatable<ComponentBuilder?>
{
    public string Source { get; set; }

    public Type ComponentType { get; set; }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ComponentBuilder);
    }

    public bool Equals(ComponentBuilder? other)
    {
        return other is not null &&
               Source == other.Source &&
               ComponentType.Equals(other.ComponentType);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Source, ComponentType);
    }

    internal bool HasSource(string name)
    {
        return string.Equals(Source, name, StringComparison.Ordinal);
    }
}
