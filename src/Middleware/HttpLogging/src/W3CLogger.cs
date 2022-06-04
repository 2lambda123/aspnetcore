// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.HttpLogging;

internal partial class W3CLogger : IAsyncDisposable
{
    private const int _maxQueuedMessages = 1024;
    private const string _fieldDirectiveStart = "#Fields:";

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

    private string _path;
    private string _fileName;
    private int? _maxFileSize;
    private int? _maxRetainedFiles;
    private int _fileNumber;
    private bool _maxFilesReached;
    private TimeSpan _flushInterval;
    private DateTime _today;
    private bool _firstFile = true;
    private W3CLoggingFields? _lastFieldsLogged;

    private readonly IOptionsMonitor<W3CLoggerOptions> _options;
    private readonly BlockingCollection<LogMessage> _messageQueue = new BlockingCollection<LogMessage>(_maxQueuedMessages);
    private readonly ILogger _logger;
    private readonly Task _outputTask;
    private readonly CancellationTokenSource _cancellationTokenSource;

    // Internal to allow for testing
    internal ISystemDateTime SystemDateTime { get; set; } = new SystemDateTime();

    private readonly object _pathLock = new object();

    public W3CLogger(IOptionsMonitor<W3CLoggerOptions> options, IHostEnvironment environment, ILoggerFactory factory)
    {
        _logger = factory.CreateLogger("Microsoft.AspNetCore.HttpLogging.FileLoggerProcessor");

        _options = options;
        var loggerOptions = _options.CurrentValue;

        _path = loggerOptions.LogDirectory;
        // If user supplies no LogDirectory, default to {ContentRoot}/logs.
        // If user supplies a relative path, use {ContentRoot}/{LogDirectory}.
        // If user supplies a full path, use that.
        if (string.IsNullOrEmpty(_path))
        {
            _path = Path.Join(environment.ContentRootPath, "logs");
        }
        else if (!Path.IsPathRooted(_path))
        {
            _path = Path.Join(environment.ContentRootPath, _path);
        }

        _fileName = loggerOptions.FileName;
        _maxFileSize = loggerOptions.FileSizeLimit;
        _maxRetainedFiles = loggerOptions.RetainedFileCountLimit;
        _flushInterval = loggerOptions.FlushInterval;

        _options.OnChange(options =>
        {
            lock (_pathLock)
            {
                // Clear the cached settings.
                loggerOptions = options;

                if (!string.IsNullOrEmpty(loggerOptions.LogDirectory))
                {
                    _path = loggerOptions.LogDirectory;
                }

                _fileName = loggerOptions.FileName;
                _maxFileSize = loggerOptions.FileSizeLimit;
                _maxRetainedFiles = loggerOptions.RetainedFileCountLimit;
                _flushInterval = loggerOptions.FlushInterval;
            }
        });

        _today = SystemDateTime.Now;

        // Start message queue processor
        _cancellationTokenSource = new CancellationTokenSource();
        _outputTask = Task.Run(ProcessLogQueue);
    }

    public void LogElements(string[] elements, W3CLoggingFields loggingFields)
    {
        EnqueueMessage(Format(elements, loggingFields), loggingFields);
    }

    // internal for testing
    internal void EnqueueMessage(string message, W3CLoggingFields loggingFields)
    {
        if (!_messageQueue.IsAddingCompleted)
        {
            try
            {
                _messageQueue.Add(new LogMessage(message, loggingFields));
                return;
            }
            catch (InvalidOperationException) { }
        }
    }

