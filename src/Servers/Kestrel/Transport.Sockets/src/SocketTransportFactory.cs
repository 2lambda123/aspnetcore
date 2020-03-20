// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets
{
    public sealed class SocketTransportFactory : IConnectionListenerFactory, System.Net.Connections.IConnectionListenerFactory
    {
        private readonly SocketTransportOptions _options;
        private readonly SocketsTrace _trace;

        public SocketTransportFactory(
            IOptions<SocketTransportOptions> options,
            ILoggerFactory loggerFactory)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _options = options.Value;
            var logger = loggerFactory.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets");
            _trace = new SocketsTrace(logger);
        }

        ValueTask<System.Net.Connections.IConnectionListener> System.Net.Connections.IConnectionListenerFactory.BindAsync(EndPoint endPoint, System.Net.Connections.IConnectionProperties options, CancellationToken cancellationToken)
        {
            var transport = new SocketConnectionListener(endPoint, _options, _trace);
            transport.Bind();
            return new ValueTask<System.Net.Connections.IConnectionListener>(transport);
        }

        public ValueTask<Connections.IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
        {
            var transport = new SocketConnectionListener(endpoint, _options, _trace);
            transport.Bind();
            return new ValueTask<Connections.IConnectionListener>(transport);
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }
    }
}
