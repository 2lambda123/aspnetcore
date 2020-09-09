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

        internal DataTransferStore Store { get; set; } = default!;

        public void SetDragImage(ElementReference imageReference, long xOffset, long yOffset)
        {
            ThrowIfStoreIsNull();

            Store.DragImage = imageReference;
            Store.DragImageXOffset = xOffset;
            Store.DragImageYOffset = yOffset;
        }

        public void SetData(string format, string data)
        {
            ThrowIfStoreIsNull();

            Store.Data.Add(format, data);
        }

        public string GetData(string format)
        {
            ThrowIfStoreIsNull();

            return Store.Data.TryGetValue(format, out var value) ? value : string.Empty;
        }

        public void ClearData(string? format = default)
        {
            // TODO: Handle files correctly: https://developer.mozilla.org/en-US/docs/Web/API/DataTransfer/clearData.

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
