// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.IO.Pipes;
using System.Net;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using NamedPipeOptions = System.IO.Pipes.PipeOptions;
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
    private readonly MemoryPool<byte> _memoryPool;
    private readonly PipeOptions _inputOptions;
    private readonly PipeOptions _outputOptions;
    private readonly Mutex _mutex;
    private Task? _listeningTask;
    private int _disposed;

    public NamedPipeConnectionListener(
        NamedPipeEndPoint endpoint,
        NamedPipeTransportOptions options,
        ILoggerFactory loggerFactory,
        Mutex mutex)
    {
        _log = loggerFactory.CreateLogger("Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes");
        _endpoint = endpoint;
        _options = options;
        _mutex = mutex;
        _acceptedQueue = Channel.CreateBounded<ConnectionContext>(new BoundedChannelOptions(options.Backlog) { SingleWriter = true });
        _memoryPool = options.MemoryPoolFactory();
        _listeningToken = _listeningTokenSource.Token;

        var maxReadBufferSize = _options.MaxReadBufferSize ?? 0;
        var maxWriteBufferSize = _options.MaxWriteBufferSize ?? 0;

        _inputOptions = new PipeOptions(_memoryPool, PipeScheduler.ThreadPool, PipeScheduler.Inline, maxReadBufferSize, maxReadBufferSize / 2, useSynchronizationContext: false);
        _outputOptions = new PipeOptions(_memoryPool, PipeScheduler.Inline, PipeScheduler.ThreadPool, maxWriteBufferSize, maxWriteBufferSize / 2, useSynchronizationContext: false);
    }

    public void Start()
    {
        Debug.Assert(_listeningTask == null, "Already started");

        // Start first stream inline to catch creation errors.
        var initialStream = CreateServerStream();
        
        _listeningTask = StartAsync(initialStream);
    }

    public EndPoint EndPoint => _endpoint;

    private async Task StartAsync(NamedPipeServerStream nextStream)
    {
        try
        {
            while (true)
            {
                try
                {
                    var stream = nextStream;

                    await stream.WaitForConnectionAsync(_listeningToken);

                    var connection = new NamedPipeConnection(stream, _endpoint, _log, _memoryPool, _inputOptions, _outputOptions);
                    connection.Start();

                    // Create the next stream before writing connected stream to the channel.
                    // This ensures there is always a created stream and another process can't
                    // create a stream with the same name with different a access policy.
                    nextStream = CreateServerStream();

                    while (!_acceptedQueue.Writer.TryWrite(connection))
                    {
                        if (!await _acceptedQueue.Writer.WaitToWriteAsync(_listeningToken))
                        {
                            throw new InvalidOperationException("Accept queue writer was unexpectedly closed.");
                        }
                    }
                }
                catch (OperationCanceledException ex) when (_listeningToken.IsCancellationRequested)
                {
                    // Cancelled the current token
                    NamedPipeLog.ConnectionListenerAborted(_log, ex);
                    break;
                }
            }

            nextStream.Dispose();
            _acceptedQueue.Writer.TryComplete();
        }
        catch (Exception ex)
        {
            _acceptedQueue.Writer.TryComplete(ex);
        }
    }

    private NamedPipeServerStream CreateServerStream()
    {
        NamedPipeServerStream stream;
        var pipeOptions = NamedPipeOptions.Asynchronous | NamedPipeOptions.WriteThrough;
        if (_options.CurrentUserOnly)
        {
            pipeOptions |= NamedPipeOptions.CurrentUserOnly;
        }

        if (_options.PipeSecurity != null)
        {
            stream = NamedPipeServerStreamAcl.Create(
                _endpoint.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                pipeOptions,
                inBufferSize: 0, // Buffer in System.IO.Pipelines
                outBufferSize: 0, // Buffer in System.IO.Pipelines
                _options.PipeSecurity);
        }
        else
        {
            stream = new NamedPipeServerStream(
                _endpoint.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                pipeOptions,
                inBufferSize: 0,
                outBufferSize: 0);
        }
        return stream;
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

    public ValueTask UnbindAsync(CancellationToken cancellationToken = default) => DisposeAsync();

    public async ValueTask DisposeAsync()
    {
        // A stream may be waiting on WaitForConnectionAsync when dispose happens.
        // Cancel the token before dispose to ensure StartAsync exits.
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _listeningTokenSource.Cancel();
        }
        
        _listeningTokenSource.Dispose();
        _mutex.Dispose();
        if (_listeningTask != null)
        {
            await _listeningTask;
        }
    }
}
