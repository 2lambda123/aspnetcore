// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Microsoft.AspNetCore.Components.Endpoints;

/// <summary>
/// Provides an abstraction for dynamically resolving a root and page components to render.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DynamicComponentResolver{TState}"/> can be used with
/// <see cref="RazorComponentsEndpointRouteBuilderExtensions.MapDynamicRazorComponentEndpoints{TComponentResolver, TState}(IEndpointRouteBuilder, string, TState?)" />
/// to implement custom logic that selects a root component and routable component to render.
/// </para>
/// </remarks>
public abstract class DynamicComponentResolver<TState>
{
    /// <summary>
    /// Resolves a root component and page component to render based on the current request and state.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext" /> for the current request.</param>
    /// <param name="state">The state provided for the resolver in the <see cref="RazorComponentsEndpointRouteBuilderExtensions.MapDynamicRazorComponentEndpoints{TComponentResolver, TState}(IEndpointRouteBuilder, string, TState?)" /> call.</param>
    /// <param name="values">The route values for the current request.</param>
    /// <returns>A <see cref="ValueTask" /> that when completed, provides the page and root components to render.</returns>
    public abstract ValueTask<ResolverResult> ResolveComponentAsync(HttpContext httpContext, RouteValueDictionary values, TState? state);

}
