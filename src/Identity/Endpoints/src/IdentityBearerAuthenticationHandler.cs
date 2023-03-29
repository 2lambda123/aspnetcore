// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Endpoints.DTO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Identity.Endpoints;

internal sealed class IdentityBearerAuthenticationHandler : SignInAuthenticationHandler<IdentityBearerOptions>
{
    private const string BearerTokenPurpose = $"Microsoft.AspNetCore.Identity.Endpoints.IdentityBearerAuthenticationHandler:v1:BearerToken";

    private static readonly AuthenticateResult TokenMissing = AuthenticateResult.Fail("Token missing");
    private static readonly AuthenticateResult FailedUnprotectingToken = AuthenticateResult.Fail("Unprotected token failed");
    private static readonly AuthenticateResult TokenExpired = AuthenticateResult.Fail("Token expired");

    private static readonly Task<AuthenticateResult> TokenMissingTask = Task.FromResult(TokenMissing);

    private readonly IDataProtectionProvider _fallbackDataProtectionProvider;

    public IdentityBearerAuthenticationHandler(
        IOptionsMonitor<IdentityBearerOptions> optionsMonitor,
        ILoggerFactory loggerFactory,
        UrlEncoder urlEncoder,
        ISystemClock clock,
        IDataProtectionProvider dataProtectionProvider)
        : base(optionsMonitor, loggerFactory, urlEncoder, clock)
    {
        _fallbackDataProtectionProvider = dataProtectionProvider;
    }

    private new IdentityBearerEvents Events => (IdentityBearerEvents)base.Events!;

    private IDataProtectionProvider DataProtectionProvider
        => Options.DataProtectionProvider ?? _fallbackDataProtectionProvider;

    private ISecureDataFormat<AuthenticationTicket> BearerTokenProtector
        => Options.BearerTokenProtector ?? new TicketDataFormat(DataProtectionProvider.CreateProtector(BearerTokenPurpose));

    protected override Task<object> CreateEventsAsync() => Task.FromResult<object>(new IdentityBearerEvents());

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Give application opportunity to find from a different location, adjust, or reject token
        var messageReceivedContext = new MessageReceivedContext(Context, Scheme, Options);

        // event can set the token
        await Events.MessageReceived(messageReceivedContext);
        if (messageReceivedContext.Result != null)
        {
            return messageReceivedContext.Result;
        }

        // If application retrieved token from somewhere else, use that.
        var token = messageReceivedContext.Token ?? GetBearerTokenOrNull();

        // If there's no bearer token, forward to cookie auth.
        if (token is null)
        {
            return Options.BearerTokenMissingFallbackScheme is string fallbackScheme
                ? await Context.AuthenticateAsync(fallbackScheme)
                : TokenMissing;
        }

        var ticket = BearerTokenProtector.Unprotect(token);

        if (ticket?.Properties?.ExpiresUtc is null)
        {
            return FailedUnprotectingToken;
        }

        if (Clock.UtcNow >= ticket.Properties.ExpiresUtc)
        {
            return TokenExpired;
        }

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        // If there's no bearer token, forward to cookie auth.
        if (GetBearerTokenOrNull() is null)
        {
            return Options.BearerTokenMissingFallbackScheme is string fallbackScheme
                ? Context.AuthenticateAsync(fallbackScheme)
                : TokenMissingTask;
        }

        Response.Headers.Append(HeaderNames.WWWAuthenticate, "Bearer");
        return base.HandleChallengeAsync(properties);
    }

    protected override Task HandleSignInAsync(ClaimsPrincipal user, AuthenticationProperties? properties)
    {
        properties ??= new();
        properties.ExpiresUtc ??= Clock.UtcNow + Options.BearerTokenExpiration;

        var ticket = new AuthenticationTicket(user, properties, Scheme.Name);
        var accessTokenResponse = new AccessTokenResponse
        {
            AccessToken = BearerTokenProtector.Protect(ticket),
            ExpiresInTotalSeconds = Options.BearerTokenExpiration.TotalSeconds,
        };

        return Context.Response.WriteAsJsonAsync(accessTokenResponse);
    }

    protected override Task HandleSignOutAsync(AuthenticationProperties? properties)
        => throw new NotSupportedException($"""
Sign out is not currently supported by identity bearer tokens.
If you want to delete cookies or clear a session, specify "{Options.BearerTokenMissingFallbackScheme}" as the authentication scheme.
""");

    private string? GetBearerTokenOrNull()
    {
        var authorization = Request.Headers.Authorization.ToString();

        return authorization.StartsWith("Bearer ", StringComparison.Ordinal)
            ? authorization["Bearer ".Length..]
            : null;
    }
}
