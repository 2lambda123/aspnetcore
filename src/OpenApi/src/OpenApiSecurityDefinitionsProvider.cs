// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Abstractions;
using Microsoft.OpenApi.Models;

namespace Microsoft.AspNetCore.OpenApi;

public class OpenApiSecurityDefinitionsProvider
{
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;

    private IDictionary<string, OpenApiSecurityScheme> _maps { get; }

    public OpenApiSecurityDefinitionsProvider(IAuthenticationSchemeProvider authenticationSchemeProvider)
    {
        _authenticationSchemeProvider = authenticationSchemeProvider;
    }

    public async Task<Dictionary<string, OpenApiSecurityScheme>> GetOpenApiSecurityScheme()
    {
        var securitySchemes = new Dictionary<string, OpenApiSecurityScheme>();
        var schemes = await _authenticationSchemeProvider.GetAllSchemesAsync();
        foreach (var scheme in schemes)
        {
            securitySchemes[scheme.Name] = new OpenApiSecurityScheme
            {
                Type = GetOpenApiSecuritySchemeTypeFromAuthenticationType(scheme.AuthenticationType),
                Scheme = GetSchemeHeaderFromAuthenticationType(scheme.AuthenticationType),
                In = GetSchemeLocationFromAuthenticationType(scheme.AuthenticationType)
            };
        }
        return securitySchemes;
    }

    private static ParameterLocation GetSchemeLocationFromAuthenticationType(AuthenticationType authenticationType)
    {
        return authenticationType switch
        {
            AuthenticationType.Basic => ParameterLocation.Header,
            AuthenticationType.Bearer => ParameterLocation.Header,
            AuthenticationType.Cookie => ParameterLocation.Cookie,
            _ => ParameterLocation.Header,
        };
    }

    private static string GetSchemeHeaderFromAuthenticationType(AuthenticationType authenticationType)
    {
        return authenticationType switch
        {
            AuthenticationType.Bearer => "Bearer",
            AuthenticationType.Basic => "Basic",
            _ => string.Empty,
        };
    }

    private static SecuritySchemeType GetOpenApiSecuritySchemeTypeFromAuthenticationType(AuthenticationType authenticationType)
    {
        return authenticationType switch
        {
            AuthenticationType.Bearer => SecuritySchemeType.Http,
            AuthenticationType.Basic => SecuritySchemeType.Http,
            AuthenticationType.Cookie => SecuritySchemeType.ApiKey,
            _ => throw new InvalidOperationException($"{nameof(AuthenticationScheme.AuthenticationType)} required to derive {nameof(SecuritySchemeType)}.")
        };
    }
}
