// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.Metrics;

namespace Microsoft.AspNetCore.Hosting;

internal sealed class HostingMetrics : IDisposable
{
    public const string MeterName = "Microsoft.AspNetCore.Hosting";

    private readonly Meter _meter;
    private readonly UpDownCounter<long> _currentRequestsCounter;
    private readonly Histogram<double> _requestDuration;

    public HostingMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _currentRequestsCounter = _meter.CreateUpDownCounter<long>(
            "current-requests",
            description: "Number of HTTP requests that are currently active on the server.");

        _requestDuration = _meter.CreateHistogram<double>(
            "request-duration",
            unit: "s",
            description: "The duration of HTTP requests on the server.");
    }

    // Note: Calling code checks whether counter is enabled.
    public void RequestStart(bool isHttps, string scheme, string method, HostString host)
    {
        // Tags must match request end.
        var tags = new TagList();
        InitializeRequestTags(ref tags, isHttps, scheme, method, host);
        _currentRequestsCounter.Add(1, tags);
    }

    public void RequestEnd(string protocol, bool isHttps, string scheme, string method, HostString host, string? route, int statusCode, Exception? exception, List<KeyValuePair<string, object?>>? customTags, long startTimestamp, long currentTimestamp)
    {
        var tags = new TagList();
        InitializeRequestTags(ref tags, isHttps, scheme, method, host);

        // Tags must match request start.
        if (_currentRequestsCounter.Enabled)
        {
            _currentRequestsCounter.Add(-1, tags);
        }

        if (_requestDuration.Enabled)
        {
            tags.Add("protocol", protocol);

            // Add information gathered during request.
            tags.Add("status-code", GetBoxedStatusCode(statusCode));
            if (route != null)
            {
                tags.Add("route", route);
            }
            // This exception is only present if there is an unhandled exception.
            // An exception caught by ExceptionHandlerMiddleware and DeveloperExceptionMiddleware isn't thrown to here. Instead, those middleware add exception-name to custom tags.
            if (exception != null)
            {
                tags.Add("exception-name", exception.GetType().FullName);
            }
            if (customTags != null)
            {
                for (var i = 0; i < customTags.Count; i++)
                {
                    tags.Add(customTags[i]);
                }
            }

            var duration = Stopwatch.GetElapsedTime(startTimestamp, currentTimestamp);
            _requestDuration.Record(duration.TotalSeconds, tags);
        }
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    public bool IsEnabled() => _currentRequestsCounter.Enabled || _requestDuration.Enabled;

    private static void InitializeRequestTags(ref TagList tags, bool isHttps, string scheme, string method, HostString host)
    {
        tags.Add("scheme", scheme);
        tags.Add("method", method);
        if (host.HasValue)
        {
            tags.Add("host", host.Host);

            // Port is parsed each time it's accessed. Store part in local variable.
            if (host.Port is { } port)
            {
                // Add port tag when not the default value for the current scheme
                if ((isHttps && port != 443) || (!isHttps && port != 80))
                {
                    tags.Add("port", port);
                }
            }
        }
    }

    // Status Codes listed at http://www.iana.org/assignments/http-status-codes/http-status-codes.xhtml
    private static readonly FrozenDictionary<int, object> BoxedStatusCodes = FrozenDictionary.ToFrozenDictionary(new[]
    {
        KeyValuePair.Create<int, object>(100, 100),
        KeyValuePair.Create<int, object>(101, 101),
        KeyValuePair.Create<int, object>(102, 102),

        KeyValuePair.Create<int, object>(200, 200),
        KeyValuePair.Create<int, object>(201, 201),
        KeyValuePair.Create<int, object>(202, 202),
        KeyValuePair.Create<int, object>(203, 203),
        KeyValuePair.Create<int, object>(204, 204),
        KeyValuePair.Create<int, object>(205, 205),
        KeyValuePair.Create<int, object>(206, 206),
        KeyValuePair.Create<int, object>(207, 207),
        KeyValuePair.Create<int, object>(208, 208),
        KeyValuePair.Create<int, object>(226, 226),

        KeyValuePair.Create<int, object>(300, 300),
        KeyValuePair.Create<int, object>(301, 301),
        KeyValuePair.Create<int, object>(302, 302),
        KeyValuePair.Create<int, object>(303, 303),
        KeyValuePair.Create<int, object>(304, 304),
        KeyValuePair.Create<int, object>(305, 305),
        KeyValuePair.Create<int, object>(306, 306),
        KeyValuePair.Create<int, object>(307, 307),
        KeyValuePair.Create<int, object>(308, 308),

        KeyValuePair.Create<int, object>(400, 400),
        KeyValuePair.Create<int, object>(401, 401),
        KeyValuePair.Create<int, object>(402, 402),
        KeyValuePair.Create<int, object>(403, 403),
        KeyValuePair.Create<int, object>(404, 404),
        KeyValuePair.Create<int, object>(405, 405),
        KeyValuePair.Create<int, object>(406, 406),
        KeyValuePair.Create<int, object>(407, 407),
        KeyValuePair.Create<int, object>(408, 408),
        KeyValuePair.Create<int, object>(409, 409),
        KeyValuePair.Create<int, object>(410, 410),
        KeyValuePair.Create<int, object>(411, 411),
        KeyValuePair.Create<int, object>(412, 412),
        KeyValuePair.Create<int, object>(413, 413),
        KeyValuePair.Create<int, object>(414, 414),
        KeyValuePair.Create<int, object>(415, 415),
        KeyValuePair.Create<int, object>(416, 416),
        KeyValuePair.Create<int, object>(417, 417),
        KeyValuePair.Create<int, object>(418, 418),
        KeyValuePair.Create<int, object>(419, 419),
        KeyValuePair.Create<int, object>(421, 421),
        KeyValuePair.Create<int, object>(422, 422),
        KeyValuePair.Create<int, object>(423, 423),
        KeyValuePair.Create<int, object>(424, 424),
        KeyValuePair.Create<int, object>(426, 426),
        KeyValuePair.Create<int, object>(428, 428),
        KeyValuePair.Create<int, object>(429, 429),
        KeyValuePair.Create<int, object>(431, 431),
        KeyValuePair.Create<int, object>(451, 451),
        KeyValuePair.Create<int, object>(499, 499),

        KeyValuePair.Create<int, object>(500, 500),
        KeyValuePair.Create<int, object>(501, 501),
        KeyValuePair.Create<int, object>(502, 502),
        KeyValuePair.Create<int, object>(503, 503),
        KeyValuePair.Create<int, object>(504, 504),
        KeyValuePair.Create<int, object>(505, 505),
        KeyValuePair.Create<int, object>(506, 506),
        KeyValuePair.Create<int, object>(507, 507),
        KeyValuePair.Create<int, object>(508, 508),
        KeyValuePair.Create<int, object>(510, 510),
        KeyValuePair.Create<int, object>(511, 511)
    }, optimizeForReading: true);

    private static object GetBoxedStatusCode(int statusCode)
    {
        if (BoxedStatusCodes.TryGetValue(statusCode, out var result))
        {
            return result;
        }

        return statusCode;
    }
}
