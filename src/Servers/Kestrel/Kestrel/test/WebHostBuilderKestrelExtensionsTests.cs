// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Net.Connections;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Libuv.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.Tests
{
    public class WebHostBuilderKestrelExtensionsTests
    {
        [Fact]
        public void ApplicationServicesNotNullAfterUseKestrelWithoutOptions()
        {
            // Arrange
            var hostBuilder = new WebHostBuilder()
                .UseKestrel()
                .Configure(app => { });

            hostBuilder.ConfigureServices(services =>
            {
                services.Configure<KestrelServerOptions>(options =>
                {
                    // Assert
                    Assert.NotNull(options.ApplicationServices);
                });
            });

            // Act
            hostBuilder.Build();
        }

        [Fact]
        public void ApplicationServicesNotNullDuringUseKestrelWithOptions()
        {
            // Arrange
            var hostBuilder = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    // Assert
                    Assert.NotNull(options.ApplicationServices);
                })
                .Configure(app => { });

            // Act
            hostBuilder.Build();
        }

        [Fact]
        public void SocketTransportIsTheDefault()
        {
            var hostBuilder = new WebHostBuilder()
                .UseKestrel()
                .Configure(app => { });

            Assert.IsType<SocketTransportFactory>(hostBuilder.Build().Services.GetService<ConnectionListenerFactory>());
        }

        [Fact]
        public void LibuvTransportCanBeManuallySelectedIndependentOfOrder()
        {
#pragma warning disable CS0618
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseSockets()
                .UseLibuv()
                .Configure(app => { })
                .Build();
#pragma warning restore CS0618

            Assert.IsType<LibuvTransportFactory>(host.Services.GetService<IConnectionListenerFactory>());
            Assert.Null(host.Services.GetService<ConnectionListenerFactory>());

#pragma warning disable CS0618
            var hostReversed = new WebHostBuilder()
                .UseSockets()
                .UseLibuv()
                .UseKestrel()
                .Configure(app => { })
                .Build();
#pragma warning restore CS0618

            Assert.IsType<LibuvTransportFactory>(hostReversed.Services.GetService<IConnectionListenerFactory>());
            Assert.Null(hostReversed.Services.GetService<ConnectionListenerFactory>());
        }

        [Fact]
        public void SocketsTransportCanBeManuallySelectedIndependentOfOrder()
        {
#pragma warning disable CS0618
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseLibuv()
                .UseSockets()
                .Configure(app => { })
                .Build();
#pragma warning restore CS0618

            Assert.IsType<SocketTransportFactory>(host.Services.GetService<ConnectionListenerFactory>());
            Assert.Null(host.Services.GetService<IConnectionListenerFactory>());

#pragma warning disable CS0618
            var hostReversed = new WebHostBuilder()
                .UseLibuv()
                .UseSockets()
                .UseKestrel()
                .Configure(app => { })
                .Build();
#pragma warning restore CS0618

            Assert.IsType<SocketTransportFactory>(hostReversed.Services.GetService<ConnectionListenerFactory>());
            Assert.Null(hostReversed.Services.GetService<IConnectionListenerFactory>());
        }

        [Fact]
        public void ServerIsKestrelServerImpl()
        {
            var hostBuilder = new WebHostBuilder()
                .UseSockets()
                .UseKestrel()
                .Configure(app => { });

            Assert.IsType<KestrelServerImpl>(hostBuilder.Build().Services.GetService<IServer>());
        }
    }
}
