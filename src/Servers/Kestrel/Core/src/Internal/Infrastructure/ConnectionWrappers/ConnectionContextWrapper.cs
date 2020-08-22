// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers
{
    internal class ConnectionContextWrapper : Connection, IConnectionProperties
    {
        private readonly ConnectionContext _connectionContext;

        public ConnectionContextWrapper(ConnectionContext connectionContext)
        {
            _connectionContext = connectionContext;
        }

        protected override IDuplexPipe CreatePipe()
        {
            return _connectionContext.Transport;
        }

        public override IConnectionProperties ConnectionProperties => this;

        public override EndPoint? LocalEndPoint => _connectionContext.LocalEndPoint;

        public override EndPoint? RemoteEndPoint => _connectionContext.RemoteEndPoint;

        protected override ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
        {
            return ConnectionWrapperUtils.CloseAsyncCore(_connectionContext, method, cancellationToken);
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            return ConnectionWrapperUtils.TryGetProperty(_connectionContext.Features, propertyKey, out property);
        }
    }
}
