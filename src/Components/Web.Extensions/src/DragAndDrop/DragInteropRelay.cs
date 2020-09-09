// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Web.Extensions
{
    internal class DragInteropRelay<TItem>
    {
        private readonly Drag<TItem> _drag;

        public TItem Item => _drag.Item; // TODO: May want to decouple this class form Drag a little (component lifecycle concerns).

        public DragInteropRelay(Drag<TItem> drag)
        {
            _drag = drag;
        }

        [JSInvokable]
        public DataTransferStore OnDragStart(MutableDragEventArgs e, Dictionary<string, string> initialData)
        {
            // Initialize the DataTransferStore so the DataTranfser instance can be mutated.
            e.DataTransfer.Store = new DataTransferStore(e.DataTransfer, initialData);

            _drag.OnDragStartCore(e);

            // Updated DataTransferStore returned to JS.
            return e.DataTransfer.Store;
        }
    }
}
