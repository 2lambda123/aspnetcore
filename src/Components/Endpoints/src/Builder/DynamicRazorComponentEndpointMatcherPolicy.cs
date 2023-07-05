// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Matching;

namespace Microsoft.AspNetCore.Components.Endpoints;

internal class DynamicRazorComponentEndpointMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    private readonly RazorComponentEndpointFactory _razorComponentEndpointFactory;

    public DynamicRazorComponentEndpointMatcherPolicy(RazorComponentEndpointFactory razorComponentEndpointFactory)
    {
        _razorComponentEndpointFactory = razorComponentEndpointFactory;
    }

    // This executes very early in the routing pipeline so that other
    // policies can see the resulting dynamicComponentEndpoint.
    public override int Order => int.MinValue + 150;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        if (!ContainsDynamicEndpoints(endpoints))
        {
            return false;
        }

        for (var i = 0; i < endpoints.Count; i++)
        {
            if (endpoints[i].Metadata.GetMetadata<DynamicRazorComponentEndpointResolverMetadata>() != null)
            {
                // Found a dynamic razor component dynamicComponentEndpoint.
                return true;
            }
        }

        return false;
    }

    public async Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(candidates);

        // There's no real benefit here from trying to avoid the async state machine.
        // We only execute on nodes that contain a dynamic policy, and thus always have
        // to await something.
        for (var i = 0; i < candidates.Count; i++)
        {
            if (!candidates.IsValidCandidate(i))
            {
                continue;
            }

            var endpoint = candidates[i].Endpoint;
            var originalValues = candidates[i].Values!;

            var dynamicResolverMetadata = endpoint.Metadata.GetMetadata<DynamicRazorComponentEndpointResolverMetadata>();
            if (dynamicResolverMetadata != null)
            {
                var result = await dynamicResolverMetadata.ResolveAndInvokeResolver(httpContext, originalValues);
                if (result == ResolverResult.Empty)
                {
                    candidates.ReplaceEndpoint(i, null, null);
                    continue;
                }
                else
                {
                    var dynamicComponentEndpoint = new Endpoint(
                        httpContext => new RazorComponentEndpointInvoker(httpContext, result.RootComponent, result.PageComponent).RenderComponent(),
                        new EndpointMetadataCollection(),
                        $"Dynamic component endpoint: Root Component = {result.RootComponent.FullName}, Page component = {result.PageComponent.FullName}");

                    candidates.ReplaceEndpoint(
                        i,
                        dynamicComponentEndpoint,
                        result.UpdatedRouteValues != null ? new RouteValueDictionary(result.UpdatedRouteValues) : originalValues);
                }
            }
            else
            {
                // Not a dynamic controller.
                continue;
            }
        }
    }
}
