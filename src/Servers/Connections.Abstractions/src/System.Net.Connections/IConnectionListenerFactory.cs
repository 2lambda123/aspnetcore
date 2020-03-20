// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Connections
{
    public interface IConnectionListenerFactory : IAsyncDisposable
    {
        ValueTask<IConnectionListener> BindAsync(EndPoint endPoint, IConnectionProperties options, CancellationToken cancellationToken = default);
    }
}
