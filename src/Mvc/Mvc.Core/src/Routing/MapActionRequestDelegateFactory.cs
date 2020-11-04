// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Mvc.Core.Routing
{
    internal class MapActionRequestDelegateFactory : IRequestDelegateFactory
    {
        public RequestDelegate CreateRequestDelegate(ActionDescriptor actionDescriptor, RouteValueDictionary dataTokens)
        {
            return null;
        }
    }
}
