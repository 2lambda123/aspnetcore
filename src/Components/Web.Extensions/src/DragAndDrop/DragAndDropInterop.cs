// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Components.Web.Extensions
{
    internal static class DragAndDropInterop
    {
        private const string JsFunctionsPrefix = "_blazorDragAndDrop.";

        public const string RegisterDragHandle = JsFunctionsPrefix + "registerDragHandle";

        public const string UnregisterDragHandle = JsFunctionsPrefix + "unregisterDragHandle";

        public const string RegisterDropHandle = JsFunctionsPrefix + "registerDropHandle";

        public const string UnregisterDropHandle = JsFunctionsPrefix + "unregisterDropHandle";

        public const string OnDragStart = JsFunctionsPrefix + "onDragStart";

        public const string OnDragEnd = JsFunctionsPrefix + "onDragEnd";

        public const string OnDrop = JsFunctionsPrefix + "onDrop";

        public const string OnDragOver = JsFunctionsPrefix + "onDragOver";
    }
}
