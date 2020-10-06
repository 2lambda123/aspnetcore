// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Abstractions;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3
{
    class Http3Stream<TContext> : Http3Stream, IHostContextContainer<TContext>
    {
        private readonly IHttpApplication<TContext> _application;

        public Http3Stream(IHttpApplication<TContext> application, Http3Connection connection, Http3StreamContext context) : base(connection, context)
        {
            _application = application;
        }

        public override void Execute()
        {
            if (_requestHeaderParsingState == Http3Stream.RequestHeaderParsingState.Ready)
            {
                // The ExecutionContext must be restored before the RequestQueuedStop event for ActivityId tracking.
                ExecutionContext.Restore(RequestQueuedExecutionContext);
                RequestQueuedExecutionContext = null;

                KestrelEventSource.Log.RequestQueuedStop(this, AspNetCore.Http.HttpProtocol.Http3);

                // Recapture the InitialExecutionContext now that ActivityTracker.Instance.m_current.Value has been reset
                // back to the parent activity of the request queuing activity RequestQueuedStart created. This prevents
                // activities created by middleware from having the request queuing activity as a parent.
                RequestQueuedExecutionContext = ExecutionContext.Capture();

                _ = ProcessRequestAsync(_application);
            }
            else
            {
                // Reset to Http3Connection's initial ExecutionContext giving access to the connection logging scope
                // and any other AsyncLocals set by connection middleware.
                ExecutionContext.Restore(ConnectionExecutionContext);

                _ = base.ProcessRequestsAsync(_application);
            }
        }

        // Pooled Host context
        TContext IHostContextContainer<TContext>.HostContext { get; set; }
    }
}
