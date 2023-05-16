// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.AspNetCore.Components.Endpoints;

namespace Microsoft.AspNetCore.Components.Infrastructure;

internal class RazorComponentEndpointDataSourceFactory
{
    private readonly RazorComponentEndpointFactory _factory;

    public RazorComponentEndpointDataSourceFactory(RazorComponentEndpointFactory factory)
    {
        _factory = factory;
    }

    public RazorComponentEndpointDataSource<TRootComponent> CreateDataSource<TRootComponent>()
    {
        var assembly = typeof(TRootComponent).Assembly;
        var rca = assembly.GetCustomAttribute<RazorComponentApplicationAttribute>();
        var builder = rca?.GetBuilder() ?? DefaultRazorComponentApplication<TRootComponent>.Instance.GetBuilder();
        if (builder == null)
        {
            throw new InvalidOperationException("");
        }

        return new RazorComponentEndpointDataSource<TRootComponent>(builder, _factory);
    }
}
