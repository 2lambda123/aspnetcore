// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Globalization;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.InMemory.FunctionalTests.TestTransport;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Interop.FunctionalTests.TestServer;

internal class InteropTestServer : IAsyncDisposable
{
    private readonly InteropTestServerContext _context;
    private readonly IHost _host;

    private readonly MemoryPool<byte> _memoryPool;
    private readonly InMemoryTransportFactory _inMemoryTransportFactory;

    public InteropTestServer(InteropTestServerContext context)
    {
        if (context.TransportType is TransportType.InMemory)
        {
            _memoryPool = PinnedBlockMemoryPoolFactory.Create();
            _inMemoryTransportFactory = new();
        }

        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder
                    .UseSetting(WebHostDefaults.ShutdownTimeoutKey, TestConstants.DefaultTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture))
                    .Configure(context.ConfigureApp);
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton(context.LoggerFactory);

                if (context.TransportType is TransportType.InMemory)
                {
                    services.AddSingleton<IConnectionListenerFactory>(_inMemoryTransportFactory);
                }
            });

        _context = context;
        _host = hostBuilder.Build();
        _host.Start();
    }

    public InMemoryTransportConnection CreateInMemoryConnection()
    {
        var transportConnection = new InMemoryTransportConnection(_memoryPool, _context.LoggerFactory.CreateLogger<InMemoryTransportConnection>());
        _inMemoryTransportFactory.AddConnection(transportConnection);
        return transportConnection;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _host.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.StopAsync().ConfigureAwait(false);
        // The concrete Host implements IAsyncDisposable
        await ((IAsyncDisposable)_host).DisposeAsync().ConfigureAwait(false);
        _memoryPool?.Dispose();
    }
}
