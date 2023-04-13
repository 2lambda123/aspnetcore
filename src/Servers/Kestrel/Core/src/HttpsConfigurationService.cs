// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Net.Security;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Server.Kestrel.Https.Internal;

namespace Microsoft.AspNetCore.Server.Kestrel.Core;

/// <inheritdoc />
internal sealed class HttpsConfigurationService : IHttpsConfigurationService
{
    private readonly IInitializer? _initializer;
    private bool _isInitialized;

    private TlsConfigurationLoader? _tlsConfigurationLoader;
    private Action<FeatureCollection, ListenOptions>? _populateMultiplexedTransportFeatures;
    private Func<ListenOptions, ListenOptions>? _useHttpsWithDefaults;

    /// <summary>
    /// Create an uninitialized <see cref="HttpsConfigurationService"/>.
    /// To initialize it later, call <see cref="Initialize"/>.
    /// </summary>
    public HttpsConfigurationService()
    {
    }

    /// <summary>
    /// Create an initialized <see cref="HttpsConfigurationService"/>.
    /// </summary>
    /// <remarks>
    /// In practice, <see cref="Initialize"/> won't be called until it's needed.
    /// </remarks>
    public HttpsConfigurationService(IInitializer initializer)
    {
        _initializer = initializer;
    }

    /// <inheritdoc />
    // If there's an initializer, it *can* be initialized, even though it might not be yet.
    // Use explicit interface implentation so we don't accidentally call it within this class.
    bool IHttpsConfigurationService.IsInitialized => _isInitialized || _initializer is not null;

