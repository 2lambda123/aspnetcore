// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests.TestTransport;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests.Http2;

public class Http2EndToEndTests : TestApplicationErrorLoggerLoggedTest
{
    [Fact]
    public async Task MiddlewareIsRunWithConnectionLoggingScopeForHttp2Requests()
    {
        var expectedLogMessage = "Log from connection scope!";
        string connectionIdFromFeature = null;

        var mockScopeLoggerProvider = new MockScopeLoggerProvider(expectedLogMessage);
        LoggerFactory.AddProvider(mockScopeLoggerProvider);

        await using var server = new TestServer(async context =>
        {
            connectionIdFromFeature = context.Features.Get<IConnectionIdFeature>().ConnectionId;

            var logger = context.RequestServices.GetRequiredService<ILogger<Http2EndToEndTests>>();
            logger.LogInformation(expectedLogMessage);

            await context.Response.WriteAsync("hello, world");
        },
        new TestServiceContext(LoggerFactory),
        listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });

        var connectionCount = 0;
        using var connection = server.CreateConnection();

        using var socketsHandler = new SocketsHttpHandler()
        {
            ConnectCallback = (_, _) =>
            {
                if (connectionCount != 0)
                {
                    throw new InvalidOperationException();
                }

                connectionCount++;
                return new ValueTask<Stream>(connection.Stream);
            },
        };

        using var httpClient = new HttpClient(socketsHandler);

        using var httpRequestMessage = new HttpRequestMessage()
        {
            RequestUri = new Uri("http://localhost/"),
            Version = new Version(2, 0),
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        using var responseMessage = await httpClient.SendAsync(httpRequestMessage);

        Assert.Equal("hello, world", await responseMessage.Content.ReadAsStringAsync());

        Assert.NotNull(connectionIdFromFeature);
        Assert.NotNull(mockScopeLoggerProvider.ConnectionLogScope);
        Assert.Equal(connectionIdFromFeature, mockScopeLoggerProvider.ConnectionLogScope[0].Value);
    }

    // Concurrency testing
    [Fact]
    public async Task MultiplexGet()
    {
        var requestsReceived = 0;
        var requestCount = 10;
        var allRequestsReceived = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var server = new TestServer(async context =>
        {
            if (Interlocked.Increment(ref requestsReceived) == requestCount)
            {
                allRequestsReceived.SetResult(0);
            }
            await allRequestsReceived.Task;
            var content = new BulkContent();
            await content.CopyToAsync(context.Response.Body).DefaultTimeout();
        },
        new TestServiceContext(LoggerFactory),
        listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });

        var connectionCount = 0;
        using var connection = server.CreateConnection();

        using var socketsHandler = new SocketsHttpHandler()
        {
            ConnectCallback = (_, _) =>
            {
                if (connectionCount != 0)
                {
                    throw new InvalidOperationException();
                }

                connectionCount++;
                return new ValueTask<Stream>(connection.Stream);
            },
        };

        using var client = new HttpClient(socketsHandler)
        {
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact,
        };

        var url = "http://localhost/";

        var requestTasks = new List<Task>(requestCount);
        for (var i = 0; i < requestCount; i++)
        {
            requestTasks.Add(RunRequest(url));
        }

        async Task RunRequest(string url)
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).DefaultTimeout();

            Assert.Equal(HttpVersion.Version20, response.Version);
            await BulkContent.VerifyContent(await response.Content.ReadAsStreamAsync()).DefaultTimeout();
        };

        await Task.WhenAll(requestTasks);
    }


    private class MockScopeLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly string _expectedLogMessage;
        private IExternalScopeProvider _scopeProvider;

        public MockScopeLoggerProvider(string expectedLogMessage)
        {
            _expectedLogMessage = expectedLogMessage;
        }

        public ConnectionLogScope ConnectionLogScope { get; private set; }

        public ILogger CreateLogger(string categoryName)
        {
            return new MockScopeLogger(this);
        }

        public void SetScopeProvider(IExternalScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        public void Dispose()
        {
        }

        private class MockScopeLogger : ILogger
        {
            private readonly MockScopeLoggerProvider _loggerProvider;

            public MockScopeLogger(MockScopeLoggerProvider parent)
            {
                _loggerProvider = parent;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return _loggerProvider._scopeProvider?.Push(state);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (formatter(state, exception) != _loggerProvider._expectedLogMessage)
                {
                    return;
                }

                _loggerProvider._scopeProvider?.ForEachScope(
                    (scopeObject, loggerPovider) =>
                    {
                        loggerPovider.ConnectionLogScope ??= scopeObject as ConnectionLogScope;
                    },
                    _loggerProvider);
            }
        }
    }

    private class BulkContent : HttpContent
    {
        private static readonly byte[] Content;
        private static readonly int Repetitions = 200;

        static BulkContent()
        {
            Content = new byte[999]; // Intentionally not matching normal memory page sizes to ensure we stress boundaries.
            for (var i = 0; i < Content.Length; i++)
            {
                Content[i] = (byte)i;
            }
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            for (var i = 0; i < Repetitions; i++)
            {
                using (var timer = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    await stream.WriteAsync(Content, 0, Content.Length, timer.Token).DefaultTimeout();
                }
                await Task.Yield(); // Intermix writes
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        public static async Task VerifyContent(Stream stream)
        {
            byte[] buffer = new byte[1024];
            var totalRead = 0;
            var patternOffset = 0;
            int read = 0;
            using (var timer = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                read = await stream.ReadAsync(buffer, 0, buffer.Length, timer.Token).DefaultTimeout();
            }

            while (read > 0)
            {
                totalRead += read;
                Assert.True(totalRead <= Repetitions * Content.Length, "Too Long");

                for (var offset = 0; offset < read; offset++)
                {
                    Assert.Equal(Content[patternOffset % Content.Length], buffer[offset]);
                    patternOffset++;
                }

                using var timer = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                read = await stream.ReadAsync(buffer, 0, buffer.Length, timer.Token).DefaultTimeout();
            }

            Assert.True(totalRead == Repetitions * Content.Length, "Too Short");
        }
    }
}
