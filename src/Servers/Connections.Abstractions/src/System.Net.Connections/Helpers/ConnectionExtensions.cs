// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace System.Net.Connections
{
    public static class ConnectionExtensions
    {
        public static bool TryGet<T>(this IConnectionProperties connectionProperties, out T property)
        {
            if (connectionProperties.TryGet(typeof(T), out object temp))
            {
                property = (T)temp;
                return true;
            }

            property = default;
            return false;
        }

        public static T GetRequiredProperty<T>(this IConnectionProperties connectionProperties)
        {
            if (!connectionProperties.TryGet(out T property))
            {
                throw new InvalidOperationException();
            }

            return property;
        }
    }
}
