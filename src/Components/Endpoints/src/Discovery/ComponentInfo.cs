// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components;

/// <summary>
/// Represents metadata about a component.
/// </summary>
/// <param name="componentType">The component <see cref="Type"/>.</param>
public class ComponentInfo(Type componentType/*, IComponentRenderMode renderMode */)
{
    /// <summary>
    /// Gets the component <see cref="Type"/>.
    /// </summary>
    public Type ComponentType { get; } = componentType;

    //
    // public IComponentRenderMode  RenderMode { get; } = renderMode;
}
