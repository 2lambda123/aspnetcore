// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using System.IO.Pipelines;

namespace System.Net.Connections
{
    public interface IConnectionStream : IAsyncDisposable
    {
        IConnectionProperties ConnectionProperties { get; }

        // If only one is implemented, the other should wrap. To prevent usage errors, whichever is retrieved first, the other one should throw.
        Stream Stream { get; }
        IDuplexPipe Pipe { get; }
    }
}
