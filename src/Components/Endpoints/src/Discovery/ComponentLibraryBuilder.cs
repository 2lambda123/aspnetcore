// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components;

/// <summary>
/// Represents an assembly along with the components and pages included in it.
/// </summary>
/// <remarks>
/// This API is meant to be consumed in a source generation context.
/// </remarks>
/// <param name="name">The assembly name.</param>
/// <param name="pages">The list of pages in the assembly.</param>
/// <param name="components">The list of components in the assembly.</param>
public class ComponentLibraryBuilder(string name, IEnumerable<PageComponentBuilder> pages, IEnumerable<ComponentBuilder> components)
{
    /// <summary>
    /// Gets the name of the assembly.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the pages in the assembly.
    /// </summary>
    public IEnumerable<PageComponentBuilder> Pages { get; } = pages;

    /// <summary>
    /// Gets the components in the assembly.
    /// </summary>
    public IEnumerable<ComponentBuilder> Components { get; } = components;
}
