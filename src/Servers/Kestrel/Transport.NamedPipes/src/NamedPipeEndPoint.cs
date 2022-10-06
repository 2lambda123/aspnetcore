// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Net;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;

/// <summary>
/// Represents a Named Pipe endpoint.
/// </summary>
public sealed class NamedPipeEndPoint : EndPoint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeEndPoint"/> class.
    /// </summary>
    /// <param name="pipeName">The name of the pipe.</param>
    /// <param name="serverName">The name of the remote computer to connect to, or "." to specify the local computer.</param>
    /// <param name="pipeOptions">One of the enumeration values that determines how to open or create the pipe.</param>
    public NamedPipeEndPoint(string pipeName, string serverName = ".", PipeOptions pipeOptions = PipeOptions.Asynchronous)
    {
        ServerName = serverName;
        PipeName = pipeName;
        PipeOptions = pipeOptions;
    }

    /// <summary>
    /// Gets the name of the remote computer to connect to.
    /// </summary>
    public string ServerName { get; }
    /// <summary>
    /// Gets the name of the pipe.
    /// </summary>
    public string PipeName { get; }
    /// <summary>
    /// Gets the pipe options.
    /// </summary>
    public PipeOptions PipeOptions { get; set; }

    /// <summary>
    /// Gets the pipe name represented by this <see cref="NamedPipeEndPoint"/> instance.
    /// </summary>
    public override string ToString()
    {
        return PipeName;
    }
}