    /// <inheritdoc/>
    public void Initialize(TlsConfigurationLoader tlsConfigurationLoader)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        _tlsConfigurationLoader = tlsConfigurationLoader;
        _populateMultiplexedTransportFeatures = PopulateMultiplexedTransportFeaturesWorker;
        _useHttpsWithDefaults = UseHttpsWithDefaultsWorker;
    }

    /// <inheritdoc/>
    public void ApplyHttpsConfiguration(
        HttpsConnectionAdapterOptions httpsOptions,
        EndpointConfig endpoint,
        KestrelServerOptions serverOptions,
        CertificateConfig? defaultCertificateConfig,
        ConfigurationReader configurationReader)
    {
        EnsureInitialized();
        _tlsConfigurationLoader.ApplyHttpsConfiguration(httpsOptions, endpoint, serverOptions, defaultCertificateConfig, configurationReader);
    }

    /// <inheritdoc/>
    public ListenOptions UseHttpsWithSni(ListenOptions listenOptions, HttpsConnectionAdapterOptions httpsOptions, EndpointConfig endpoint)
    {
        EnsureInitialized();
        return _tlsConfigurationLoader.UseHttpsWithSni(listenOptions, httpsOptions, endpoint);
    }

    /// <inheritdoc/>
    public CertificateAndConfig? LoadDefaultCertificate(ConfigurationReader configurationReader)
    {
        EnsureInitialized();
        return _tlsConfigurationLoader.LoadDefaultCertificate(configurationReader);
    }

    /// <inheritdoc/>
    public void PopulateMultiplexedTransportFeatures(FeatureCollection features, ListenOptions listenOptions)
    {
        EnsureInitialized();
        _populateMultiplexedTransportFeatures.Invoke(features, listenOptions);
    }

    /// <inheritdoc/>
    public ListenOptions UseHttpsWithDefaults(ListenOptions listenOptions)
    {
        EnsureInitialized();
        return _useHttpsWithDefaults.Invoke(listenOptions);
    }

    /// <summary>
    /// If this instance has not been initialized, initialize it if possible and throw otherwise.
    /// </summary>
    /// <exception cref="InvalidOperationException">If initialization is not possible.</exception>
    [MemberNotNull(nameof(_useHttpsWithDefaults), nameof(_tlsConfigurationLoader), nameof(_populateMultiplexedTransportFeatures))]
    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            if (_initializer is not null)
            {
                _initializer.Initialize(this);
            }
            else
            {
                throw new InvalidOperationException(CoreStrings.NeedHttpsConfiguration);
            }
        }

        Debug.Assert(_useHttpsWithDefaults != null);
        Debug.Assert(_tlsConfigurationLoader != null);
        Debug.Assert(_populateMultiplexedTransportFeatures != null);
    }

    /// <summary>
    /// The initialized implementation of <see cref="PopulateMultiplexedTransportFeatures"/>.
    /// </summary>
    internal static void PopulateMultiplexedTransportFeaturesWorker(FeatureCollection features, ListenOptions listenOptions)
    {
        // HttpsOptions or HttpsCallbackOptions should always be set in production, but it's not set for InMemory tests.
        // The QUIC transport will check if TlsConnectionCallbackOptions is missing.
        if (listenOptions.HttpsOptions != null)
        {
            var sslServerAuthenticationOptions = HttpsConnectionMiddleware.CreateHttp3Options(listenOptions.HttpsOptions);
            features.Set(new TlsConnectionCallbackOptions
            {
                ApplicationProtocols = sslServerAuthenticationOptions.ApplicationProtocols ?? new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                OnConnection = (context, cancellationToken) => ValueTask.FromResult(sslServerAuthenticationOptions),
                OnConnectionState = null,
            });
        }
        else if (listenOptions.HttpsCallbackOptions != null)
        {
            features.Set(new TlsConnectionCallbackOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                OnConnection = (context, cancellationToken) =>
                {
                    return listenOptions.HttpsCallbackOptions.OnConnection(new TlsHandshakeCallbackContext
                    {
                        ClientHelloInfo = context.ClientHelloInfo,
                        CancellationToken = cancellationToken,
                        State = context.State,
                        Connection = new ConnectionContextAdapter(context.Connection),
                    });
                },
                OnConnectionState = listenOptions.HttpsCallbackOptions.OnConnectionState,
            });
        }
    }

    /// <summary>
    /// The initialized implementation of <see cref="UseHttpsWithDefaults"/>.
    /// </summary>
    internal static ListenOptions UseHttpsWithDefaultsWorker(ListenOptions listenOptions)
    {
        return listenOptions.UseHttps();
    }

    /// <summary>
    /// TlsHandshakeCallbackContext.Connection is ConnectionContext but QUIC connection only implements BaseConnectionContext.
    /// </summary>
    private sealed class ConnectionContextAdapter : ConnectionContext
    {
        private readonly BaseConnectionContext _inner;

        public ConnectionContextAdapter(BaseConnectionContext inner) => _inner = inner;

        public override IDuplexPipe Transport
        {
            get => throw new NotSupportedException("Not supported by HTTP/3 connections.");
            set => throw new NotSupportedException("Not supported by HTTP/3 connections.");
        }
        public override string ConnectionId
        {
            get => _inner.ConnectionId;
            set => _inner.ConnectionId = value;
        }
        public override IFeatureCollection Features => _inner.Features;
        public override IDictionary<object, object?> Items
        {
            get => _inner.Items;
            set => _inner.Items = value;
        }
        public override EndPoint? LocalEndPoint
        {
            get => _inner.LocalEndPoint;
            set => _inner.LocalEndPoint = value;
        }
        public override EndPoint? RemoteEndPoint
        {
            get => _inner.RemoteEndPoint;
            set => _inner.RemoteEndPoint = value;
        }
        public override CancellationToken ConnectionClosed
        {
            get => _inner.ConnectionClosed;
            set => _inner.ConnectionClosed = value;
        }
        public override ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    /// <summary>
    /// Register an instance of this type to initialize registered instances of <see cref="HttpsConfigurationService"/>.
    /// </summary>
    internal interface IInitializer
    {
        /// <summary>
        /// Invokes <see cref="IHttpsConfigurationService.Initialize"/>, passing appropriate arguments.
        /// </summary>
        void Initialize(IHttpsConfigurationService httpsConfigurationService);
    }

    /// <inheritdoc/>
    internal sealed class Initializer : IInitializer
    {
        private readonly TlsConfigurationLoader _configurationLoader;

        public Initializer(TlsConfigurationLoader configurationLoader)
        {
            _configurationLoader = configurationLoader;
        }

        /// <inheritdoc/>
        public void Initialize(IHttpsConfigurationService httpsConfigurationService)
        {
            httpsConfigurationService.Initialize(_configurationLoader);
        }
    }
}

