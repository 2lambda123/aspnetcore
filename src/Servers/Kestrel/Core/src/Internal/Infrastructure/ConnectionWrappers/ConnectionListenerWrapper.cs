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
using Microsoft.AspNetCore.Connections.Features;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers
{
    internal class ConnectionListenerWrapper : ConnectionListener, IConnectionProperties, IUnbindFeature
    {
        private readonly IConnectionListener _listener;

        public ConnectionListenerWrapper(IConnectionListener listener)
        {
            _listener = listener;
        }

        public override IConnectionProperties ListenerProperties => this;

        public override EndPoint? LocalEndPoint => _listener.EndPoint;

        public override async ValueTask<Connection> AcceptAsync(IConnectionProperties? options = null, CancellationToken cancellationToken = default)
        {
            var connection = await _listener.AcceptAsync(cancellationToken);
            if (connection is null)
            {
                // Hopefully this will be allowed soon: https://github.com/dotnet/runtime/issues/41304
                return null!;
            }

            return new ConnectionContextWrapper(connection);
        }

        public void Unbind()
        {
            _listener.UnbindAsync().GetAwaiter().GetResult();
        }

        protected override ValueTask DisposeAsyncCore() => _listener.DisposeAsync();

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object? property)
        {
            if (propertyKey == typeof(IUnbindFeature))
            {
                property = this;
                return true;
            }

            property = null;
            return false;
        }
    }
}
