// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Web.Extensions
{
    internal class DragInteropRelay<TItem>
    {
        private readonly Drag<TItem> _drag;

        public DragInteropRelay(Drag<TItem> drag)
        {
            _drag = drag;
        }

        [JSInvokable]
        public DragEventArgs OnDragStart(DragEventArgs e)
        {
            // This call may mutate the event args
            _drag.OnDragStartCore(e);

            // Mutated instance returned back to JS
            return e;
        }
    }
}
