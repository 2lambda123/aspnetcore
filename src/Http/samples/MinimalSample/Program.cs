// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", (EndpointDataSource dataSource)
    => EndpointDataSource.GetDebuggerDisplayStringForEndpoints(dataSource.Endpoints));

((IEndpointRouteBuilder)app).DataSources.Add(new CustomEndpointDataSource());

app.Run();

class CustomEndpointDataSource : EndpointDataSource
{
    public override IReadOnlyList<Endpoint> Endpoints => Enumerable.Range(0, 10).Select(CreateEndpoint).ToArray();

    public override IChangeToken GetChangeToken() => NullChangeToken.Singleton;

    static Endpoint CreateEndpoint(int id)
    {
        var displayName = $"Custom endpoint #{id}";
        var metadata = new EndpointMetadataCollection(new[] { new RouteNameMetadata(displayName) });

        return new RouteEndpoint(
            context => context.Response.WriteAsync(displayName),
            RoutePatternFactory.Parse($"/custom/{id}"),
            order: 0, metadata, displayName);
    }
}

