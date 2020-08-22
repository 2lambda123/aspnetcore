// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Connections;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure.ConnectionWrappers
{
    internal static class ConnectionWrapperUtils
    {
        // Hopefully this is temporary: https://github.com/dotnet/runtime/issues/41110
        public static string ConnectionId(this ConnectionBase connectionBase)
        {
            if (connectionBase.ConnectionProperties.TryGet<IConnectionIdFeature>(out var connectionIdFeature))
            {
                return connectionIdFeature.ConnectionId;
            }

            return connectionBase.ToString() ?? string.Empty;
        }

        public static async ValueTask CloseAsyncCore(BaseConnectionContext baseContext, ConnectionCloseMethod method, CancellationToken cancellationToken)
        {
            IDisposable? cancellationRegistration = null;

            if (baseContext.ConnectionClosed.IsCancellationRequested)
            {
                // No need to abort if the connection is already closed.
            }
            else if (method != ConnectionCloseMethod.GracefulShutdown)
            {
                baseContext.Abort(new ConnectionAbortedException($"The connection was closed with ConnectionCloseMethod.{method}."));
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                baseContext.Abort(new ConnectionAbortedException("The connection was closed with a canceled token."));
            }
            else if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(state =>
                {
                    var connectionContext = (BaseConnectionContext)state!;
                    connectionContext.Abort(new ConnectionAbortedException("Graceful connection close was canceled with a token."));
                }, baseContext, useSynchronizationContext: false);
            }

            using (cancellationRegistration)
            {
                // This method can be called multiple times. Graceful shutdown is only attempted after middleware is done reading and writing.
                // Abortive shutdown can happen while reads and writes are ongoing.
                if (method == ConnectionCloseMethod.GracefulShutdown)
                {
                    await baseContext.DisposeAsync();
                }
                else
                {
                    await CancellationTokenAsTask(baseContext.ConnectionClosed);
                }
            }
        }

        public static bool TryGetProperty(IFeatureCollection features, Type propertyKey, [NotNullWhen(true)] out object property)
        {
            property = features[propertyKey]!;
            return property != null;
        }

        private static Task CancellationTokenAsTask(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => tcs.SetResult());
            return tcs.Task;
        }
    }
}
