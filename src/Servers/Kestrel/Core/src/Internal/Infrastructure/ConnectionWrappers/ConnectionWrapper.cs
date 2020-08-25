// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Connections;
using System.Threading;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers
{
    internal class ConnectionWrapper : ConnectionContext, IFeatureCollection, IConnectionIdFeature, IConnectionLifetimeFeature, IItemsFeature
    {
        private readonly Connection _connection;

        private IDuplexPipe? _modifiedTransport;
        private FeatureCollection? _extraFeatures;

        public ConnectionWrapper(Connection connection)
        {
            _connection = connection;
        }

        public Connection ModifiedConnection => _modifiedTransport is null && _extraFeatures is null ?
            _connection : new ConnectionContextWrapper(this);

        public override IDuplexPipe Transport
        {
            get => _modifiedTransport ?? _connection.Pipe;
            set => _modifiedTransport = value;
        }

        public override IFeatureCollection Features => this;

        public override string ConnectionId
        {
            get
            {
                var connectionIdFeature = Features.Get<IConnectionIdFeature>();

                if (connectionIdFeature is null)
                {
                    return _connection.ConnectionId();
                }

                return connectionIdFeature.ConnectionId;
            }
            set
            {
                var connectionIdFeature = Features.Get<IConnectionIdFeature>();

                if (connectionIdFeature is null)
                {
                    connectionIdFeature = this;
                    Features.Set<IConnectionIdFeature>(this);
                }

                connectionIdFeature.ConnectionId = value;
            }
        }

        public override CancellationToken ConnectionClosed
        {
            get => GetOrInitializeLifetimeFeature().ConnectionClosed;
            set => GetOrInitializeLifetimeFeature().ConnectionClosed = value;
        }

        private IConnectionLifetimeFeature GetOrInitializeLifetimeFeature()
        {
            var lifetimeFeature = Features.Get<IConnectionLifetimeFeature>();

            if (lifetimeFeature is null)
            {
                lifetimeFeature = this;
                Features.Set<IConnectionLifetimeFeature>(this);
            }

            return lifetimeFeature;
        }

        public override IDictionary<object, object?> Items
        {
            get => GetOrInitializeItemsFeature().Items;
            set => GetOrInitializeItemsFeature().Items = value;
        }

        private IItemsFeature GetOrInitializeItemsFeature()
        {
            var itemsFeature = Features.Get<IItemsFeature>();

            if (itemsFeature is null)
            {
                itemsFeature = this;
                itemsFeature.Items = new Dictionary<object, object?>();
                Features.Set<IItemsFeature>(this);
            }

            return itemsFeature;
        }

        // Silence the compiler's complaints about feature properties being null. We initialize these if used.
        string IConnectionIdFeature.ConnectionId { get; set; } = string.Empty;

        CancellationToken IConnectionLifetimeFeature.ConnectionClosed
        {
            get => base.ConnectionClosed;
            set => base.ConnectionClosed = value;
        }

        IDictionary<object, object?> IItemsFeature.Items { get; set; } = ImmutableDictionary<object, object?>.Empty;

        bool IFeatureCollection.IsReadOnly => false;

        int IFeatureCollection.Revision => _extraFeatures?.Revision ?? 0;

        object? IFeatureCollection.this[Type key]
        {
            get
            {
                var feature = _extraFeatures?[key];

                if (feature is null && _connection.ConnectionProperties.TryGet(key, out var property))
                {
                    return property;
                }

                return feature;
            }
            set
            {
                _extraFeatures ??= new FeatureCollection();
                _extraFeatures[key] = value;
            }
        }

        [return: MaybeNull]
        TFeature IFeatureCollection.Get<TFeature>()
        {
            object? feature = null;

            // `?.` isn't allowed here for some reason.
            if (_extraFeatures != null)
            {
                feature = _extraFeatures.Get<TFeature>();
            }

            if (feature is null && _connection.ConnectionProperties.TryGet<TFeature>(out var property))
            {
                return property;
            }

            return (TFeature)feature;
        }

        void IFeatureCollection.Set<TFeature>(TFeature instance)
        {
            _extraFeatures ??= new FeatureCollection();
            _extraFeatures.Set(instance);
        }

        // We cannot enumerate the _connection's properties, so we just enumerate _extraFeatures as a best effort.
        // This could be addressed by putting the IFeatureCollection inside itself! 
        // That recursive reference could also help avoid multiple layers of _extraFeatures.
        IEnumerator<KeyValuePair<Type, object>> IEnumerable<KeyValuePair<Type, object>>.GetEnumerator()
        {
            _extraFeatures ??= new FeatureCollection();
            return _extraFeatures.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            _extraFeatures ??= new FeatureCollection();
            return ((IEnumerable)_extraFeatures).GetEnumerator();
        }
    }
}
