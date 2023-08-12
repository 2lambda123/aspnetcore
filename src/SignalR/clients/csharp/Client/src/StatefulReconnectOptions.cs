// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Http.Connections.Client;

namespace Microsoft.AspNetCore.SignalR.Client;

/// <summary>
/// Options to configure stateful reconnect in both <see cref="HttpConnection"/> and <see cref="HubConnection"/>.
/// </summary>
public sealed class StatefulReconnectOptions
{
    /// <summary>
    /// Gets or sets the maximum bytes to buffer on the client when using stateful reconnect.
    /// </summary>
    /// <remarks>Defaults to <c>100,000</c> bytes.</remarks>
    public long StatefulReconnectBufferSize { get; set; } = 100_000;
}
