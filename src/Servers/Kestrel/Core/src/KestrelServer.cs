// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Server.Kestrel.Core
{
    public class KestrelServer : IServer
    {
        private KestrelServerImpl _innerKestrelServer;

        public KestrelServer(IOptions<KestrelServerOptions> options, IConnectionListenerFactory transportFactory, ILoggerFactory loggerFactory)
            : this(options, new[] { transportFactory ?? throw new ArgumentNullException(nameof(transportFactory)) }, loggerFactory)
        {
        }

        internal KestrelServer(
            IOptions<KestrelServerOptions> options,
            IEnumerable<IConnectionListenerFactory> transportFactories,
            ILoggerFactory loggerFactory)
        {
            _innerKestrelServer = new KestrelServerImpl(options, WrapTransportFactories(transportFactories), loggerFactory);
        }

        // For testing
        internal KestrelServer(IEnumerable<IConnectionListenerFactory> transportFactories, ServiceContext serviceContext)
        {
            _innerKestrelServer = new KestrelServerImpl(WrapTransportFactories(transportFactories), serviceContext);
        }

        public IFeatureCollection Features => _innerKestrelServer.Features;

        public KestrelServerOptions Options => _innerKestrelServer.Options;

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            return _innerKestrelServer.StartAsync(application, cancellationToken);
        }

        // Graceful shutdown if possible
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _innerKestrelServer.StopAsync(cancellationToken);
        }

        // Ungraceful shutdown
        public void Dispose()
        {
            _innerKestrelServer.Dispose();
        }

        private static IEnumerable<ConnectionListenerFactory> WrapTransportFactories(IEnumerable<IConnectionListenerFactory> transportFactories)
        {
            if (transportFactories is null)
            {
                return null;
            }

            var lastListener = transportFactories.LastOrDefault();

            if (lastListener is null)
            {
                return Enumerable.Empty<ConnectionListenerFactory>();
            }
            else
            {
                return new ConnectionListenerFactory[] { new ConnectionListenerFactoryWrapper(lastListener) };
            }
        }
    }
}
