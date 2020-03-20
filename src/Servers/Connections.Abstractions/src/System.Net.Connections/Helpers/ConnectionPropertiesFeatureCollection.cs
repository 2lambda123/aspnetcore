// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http.Features;

namespace System.Net.Connections.Helpers
{
    public class ConnectionPropertiesFeatureCollection : IFeatureCollection
    {
        private readonly IConnectionProperties _connectionProperties;
        private readonly FeatureCollection _additionalFeatures = new FeatureCollection();

        public ConnectionPropertiesFeatureCollection(IConnectionProperties connectionProperties)
        {
            _connectionProperties = connectionProperties;
        }

        public object this[Type key]
        {
            get
            {
                var additionalFeature = _additionalFeatures[key];

                if (additionalFeature != null)
                {
                    return additionalFeature;
                }

                _connectionProperties.TryGet(key, out object property);
                return property;
            }
            set
            {
                _additionalFeatures[key] = value;
            }
        }

        public bool IsReadOnly => false;

        public int Revision => _additionalFeatures.Revision;

        public TFeature Get<TFeature>()
        {
            return (TFeature)this[typeof(TFeature)];
        }

        public IEnumerator<KeyValuePair<Type, object>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public void Set<TFeature>(TFeature instance)
        {
            this[typeof(TFeature)] = instance;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
