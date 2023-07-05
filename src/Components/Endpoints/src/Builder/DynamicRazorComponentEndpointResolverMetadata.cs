// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Components.Endpoints;

internal class DynamicRazorComponentEndpointResolverMetadata : IDynamicEndpointMetadata
{
    private readonly Func<HttpContext, RouteValueDictionary, ValueTask<ResolverResult>> _resolver;

    public DynamicRazorComponentEndpointResolverMetadata(Func<HttpContext, RouteValueDictionary, ValueTask<ResolverResult>> resolver) =>
        _resolver = resolver;

    public bool IsDynamic => true;

    internal static DynamicRazorComponentEndpointResolverMetadata Create<TResolver, TState>(TState? state)
        where TResolver : DynamicComponentResolver<TState>
    {
        return new DynamicRazorComponentEndpointResolverMetadata((HttpContext context, RouteValueDictionary routeValues) =>
        {
            var resolver = context.RequestServices.GetRequiredService<TResolver>();
            return resolver.ResolveComponentAsync(context, routeValues, state);
        });
    }

    public ValueTask<ResolverResult> ResolveAndInvokeResolver(HttpContext httpContext, RouteValueDictionary values) =>
        _resolver(httpContext, values);
}
