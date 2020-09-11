// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Components.Web.Extensions
{
    public class MutableDataTransfer : DataTransfer
    {
        // TODO: "Items" is not updated on the .NET side via SetData, ClearData, etc. Is this ok?
        // A workaround could be to regenerate the "Items" array when a shadowed "Items" property is accessed.
        // That way, we don't unnecessarily reallocate, but if the user *has* to use "Items", they can do so.

        // TODO: The HTML spec describes certain data not being mutable outside of the dragstart callback:
        // https://html.spec.whatwg.org/multipage/dnd.html#datatransfer
        // We should probably emulate this to some extent in our own callbacks, where we throw if you try to "set"
        // at the wrong time.
        // If we go that route, we might want to consider renaming "MutableDataTransfer" and "MutableDragEventArgs" to something else,
        // since receiving a MutableDragEventArgs in a callback that can't mutate the data transfer might be confusing.

        // Overall, the behavior here doesn't really match the behavior of DataTransfer in the HTML spec completely.
        // We'll need to decide how closely we want to follow it. Alternatively, we could pretend the HTML
        // spec doesn't exist, and this is our own type that happens to look kind of like the HTML DataTransfer object.

        internal DataTransferStore Store { get; set; } = default!;

        public void SetDragImage(ElementReference imageReference, long xOffset, long yOffset)
        {
            // TODO: Provide an overload accepting an image URL (the ElementReference overload might not even be necessary).
            
            ThrowIfStoreIsNull();

            Store.DragImage = imageReference;
            Store.DragImageXOffset = xOffset;
            Store.DragImageYOffset = yOffset;
        }

        public void SetData(string format, string data)
        {
            ThrowIfStoreIsNull();

            Store.Data[format] = data;
        }

        public string GetData(string format)
        {
            ThrowIfStoreIsNull();

            return Store.Data.TryGetValue(format, out var value) ? value : string.Empty;
        }

        public void ClearData(string? format = default)
        {
            // TODO: Handle "file" format correctly: https://developer.mozilla.org/en-US/docs/Web/API/DataTransfer/clearData

            ThrowIfStoreIsNull();

            if (string.IsNullOrEmpty(format))
            {
                Store.Data.Clear();
            }
            else
            {
                Store.Data.Remove(format);
            }
        }

        private void ThrowIfStoreIsNull()
        {
            if (Store == null)
            {
                throw new InvalidOperationException(
                    $"Cannot perform operations on a {GetType()} without an underlying store. " +
                    $"Instances of {GetType()} should only be obtained from drag and drop events.");
            }
        }
    }
}
