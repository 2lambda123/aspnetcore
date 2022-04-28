// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Microsoft.Extensions.DependencyInjection;

internal class AuthenticationStateProviderAccessor : IAuthenticationStateProviderAccessor
{
    private readonly CircuitLocal<IServiceProvider> _provider =
        new CircuitLocal<IServiceProvider>();

    public AuthenticationStateProvider AuthenticationStateProvider =>
        _provider.Value.GetRequiredService<AuthenticationStateProvider>();
}
