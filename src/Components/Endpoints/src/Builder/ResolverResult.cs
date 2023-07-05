// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Endpoints;

/// <summary>
/// Result from resolving a root component and page component to render.
/// </summary>
public readonly struct ResolverResult : IEquatable<ResolverResult>
{
    /// <summary>
    /// A default <see cref="ResolverResult"/> that indicates no components were resolved.
    /// </summary>
    public static readonly ResolverResult Empty;

    /// <summary>
    /// Initializes a new instance of <see cref="ResolverResult"/>.
    /// </summary>
    /// <param name="rootComponent">The root component to render.</param>
    /// <param name="pageComponent">The page component to render.</param>
    /// <param name="updatedRouteValues"></param>
    public ResolverResult(Type rootComponent, Type pageComponent, IReadOnlyDictionary<string, object?> updatedRouteValues)
    {
        ArgumentNullException.ThrowIfNull(rootComponent);
        ArgumentNullException.ThrowIfNull(pageComponent);
        ArgumentNullException.ThrowIfNull(updatedRouteValues);
        RootComponent = rootComponent;
        PageComponent = pageComponent;
        UpdatedRouteValues = updatedRouteValues;
    }

    /// <summary>
    /// Gets the root component to render.
    /// </summary>
    public Type RootComponent { get; }

    /// <summary>
    /// Gets the page component to render.
    /// </summary>
    public Type PageComponent { get; }
    public IReadOnlyDictionary<string, object?> UpdatedRouteValues { get; }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ResolverResult result && Equals(result);

    /// <inheritdoc/>
    public bool Equals(ResolverResult other) => EqualityComparer<Type>.Default.Equals(RootComponent, other.RootComponent) &&
        EqualityComparer<Type>.Default.Equals(PageComponent, other.PageComponent);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(RootComponent, PageComponent);

    /// <inheritdoc/>
    public static bool operator ==(ResolverResult left, ResolverResult right) => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(ResolverResult left, ResolverResult right) => !(left == right);
}
