// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers
{
    internal class ConnectionContextWrapper : Connection,
                                              IConnectionProperties,
                                              IConnectionIdFeature,
                                              IConnectionItemsFeature,
                                              IConnectionLifetimeFeature,
                                              IAbortWithReasonFeature
    {
        private readonly ConnectionContext _connectionContext;

        public ConnectionContextWrapper(ConnectionContext connectionContext)
        {
            _connectionContext = connectionContext;
        }

        protected override IDuplexPipe CreatePipe() => _connectionContext.Transport;

        public override IConnectionProperties ConnectionProperties => this;

        public override EndPoint? LocalEndPoint => _connectionContext.LocalEndPoint;

        public override EndPoint? RemoteEndPoint => _connectionContext.RemoteEndPoint;

        protected override ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
        {
            return ConnectionWrapperUtils.CloseAsyncCore(_connectionContext, method, cancellationToken);
        }

        bool IConnectionProperties.TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            if (ConnectionWrapperUtils.TryGetProperty(_connectionContext.Features, propertyKey, out property))
            {
                return true;
            }

            if (propertyKey == typeof(IConnectionIdFeature) ||
                propertyKey == typeof(IConnectionItemsFeature) ||
                propertyKey == typeof(IConnectionLifetimeFeature) ||
                propertyKey == typeof(IAbortWithReasonFeature))
            {
                property = this;
                return true;
            }

            return false;
        }

        string IConnectionIdFeature.ConnectionId
        {
            get => _connectionContext.ConnectionId;
            set => _connectionContext.ConnectionId = value;
        }

        IDictionary<object, object?> IConnectionItemsFeature.Items
        {
            get => _connectionContext.Items;
            set => _connectionContext.Items = value;
        }

        CancellationToken IConnectionLifetimeFeature.ConnectionClosed
        {
            get => _connectionContext.ConnectionClosed;
            set => _connectionContext.ConnectionClosed = value;
        }

        void IConnectionLifetimeFeature.Abort()
        {
            _connectionContext.Abort();
        }

        void IAbortWithReasonFeature.Abort(ConnectionAbortedException? abortReason)
        {
            _connectionContext.Abort(abortReason);
        }
    }
}
