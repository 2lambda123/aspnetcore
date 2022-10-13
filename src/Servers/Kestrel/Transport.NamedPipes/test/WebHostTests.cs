// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Internal;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes.Tests;

public class WebHostTests : LoggedTest
{
    [Fact]
    public async Task ListenNamedPipeEndpoint_HelloWorld_ClientSuccess()
    {
        // Arrange
        using var httpEventSource = new HttpEventSourceListener(LoggerFactory);
        var pipeName = NamedPipeTestHelpers.GetUniquePipeName();

        var builder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.UseNamedPipes();
                webHostBuilder
                    .UseKestrel(o =>
                    {
                        o.Listen(new NamedPipeEndPoint(pipeName));
                    })
                    .Configure(app =>
                    {
                        app.Run(async context =>
                        {
                            await context.Response.WriteAsync("hello, world");
                        });
                    });
            })
            .ConfigureServices(AddTestLogging);

        using (var host = builder.Build())
        using (var client = CreateClient(pipeName))
        {
            await host.StartAsync().DefaultTimeout();

            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1/")
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };

            // Act
            var response = await client.SendAsync(request).DefaultTimeout();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpVersion.Version11, response.Version);
            var responseText = await response.Content.ReadAsStringAsync().DefaultTimeout();
            Assert.Equal("hello, world", responseText);

            await host.StopAsync().DefaultTimeout();
        }
    }

    [ConditionalFact]
    [OSSkipCondition(OperatingSystems.Linux | OperatingSystems.MacOSX, SkipReason = "Impersonation is only supported on Windows.")]
    public async Task ListenNamedPipeEndpoint_Impersonation_ClientSuccess()
    {
        AppDomain.CurrentDomain.SetPrincipalPolicy(PrincipalPolicy.WindowsPrincipal);

        // Arrange
        using var httpEventSource = new HttpEventSourceListener(LoggerFactory);
        var pipeName = NamedPipeTestHelpers.GetUniquePipeName();

        var builder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.UseNamedPipes(options =>
                {
                    var ps = new PipeSecurity();
                    ps.AddAccessRule(new PipeAccessRule("Users", PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));

                    options.PipeSecurity = ps;
                    options.CurrentUserOnly = false;
                });
                webHostBuilder
                    .UseKestrel(o =>
                    {
                        o.Listen(new NamedPipeEndPoint(pipeName), listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http1;
                        });
                    })
                    .Configure(app =>
                    {
                        app.Run(async context =>
                        {
                            var serverName = Thread.CurrentPrincipal.Identity.Name;

                            var namedPipeStream = context.Features.Get<IConnectionNamedPipeFeature>().NamedPipe;
                            var impersonatedName = namedPipeStream.GetImpersonationUserName();

                            context.Response.Headers.Add("X-Server-Identity", serverName);
                            context.Response.Headers.Add("X-Impersonated-Identity", impersonatedName);

                            var buffer = new byte[1024];
                            while (await context.Request.Body.ReadAsync(buffer) != 0)
                            {

                            }

                            await context.Response.WriteAsync("hello, world");
                        });
                    });
            })
            .ConfigureServices(AddTestLogging);

        using (var host = builder.Build())
        using (var client = CreateClient(pipeName, TokenImpersonationLevel.Impersonation))
        {
            await host.StartAsync().DefaultTimeout();

            var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1/")
            {
                Version = HttpVersion.Version11,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('c', 1024 * 1024)))
            };

            // Act
            var response = await client.SendAsync(request).DefaultTimeout();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(HttpVersion.Version11, response.Version);
            var responseText = await response.Content.ReadAsStringAsync().DefaultTimeout();
            Assert.Equal("hello, world", responseText);

            var serverIdentity = string.Join(",", response.Headers.GetValues("X-Server-Identity"));
            var impersonatedIdentity = string.Join(",", response.Headers.GetValues("X-Impersonated-Identity"));

            Assert.Equal(serverIdentity.Split('\\')[1], impersonatedIdentity);

            await host.StopAsync().DefaultTimeout();
        }
    }

    [Theory]
    [InlineData(HttpProtocols.Http1)]
    [InlineData(HttpProtocols.Http2)]
    public async Task ListenNamedPipeEndpoint_ProtocolVersion_ClientSuccess(HttpProtocols protocols)
    {
        // Arrange
        using var httpEventSource = new HttpEventSourceListener(LoggerFactory);
        var pipeName = NamedPipeTestHelpers.GetUniquePipeName();
        var clientVersion = GetClientVersion(protocols);

        var builder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.UseNamedPipes();
                webHostBuilder
                    .UseKestrel(o =>
                    {
                        o.Listen(new NamedPipeEndPoint(pipeName), options =>
                        {
                            options.Protocols = protocols;
                        });
                    })
                    .Configure(app =>
                    {
                        app.Run(async context =>
                        {
                            await context.Response.WriteAsync("hello, world");
                        });
                    });
            })
            .ConfigureServices(AddTestLogging);

        using (var host = builder.Build())
        using (var client = CreateClient(pipeName))
        {
            await host.StartAsync().DefaultTimeout();

            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1/")
            {
                Version = clientVersion,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };

            // Act
            var response = await client.SendAsync(request).DefaultTimeout();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(clientVersion, response.Version);
            var responseText = await response.Content.ReadAsStringAsync().DefaultTimeout();
            Assert.Equal("hello, world", responseText);

            await host.StopAsync().DefaultTimeout();
        }
    }

    private static Version GetClientVersion(HttpProtocols protocols)
    {
        return protocols switch
        {
            HttpProtocols.Http1 => HttpVersion.Version11,
            HttpProtocols.Http2 => HttpVersion.Version20,
            _ => throw new InvalidOperationException(),
        };
    }

    [ConditionalTheory]
    [OSSkipCondition(OperatingSystems.MacOSX, SkipReason = "Missing SslStream ALPN support: https://github.com/dotnet/runtime/issues/27727")]
    [InlineData(HttpProtocols.Http1)]
    [InlineData(HttpProtocols.Http2)]
    public async Task ListenNamedPipeEndpoint_Tls_ClientSuccess(HttpProtocols protocols)
    {
        // Arrange
        using var httpEventSource = new HttpEventSourceListener(LoggerFactory);
        var pipeName = NamedPipeTestHelpers.GetUniquePipeName();
        var clientVersion = GetClientVersion(protocols);

        var builder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.UseNamedPipes();
                webHostBuilder
                    .UseKestrel(o =>
                    {
                        o.Listen(new NamedPipeEndPoint(pipeName), options =>
                        {
                            options.Protocols = protocols;
                            options.UseHttps(TestResources.GetTestCertificate());
                        });
                    })
                    .Configure(app =>
                    {
                        app.Run(async context =>
                        {
                            await context.Response.WriteAsync("hello, world");
                        });
                    });
            })
            .ConfigureServices(AddTestLogging);

        using (var host = builder.Build())
        using (var client = CreateClient(pipeName))
        {
            await host.StartAsync().DefaultTimeout();

            var request = new HttpRequestMessage(HttpMethod.Get, $"https://127.0.0.1/")
            {
                Version = clientVersion,
                VersionPolicy = HttpVersionPolicy.RequestVersionExact
            };

            // Act
            var response = await client.SendAsync(request).DefaultTimeout();

            // Assert
            response.EnsureSuccessStatusCode();
            Assert.Equal(clientVersion, response.Version);
            var responseText = await response.Content.ReadAsStringAsync().DefaultTimeout();
            Assert.Equal("hello, world", responseText);

            await host.StopAsync().DefaultTimeout();
        }
    }

    private static HttpClient CreateClient(string pipeName, TokenImpersonationLevel? impersonationLevel = null)
    {
        var httpHandler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, __, ___, ____) => true
            }
        };

        var connectionFactory = new NamedPipesConnectionFactory(pipeName, impersonationLevel);
        httpHandler.ConnectCallback = connectionFactory.ConnectAsync;

        return new HttpClient(httpHandler);
    }

    public class NamedPipesConnectionFactory
    {
        private readonly string _pipeName;
        private readonly TokenImpersonationLevel? _impersonationLevel;

        public NamedPipesConnectionFactory(string pipeName, TokenImpersonationLevel? impersonationLevel = null)
        {
            _pipeName = pipeName;
            _impersonationLevel = impersonationLevel;
        }

        public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext _,
            CancellationToken cancellationToken = default)
        {
            var clientStream = new NamedPipeClientStream(
                serverName: ".",
                pipeName: _pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.WriteThrough | PipeOptions.Asynchronous,
                impersonationLevel: _impersonationLevel ?? TokenImpersonationLevel.Anonymous);
            
            try
            {
                await clientStream.ConnectAsync(cancellationToken).ConfigureAwait(false);
                return clientStream;
            }
            catch
            {
                clientStream.Dispose();
                throw;
            }
        }
    }
}
