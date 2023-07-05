// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for <see cref="IEndpointRouteBuilder"/> to add Razor Components endpoints.
/// </summary>
public static class RazorComponentsEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the components discovered starting from <typeparamref name="TRootComponent"/> as Razor Components endpoints.
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/>.</param>
    /// <returns>A <see cref="RazorComponentEndpointConventionBuilder"/> to further customize the defined endpoints.</returns>
    public static RazorComponentEndpointConventionBuilder MapRazorComponents<TRootComponent>(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        EnsureRazorComponentServices(endpoints);
        AddRazorComponentsWebJsEndpoint(endpoints);

        return GetOrCreateDataSource<TRootComponent>(endpoints).DefaultBuilder;
    }

    /// <summary>
    /// Adds a specialized <see cref="RouteEndpoint"/> to the <see cref="IEndpointRouteBuilder"/> that will
    /// attempt to select a a root and page component to render using values produced by TComponentSelector
    /// </summary>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> to add the route to.</param>
    /// <param name="pattern">The URL pattern of the route.</param>
    /// <param name="state">A state object to provide to the <typeparamref name="TComponentResolver" /> instance.</param>
    /// <typeparam name="TComponentResolver">The type of a <see cref="DynamicComponentResolver{TState}"/>.</typeparam>
    /// <typeparam name="TState">The type of the state object provided to the transformer.</typeparam>
    /// <remarks>
    /// <para>
    /// This method allows the registration of a <see cref="RouteEndpoint"/> and <see cref="DynamicComponentResolver{TState}"/>
    /// that combine to dynamically select a controller action using custom logic.
    /// </para>
    /// <para>
    /// The instance of <typeparamref name="TComponentResolver"/> will be retrieved from the dependency injection container.
    /// Register <typeparamref name="TComponentResolver"/> as transient in <c>ConfigureServices</c>.
    /// </para>
    /// </remarks>
    public static void MapDynamicRazorComponentEndpoints<TComponentResolver, TState>(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern,
        TState? state = default)
        where TComponentResolver : DynamicComponentResolver<TState>
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        EnsureRazorComponentServices(endpoints);
        AddRazorComponentsWebJsEndpoint(endpoints);

        endpoints.Map(
            pattern,
            context =>
            {
                throw new InvalidOperationException("This endpoint is not expected to be executed directly.");
            })
            .Add(b =>
            {
                // TODO: This needs an incremental order. We want these routes to be ordered in the sequence they are added
                // to avoid conflicts with other routes and offer a predictable behavior.
                // We start at 1, similar to conventional routes. These are potentially very generic routes, so we don't want them to
                // intefere with other routes in the app, so we let those win.
                ((RouteEndpointBuilder)b).Order = 1;
                b.Metadata.Add(DynamicRazorComponentEndpointResolverMetadata.Create<DynamicComponentResolver<TState>, TState>(state));
            });

    }

    private static void AddRazorComponentsWebJsEndpoint(IEndpointRouteBuilder endpoints)
    {
        var options = new StaticFileOptions
        {
            FileProvider = new ManifestEmbeddedFileProvider(typeof(RazorComponentsEndpointRouteBuilderExtensions).Assembly),
            OnPrepareResponse = CacheHeaderSettings.SetCacheHeaders
        };

        var app = endpoints.CreateApplicationBuilder();
        app.Use(next => context =>
        {
            // Set endpoint to null so the static files middleware will handle the request.
            context.SetEndpoint(null);

            return next(context);
        });
        app.UseStaticFiles(options);

        var blazorEndpoint = endpoints.Map("/_framework/blazor.web.js", app.Build())
            .WithDisplayName("Blazor web static files");

        blazorEndpoint.Add((builder) => ((RouteEndpointBuilder)builder).Order = int.MinValue);

#if DEBUG
        // We only need to serve the sourcemap when working on the framework, not in the distributed packages
        endpoints.Map("/_framework/blazor.web.js.map", app.Build())
            .WithDisplayName("Blazor web static files sourcemap")
            .Add((builder) => ((RouteEndpointBuilder)builder).Order = int.MinValue);
#endif
    }

    private static RazorComponentEndpointDataSource<TRootComponent> GetOrCreateDataSource<TRootComponent>(IEndpointRouteBuilder endpoints)
    {
        var dataSource = endpoints.DataSources.OfType<RazorComponentEndpointDataSource<TRootComponent>>().FirstOrDefault();
        if (dataSource == null)
        {
            // Very likely this needs to become a factory and we might need to have multiple endpoint data
            // sources, once we figure out the exact scenarios for
            // https://github.com/dotnet/aspnetcore/issues/46992
            var factory = endpoints.ServiceProvider.GetRequiredService<RazorComponentEndpointDataSourceFactory>();
            dataSource = factory.CreateDataSource<TRootComponent>(endpoints);
            endpoints.DataSources.Add(dataSource);
        }

        return dataSource;
    }

    private static void EnsureRazorComponentServices(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var marker = endpoints.ServiceProvider.GetService<RazorComponentsMarkerService>();
        if (marker == null)
        {
            throw new InvalidOperationException(Resources.FormatUnableToFindServices(
                nameof(IServiceCollection),
                nameof(RazorComponentsServiceCollectionExtensions.AddRazorComponents)));
        }
    }
}
