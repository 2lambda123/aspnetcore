// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http.Features;

namespace System.Net.Connections.Helpers
{
    public class FeatureCollectionConnectionProperties : IConnectionProperties
    {
        private readonly IFeatureCollection _featureCollection;

        public FeatureCollectionConnectionProperties(IFeatureCollection featureCollection)
        {
            _featureCollection = featureCollection;
        }

        public bool TryGet(Type propertyType, out object property)
        {
            // REVIEW: We lose out on the optimized path since there's on generic TryGet on the interface :(
            property = _featureCollection[propertyType];
            return property != null;
        }
    }
}
