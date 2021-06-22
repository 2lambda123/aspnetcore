// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Components.Web.DragAndDrop
{
    public readonly struct DropInfo<TItem>
    {
        public MutableDragEventArgs EventArgs { get; }

        public TItem Item { get; }

        public DropInfo(MutableDragEventArgs e, TItem item)
        {
            EventArgs = e;
            Item = item;
        }
    }
}
