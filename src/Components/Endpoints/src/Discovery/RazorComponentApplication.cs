// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components;

/// <summary>
/// The definition of a component based application.
/// </summary>
public class RazorComponentApplication
{
    private readonly PageComponentInfo[] _pages;
    private readonly ComponentInfo[] _components;

    internal RazorComponentApplication(
        PageComponentInfo[] pageCollection,
        ComponentInfo[] componentCollection)
    {
        _pages = pageCollection;
        _components = componentCollection;
    }

    /// <summary>
    /// Gets the list of <see cref="PageComponentInfo"/> associated with the application.
    /// </summary>
    /// <returns>The list of pages.</returns>
    public IReadOnlyList<PageComponentInfo> Pages => _pages;

    /// <summary>
    /// Gets the list of <see cref="ComponentInfo"/> associated with the application.
    /// </summary>
    public IReadOnlyList<ComponentInfo> Components => _components;
}
