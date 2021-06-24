// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Web.DragAndDrop
{
    internal class DragInteropHandle<TItem>
    {
        private readonly Drag<TItem> _drag;

        public TItem Item => _drag.Item;

        public DragInteropHandle(Drag<TItem> drag)
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

        [JSInvokable]
        public void OnDragEnd(MutableDragEventArgs e, Dictionary<string, string> initialData, DotNetObjectReference<object>? dropHandleObjectReference)
        {
            e.DataTransfer.Store = new DataTransferStore(e.DataTransfer, initialData);

            var targetDrop = dropHandleObjectReference?.Value is DropInteropHandle<TItem> dropHandle && dropHandle.Drop.CanDropCore(_drag.Item)
                ? dropHandle.Drop
                : null;

            _drag.OnDragEndCore(e, targetDrop);
        }
    }
}
