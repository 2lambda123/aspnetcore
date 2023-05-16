// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components;

/// <summary>
/// Builder used to configure a <see cref="RazorComponentApplication"/> instance.
/// </summary>
public class ComponentApplicationBuilder
{
    private readonly HashSet<string> _assemblies = new();
    private readonly PageCollectionBuilder _pageCollectionBuilder = new();
    private readonly ComponentCollectionBuilder _componentCollectionBuilder = new();

    public void AddLibrary(ComponentLibraryBuilder libraryBuilder)
    {
        if (_assemblies.Contains(libraryBuilder.Name))
        {
            throw new InvalidOperationException("Assembly already defined.");
        }
        _assemblies.Add(libraryBuilder.Name);
        _pageCollectionBuilder.AddFromLibraryInfo(libraryBuilder.Pages);
        _componentCollectionBuilder.AddFromLibraryInfo(libraryBuilder.Components);
    }

    /// <summary>
    /// Builds the component application definition.
    /// </summary>
    /// <returns>The <see cref="RazorComponentApplication"/>.</returns>
    public RazorComponentApplication Build()
    {
        return new RazorComponentApplication(
            _pageCollectionBuilder.ToPageCollection(),
            _componentCollectionBuilder.ToComponentCollection());
    }

    public bool HasAssembly(string assemblyName)
    {
        return _assemblies.Contains(assemblyName);
    }

    public void Combine(ComponentApplicationBuilder other)
    {
        _assemblies.UnionWith(other._assemblies);
        Pages.Combine(other.Pages);
        Components.Combine(other.Components);
    }

    public void Exclude(ComponentApplicationBuilder builder)
    {
        _assemblies.ExceptWith(builder._assemblies);
        Pages.Exclude(builder.Pages);
        Components.Exclude(builder.Components);
    }

    public void Remove(string assemblyName)
    {
        _assemblies.Remove(assemblyName);
        Pages.RemoveFromAssembly(assemblyName);
        Components.Remove(assemblyName);
    }

    public PageCollectionBuilder Pages { get; } = new PageCollectionBuilder();

    public ComponentCollectionBuilder Components { get; } = new ComponentCollectionBuilder();
}
