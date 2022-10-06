// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Internal;

internal sealed class NamedPipeTransportFactory : IConnectionListenerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly NamedPipeTransportOptions _options;

    public NamedPipeTransportFactory(
        ILoggerFactory loggerFactory,
        IOptions<NamedPipeTransportOptions> options)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
        _options = options.Value;
    }

    public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
    {
        if (endpoint is not NamedPipeEndPoint np)
        {
            throw new NotSupportedException($"{endpoint.GetType()} is not supported.");
        }

        var listener = new NamedPipeConnectionListener(np, _options, _loggerFactory);
        return new ValueTask<IConnectionListener>(listener);
    }
}
