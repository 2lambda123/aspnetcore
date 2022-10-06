// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Internal;
using Microsoft.AspNetCore.Testing;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Tests;

public class NamedPipeConnectionListenerTests : TestApplicationErrorLoggerLoggedTest
{
    [Fact]
    public async Task AcceptAsync_AfterUnbind_ReturnNull()
    {
        // Arrange
        await using var connectionListener = await NamedPipeTestHelpers.CreateConnectionListenerFactory(LoggerFactory);

        // Act
        await connectionListener.UnbindAsync().DefaultTimeout();

        // Assert
        Assert.Null(await connectionListener.AcceptAsync().DefaultTimeout());
    }

    [Fact]
    public async Task AcceptAsync_ClientCreatesConnection_ServerAccepts()
    {
        // Arrange
        await using var connectionListener = await NamedPipeTestHelpers.CreateConnectionListenerFactory(LoggerFactory);

        // Act
        var acceptTask = connectionListener.AcceptAsync();

        await using var clientStream = NamedPipeTestHelpers.CreateClientStream(connectionListener.EndPoint);
        await clientStream.ConnectAsync();

        // Assert
        var serverConnection = await acceptTask.DefaultTimeout();
        Assert.False(serverConnection.ConnectionClosed.IsCancellationRequested);

        await serverConnection.DisposeAsync().AsTask().DefaultTimeout();

        Assert.True(serverConnection.ConnectionClosed.IsCancellationRequested);
    }

    [Fact]
    public async Task AcceptAsync_UnbindAfterCall_CleanExitAndLog()
    {
        // Arrange
        await using var connectionListener = await NamedPipeTestHelpers.CreateConnectionListenerFactory(LoggerFactory);

        // Act
        var acceptTask = connectionListener.AcceptAsync();

        await connectionListener.UnbindAsync().DefaultTimeout();

        // Assert
        Assert.Null(await acceptTask.AsTask().DefaultTimeout());

        Assert.Contains(LogMessages, m => m.EventId.Name == "ConnectionListenerAborted");
    }

    [Fact]
    public async Task AcceptAsync_DisposeAfterCall_CleanExitAndLog()
    {
        // Arrange
        await using var connectionListener = await NamedPipeTestHelpers.CreateConnectionListenerFactory(LoggerFactory);

        // Act
        var acceptTask = connectionListener.AcceptAsync();

        await connectionListener.DisposeAsync().DefaultTimeout();

        // Assert
        Assert.Null(await acceptTask.AsTask().DefaultTimeout());

        Assert.Contains(LogMessages, m => m.EventId.Name == "ConnectionListenerAborted");
    }

    [Fact(Skip = "No warning when dupliate pipe name used with server?")]
    public async Task BindAsync_ListenersSharePort_ThrowAddressInUse()
    {
        // Arrange
        await using var connectionListener = await NamedPipeTestHelpers.CreateConnectionListenerFactory(LoggerFactory);

        // Act & Assert
        var pipeName = ((NamedPipeEndPoint)connectionListener.EndPoint).PipeName;

        await Assert.ThrowsAsync<Exception>(() => NamedPipeTestHelpers.CreateConnectionListenerFactory(LoggerFactory, pipeName: pipeName));
    }
}
