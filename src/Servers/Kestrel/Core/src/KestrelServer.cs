// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
    // REVIEW: Obsolete KestrelServer?
    // Should we add a ConnectionListenerFactory ctor anyway to make porting KestrelServer decorators easier?
    // A ConnectionListenerFactory ctor could also avoid a runtime break of apps that add KestrelServer to DI directly.
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
            _innerKestrelServer = new KestrelServerImpl(
                options,
                transportFactories: null,
                legacyTransportFactories: transportFactories,
                multiplexedTransportFactories: null,
                loggerFactory);
        }

        // For testing
        internal KestrelServer(IConnectionListenerFactory transportFactory, ServiceContext serviceContext)
        {
            _innerKestrelServer = new KestrelServerImpl(new ConnectionListenerFactoryWrapper(transportFactory), null, serviceContext);
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
    }
}
