// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers
{
    internal class ConnectionWrapper : ConnectionContext,
                                       IFeatureCollection,
                                       IConnectionIdFeature,
                                       IItemsFeature,
                                       IConnectionLifetimeFeature,
                                       IAbortWithReasonFeature
    {
        private readonly Connection _connection;

        private bool _wasModified;
        private IDuplexPipe? _transport;
        private FeatureCollection? _extraFeatures;

        private string? _connectionId;
        private IDictionary<object, object?>? _connectionItems;

        public ConnectionWrapper(Connection connection)
        {
            _connection = connection;
        }

        public Connection ModifiedConnection => _wasModified ? new ConnectionContextWrapper(this) : _connection;

        public override IFeatureCollection Features => this;

        public override IDuplexPipe Transport
        {
            get => _transport ?? _connection.Pipe;
            set
            {
                _wasModified = true;
                _transport = value;
            }
        }

        public override string ConnectionId
        {
            get
            {
                if (_connection.ConnectionProperties.TryGet<IConnectionIdFeature>(out var connectionIdFeature))
                {
                    return connectionIdFeature.ConnectionId;
                }

                _wasModified = true;
                _connectionId ??= CorrelationIdGenerator.GetNextId();

                return _connectionId;
            }
            set
            {
                if (_connection.ConnectionProperties.TryGet<IConnectionIdFeature>(out var connectionIdFeature))
                {
                    connectionIdFeature.ConnectionId = value;
                }
                else
                {
                    _wasModified = true;
                    _connectionId = value;
                }
            }
        }

        public override EndPoint? LocalEndPoint
        {
            get => base.LocalEndPoint ?? _connection.LocalEndPoint;
            set
            {
                _wasModified = true;
                base.LocalEndPoint = value;
            }
        }

        public override EndPoint? RemoteEndPoint
        {
            get => base.RemoteEndPoint ?? _connection.RemoteEndPoint;
            set
            {
                _wasModified = true;
                base.RemoteEndPoint = value;
            }
        }

        public override IDictionary<object, object?> Items
        {
            get
            {
                if (_connection.ConnectionProperties.TryGet<IItemsFeature>(out var itemsFeature))
                {
                    return itemsFeature.Items;
                }

                _wasModified = true;
                _connectionItems ??= new ConnectionItems();

                return _connectionItems;
            }
            set
            {
                if (_connection.ConnectionProperties.TryGet<IItemsFeature>(out var itemsFeature))
                {
                    itemsFeature.Items = value;
                }
                else
                {
                    _wasModified = true;
                    _connectionItems = value;
                }
            }
        }

        public override CancellationToken ConnectionClosed
        {
            get
            {
                if (_connection.ConnectionProperties.TryGet<IConnectionLifetimeFeature>(out var lifetimeFeature))
                {
                    return lifetimeFeature.ConnectionClosed;
                }

                // It's going to require more work involving wrapping transport reads and writes to observe
                // the connection closing if there's no IConnectionLifetimeFeature in ConnectionPropertes.
                // Nothing in Kestrel null-checks ConnectionClosed, so immediately throwing better highlights bugs.
                return (CancellationToken?)base.ConnectionClosed ?? throw new NotImplementedException();
            }
            set
            {
                if (_connection.ConnectionProperties.TryGet<IConnectionLifetimeFeature>(out var lifetimeFeature))
                {
                    lifetimeFeature.ConnectionClosed = value;
                }
                else
                {
                    _wasModified = true;
                    base.ConnectionClosed = value;
                }
            }
        }

        public override void Abort(ConnectionAbortedException? abortReason)
        {
            if (_connection.ConnectionProperties.TryGet<IAbortWithReasonFeature>(out var abortFeature))
            {
                abortFeature.Abort(abortReason);
            }
            else if (_connection.ConnectionProperties.TryGet<IConnectionLifetimeFeature>(out var lifetimeFeature))
            {
                lifetimeFeature.Abort();
            }
            else
            {
                // CloseAsync is terminal and will make the Pipe unusable unlike "real" ConnectionContext.Abort implementations,
                // so we only call it as a last resort.
                _connection.CloseAsync(ConnectionCloseMethod.Abort).GetAwaiter().GetResult();
            }
        }

        public override ValueTask DisposeAsync()
        {
            return _connection.DisposeAsync();
        }

        bool IFeatureCollection.IsReadOnly => false;

        int IFeatureCollection.Revision => _extraFeatures?.Revision ?? 0;

        object? IFeatureCollection.this[Type key]
        {
            get => GetFeature(key);
            set => SetFeature(key, value);
        }

        [return: MaybeNull]
        TFeature IFeatureCollection.Get<TFeature>() => (TFeature)GetFeature(typeof(TFeature));

        void IFeatureCollection.Set<TFeature>(TFeature instance) => SetFeature(typeof(TFeature), instance);

        // We cannot enumerate the _connection's properties, so we throw since Kestrel doesn't enumerate features anyway.
        // Maybe this could be addressed by putting the IFeatureCollection inside itself.
        // That recursive reference could also help avoid multiple layers of _extraFeatures.
        IEnumerator<KeyValuePair<Type, object>> IEnumerable<KeyValuePair<Type, object>>.GetEnumerator()
            => throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();

        private object? GetFeature(Type key)
        {
            var extraFeature = _extraFeatures?[key];

            if (extraFeature != null)
            {
                return extraFeature;
            }

            if (_connection.ConnectionProperties.TryGet(key, out var property))
            {
                return property;
            }

            if (key == typeof(IConnectionIdFeature) ||
                key == typeof(IItemsFeature) ||
                key == typeof(IConnectionLifetimeFeature) ||
                key == typeof(IAbortWithReasonFeature))
            {
                return this;
            }

            return null;
        }

        private void SetFeature(Type key, object? feature)
        {
            _wasModified = true;
            _extraFeatures ??= new FeatureCollection();
            _extraFeatures[key] = feature;
        }
    }
}
