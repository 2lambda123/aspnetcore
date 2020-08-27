// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Connections;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Tests
{
    public class KestrelServereImplTests
    {
        [Fact]
        public void ConstructorProvidedWithBothNewAndOldTransportsThrows()
        {
            var exception = Assert.Throws<ArgumentException>(() => new KestrelServerImpl(
                Options.Create<KestrelServerOptions>(null),
                new[] { Mock.Of<ConnectionListenerFactory>() },
                new[] { Mock.Of<IConnectionListenerFactory>() },
                multiplexedTransportFactories: null,
                new LoggerFactory()));

            Assert.Equal(CoreStrings.MultipleTransportsFound, exception.Message);
        }
    }
}
