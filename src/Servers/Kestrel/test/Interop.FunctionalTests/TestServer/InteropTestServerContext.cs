// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace Interop.FunctionalTests.TestServer;

internal class InteropTestServerContext
{
    public Action<IApplicationBuilder> ConfigureApp { get; init; }
    public ILoggerFactory LoggerFactory { get; init; }
    public TransportType TransportType { get; init; }
    public bool UseHttps { get; init; }
}
