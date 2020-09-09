// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Web.Extensions
{
    internal class DropInteropRelay<TItem>
    {
        private readonly Drop<TItem> _drop;

        public DropInteropRelay(Drop<TItem> drop)
        {
            _drop = drop;
        }

        [JSInvokable]
        public void OnDrop(MutableDragEventArgs eventArgs, Dictionary<string, string> initialData, DotNetObjectReference<DragInteropRelay<TItem>>[] drags)
        {
            eventArgs.DataTransfer.Store = new DataTransferStore(eventArgs.DataTransfer, initialData);
            _drop.OnDropCore(eventArgs, drags.Select(d => d.Value.Item).ToArray());
        }
    }
}
