// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Net;
using System.Security.Principal;
using System.Text;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Internal;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Tests;

internal static class NamedPipeTestHelpers
{
    private static readonly byte[] TestData = Encoding.UTF8.GetBytes("Hello world");

    public static string GetUniquePipeName() => "Kestrel-" + Path.GetRandomFileName();

    public static NamedPipeTransportFactory CreateTransportFactory(
        ILoggerFactory loggerFactory = null)
    {
        var options = new NamedPipeTransportOptions();
        return new NamedPipeTransportFactory(loggerFactory ?? NullLoggerFactory.Instance, Options.Create(options));
    }

    public static async Task<NamedPipeConnectionListener> CreateConnectionListenerFactory(
        ILoggerFactory loggerFactory = null,
        string pipeName = null)
    {
        var transportFactory = CreateTransportFactory(loggerFactory);

        var endpoint = new NamedPipeEndPoint(pipeName ?? GetUniquePipeName());

        return (NamedPipeConnectionListener)await transportFactory.BindAsync(endpoint, cancellationToken: CancellationToken.None);
    }

    public static NamedPipeClientStream CreateClientStream(EndPoint remoteEndPoint, TokenImpersonationLevel? impersonationLevel = null)
    {
        var namedPipeEndPoint = (NamedPipeEndPoint)remoteEndPoint;
        var clientStream = new NamedPipeClientStream(
            serverName: namedPipeEndPoint.ServerName,
            pipeName: namedPipeEndPoint.PipeName,
            direction: PipeDirection.InOut,
            options: PipeOptions.WriteThrough | PipeOptions.Asynchronous,
            impersonationLevel: impersonationLevel ?? TokenImpersonationLevel.Anonymous);
        return clientStream;
    }

    public static async Task<NamedPipeConnection> CreateAndCompleteBidirectionalStreamGracefully(NamedPipeClientStream clientConnection, NamedPipeConnectionListener connectionListener, ILogger logger)
    {
        logger.LogInformation("Client connecting.");
        await clientConnection.ConnectAsync().DefaultTimeout();

        logger.LogInformation("Server accepting stream.");
        var serverConnectionTask = connectionListener.AcceptAsync();

        logger.LogInformation("Client sending data.");
        var writeTask = clientConnection.WriteAsync(TestData);

        var serverConnection = await serverConnectionTask.DefaultTimeout();
        await writeTask.DefaultTimeout();

        logger.LogInformation("Server reading data.");
        var readResult = await serverConnection.Transport.Input.ReadAtLeastAsync(TestData.Length).DefaultTimeout();
        serverConnection.Transport.Input.AdvanceTo(readResult.Buffer.End);

        clientConnection.Close();

        // Input should be completed.
        readResult = await serverConnection.Transport.Input.ReadAsync();
        Assert.True(readResult.IsCompleted);

        // Complete reading and writing.
        logger.LogInformation("Server completing input and output.");
        await serverConnection.Transport.Input.CompleteAsync();
        await serverConnection.Transport.Output.CompleteAsync();

        logger.LogInformation("Server disposing connection.");
        await serverConnection.DisposeAsync();

        return Assert.IsType<NamedPipeConnection>(serverConnection);
    }
}
