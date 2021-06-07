// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.AspNetCore.Http.Metadata
{
    /// <summary>
    /// 
    /// </summary>
    public interface IActionMethodMetadata
    {
        /// <summary>
        /// 
        /// </summary>
        public MethodInfo MethodInfo { get; }

        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<string> ParameterSources { get; }

        /// <summary>
        /// 
        /// </summary>
        public Type ResponseBodyType { get; }
    }
}
