// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections.Features;

namespace Microsoft.AspNetCore.Testing
{
    public class MockConnection : Connection, IConnectionProperties, IConnectionIdFeature, IConnectionLifetimeFeature
    {
        private CancellationTokenSource _connectionClosedCts = new CancellationTokenSource();

        public override IConnectionProperties ConnectionProperties => this;

        public override EndPoint LocalEndPoint => null;

        public override EndPoint RemoteEndPoint => null;

        public string ConnectionId { get; set; } = Guid.NewGuid().ToString();

        public CancellationToken ConnectionClosed { get; set; }

        protected override ValueTask CloseAsyncCore(ConnectionCloseMethod method, CancellationToken cancellationToken)
        {
            ThreadPool.UnsafeQueueUserWorkItem(cts =>
            {
                ((CancellationTokenSource)cts).Cancel();
            }, _connectionClosedCts);

            return default;
        }

        public bool TryGet(Type propertyKey, [NotNullWhen(true)] out object property)
        {
            if (propertyKey == typeof(IConnectionIdFeature))
            {
                property = this;
                return true;
            }

            property = null;
            return false;
        }

        public void Abort()
        {
            CloseAsyncCore(ConnectionCloseMethod.Abort, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
