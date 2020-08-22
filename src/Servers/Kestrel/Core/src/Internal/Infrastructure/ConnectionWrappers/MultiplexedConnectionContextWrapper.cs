// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections.Experimental;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers
{
    internal class MultiplexedConnectionContextWrapper : ConnectionBase, IConnectionProperties
    {
        public MultiplexedConnectionContextWrapper(MultiplexedConnectionContext multiplexedConnectionContext)
        {
            MultiplexedConnectionContext = multiplexedConnectionContext;
        }

        public MultiplexedConnectionContext MultiplexedConnectionContext { get; }

        public override IConnectionProperties ConnectionProperties => this;

        public override EndPoint? LocalEndPoint => MultiplexedConnectionContext.LocalEndPoint;

        public override EndPoint? RemoteEndPoint => MultiplexedConnectionContext.RemoteEndPoint;

        protected override ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
        {
            return ConnectionWrapperUtils.CloseAsyncCore(MultiplexedConnectionContext, method, cancellationToken);
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            return ConnectionWrapperUtils.TryGetProperty(MultiplexedConnectionContext.Features, propertyKey, out property);
        }
    }
}

