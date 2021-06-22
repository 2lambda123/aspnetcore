// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Components.Web.DragAndDrop
{
    internal static class DragAndDropInterop
    {
        private const string JsFunctionsPrefix = "Blazor._internal.DragAndDrop.";

        public const string RegisterDragDotNetHelper = JsFunctionsPrefix + "registerDragDotNetHelper";

        public const string UnregisterDragDotNetHelper = JsFunctionsPrefix + "unregisterDragDotNetHelper";

        public const string RegisterDropDotNetHelper = JsFunctionsPrefix + "registerDropDotNetHelper";

        public const string UnregisterDropDotNetHelper = JsFunctionsPrefix + "unregisterDropDotNetHelper";

        public const string OnDragStart = JsFunctionsPrefix + "onDragStart";

        public const string OnDragEnd = JsFunctionsPrefix + "onDragEnd";

        public const string OnDrop = JsFunctionsPrefix + "onDrop";

        public const string OnDragOver = JsFunctionsPrefix + "onDragOver";
    }
}
