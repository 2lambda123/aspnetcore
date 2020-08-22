// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    internal class KestrelConnection<T> : KestrelConnection, IThreadPoolWorkItem where T : ConnectionBase
    {
        private readonly Func<T, Task<T>> _connectionDelegate;
        private readonly T _transportConnection;

        public KestrelConnection(long id,
                                 ServiceContext serviceContext,
                                 TransportConnectionManager transportConnectionManager,
                                 Func<T, Task<T>> connectionDelegate,
                                 T transportConnection,
                                 IKestrelTrace logger)
            : base(id, serviceContext, transportConnectionManager, logger)
        {
            _connectionDelegate = connectionDelegate;

            // I know the object casts are super ugly, but it's either that or we're duplicating a lot of code paths.
            // This is only necessary since there's no generic way to add features to an arbitrary object that implements ConnectionBase
            // while keeping the same derived type.
            if (typeof(T) == typeof(Connection))
            {
                _transportConnection = (T)(object)new ConnectionWithKestrelFeatures((Connection)(object)transportConnection, this);
            }
            else if (typeof(T) == typeof(MultiplexedConnectionContextWrapper))
            {
                var multiplexedConnection = (MultiplexedConnectionContextWrapper)(object)transportConnection;
                var features = multiplexedConnection.MultiplexedConnectionContext.Features;
                features.Set<IConnectionHeartbeatFeature>(this);
                features.Set<IConnectionCompleteFeature>(this);
                features.Set<IConnectionLifetimeNotificationFeature>(this);
                _transportConnection = transportConnection;
            }
            else
            {
                throw new ArgumentException($"T must be {nameof(Connection)} or {nameof(MultiplexedConnectionContextWrapper)}.");
            }
        }

        public override ConnectionBase TransportConnection => _transportConnection;

        void IThreadPoolWorkItem.Execute()
        {
            _ = ExecuteAsync();
        }

        internal async Task ExecuteAsync()
        {
            var transportConnection = _transportConnection;

            try
            {
                KestrelEventSource.Log.ConnectionQueuedStop(transportConnection);

                Logger.ConnectionStart(transportConnection.ConnectionId());
                KestrelEventSource.Log.ConnectionStart(transportConnection);

                using (BeginConnectionScope(transportConnection))
                {
                    try
                    {
                        await _connectionDelegate(transportConnection);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(0, ex, "Unhandled exception while processing {ConnectionId}.", transportConnection.ConnectionId());
                    }
                }
            }
            finally
            {
                await FireOnCompletedAsync();

                Logger.ConnectionStop(transportConnection.ConnectionId());
                KestrelEventSource.Log.ConnectionStop(transportConnection);

                // Dispose the transport connection, this needs to happen before removing it from the
                // connection manager so that we only signal completion of this connection after the transport
                // is properly torn down.
                await transportConnection.DisposeAsync();

                _transportConnectionManager.RemoveConnection(_id);
            }
        }

        private class ConnectionWithKestrelFeatures : Connection, IConnectionProperties
        {
            private readonly Connection _innerConnection;
            private readonly KestrelConnection _kestrelConnection;

            public ConnectionWithKestrelFeatures(Connection innerConnection, KestrelConnection kestrelConnection)
            {
                _innerConnection = innerConnection;
                _kestrelConnection = kestrelConnection;
            }

            public override IConnectionProperties ConnectionProperties => this;

            public override EndPoint? LocalEndPoint => _innerConnection.LocalEndPoint;

            public override EndPoint? RemoteEndPoint => _innerConnection.RemoteEndPoint;

            protected override ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
                => _innerConnection.CloseAsync(method, cancellationToken);

            public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object? property)
            {
                if (propertyKey == typeof(IConnectionHeartbeatFeature) ||
                    propertyKey == typeof(IConnectionCompleteFeature) ||
                    propertyKey == typeof(IConnectionLifetimeNotificationFeature))
                {
                    property = _kestrelConnection;
                    return true;
                }

                return _innerConnection.ConnectionProperties.TryGet(propertyKey, out property);
            }
        }
    }
}
