// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Microsoft.AspNetCore.HttpLogging;

public class W3CLoggerProcessorTests
{
    private const string _versionLine = "#Version: 1.0";
    private const string _defaultFieldsDirective = "#Fields: date time c-ip s-computername s-ip s-port cs-method cs-uri-stem cs-uri-query sc-status time-taken cs-version cs-host cs(User-Agent) cs(Referer)";

    private const string _messageOne = "Message one";
    private const string _messageTwo = "Message two";
    private const string _messageThree = "Message three";
    private const string _messageFour = "Message four";

    private DateTime _today = new DateTime(2021, 01, 01, 12, 00, 00);

    [Fact]
    public async Task WritesToTextFile()
    {
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            string fileName;
            var options = new W3CLoggerOptions()
            {
                LogDirectory = path
            };
            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                fileName = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"));
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(fileName, _messageOne.Length).DefaultTimeout();
            }

            await AssertMessageAsync(fileName, _messageOne);
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }

    [Fact]
    public async Task RollsTextFilesBasedOnDate()
    {
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };
        var tomorrow = _today.AddDays(1);

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var options = new W3CLoggerOptions()
        {
            LogDirectory = path
        };

        try
        {
            string fileNameToday;
            string fileNameTomorrow;

            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);

                fileNameToday = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"));

                await WaitForFile(fileNameToday, _messageOne.Length).DefaultTimeout();

                mockSystemDateTime.Now = tomorrow;
                logger.EnqueueMessage(_messageTwo, options.LoggingFields);

                fileNameTomorrow = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{tomorrow.Year:0000}{tomorrow.Month:00}{tomorrow.Day:00}.0000.txt"));

                await WaitForFile(fileNameTomorrow, _messageTwo.Length).DefaultTimeout();
            }

            await AssertMessageAsync(fileNameToday, _messageOne);
            await AssertMessageAsync(fileNameTomorrow, _messageTwo);
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }

    [Fact]
    public async Task RollsTextFilesBasedOnSize()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            string fileName1;
            string fileName2;
            var mockSystemDateTime = new MockSystemDateTime
            {
                Now = _today
            };
            var options = new W3CLoggerOptions()
            {
                LogDirectory = path,
                FileSizeLimit = 5
            };
            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                logger.EnqueueMessage(_messageTwo, options.LoggingFields);
                fileName1 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"));
                fileName2 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0001.txt"));
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(fileName2, _messageTwo.Length).DefaultTimeout();
            }

            await AssertMessageAsync(fileName1, _messageOne);
            await AssertMessageAsync(fileName2, _messageTwo);
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }

    [Fact]
    public async Task RespectsMaxFileCount()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "randomFile.txt"), "Text");
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };

        try
        {
            string lastFileName;
            var options = new W3CLoggerOptions()
            {
                LogDirectory = path,
                RetainedFileCountLimit = 3,
                FileSizeLimit = 5
            };
            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                for (int i = 0; i < 10; i++)
                {
                    logger.EnqueueMessage(_messageOne, options.LoggingFields);
                }
                lastFileName = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0009.txt"));
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(lastFileName, _messageOne.Length).DefaultTimeout();
                for (int i = 0; i < 6; i++)
                {
                    await WaitForRoll(Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.{i:0000}.txt"))).DefaultTimeout();
                }
            }

            var actualFiles = new DirectoryInfo(path)
                .GetFiles()
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToArray();

            Assert.Equal(4, actualFiles.Length);
            Assert.Equal("randomFile.txt", actualFiles[0]);
            for (int i = 1; i < 4; i++)
            {
                Assert.True((actualFiles[i].StartsWith($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}", StringComparison.InvariantCulture)));
            }
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }

    [Fact]
    public async Task StopsLoggingAfter10000Files()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };

        try
        {
            string lastFileName;
            var options = new W3CLoggerOptions()
            {
                LogDirectory = path,
                FileSizeLimit = 5,
                RetainedFileCountLimit = 10000
            };
            var testSink = new TestSink();
            var testLogger = new TestLoggerFactory(testSink, enabled:true);
            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), testLogger))
            {
                logger.SystemDateTime = mockSystemDateTime;
                for (int i = 0; i < 10000; i++)
                {
                    logger.EnqueueMessage(_messageOne, options.LoggingFields);
                }
                lastFileName = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.9999.txt"));

                // This test can actually take a while, so don't use the 5 second default timeout in debug builds.
                await WaitForFile(lastFileName, _messageOne.Length).TimeoutAfter(TimeSpan.FromSeconds(30));

                // directory is full, no warnings yet
                Assert.Equal(0, testSink.Writes.Count);

                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                await WaitForCondition(() => testSink.Writes.FirstOrDefault()?.EventId.Name == "MaxFilesReached").DefaultTimeout();
            }

            Assert.Equal(10000, new DirectoryInfo(path)
                .GetFiles()
                .ToArray().Length);

            // restarting the logger should do nothing since the folder is still full
            var testSink2 = new TestSink();
            var testLogger2 = new TestLoggerFactory(testSink2, enabled:true);
            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), testLogger2))
            {
                Assert.Equal(0, testSink2.Writes.Count);

                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                await WaitForCondition(() => testSink2.Writes.FirstOrDefault()?.EventId.Name == "MaxFilesReached").DefaultTimeout();
            }
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }

    [Fact]
    public async Task InstancesWriteToSameDirectory()
    {
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);

        try
        {
            var options = new W3CLoggerOptions()
            {
                LogDirectory = path,
                RetainedFileCountLimit = 10,
                FileSizeLimit = 5
            };
            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                for (int i = 0; i < 3; i++)
                {
                    logger.EnqueueMessage(_messageOne, options.LoggingFields);
                }
                var filePath = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0002.txt"));
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(filePath, _messageOne.Length).DefaultTimeout();
            }

            // Second instance should pick up where first one left off
            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                for (int i = 0; i < 3; i++)
                {
                    logger.EnqueueMessage(_messageOne, options.LoggingFields);
                }
                var filePath = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0005.txt"));
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(filePath, _messageOne.Length).DefaultTimeout();
            }

            var actualFiles1 = new DirectoryInfo(path)
                .GetFiles()
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToArray();

            Assert.Equal(6, actualFiles1.Length);
            for (int i = 0; i < 6; i++)
            {
                Assert.Contains($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.{i:0000}.txt", actualFiles1[i]);
            }

            // Third instance should roll to 5 most recent files
            options.RetainedFileCountLimit = 5;
            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0006.txt")), _messageOne.Length).DefaultTimeout();
                await WaitForRoll(Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"))).DefaultTimeout();
                await WaitForRoll(Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0001.txt"))).DefaultTimeout();
            }

            var actualFiles2 = new DirectoryInfo(path)
                .GetFiles()
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToArray();

            Assert.Equal(5, actualFiles2.Length);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.{i + 2:0000}.txt", actualFiles2[i]);
            }
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }

    [Fact]
    public async Task WritesToNewFileOnNewInstance()
    {
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);

        try
        {
            var options = new W3CLoggerOptions()
            {
                LogDirectory = path,
                FileSizeLimit = 5
            };
            var fileName1 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"));
            var fileName2 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0001.txt"));
            var fileName3 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0002.txt"));

            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                logger.EnqueueMessage(_messageTwo, options.LoggingFields);
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(fileName2, _messageTwo.Length).DefaultTimeout();
            }

            // Even with a big enough FileSizeLimit, we still won't try to write to files from a previous instance.
            options.FileSizeLimit = 10000;

            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageThree, options.LoggingFields);
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(fileName3, _messageThree.Length).DefaultTimeout();
            }

            var actualFiles = new DirectoryInfo(path)
                .GetFiles()
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToArray();

            Assert.Equal(3, actualFiles.Length);

            await AssertMessageAsync(fileName1, _messageOne);
            await AssertMessageAsync(fileName2, _messageTwo);
            await AssertMessageAsync(fileName3, _messageThree);
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }
    [Fact]
    public async Task RollsTextFilesWhenFirstLogOfDayIsMissing()
    {
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);

        try
        {
            var options = new W3CLoggerOptions()
            {
                LogDirectory = path,
                FileSizeLimit = 5,
                RetainedFileCountLimit = 2,
            };
            var fileName1 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"));
            var fileName2 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0001.txt"));
            var fileName3 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0002.txt"));
            var fileName4 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0003.txt"));

            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                logger.EnqueueMessage(_messageTwo, options.LoggingFields);
                logger.EnqueueMessage(_messageThree, options.LoggingFields);
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(fileName3, _messageThree.Length).DefaultTimeout();
            }

            // Even with a big enough FileSizeLimit, we still won't try to write to files from a previous instance.
            options.FileSizeLimit = 10000;

            await using (var logger = new W3CLoggerProcessor(new OptionsWrapperMonitor<W3CLoggerOptions>(options), new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageFour, options.LoggingFields);
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(fileName4, _messageFour.Length).DefaultTimeout();
            }

            var actualFiles = new DirectoryInfo(path)
                .GetFiles()
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToArray();

            Assert.Equal(2, actualFiles.Length);

            Assert.False(File.Exists(fileName1));
            Assert.False(File.Exists(fileName2));
            Assert.True(File.Exists(fileName3));
            Assert.True(File.Exists(fileName4));

            await AssertMessageAsync(fileName3, _messageThree);
            await AssertMessageAsync(fileName4, _messageFour);
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }

    [Fact]
    public async Task WritesToNewFileOnLoggingFieldOptionsChange()
    {
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };

        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);

        try
        {
            var options = new W3CLoggerOptions()
            {
                LogDirectory = path,
                LoggingFields = W3CLoggingFields.Time,
                FileSizeLimit = 10000
            };
            var fileName1 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"));
            var fileName2 = Path.Combine(path, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0001.txt"));
            var monitor = new OptionsWrapperMonitor<W3CLoggerOptions>(options);

            await using (var logger = new W3CLoggerProcessor(monitor, new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                await WaitForFile(fileName1, _messageOne.Length).DefaultTimeout();
                options.LoggingFields = W3CLoggingFields.Date;
                logger.EnqueueMessage(_messageTwo, options.LoggingFields);
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(fileName2, _messageTwo.Length).DefaultTimeout();
            }

            var actualFiles = new DirectoryInfo(path)
                .GetFiles()
                .Select(f => f.Name)
                .OrderBy(f => f)
                .ToArray();

            Assert.Equal(2, actualFiles.Length);

            await AssertMessageAsync(fileName1, _messageOne, "#Fields: time");
            await AssertMessageAsync(fileName2, _messageTwo, "#Fields: date");
        }
        finally
        {
            Helpers.DisposeDirectory(path);
        }
    }

    [Fact]
    public async Task WritesToNewFileOnLogDirectoryOptionsChange()
    {
        var mockSystemDateTime = new MockSystemDateTime
        {
            Now = _today
        };

        var path1 = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var path2 = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            Directory.CreateDirectory(path1);
            Directory.CreateDirectory(path2);

            var options = new W3CLoggerOptions()
            {
                LogDirectory = path1,
            };
            var fileName1 = Path.Combine(path1, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"));
            var fileName2 = Path.Combine(path2, FormattableString.Invariant($"{options.FileName}{_today.Year:0000}{_today.Month:00}{_today.Day:00}.0000.txt"));
            var monitor = new OptionsWrapperMonitor<W3CLoggerOptions>(options);

            await using (var logger = new W3CLoggerProcessor(monitor, new HostingEnvironment(), NullLoggerFactory.Instance))
            {
                logger.SystemDateTime = mockSystemDateTime;
                logger.EnqueueMessage(_messageOne, options.LoggingFields);
                await WaitForFile(fileName1, _messageOne.Length).DefaultTimeout();
                options.LogDirectory = path2;
                monitor.InvokeChanged();
                logger.EnqueueMessage(_messageTwo, options.LoggingFields);
                // Pause for a bit before disposing so logger can finish logging
                await WaitForFile(fileName2, _messageTwo.Length).DefaultTimeout();
            }
            await AssertMessageAsync(fileName1, _messageOne);
            await AssertMessageAsync(fileName2, _messageTwo);
        }
        finally
        {
            Helpers.DisposeDirectory(path1);
            Helpers.DisposeDirectory(path2);
        }
    }

    private async Task WaitForFile(string fileName, int length)
    {
        while (!File.Exists(fileName))
        {
            await Task.Delay(100);
        }
        while (true)
        {
            try
            {
                if (File.ReadAllText(fileName).Length >= length)
                {
                    break;
                }
            }
            catch
            {
                // Continue
            }
            await Task.Delay(10);
        }
    }

    private async Task WaitForCondition(Func<bool> waitForLog)
    {
        while (!waitForLog())
        {
            await Task.Delay(10);
        }
    }

    private async Task WaitForRoll(string fileName)
    {
        while (File.Exists(fileName))
        {
            await Task.Delay(100);
        }
    }

    private async Task AssertMessageAsync(string fileName, string message, string fieldsDirective = null)
    {
        Assert.True(File.Exists(fileName));

        using var file = new StreamReader(fileName);

        Assert.Equal(_versionLine, await file.ReadLineAsync().DefaultTimeout());
        Assert.StartsWith("#Start-Date", await file.ReadLineAsync().DefaultTimeout());
        Assert.Equal(fieldsDirective ?? _defaultFieldsDirective, await file.ReadLineAsync().DefaultTimeout());
        Assert.Equal(message, await file.ReadLineAsync().DefaultTimeout());
    }
}
