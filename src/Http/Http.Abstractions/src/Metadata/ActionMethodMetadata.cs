// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.AspNetCore.Http.Metadata
{
    /// <inheritdoc />
    public sealed class ActionMethodMetadata : IActionMethodMetadata
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <param name="parameterSources"></param>
        /// <param name="responseBodyType"></param>
        public ActionMethodMetadata(MethodInfo methodInfo, IEnumerable<string> parameterSources, Type responseBodyType)
        {
            MethodInfo = methodInfo;
            ParameterSources = parameterSources;
            ResponseBodyType = responseBodyType;
        }

        /// <inheritdoc />
        public MethodInfo MethodInfo { get; }

        /// <inheritdoc />
        public IEnumerable<string> ParameterSources { get; }

        /// <inheritdoc />
        public Type ResponseBodyType { get; }
    }
}
