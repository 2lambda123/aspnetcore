// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests.TestTransport;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests
{
    public class EventSourceTests : LoggedTest
    {
        private static X509Certificate2 _x509Certificate2 = TestResources.GetTestCertificate();

        private TestEventListener _listener;// = new TestEventListener();

        public override void Initialize(TestContext context, MethodInfo methodInfo, object[] testMethodArguments, ITestOutputHelper testOutputHelper)
        {
            base.Initialize(context, methodInfo, testMethodArguments, testOutputHelper);

            _listener = new TestEventListener(Logger);
            _listener.EnableEvents(KestrelEventSource.Log, EventLevel.Verbose);
        }

        [Fact]
        public async Task EmitsHttp1StartAndStopEventsWithActivityIds()
        {
            int port;
            string connectionId = null;

            const int requestsToSend = 2;
            var requestIds = new string[requestsToSend];
            var requestsReceived = 0;

            await using (var server = new TestServer(async context =>
            {
                connectionId = context.Features.Get<IHttpConnectionFeature>().ConnectionId;
                requestIds[requestsReceived++] = context.TraceIdentifier;

                var upgradeFeature = context.Features.Get<IHttpUpgradeFeature>();

                if (upgradeFeature.IsUpgradableRequest)
                {
                    await upgradeFeature.UpgradeAsync();
                }
            },
            new TestServiceContext(LoggerFactory)))
            {
                port = server.Port;

                using var connection = server.CreateConnection();

                await connection.SendEmptyGet();
                await connection.Receive(
                    "HTTP/1.1 200 OK",
                    $"Date: {server.Context.DateHeaderValue}",
                    "Content-Length: 0",
                    "",
                    "");

                await connection.SendEmptyGetWithUpgrade();
                await connection.ReceiveEnd("HTTP/1.1 101 Switching Protocols",
                    "Connection: Upgrade",
                    $"Date: {server.Context.DateHeaderValue}",
                    "",
                    "");
            }

            Assert.NotNull(connectionId);
            Assert.Equal(2, requestsReceived);

            // Other tests executing in parallel may log events.
            var events = _listener.EventData.Where(e => e != null && GetProperty(e, "connectionId") == connectionId).ToList();

            var connectionQueuedStart = Assert.Single(events, e => e.EventName == "ConnectionQueuedStart");
            Assert.All(new[] { "connectionId", "remoteEndPoint", "localEndPoint" }, p => Assert.Contains(p, connectionQueuedStart.PayloadNames));
            Assert.Equal($"127.0.0.1:{port}", GetProperty(connectionQueuedStart, "localEndPoint"));
            Assert.NotEqual(Guid.Empty, connectionQueuedStart.ActivityId);
            Assert.Equal(Guid.Empty, connectionQueuedStart.RelatedActivityId);

            var connectionQueuedStop = Assert.Single(events, e => e.EventName == "ConnectionQueuedStop");
            Assert.All(new[] { "connectionId", "remoteEndPoint", "localEndPoint" }, p => Assert.Contains(p, connectionQueuedStop.PayloadNames));
            Assert.Equal($"127.0.0.1:{port}", GetProperty(connectionQueuedStop, "localEndPoint"));
            Assert.Equal(connectionQueuedStart.ActivityId, connectionQueuedStop.ActivityId);
            Assert.Equal(Guid.Empty, connectionQueuedStop.RelatedActivityId);

            var connectionStart = Assert.Single(events, e => e.EventName == "ConnectionStart");
            Assert.All(new[] { "connectionId", "remoteEndPoint", "localEndPoint" }, p => Assert.Contains(p, connectionStart.PayloadNames));
            Assert.Equal($"127.0.0.1:{port}", GetProperty(connectionStart, "localEndPoint"));
            Assert.NotEqual(Guid.Empty, connectionStart.ActivityId);
            Assert.Equal(Guid.Empty, connectionStart.RelatedActivityId);

            for (int i = 0; i < requestsToSend; i++)
            {
                var requestStart = Assert.Single(events, e => e.EventName == "RequestStart" && GetProperty(e, "requestId") == requestIds[i]);
                Assert.All(new[] { "connectionId", "requestId" }, p => Assert.Contains(p, requestStart.PayloadNames));
                Assert.Same(KestrelEventSource.Log, requestStart.EventSource);
                Assert.NotEqual(Guid.Empty, requestStart.ActivityId);
                Assert.Equal(connectionStart.ActivityId, requestStart.RelatedActivityId);

                var requestStop = Assert.Single(events, e => e.EventName == "RequestStop" && GetProperty(e, "requestId") == requestIds[i]);
                Assert.All(new[] { "connectionId", "requestId" }, p => Assert.Contains(p, requestStop.PayloadNames));
                Assert.Same(KestrelEventSource.Log, requestStop.EventSource);
                Assert.Equal(requestStart.ActivityId, requestStop.ActivityId);
                Assert.Equal(Guid.Empty, requestStop.RelatedActivityId);
            }

            var connectionStop = Assert.Single(events, e => e.EventName == "ConnectionStop");
            Assert.All(new[] { "connectionId" }, p => Assert.Contains(p, connectionStop.PayloadNames));
            Assert.Same(KestrelEventSource.Log, connectionStop.EventSource);
            Assert.Equal(connectionStart.ActivityId, connectionStop.ActivityId);
            Assert.Equal(Guid.Empty, connectionStop.RelatedActivityId);
        }

        [Fact]
        public async Task EmitsHttp2StartAndStopEventsWithActivityIds()
        {
            int port;
            string connectionId = null;

            const int requestsToSend = 2;
            var requestIds = new string[requestsToSend];
            var requestsReceived = 0;

            await using (var server = new TestServer(context =>
            {
                connectionId = context.Features.Get<IHttpConnectionFeature>().ConnectionId;
                requestIds[requestsReceived++] = context.TraceIdentifier;
                return Task.CompletedTask;
            },
            new TestServiceContext(LoggerFactory),
            listenOptions =>
            {
                listenOptions.UseHttps(_x509Certificate2);
                listenOptions.Protocols = HttpProtocols.Http2;
            }))
            {
                port = server.Port;

                using var connection = server.CreateConnection();

                using var socketsHandler = new SocketsHttpHandler()
                {
                    ConnectCallback = (_, _) =>
                    {
                        // This test should only require a single connection.
                        if (connectionId != null)
                        {
                            throw new InvalidOperationException();
                        }

                        return new ValueTask<Stream>(connection.Stream);
                    },
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = (_, _, _, _) => true
                    }
                };

                using var httpClient = new HttpClient(socketsHandler);

                for (int i = 0; i < requestsToSend; i++)
                {
                    using var httpRequestMessage = new HttpRequestMessage()
                    {
                        RequestUri = new Uri("https://localhost/"),
                        Version = new Version(2, 0),
                        VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                    };

                    using var responseMessage = await httpClient.SendAsync(httpRequestMessage);
                    responseMessage.EnsureSuccessStatusCode();
                }
            }

            Assert.NotNull(connectionId);
            Assert.Equal(2, requestsReceived);

            // Other tests executing in parallel may log events.
            var events = _listener.EventData.Where(e => e != null && GetProperty(e, "connectionId") == connectionId).ToList();

            var connectionQueuedStart = Assert.Single(events, e => e.EventName == "ConnectionQueuedStart");
            Assert.All(new[] { "connectionId", "remoteEndPoint", "localEndPoint" }, p => Assert.Contains(p, connectionQueuedStart.PayloadNames));
            Assert.Equal($"127.0.0.1:{port}", GetProperty(connectionQueuedStart, "localEndPoint"));
            Assert.NotEqual(Guid.Empty, connectionQueuedStart.ActivityId);
            Assert.Equal(Guid.Empty, connectionQueuedStart.RelatedActivityId);

            var connectionQueuedStop = Assert.Single(events, e => e.EventName == "ConnectionQueuedStop");
            Assert.All(new[] { "connectionId", "remoteEndPoint", "localEndPoint" }, p => Assert.Contains(p, connectionQueuedStop.PayloadNames));
            Assert.Equal($"127.0.0.1:{port}", GetProperty(connectionQueuedStop, "localEndPoint"));
            Assert.Equal(connectionQueuedStart.ActivityId, connectionQueuedStop.ActivityId);
            Assert.Equal(Guid.Empty, connectionQueuedStop.RelatedActivityId);

            var connectionStart = Assert.Single(events, e => e.EventName == "ConnectionStart");
            Assert.All(new[] { "connectionId", "remoteEndPoint", "localEndPoint" }, p => Assert.Contains(p, connectionStart.PayloadNames));
            Assert.Equal($"127.0.0.1:{port}", GetProperty(connectionStart, "localEndPoint"));
            Assert.NotEqual(Guid.Empty, connectionStart.ActivityId);
            Assert.Equal(Guid.Empty, connectionStart.RelatedActivityId);

            var tlsHandshakeStart = Assert.Single(events, e => e.EventName == "TlsHandshakeStart");
            Assert.All(new[] { "connectionId" , "sslProtocols" }, p => Assert.Contains(p, tlsHandshakeStart.PayloadNames));
            Assert.Same(KestrelEventSource.Log, tlsHandshakeStart.EventSource);
            Assert.NotEqual(Guid.Empty, tlsHandshakeStart.ActivityId);
            Assert.Equal(connectionStart.ActivityId, tlsHandshakeStart.RelatedActivityId);

            var tlsHandshakeStop = Assert.Single(events, e => e.EventName == "TlsHandshakeStop");
            Assert.All(new[] { "connectionId", "sslProtocols", "applicationProtocol", "hostName" }, p => Assert.Contains(p, tlsHandshakeStop.PayloadNames));
            Assert.Equal("h2", GetProperty(tlsHandshakeStop, "applicationProtocol"));
            Assert.Same(KestrelEventSource.Log, tlsHandshakeStop.EventSource);
            Assert.Equal(tlsHandshakeStart.ActivityId, tlsHandshakeStop.ActivityId);
            Assert.Equal(Guid.Empty, tlsHandshakeStop.RelatedActivityId);

            for (int i = 0; i < requestsToSend; i++)
            {
                var requestStart = Assert.Single(events, e => e.EventName == "RequestStart" && GetProperty(e, "requestId") == requestIds[i]);
                Assert.All(new[] { "connectionId", "requestId" }, p => Assert.Contains(p, requestStart.PayloadNames));
                Assert.Same(KestrelEventSource.Log, requestStart.EventSource);
                Assert.NotEqual(Guid.Empty, requestStart.ActivityId);
                Assert.Equal(connectionStart.ActivityId, requestStart.RelatedActivityId);

                var requestStop = Assert.Single(events, e => e.EventName == "RequestStop" && GetProperty(e, "requestId") == requestIds[i]);
                Assert.All(new[] { "connectionId", "requestId" }, p => Assert.Contains(p, requestStop.PayloadNames));
                Assert.Same(KestrelEventSource.Log, requestStop.EventSource);
                Assert.Equal(requestStart.ActivityId, requestStop.ActivityId);
                Assert.Equal(Guid.Empty, requestStop.RelatedActivityId);
            }

            var connectionStop = Assert.Single(events, e => e.EventName == "ConnectionStop");
            Assert.All(new[] { "connectionId" }, p => Assert.Contains(p, connectionStop.PayloadNames));
            Assert.Same(KestrelEventSource.Log, connectionStop.EventSource);
            Assert.Equal(connectionStart.ActivityId, connectionStop.ActivityId);
            Assert.Equal(Guid.Empty, connectionStop.RelatedActivityId);
        }

        private string GetProperty(EventWrittenEventArgs data, string propName)
        {
            var index = data.PayloadNames.IndexOf(propName);
            return index >= 0 ? data.Payload[index] as string : null;
        }

        private class TestEventListener : EventListener
        {
            private readonly ConcurrentQueue<EventWrittenEventArgs> _events = new ConcurrentQueue<EventWrittenEventArgs>();
            private readonly ILogger _logger;
            private volatile bool _disposed;

            public TestEventListener(ILogger logger)
            {
                _logger = logger;
            }

            public IEnumerable<EventWrittenEventArgs> EventData => _events;

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "System.Threading.Tasks.TplEventSource")
                {
                    // Enable TasksFlowActivityIds
                    EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)0x80);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (!_disposed)
                {
                    _logger.LogInformation("{event}", JsonSerializer.Serialize(eventData, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));

                    _events.Enqueue(eventData);
                }
            }

            public override void Dispose()
            {
                _disposed = true;
                base.Dispose();
            }
        }

        public override void Dispose()
        {
            _listener.Dispose();
            base.Dispose();
        }
    }
}
