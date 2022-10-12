// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Primitives;

namespace MinimalSample;

public sealed class DynamicEndpointDataSource : EndpointDataSource, IDisposable
{
    private readonly PeriodicTimer _timer;
    private readonly Task _timerTask;

    private Endpoint[] _endpoints = Array.Empty<Endpoint>();
    private CancellationTokenSource _cts = new();

    public DynamicEndpointDataSource()
    {
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        _timerTask = TimerLoop();
    }

    public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

    public async Task TimerLoop()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            var newEndpoints = new Endpoint[_endpoints.Length + 1];
            Array.Copy(_endpoints, 0, newEndpoints, 0, _endpoints.Length);

            newEndpoints[_endpoints.Length] = CreateDynamicRouteEndpoint(_endpoints.Length);

            _endpoints = newEndpoints;
            var oldCts = _cts;
            _cts = new CancellationTokenSource();
            oldCts.Cancel();
        }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _timerTask.GetAwaiter().GetResult();
    }

    public override IChangeToken GetChangeToken()
    {
        return new CancellationChangeToken(_cts.Token);
    }

    private static RouteEndpoint CreateDynamicRouteEndpoint(int id)
    {
        var displayName = $"Dynamic endpoint #{id}";
        var metadata = new EndpointMetadataCollection(new[] { new RouteNameMetadata(displayName) });

        return new RouteEndpoint(
            context => context.Response.WriteAsync(displayName),
            RoutePatternFactory.Parse($"/dynamic/{id}"),
            order: 0, metadata, displayName);
    }
}
