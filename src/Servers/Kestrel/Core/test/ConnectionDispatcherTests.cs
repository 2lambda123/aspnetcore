// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Tests
{
    public class ConnectionDispatcherTests
    {
        [Fact]
        public async Task OnConnectionCreatesLogScopeWithConnectionId()
        {
            var serviceContext = new TestServiceContext();
            // This needs to run inline
            var tcs = new TaskCompletionSource();

            var connection = new MockConnection();
            connection.ConnectionClosed = new CancellationToken(canceled: true);
            var transportConnectionManager = new TransportConnectionManager(serviceContext.ConnectionManager);
            var kestrelConnection = new KestrelConnection<Connection>(0, serviceContext, transportConnectionManager, async c =>
            {
                await tcs.Task;
                return c;
            }, connection, serviceContext.Log);
            transportConnectionManager.AddConnection(0, kestrelConnection);

            var task = kestrelConnection.ExecuteAsync();

            // The scope should be created
            var scopeObjects = ((TestKestrelTrace)serviceContext.Log)
                                    .Logger
                                    .Scopes
                                    .OfType<IReadOnlyList<KeyValuePair<string, object>>>()
                                    .ToList();

            Assert.Single(scopeObjects);
            var pairs = scopeObjects[0].ToDictionary(p => p.Key, p => p.Value);
            Assert.True(pairs.ContainsKey("ConnectionId"));
            Assert.Equal(connection.ConnectionId, pairs["ConnectionId"]);

            tcs.TrySetResult();

            await task;

            // Verify the scope was disposed after request processing completed
            Assert.True(((TestKestrelTrace)serviceContext.Log).Logger.Scopes.IsEmpty);
        }

        [Fact]
        public async Task StartAcceptingConnectionsAsyncLogsIfAcceptAsyncThrows()
        {
            var serviceContext = new TestServiceContext();
            var logger = ((TestKestrelTrace)serviceContext.Log).Logger;
            logger.ThrowOnCriticalErrors = false;

            var dispatcher = new ConnectionDispatcher<Connection>(serviceContext, c => new ValueTask<Connection>(c), new TransportConnectionManager(serviceContext.ConnectionManager));

            await dispatcher.StartAcceptingConnections(new ThrowingListener());

            Assert.Equal(1, logger.CriticalErrorsLogged);
            var critical = logger.Messages.SingleOrDefault(m => m.LogLevel == LogLevel.Critical);
            Assert.NotNull(critical);
            Assert.IsType<InvalidOperationException>(critical.Exception);
            Assert.Equal("Unexpected error listening", critical.Exception.Message);
        }

        [Fact]
        public async Task OnConnectionFiresOnCompleted()
        {
            var serviceContext = new TestServiceContext();

            var connection = new MockConnection();
            connection.ConnectionClosed = new CancellationToken(canceled: true);
            var transportConnectionManager = new TransportConnectionManager(serviceContext.ConnectionManager);
            var kestrelConnection = new KestrelConnection<Connection>(0, serviceContext, transportConnectionManager, c => new ValueTask<Connection>(c), connection, serviceContext.Log);
            transportConnectionManager.AddConnection(0, kestrelConnection);

            Assert.True(kestrelConnection.TransportConnection.ConnectionProperties.TryGet<IConnectionCompleteFeature>(out var completeFeature));

            Assert.NotNull(completeFeature);
            object stateObject = new object();
            object callbackState = null;
            completeFeature.OnCompleted(state => { callbackState = state; return Task.CompletedTask; }, stateObject);

            await kestrelConnection.ExecuteAsync();

            Assert.Equal(stateObject, callbackState);
        }

        [Fact]
        public async Task OnConnectionOnCompletedExceptionCaught()
        {
            var serviceContext = new TestServiceContext();
            var logger = ((TestKestrelTrace)serviceContext.Log).Logger;
            var connection = new MockConnection();
            connection.ConnectionClosed = new CancellationToken(canceled: true);
            var transportConnectionManager = new TransportConnectionManager(serviceContext.ConnectionManager);
            var kestrelConnection = new KestrelConnection<Connection>(0, serviceContext, transportConnectionManager, c => new ValueTask<Connection>(c), connection, serviceContext.Log);
            transportConnectionManager.AddConnection(0, kestrelConnection);

            Assert.True(kestrelConnection.TransportConnection.ConnectionProperties.TryGet<IConnectionCompleteFeature>(out var completeFeature));

            Assert.NotNull(completeFeature);
            object stateObject = new object();
            object callbackState = null;
            completeFeature.OnCompleted(state => { callbackState = state; throw new InvalidTimeZoneException(); }, stateObject);

            await kestrelConnection.ExecuteAsync();

            Assert.Equal(stateObject, callbackState);
            var errors = logger.Messages.Where(e => e.LogLevel >= LogLevel.Error).ToArray();
            Assert.Single(errors);
            Assert.Equal("An error occurred running an IConnectionCompleteFeature.OnCompleted callback.", errors[0].Message);
        }

        private class ThrowingListener : IConnectionListener<Connection>
        {
            public EndPoint EndPoint { get; set; }

            public ValueTask<Connection> AcceptAsync(CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Unexpected error listening");
            }

            public ValueTask DisposeAsync()
            {
                return default;
            }

            public ValueTask UnbindAsync(CancellationToken cancellationToken = default)
            {
                return default;
            }
        }
    }
}
