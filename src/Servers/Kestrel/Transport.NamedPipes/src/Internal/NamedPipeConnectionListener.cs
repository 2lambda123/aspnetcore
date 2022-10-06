// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using PipeOptions = System.IO.Pipelines.PipeOptions;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Internal;

internal sealed class NamedPipeConnectionListener : IConnectionListener
{
    private readonly ILogger _log;
    private readonly NamedPipeEndPoint _endpoint;
    private readonly NamedPipeTransportOptions _options;
    private readonly CancellationTokenSource _listeningTokenSource = new CancellationTokenSource();
    private readonly CancellationToken _listeningToken;
    private readonly Channel<ConnectionContext> _acceptedQueue;
    private readonly Task _listeningTask;
    private readonly MemoryPool<byte> _memoryPool;
    private readonly PipeOptions _inputOptions;
    private readonly PipeOptions _outputOptions;
    private int _disposed;

    public NamedPipeConnectionListener(
        NamedPipeEndPoint endpoint,
        NamedPipeTransportOptions options,
        ILoggerFactory loggerFactory)
    {
        _log = loggerFactory.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes");
        _endpoint = endpoint;
        _options = options;
        _acceptedQueue = Channel.CreateBounded<ConnectionContext>(new BoundedChannelOptions(options.Backlog));
        _memoryPool = options.MemoryPoolFactory();
        _listeningToken = _listeningTokenSource.Token;

        var maxReadBufferSize = _options.MaxReadBufferSize ?? 0;
        var maxWriteBufferSize = _options.MaxWriteBufferSize ?? 0;

        _inputOptions = new PipeOptions(_memoryPool, PipeScheduler.ThreadPool, PipeScheduler.Inline, maxReadBufferSize, maxReadBufferSize / 2, useSynchronizationContext: false);
        _outputOptions = new PipeOptions(_memoryPool, PipeScheduler.Inline, PipeScheduler.ThreadPool, maxWriteBufferSize, maxWriteBufferSize / 2, useSynchronizationContext: false);

        // Start after all fields are initialized.
        _listeningTask = StartAsync();
    }

    public EndPoint EndPoint => _endpoint;

    private async Task StartAsync()
    {
        try
        {
            while (true)
            {
                NamedPipeServerStream stream;

                try
                {
                    _listeningToken.ThrowIfCancellationRequested();

                    stream = NamedPipeServerStreamAcl.Create(
                        _endpoint.PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        _endpoint.PipeOptions,
                        inBufferSize: 0, // Buffer in System.IO.Pipelines
                        outBufferSize: 0, // Buffer in System.IO.Pipelines
                        _options.PipeSecurity);

                    await stream.WaitForConnectionAsync(_listeningToken);
                }
                catch (OperationCanceledException ex) when (_listeningToken.IsCancellationRequested)
                {
                    // Cancelled the current token
                    NamedPipeLog.ConnectionListenerAborted(_log, ex);
                    break;
                }

                var connection = new NamedPipeConnection(stream, _endpoint, _log, _memoryPool, _inputOptions, _outputOptions);
                connection.Start();

                _acceptedQueue.Writer.TryWrite(connection);
            }

            _acceptedQueue.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            _acceptedQueue.Writer.TryComplete(ex);
        }
    }

    public async ValueTask<ConnectionContext?> AcceptAsync(CancellationToken cancellationToken = default)
    {
        while (await _acceptedQueue.Reader.WaitToReadAsync(cancellationToken))
        {
            if (_acceptedQueue.Reader.TryRead(out var connection))
            {
                NamedPipeLog.AcceptedConnection(_log, connection);
                return connection;
            }
        }
        
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        // A stream may be waiting on WaitForConnectionAsync when dispose happens.
        // Cancel the token before dispose to ensure StartAsync exits.
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _listeningTokenSource.Cancel();
        }
        
        _listeningTokenSource.Dispose();
        await _listeningTask;
    }

    public async ValueTask UnbindAsync(CancellationToken cancellationToken = default)
    {
        _listeningTokenSource.Cancel();
        await _listeningTask;
    }
}
