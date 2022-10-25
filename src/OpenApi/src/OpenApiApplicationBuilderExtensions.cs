using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.AspNetCore.OpenApi;

public static class OpenApiApplicationBuilderExtensions
{
    public static WebApplicationBuilder UseOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<OpenApiDocument>, OpenApiDocumentConfigureOptions>());
        // builder.Services.Configure<OpenApiDocument>();
        return builder;
    }
}

internal sealed class OpenApiDocumentConfigureOptions : IConfigureOptions<OpenApiDocument>
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IServiceProviderIsService _serviceProviderIsService;
    private readonly EndpointDataSource _endpointDataSource;
    private readonly IAuthenticationSchemeProvider? _authenticationSchemeProvider;
    private readonly OpenApiGenerator _generator;

    public OpenApiDocumentConfigureOptions(IHostEnvironment hostEnvironment, IServiceProviderIsService serviceProviderIsService, EndpointDataSource endpointDataSource, IAuthenticationSchemeProvider? authenticationSchemeProvider)
    {
        _hostEnvironment = hostEnvironment;
        _serviceProviderIsService = serviceProviderIsService;
        _endpointDataSource = endpointDataSource;
        _authenticationSchemeProvider = authenticationSchemeProvider;
        _generator = new OpenApiGenerator(hostEnvironment, serviceProviderIsService);
    }

    public void Configure(OpenApiDocument document)
    {
        document = _generator.GetOpenApiDocument(document, _endpointDataSource.Endpoints);
        var authSchemes = _authenticationSchemeProvider?.GetAllSchemesAsync().Result ?? Enumerable.Empty<AuthenticationScheme>();
        foreach (var scheme in authSchemes)
        {
            document.Components.SecuritySchemes.Add(scheme.Name, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
            });
        }
    }
}


