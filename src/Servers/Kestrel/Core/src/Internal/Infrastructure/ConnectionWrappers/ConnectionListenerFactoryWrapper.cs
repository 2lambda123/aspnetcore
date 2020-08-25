// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers
{
    public class ConnectionListenerFactoryWrapper : ConnectionListenerFactory, IConnectionProperties
    {
        private IConnectionListenerFactory _listenerFactory;

        public ConnectionListenerFactoryWrapper(IConnectionListenerFactory listenerFactory)
        {
            _listenerFactory = listenerFactory;
        }

        public override async ValueTask<ConnectionListener> ListenAsync(EndPoint? endPoint, IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            return new ConnectionListenerWrapper(await _listenerFactory.BindAsync(endPoint!, cancellationToken));
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object? property)
        {
            property = null;
            return false;
        }
    }
}
