// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.Components.Web.DragAndDrop
{
    internal class DataTransferStore
    {
        public MutableDataTransfer DataTransfer { get; }

        public Dictionary<string, string> Data { get; }

        public ElementReference? DragImageElement { get; set; }

        public string? DragImageSourceUrl { get; set; }

        public long DragImageXOffset { get; set; }

        public long DragImageYOffset { get; set; }

        public DataTransferStore(MutableDataTransfer dataTransfer, Dictionary<string, string> data)
        {
            DataTransfer = dataTransfer;
            Data = data;
        }
    }
}
