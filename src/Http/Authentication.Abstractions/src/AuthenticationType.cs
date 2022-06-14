// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Authentication.Abstractions;

public enum AuthenticationType
{
    Basic,
    Bearer,
    Cookie,
    OpenIdConnect,
    WsFederation
}
