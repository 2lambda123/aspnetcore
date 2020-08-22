// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Abstractions.Features;
using Microsoft.AspNetCore.Connections.Experimental;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    internal class TransportManager
    {
        private readonly List<ActiveTransport> _transports = new List<ActiveTransport>();

        private readonly ConnectionListenerFactory? _transportFactory;
        private readonly IMultiplexedConnectionListenerFactory? _multiplexedTransportFactory;
        private readonly ServiceContext _serviceContext;

        public TransportManager(
            ConnectionListenerFactory? transportFactory,
            IMultiplexedConnectionListenerFactory? multiplexedTransportFactory,
            ServiceContext serviceContext)
        {
            _transportFactory = transportFactory;
            _multiplexedTransportFactory = multiplexedTransportFactory;
            _serviceContext = serviceContext;
        }

        private ConnectionManager ConnectionManager => _serviceContext.ConnectionManager;
        private IKestrelTrace Trace => _serviceContext.Log;

        //public async Task<EndPoint?> BindAsync(EndPoint endPoint, ConnectionDelegate connectionDelegate, EndpointConfig? endpointConfig)
        //{
        //    if (_transportFactory is null)
        //    {
        //        throw new InvalidOperationException($"Cannot bind with {nameof(ConnectionDelegate)} no {nameof(ConnectionListenerFactory)} is registered.");
        //    }

        //    var transport = await _transportFactory.ListenAsync(endPoint).ConfigureAwait(false);
        //    StartAcceptLoop(new GenericConnectionListener(transport), c => connectionDelegate(c), endpointConfig);
        //    return transport.LocalEndPoint;
        //}

        public async Task<EndPoint?> BindAsync(EndPoint endPoint, Func<Connection, Task<Connection>> connectionDelegate, EndpointConfig? endpointConfig)
        {
            if (_transportFactory is null)
            {
                throw new InvalidOperationException($"Cannot bind with {nameof(ConnectionDelegate)} no {nameof(ConnectionListenerFactory)} is registered.");
            }

            var transport = await _transportFactory.ListenAsync(endPoint).ConfigureAwait(false);
            StartAcceptLoop(new GenericConnectionListener(transport), c => connectionDelegate(c), endpointConfig);
            return transport.LocalEndPoint;
        }

        public async Task<EndPoint> BindAsync(EndPoint endPoint, MultiplexedConnectionDelegate multiplexedConnectionDelegate, EndpointConfig? endpointConfig)
        {
            if (_multiplexedTransportFactory is null)
            {
                throw new InvalidOperationException($"Cannot bind with {nameof(MultiplexedConnectionDelegate)} no {nameof(IMultiplexedConnectionListenerFactory)} is registered.");
            }

            static Func<MultiplexedConnectionContextWrapper, Task<MultiplexedConnectionContextWrapper>> ConvertDelegate(MultiplexedConnectionDelegate unwrappedDelegate)
            {
                return async wrapper =>
                {
                    await unwrappedDelegate(wrapper.MultiplexedConnectionContext);
                    return wrapper;
                };
            }

            var transport = await _multiplexedTransportFactory.BindAsync(endPoint).ConfigureAwait(false);
            StartAcceptLoop(new GenericMultiplexedConnectionListener(transport), ConvertDelegate(multiplexedConnectionDelegate), endpointConfig);
            return transport.EndPoint;
        }

        private void StartAcceptLoop<T>(IConnectionListener<T> connectionListener, Func<T, Task<T>> connectionDelegate, EndpointConfig? endpointConfig) where T : ConnectionBase
        {
            var transportConnectionManager = new TransportConnectionManager(_serviceContext.ConnectionManager);
            var connectionDispatcher = new ConnectionDispatcher<T>(_serviceContext, connectionDelegate, transportConnectionManager);
            var acceptLoopTask = connectionDispatcher.StartAcceptingConnections(connectionListener);

            _transports.Add(new ActiveTransport(connectionListener, acceptLoopTask, transportConnectionManager, endpointConfig));
        }

        public Task StopEndpointsAsync(List<EndpointConfig> endpointsToStop, CancellationToken cancellationToken)
        {
            var transportsToStop = _transports.Where(t => t.EndpointConfig != null && endpointsToStop.Contains(t.EndpointConfig)).ToList();
            return StopTransportsAsync(transportsToStop, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return StopTransportsAsync(new List<ActiveTransport>(_transports), cancellationToken);
        }

        private async Task StopTransportsAsync(List<ActiveTransport> transportsToStop, CancellationToken cancellationToken)
        {
            var tasks = new Task[transportsToStop.Count];

            for (int i = 0; i < transportsToStop.Count; i++)
            {
                tasks[i] = transportsToStop[i].UnbindAsync(cancellationToken);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            async Task StopTransportConnection(ActiveTransport transport)
            {
                if (!await transport.TransportConnectionManager.CloseAllConnectionsAsync(cancellationToken).ConfigureAwait(false))
                {
                    Trace.NotAllConnectionsClosedGracefully();

                    if (!await transport.TransportConnectionManager.AbortAllConnectionsAsync().ConfigureAwait(false))
                    {
                        Trace.NotAllConnectionsAborted();
                    }
                }
            }

            for (int i = 0; i < transportsToStop.Count; i++)
            {
                tasks[i] = StopTransportConnection(transportsToStop[i]);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            for (int i = 0; i < transportsToStop.Count; i++)
            {
                tasks[i] = transportsToStop[i].DisposeAsync().AsTask();
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            foreach (var transport in transportsToStop)
            {
                _transports.Remove(transport);
            }
        }

        private class ActiveTransport : IAsyncDisposable
        {
            public ActiveTransport(IConnectionListenerBase transport, Task acceptLoopTask, TransportConnectionManager transportConnectionManager, EndpointConfig? endpointConfig = null)
            {
                ConnectionListener = transport;
                AcceptLoopTask = acceptLoopTask;
                TransportConnectionManager = transportConnectionManager;
                EndpointConfig = endpointConfig;
            }

            public IConnectionListenerBase ConnectionListener { get; }
            public Task AcceptLoopTask { get; }
            public TransportConnectionManager TransportConnectionManager { get; }

            public EndpointConfig? EndpointConfig { get; }

            public async Task UnbindAsync(CancellationToken cancellationToken)
            {
                await ConnectionListener.UnbindAsync(cancellationToken).ConfigureAwait(false);
                await AcceptLoopTask.ConfigureAwait(false);
            }

            public ValueTask DisposeAsync()
            {
                return ConnectionListener.DisposeAsync();
            }
        }

        private class GenericConnectionListener : IConnectionListener<Connection>
        {
            private readonly ConnectionListener _connectionListener;
            private readonly IUnbindFeature? _unbindFeature;
            private readonly TaskCompletionSource? _fakeUnbindTcs;

            public GenericConnectionListener(ConnectionListener connectionListener)
            {
                _connectionListener = connectionListener;

                if (!_connectionListener.ListenerProperties.TryGet(out _unbindFeature))
                {
                    _fakeUnbindTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            public EndPoint? EndPoint => _connectionListener.LocalEndPoint;

            public ValueTask<Connection?> AcceptAsync(CancellationToken cancellationToken = default)
            {
                if (_unbindFeature is null)
                {
                    if (_fakeUnbindTcs!.Task.IsCompleted)
                    {
                        return new ValueTask<Connection?>(result: null);
                    }

                    return AcceptAsyncAwaited(cancellationToken);
                }

                return _connectionListener.AcceptAsync(options: null, cancellationToken)!;
            }

            // This should be temporary: https://github.com/dotnet/runtime/issues/41118
            private async ValueTask<Connection?> AcceptAsyncAwaited(CancellationToken cancellationToken)
            {
                var acceptTask = _connectionListener.AcceptAsync(options: null, cancellationToken).AsTask();
                var completedTask = await Task.WhenAny(acceptTask, _fakeUnbindTcs!.Task);

                static async Task RefuseAcceptedConnection(Task<Connection> task)
                {
                    try
                    {
                        using var connection = await task;
                        connection?.CloseAsync(ConnectionCloseMethod.Abort);
                    }
                    catch
                    {
                        // If we expect to keep this, we should log.
                    }
                }

                if (completedTask == _fakeUnbindTcs!.Task)
                {
                    _ = RefuseAcceptedConnection(acceptTask);
                    return null;
                }

                return await acceptTask;
            }

            public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
            {
                if (_unbindFeature is null)
                {
                    _fakeUnbindTcs!.TrySetResult();
                }
                else
                {
                    _unbindFeature.Unbind();
                }

                return default;
            }

            public ValueTask DisposeAsync()
                => _connectionListener.DisposeAsync();
        }

        private class GenericMultiplexedConnectionListener : IConnectionListener<MultiplexedConnectionContextWrapper>
        {
            private readonly IMultiplexedConnectionListener _multiplexedConnectionListener;

            public GenericMultiplexedConnectionListener(IMultiplexedConnectionListener multiplexedConnectionListener)
            {
                _multiplexedConnectionListener = multiplexedConnectionListener;
            }

            public EndPoint? EndPoint => _multiplexedConnectionListener.EndPoint;

            public async ValueTask<MultiplexedConnectionContextWrapper?> AcceptAsync(CancellationToken cancellationToken = default)
            {
                var multiplexedConnection = await _multiplexedConnectionListener.AcceptAsync(features: null, cancellationToken);

                if (multiplexedConnection is null)
                {
                    return null;
                }

                return new MultiplexedConnectionContextWrapper(multiplexedConnection);
            }

            public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
                => _multiplexedConnectionListener.UnbindAsync();

            public ValueTask DisposeAsync()
                => _multiplexedConnectionListener.DisposeAsync();
        }
    }
}
