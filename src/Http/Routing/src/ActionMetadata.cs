// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information

using System.Reflection;

namespace Microsoft.AspNetCore.Routing
{
    /// <inheritdoc />
    public sealed class ActionMethodInfoMetadata : IActionMethodInfoMetadata
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="methodInfo"></param>
        public ActionMethodInfoMetadata(MethodInfo methodInfo)
        {
            MethodInfo = methodInfo;
        }

        /// <inheritdoc />
        public MethodInfo MethodInfo { get; }
    }
}
