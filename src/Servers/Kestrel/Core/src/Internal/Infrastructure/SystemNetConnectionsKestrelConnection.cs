// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure
{
    class SystemNetConnectionsConnectionContext : ConnectionContext
    {
        private readonly System.Net.Connections.IConnection _connection;
        private IDuplexPipe _replacementTransport;

        public SystemNetConnectionsConnectionContext(System.Net.Connections.IConnection connection)
        {
            _connection = connection;
            Features = new System.Net.Connections.Helpers.ConnectionPropertiesFeatureCollection(_connection.ConnectionProperties);
            ConnectionClosed = System.Net.Connections.ConnectionExtensions.GetRequiredProperty<IConnectionLifetimeFeature>(_connection.ConnectionProperties).ConnectionClosed;
        }

        public override IDuplexPipe Transport
        {
            get => _replacementTransport ?? _connection.Pipe;
            set => _replacementTransport = value;
        }

        public override string ConnectionId
        {
            get
            {
                return System.Net.Connections.ConnectionExtensions.GetRequiredProperty<IConnectionIdFeature>(_connection.ConnectionProperties).ConnectionId;
            }
            set
            {
                System.Net.Connections.ConnectionExtensions.GetRequiredProperty<IConnectionIdFeature>(_connection.ConnectionProperties).ConnectionId = value;
            }
        }

        public override IFeatureCollection Features { get; }
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

        public override ValueTask DisposeAsync()
        {
            return _connection.DisposeAsync();
        }
    }
}
