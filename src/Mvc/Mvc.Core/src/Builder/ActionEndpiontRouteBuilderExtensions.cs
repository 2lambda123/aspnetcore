// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Mvc.Core.Builder
{
    /// <summary>
    /// Contains extension methods for using Actions directly with <see cref="IEndpointRouteBuilder"/>.
    /// </summary>
    public static class ActionEndpiontRouteBuilderExtensions
    {
        /// <summary>
        /// Adds endpoint for action to the <see cref="IEndpointRouteBuilder"/> without specifying any routes.
        /// </summary>
        /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/>.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <param name="action">The action to bind.</param>
        /// <returns>The <see cref="IEndpointConventionBuilder"/>.</returns>
        public static IEndpointConventionBuilder MapAction<TIn, TOut>(this IEndpointRouteBuilder endpoints, string pattern, Func<TIn, TOut> action)
        {
            return endpoints.MapPost(pattern, async httpContext =>
            {
                httpContext.Response.Headers["Content-Type"] = "application/json; charset=utf-8";

                var input = await JsonSerializer.DeserializeAsync<TIn>(httpContext.Request.Body);
                var output = action(input);
                await JsonSerializer.SerializeAsync(httpContext.Response.Body, output);
            });
        }
    }
}
