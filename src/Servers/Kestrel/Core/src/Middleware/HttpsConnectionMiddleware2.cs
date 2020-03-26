// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Connections;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Certificates.Generation;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Https.Internal
{
    internal class HttpsConnectionMiddleware2 : IConnectionListener
    {
        private readonly IConnectionListener _previous;
        private readonly HttpsConnectionAdapterOptions _options;
        private readonly ILogger _logger;
        private readonly X509Certificate2 _serverCertificate;

        public HttpsConnectionMiddleware2(IConnectionListener previous, HttpsConnectionAdapterOptions options, ILoggerFactory loggerFactory)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // This configuration will always fail per-request, preemptively fail it here. See HttpConnection.SelectProtocol().
            if (options.HttpProtocols == HttpProtocols.Http2)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    throw new NotSupportedException(CoreStrings.HTTP2NoTlsOsx);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.OSVersion.Version < new Version(6, 2))
                {
                    throw new NotSupportedException(CoreStrings.HTTP2NoTlsWin7);
                }
            }

            _previous = previous;
            // capture the certificate now so it can't be switched after validation
            _serverCertificate = options.ServerCertificate;
            if (_serverCertificate == null && options.ServerCertificateSelector == null)
            {
                throw new ArgumentException(CoreStrings.ServerCertificateRequired, nameof(options));
            }

            // If a selector is provided then ignore the cert, it may be a default cert.
            if (options.ServerCertificateSelector != null)
            {
                // SslStream doesn't allow both.
                _serverCertificate = null;
            }
            else
            {
                EnsureCertificateIsAllowedForServerAuth(_serverCertificate);
            }

            _options = options;
            _logger = loggerFactory.CreateLogger<HttpsConnectionMiddleware2>();
        }


        public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var connection = await _previous.AcceptAsync(cancellationToken);

                // HttpsConnectionMiddleware2 has already run!
                if (connection.ConnectionProperties.TryGet<ITlsConnectionFeature>(out _))
                {
                    return connection;
                }

                try
                {
                    return await AdaptConnectionAsync(connection);
                }
                catch
                {
                    // Try again. The exception is already logged by AdaptConnectionAsync().
                }
            }
        }

        public ValueTask DisposeAsync()
        {
            return _previous.DisposeAsync();
        }

        private async Task<IConnection> AdaptConnectionAsync(IConnection innerConnection)
        {
            bool certificateRequired;
            SslStream sslStream;
            Connections.ConnectionContext connectionContextWrapper = null;

            if (_options.ClientCertificateMode == ClientCertificateMode.NoCertificate)
            {
                sslStream = new SslStream(innerConnection.Stream);
                certificateRequired = false;
            }
            else
            {
                sslStream = new SslStream(innerConnection.Stream,
                    leaveInnerStreamOpen: false,
                    userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if (certificate == null)
                        {
                            return _options.ClientCertificateMode != ClientCertificateMode.RequireCertificate;
                        }

                        if (_options.ClientCertificateValidation == null)
                        {
                            if (sslPolicyErrors != SslPolicyErrors.None)
                            {
                                return false;
                            }
                        }

                        var certificate2 = ConvertToX509Certificate2(certificate);
                        if (certificate2 == null)
                        {
                            return false;
                        }

                        if (_options.ClientCertificateValidation != null)
                        {
                            if (!_options.ClientCertificateValidation(certificate2, chain, sslPolicyErrors))
                            {
                                return false;
                            }
                        }

                        return true;
                    });

                certificateRequired = true;
            }

            var sslConnection = new SslConnection(innerConnection, sslStream);
            using (var cancellationTokeSource = new CancellationTokenSource(_options.HandshakeTimeout))
            {
                try
                {
                    // Adapt to the SslStream signature
                    ServerCertificateSelectionCallback selector = null;
                    if (_options.ServerCertificateSelector is object)
                    {
                        selector = (sender, name) =>
                        {
                            connectionContextWrapper = ConvertToConnectionContext(sslConnection);
                            var cert = _options.ServerCertificateSelector(connectionContextWrapper, name);
                            if (cert != null)
                            {
                                EnsureCertificateIsAllowedForServerAuth(cert);
                            }
                            return cert;
                        };
                    }

                    var sslOptions = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _serverCertificate,
                        ServerCertificateSelectionCallback = selector,
                        ClientCertificateRequired = certificateRequired,
                        EnabledSslProtocols = _options.SslProtocols,
                        CertificateRevocationCheckMode = _options.CheckCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                        ApplicationProtocols = new List<SslApplicationProtocol>()
                    };

                    // This is order sensitive
                    if ((_options.HttpProtocols & HttpProtocols.Http2) != 0)
                    {
                        sslOptions.ApplicationProtocols.Add(SslApplicationProtocol.Http2);
                        // https://tools.ietf.org/html/rfc7540#section-9.2.1
                        sslOptions.AllowRenegotiation = false;
                    }

                    if ((_options.HttpProtocols & HttpProtocols.Http1) != 0)
                    {
                        sslOptions.ApplicationProtocols.Add(SslApplicationProtocol.Http11);
                    }

                    _options.OnAuthenticate?.Invoke(connectionContextWrapper ?? ConvertToConnectionContext(sslConnection), sslOptions);

                    await sslStream.AuthenticateAsServerAsync(sslOptions, cancellationTokeSource.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug(2, CoreStrings.AuthenticationTimedOut);
                    await sslConnection.DisposeAsync();
                    throw;
                }
                catch (IOException ex)
                {
                    _logger.LogDebug(1, ex, CoreStrings.AuthenticationFailed);
                    await sslConnection.DisposeAsync();
                    throw;
                }
                catch (AuthenticationException ex)
                {
                    if (_serverCertificate == null ||
                        !CertificateManager.IsHttpsDevelopmentCertificate(_serverCertificate) ||
                        CertificateManager.CheckDeveloperCertificateKey(_serverCertificate))
                    {
                        _logger.LogDebug(1, ex, CoreStrings.AuthenticationFailed);
                    }
                    else
                    {
                        _logger.LogError(3, ex, CoreStrings.BadDeveloperCertificateState);
                    }

                    await sslConnection.DisposeAsync();
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected exception!");
                    await sslConnection.DisposeAsync();
                }

                return sslConnection;
            }
        }

        private static void EnsureCertificateIsAllowedForServerAuth(X509Certificate2 certificate)
        {
            if (!CertificateLoader.IsCertificateAllowedForServerAuth(certificate))
            {
                throw new InvalidOperationException(CoreStrings.FormatInvalidServerCertificateEku(certificate.Thumbprint));
            }
        }

        private static X509Certificate2 ConvertToX509Certificate2(X509Certificate certificate)
        {
            if (certificate == null)
            {
                return null;
            }

            if (certificate is X509Certificate2 cert2)
            {
                return cert2;
            }

            return new X509Certificate2(certificate);
        }

        private static Connections.ConnectionContext ConvertToConnectionContext(IConnection connection)
        {
            return new SystemNetConnectionsConnectionContext(connection);
        }

        private class SslConnection :
            IConnection,
            IDuplexPipe,
            IConnectionProperties,
            ITlsConnectionFeature,
            ITlsApplicationProtocolFeature,
            ITlsHandshakeFeature
        {
            private readonly IConnection _innerConnection;
            private readonly SslStream _sslStream;
            private X509Certificate2  _clientCertificate;

            // REVIEW: This is super annoying
            private bool _streamAccessed;

            public SslConnection(IConnection innerConnection, SslStream sslStream)
            {
                _innerConnection = innerConnection;
                _sslStream = sslStream;
            }

            public EndPoint LocalEndPoint => _innerConnection.LocalEndPoint;
            public EndPoint RemoteEndPoint => _innerConnection.RemoteEndPoint;
            public IConnectionProperties ConnectionProperties => this;

            public Stream Stream
            {
                get
                {
                    if (Input is object)
                    {
                        throw new InvalidOperationException("IConnection.Stream cannot be accessed after IConnection.Pipe");
                    }

                    _streamAccessed = true;
                    return _sslStream;
                }
            }

            public IDuplexPipe Pipe
            {
                get
                {
                    if (_streamAccessed)
                    {
                        throw new InvalidOperationException("IConnection.Pipe cannot be accessed after IConnection.Stream");
                    }

                    if (Input is null)
                    {
                        Input = PipeReader.Create(_sslStream);
                        Output = PipeWriter.Create(_sslStream);
                    }

                    return this;
                }
            }

            public PipeReader Input { get; set; }
            public PipeWriter Output { get; set; }
            public async ValueTask DisposeAsync()
            {
                if (Input is object)
                {
                    await Input.CompleteAsync();
                    await Output.CompleteAsync();
                }

                await _sslStream.DisposeAsync();
                await _innerConnection.DisposeAsync();
            }

            bool IConnectionProperties.TryGet(Type propertyType, out object property)
            {
                if (propertyType == typeof(ITlsConnectionFeature) ||
                    propertyType == typeof(ITlsHandshakeFeature) ||
                    propertyType == typeof(ITlsApplicationProtocolFeature))
                {
                    property = this;
                    return true;
                }
                else if (propertyType == typeof(SslStream))
                {
                    property = _sslStream;
                    return true;
                }

                return _innerConnection.ConnectionProperties.TryGet(propertyType, out property);
            }

            // Feature implementations
            X509Certificate2 ITlsConnectionFeature.ClientCertificate
            {
                get => _clientCertificate ??= ConvertToX509Certificate2(_sslStream.RemoteCertificate);
                set => _clientCertificate = value;
            }

            ReadOnlyMemory<byte> ITlsApplicationProtocolFeature.ApplicationProtocol => _sslStream.NegotiatedApplicationProtocol.Protocol;
            SslProtocols ITlsHandshakeFeature.Protocol => _sslStream.SslProtocol;
            CipherAlgorithmType ITlsHandshakeFeature.CipherAlgorithm => _sslStream.CipherAlgorithm;
            int ITlsHandshakeFeature.CipherStrength => _sslStream.CipherStrength;
            HashAlgorithmType ITlsHandshakeFeature.HashAlgorithm => _sslStream.HashAlgorithm;
            int ITlsHandshakeFeature.HashStrength => _sslStream.HashStrength;
            ExchangeAlgorithmType ITlsHandshakeFeature.KeyExchangeAlgorithm => _sslStream.KeyExchangeAlgorithm;
            int ITlsHandshakeFeature.KeyExchangeStrength => _sslStream.KeyExchangeStrength;

            Task<X509Certificate2> ITlsConnectionFeature.GetClientCertificateAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(((ITlsConnectionFeature)this).ClientCertificate);
            }
        }

        // REVIEW: This should be in the runtime for people who don't want to implement IDuplexPipe and IConnection
        // in the same class.
        private class StreamDuplexPipe : IDuplexPipe
        {
            public StreamDuplexPipe(Stream innerStreawm)
            {
                Input = PipeReader.Create(innerStreawm);
                Output = PipeWriter.Create(innerStreawm);
            }

            public PipeReader Input { get; set; }

            public PipeWriter Output { get; set; }
        }
    }
}
