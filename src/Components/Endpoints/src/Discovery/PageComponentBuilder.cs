// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;

namespace Microsoft.AspNetCore.Components;

public class PageComponentBuilder : IEquatable<PageComponentBuilder?>
{
    private List<string>? _routeTemplates;

    public string? Source { get; set; }

    public List<string>? RouteTemplates
    {
        get => _routeTemplates;
        set
        {
            value?.Sort(StringComparer.Ordinal);
            _routeTemplates = value;
        }
    }

    public Type? PageType { get; set; }

    public bool HasSource(string name)
    {
        return string.Equals(Source, name, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PageComponentBuilder);
    }

    public bool Equals(PageComponentBuilder? other)
    {
        return other is not null &&
               Source == other.Source &&
               (ReferenceEquals(RouteTemplates, other.RouteTemplates) || (RouteTemplates != null &&
               Enumerable.SequenceEqual(RouteTemplates, other.RouteTemplates!, StringComparer.OrdinalIgnoreCase))) &&
               EqualityComparer<Type>.Default.Equals(PageType, other.PageType);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Source);
        if (RouteTemplates != null)
        {
            for (var i = 0; i < RouteTemplates.Count; i++)
            {
                hash.Add(RouteTemplates[i]);
            }
        }
        hash.Add(PageType);
        return hash.ToHashCode();
    }
}
