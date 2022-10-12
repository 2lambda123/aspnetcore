// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using MinimalSample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.MapGet("/", (EndpointDataSource dataSource, HttpResponse response) =>
{
    response.Headers["Refresh"] = "1";
    return EndpointDataSource.GetDebuggerDisplayStringForEndpoints(dataSource.Endpoints);
});

var preview = app.MapGroup("/preview");
AddEndpoints(app);
AddEndpoints(preview);

preview.WithMetadata("PREVIEW! ");

app.Use(PrintMetadataMiddleware("Before"));

app.UseRouting();

app.Use(PrintMetadataMiddleware("Middle"));

app.UseEndpoints(_ => { });

app.Use(PrintMetadataMiddleware("After"));

app.Run();

static Func<RequestDelegate, RequestDelegate> PrintMetadataMiddleware(string name)
{
    return (next) =>
    {
        return context =>
        {
            var log = $"{name}: {context.Request.Path}";

            if (context.GetEndpoint()?.Metadata.GetMetadata<string>() is string metadata)
            {
                Console.WriteLine($"{log} {metadata}");
            }
            else
            {
                Console.WriteLine($"{log} No string metadata found");
            }

            return next(context);
        };
    };
}

static void AddEndpoints(IEndpointRouteBuilder app)
{
    app.MapGet("/hello/{name}", (string name) => $"Hello {name}!")
        .AddEndpointFilterFactory((context, next) =>
        {
            Console.WriteLine("Running filter factory!");

            var parameters = context.MethodInfo.GetParameters();
            // Only operate handlers with a single argument
            if (parameters.Length == 1 &&
                parameters[0] is ParameterInfo parameter &&
                parameter.ParameterType == typeof(string))
            {
                return invocationContext =>
                {
                    var modifiedArgument = invocationContext
                        .GetArgument<string>(0)
                        .ToUpperInvariant();
                    invocationContext.Arguments[0] = modifiedArgument;
                    return next(invocationContext);
                };
            }

            return invocationContext => next(invocationContext);
        });

    app.MapControllers()
        .AddEndpointFilter((invocationContext, next) =>
        {
            var argument = invocationContext.GetArgument<string>(0);
            if (argument != null)
            {
                invocationContext.Arguments[0] = Convert.ToBase64String(Encoding.UTF8.GetBytes(argument));
            }
            return next(invocationContext);
        });

    app.DataSources.Add(new DefaultEndpointDataSource(CustomEndpointDataSource.CreateEndpoint(0), CustomEndpointDataSource.CreateEndpoint(1)));
    app.DataSources.Add(new DynamicEndpointDataSource());
}

class CustomEndpointDataSource : EndpointDataSource
{
    public override IReadOnlyList<Endpoint> Endpoints => new[] { CreateEndpoint(0), CreateEndpoint(1) };
    public override IReadOnlyList<Endpoint> GetGroupedEndpoints(RouteGroupContext context)
    {
        return base.GetGroupedEndpoints(context);
    }

    public override IChangeToken GetChangeToken() => NullChangeToken.Singleton;

    public static Endpoint CreateEndpoint(int id)
    {
        var displayName = $"Custom endpoint #{id}";
        var metadata = new EndpointMetadataCollection(new[] { new RouteNameMetadata(displayName) });

        return new RouteEndpoint(
            context => context.Response.WriteAsync($"{context.GetEndpoint()!.Metadata.GetMetadata<string>()}{displayName}"),
            RoutePatternFactory.Parse($"/custom/{id}"),
            order: 0, metadata, displayName);
    }
}

