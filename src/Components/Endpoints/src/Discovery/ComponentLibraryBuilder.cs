// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components;

public class ComponentLibraryBuilder(string name, IEnumerable<PageComponentBuilder> pages, IEnumerable<ComponentBuilder> components)
{
    public string Name { get; } = name;
    public IEnumerable<PageComponentBuilder> Pages { get; } = pages;
    public IEnumerable<ComponentBuilder> Components { get; } = components;
}
