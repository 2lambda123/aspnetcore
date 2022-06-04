// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.HttpLogging;

#pragma warning disable CA1852 // Seal internal types
internal class W3CLoggerProcessor : FileLoggerProcessor
#pragma warning restore CA1852 // Seal internal types
{
    private const string _fieldDirectiveStart = "#Fields:";

    // This will make parsing the fields directive easier if we ever decide to do it.
    private static readonly FieldNameEntry[] _fieldNameEntries = new FieldNameEntry[]
    {
        new(W3CLoggingFields.Date, "date"),
        new(W3CLoggingFields.Time, "time"),
        new(W3CLoggingFields.ClientIpAddress, "c-ip"),
        new(W3CLoggingFields.UserName, "cs-username"),
        new(W3CLoggingFields.ServerName, "s-computername"),
        new(W3CLoggingFields.ServerIpAddress, "s-ip"),
        new(W3CLoggingFields.ServerPort, "s-port"),
        new(W3CLoggingFields.Method, "cs-method"),
        new(W3CLoggingFields.UriStem, "cs-uri-stem"),
        new(W3CLoggingFields.UriQuery, "cs-uri-query"),
        new(W3CLoggingFields.ProtocolStatus, "sc-status"),
        new(W3CLoggingFields.TimeTaken, "time-taken"),
        new(W3CLoggingFields.ProtocolVersion, "cs-version"),
        new(W3CLoggingFields.Host, "cs-host"),
        new(W3CLoggingFields.UserAgent, "cs(User-Agent)"),
        new(W3CLoggingFields.Cookie, "cs(Cookie)"),
        new(W3CLoggingFields.Referer, "cs(Referer)"),
    };

    public W3CLoggerProcessor(IOptionsMonitor<W3CLoggerOptions> options, IHostEnvironment environment, ILoggerFactory factory)
        : base(options, environment, factory)
    {
    }

    public void Log(string[] elements, W3CLoggingFields loggingFields)
    {
        EnqueueMessage(Format(elements, loggingFields), loggingFields);
    }

    private static string Format(string[] elements, W3CLoggingFields loggingFields)
    {
        // 200 is around the length of an average cookie-less entry
        var sb = new ValueStringBuilder(200);
        var firstElement = true;
        for (var i = 0; i < elements.Length; i++)
        {
            if (loggingFields.HasFlag((W3CLoggingFields)(1 << i)))
            {
                if (!firstElement)
                {
                    sb.Append(' ');
                }
                else
                {
                    firstElement = false;
                }
                // If the element was not logged, or was the empty string, we log it as a dash
                if (string.IsNullOrEmpty(elements[i]))
                {
                    sb.Append('-');
                }
                else
                {
                    sb.Append(elements[i]);
                }
            }
        }
        return sb.ToString();
    }

    public override async Task OnFirstWrite(StreamWriter streamWriter, W3CLoggingFields loggingFields, CancellationToken cancellationToken)
    {
        await WriteMessageAsync("#Version: 1.0", streamWriter, cancellationToken);

        await WriteMessageAsync("#Start-Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), streamWriter, cancellationToken);

        await WriteMessageAsync(GetFieldsDirective(loggingFields), streamWriter, cancellationToken);
    }

    private static string GetFieldsDirective(W3CLoggingFields loggingFields)
    {
        // 152 is the length of the default fields directive
        var sb = new ValueStringBuilder(152);
        sb.Append(_fieldDirectiveStart);

        foreach (var entry in _fieldNameEntries)
        {
            if (loggingFields.HasFlag(entry.Field))
            {
                sb.Append(' ');
                sb.Append(entry.Name);
            }
        }

        return sb.ToString();
    }

    private struct FieldNameEntry
    {
        public FieldNameEntry(W3CLoggingFields field, string name) => (Field, Name) = (field, name);
        public W3CLoggingFields Field { get; }
        public string Name { get; }
    }
}
