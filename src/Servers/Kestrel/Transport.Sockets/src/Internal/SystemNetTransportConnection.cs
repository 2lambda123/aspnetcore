// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Connections
{
    internal abstract partial class SystemNetTransportConnection : Connection, IConnectionProperties
    {
        private IDictionary<object, object> _items;
        private string _connectionId;

        private readonly EndPoint _localEndPoint;
        private readonly EndPoint _remoteEndPoint;

        public SystemNetTransportConnection(EndPoint localEndPoint, EndPoint remoteEndPoint)
        {
            _localEndPoint = localEndPoint;
            _remoteEndPoint = remoteEndPoint;
            FastReset();
        }

        public override EndPoint LocalEndPoint => _localEndPoint;
        public override EndPoint RemoteEndPoint => _remoteEndPoint;

        public string ConnectionId
        {
            get
            {
                if (_connectionId == null)
                {
                    _connectionId = CorrelationIdGenerator.GetNextId();
                }

                return _connectionId;
            }
            set
            {
                _connectionId = value;
            }
        }

        public override IConnectionProperties ConnectionProperties => this;
        public IFeatureCollection Features => this;

        public virtual MemoryPool<byte> MemoryPool { get; }

        public IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        public IDictionary<object, object> Items
        {
            get
            {
                // Lazily allocate connection metadata
                return _items ?? (_items = new ConnectionItems());
            }
            set
            {
                _items = value;
            }
        }

        public CancellationToken ConnectionClosed { get; set; }

        public abstract void Abort(ConnectionAbortedException abortReason);

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            property = ((IFeatureCollection)this)[propertyKey];
            return property != null;
        }
    }
}
