// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SignalR.Client;

/// <summary>
/// Configures settings for the <see cref="HubConnection" />.
/// </summary>
public sealed class HubConnectionOptions
{
    /// <summary>
    /// Configures ServerTimeout for the <see cref="HubConnection" />.
    /// </summary>
    internal TimeSpan? ServerTimeout { get; set; }

    /// <summary>
    /// Configures KeepAliveInterval for the <see cref="HubConnection" />.
    /// </summary>
    internal TimeSpan? KeepAliveInterval { get; set; }

    internal const int DefaultMessageBufferSize = 100_000;

    /// <summary>
    /// Gets or sets the maximum bytes to buffer on the client when using stateful reconnect.
    /// </summary>
    public long StatefulReconnectBufferSize { get; set; } = DefaultMessageBufferSize;

}
