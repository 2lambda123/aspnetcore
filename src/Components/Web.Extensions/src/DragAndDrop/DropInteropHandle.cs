// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Web.Extensions
{
    internal class DropInteropHandle<TItem>
    {
        public Drop<TItem> Drop { get; }

        public DropInteropHandle(Drop<TItem> drop)
        {
            Drop = drop;
        }

        [JSInvokable]
        public void OnDrop(MutableDragEventArgs eventArgs, Dictionary<string, string> initialData, DotNetObjectReference<object> dragHandleObjectReference)
        {
            if (TryGetValidDragHandle(dragHandleObjectReference, out var dragHandle))
            {
                eventArgs.DataTransfer.Store = new DataTransferStore(eventArgs.DataTransfer, initialData);

                Drop.OnDropCore(dragHandle.Item, eventArgs);
            }
        }

        [JSInvokable]
        public void OnDragOver(MutableDragEventArgs eventArgs, Dictionary<string, string> initialData, DotNetObjectReference<object> dragHandleObjectReference)
        {
            if (TryGetValidDragHandle(dragHandleObjectReference, out var dragHandle))
            {
                eventArgs.DataTransfer.Store = new DataTransferStore(eventArgs.DataTransfer, initialData);

                Drop.OnDragOverCore(dragHandle.Item, eventArgs);
            }
        }

        private bool TryGetValidDragHandle(DotNetObjectReference<object> objRef, [NotNullWhen(returnValue: true)] out DragInteropHandle<TItem>? result)
        {
            result = objRef.Value as DragInteropHandle<TItem>;
            return result != null && Drop.CanDropCore(result.Item);
        }
    }
}