    private async Task ProcessLogQueue()
    {
        var currentBatch = new List<LogMessage>();

        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            while (_messageQueue.TryTake(out var message))
            {
                currentBatch.Add(message);
            }
            if (currentBatch.Count > 0)
            {
                try
                {
                    await WriteMessagesAsync(currentBatch, _cancellationTokenSource.Token);
                }
                catch (Exception ex)
                {
                    Log.WriteMessagesFailed(_logger, ex);
                }

                currentBatch.Clear();
            }
            else
            {
                try
                {
                    await Task.Delay(_flushInterval, _cancellationTokenSource.Token);
                }
                catch
                {
                    // Exit if task was canceled
                    return;
                }
            }
        }
    }

    private async Task WriteMessagesAsync(List<LogMessage> messages, CancellationToken cancellationToken)
    {
        // Files are written up to _maxFileSize before rolling to a new file
        DateTime today = SystemDateTime.Now;

        if (!TryCreateDirectory())
        {
            // return early if we fail to create the directory
            return;
        }

        var fullName = GetFullName(today);
        // Don't write to an incomplete file left around by a previous FileLoggerProcessor
        if (_firstFile)
        {
            _fileNumber = GetFirstFileCount(today);
            fullName = GetFullName(today);
            if (_fileNumber >= W3CLoggerOptions.MaxFileCount)
            {
                _maxFilesReached = true;
                // Return early if log directory is already full
                Log.MaxFilesReached(_logger);
                return;
            }
        }

        _firstFile = false;
        if (_maxFilesReached)
        {
            // Return early if we've already logged that today's file limit has been reached.
            // Need to do this check after the call to GetFullName(), since it resets _maxFilesReached
            // when a new day starts.
            return;
        }
        var fileInfo = new FileInfo(fullName);
        var streamWriter = GetStreamWriter(fullName);

        try
        {
            foreach (var message in messages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                fileInfo.Refresh();
                // Roll to new file if _maxFileSize is reached or new fields are being logged.
                // _maxFileSize could be less than the length of the file header - in that case we still write the first log message before rolling.
                if (fileInfo.Exists && (fileInfo.Length > _maxFileSize || LoggingFieldsChanged(message)))
                {
                    streamWriter.Dispose();
                    _fileNumber++;
                    if (_fileNumber >= W3CLoggerOptions.MaxFileCount)
                    {
                        streamWriter = null;
                        _maxFilesReached = true;
                        // Return early if log directory is already full
                        Log.MaxFilesReached(_logger);
                        return;
                    }
                    fullName = GetFullName(today);
                    fileInfo = new FileInfo(fullName);
                    if (!TryCreateDirectory())
                    {
                        streamWriter = null;
                        // return early if we fail to create the directory
                        return;
                    }
                    streamWriter = GetStreamWriter(fullName);
                }

                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    await WriteDirectivesAsync(streamWriter, message.LoggingFields, cancellationToken);
                }

                _lastFieldsLogged = message.LoggingFields;
                await WriteMessageAsync(message.Message, streamWriter, cancellationToken);
            }
        }
        finally
        {
            RollFiles();
            streamWriter?.Dispose();
        }
    }

    private bool LoggingFieldsChanged(LogMessage message) => _lastFieldsLogged is not null && _lastFieldsLogged != message.LoggingFields;

    private bool TryCreateDirectory()
    {
        if (!Directory.Exists(_path))
        {
            try
            {
                Directory.CreateDirectory(_path);
                return true;
            }
            catch (Exception ex)
            {
                Log.CreateDirectoryFailed(_logger, _path, ex);
                return false;
            }
        }
        return true;
    }

    private async Task WriteMessageAsync(string message, StreamWriter streamWriter, CancellationToken cancellationToken)
    {
        OnWrite(message);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        await streamWriter.WriteLineAsync(message.AsMemory(), cancellationToken);
        await streamWriter.FlushAsync();
    }

    // Virtual for testing
    protected virtual void OnWrite(string message) { }
    protected virtual StreamWriter GetStreamWriter(string fileName)
    {
        return File.AppendText(fileName);
    }

    private void RollFiles()
    {
        if (_maxRetainedFiles > 0)
        {
            lock (_pathLock)
            {
                var files = new DirectoryInfo(_path)
                    .GetFiles(_fileName + "*")
                    .OrderByDescending(f => f.Name)
                    .Skip(_maxRetainedFiles.Value);

                foreach (var item in files)
                {
                    item.Delete();
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();
        _messageQueue.CompleteAdding();
        await _outputTask;
        _messageQueue.Dispose();
    }

    private int GetFirstFileCount(DateTime date)
    {
        lock (_pathLock)
        {
            var searchString = FormattableString.Invariant($"{_fileName}{date.Year:0000}{date.Month:00}{date.Day:00}.*.txt");
            var files = new DirectoryInfo(_path)
                .GetFiles(searchString);

            return files.Length == 0
                ? 0
                : files
                    .Max(x => int.TryParse(x.Name.Split('.').ElementAtOrDefault(Index.FromEnd(2)), out var parsed)
                        ? parsed + 1
                        : 0);
        }
    }

    private string GetFullName(DateTime date)
    {
        lock (_pathLock)
        {
            if ((date.Date - _today.Date).Days != 0)
            {
                _today = date;
                _fileNumber = 0;
                _maxFilesReached = false;
            }
            return Path.Combine(_path, FormattableString.Invariant($"{_fileName}{date.Year:0000}{date.Month:00}{date.Day:00}.{_fileNumber:0000}.txt"));
        }
    }

    private async Task WriteDirectivesAsync(StreamWriter streamWriter, W3CLoggingFields loggingFields, CancellationToken cancellationToken)
    {
        await WriteMessageAsync("#Version: 1.0", streamWriter, cancellationToken);
        await WriteMessageAsync("#Start-Date: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), streamWriter, cancellationToken);
        await WriteMessageAsync(GetFieldsDirective(loggingFields), streamWriter, cancellationToken);
    }

    // TODO: Format elements including the trimming and whitespace to '+' conversion while writing to the output file to save string allocations.
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

    private readonly struct LogMessage
    {
        public LogMessage(string message, W3CLoggingFields loggingFields) => (Message, LoggingFields) = (message, loggingFields);
        public string Message { get; }
        public W3CLoggingFields LoggingFields { get;}
    }
    
    private readonly struct FieldNameEntry
    {
        public FieldNameEntry(W3CLoggingFields field, string name) => (Field, Name) = (field, name);
        public W3CLoggingFields Field { get; }
        public string Name { get; }
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "Failed to write all messages.", EventName = "WriteMessagesFailed")]
        public static partial void WriteMessagesFailed(ILogger logger, Exception ex);

        [LoggerMessage(2, LogLevel.Debug, "Failed to create directory {Path}.", EventName = "CreateDirectoryFailed")]
        public static partial void CreateDirectoryFailed(ILogger logger, string path, Exception ex);

        [LoggerMessage(3, LogLevel.Warning, "Limit of 10000 files per day has been reached", EventName = "MaxFilesReached")]
        public static partial void MaxFilesReached(ILogger logger);
    }
}
