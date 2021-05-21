using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.PersistentFile.Tests.Support;
using Xunit;

namespace Serilog.Sinks.PersistentFile.Tests
{
    public class RollingFileSinkTests
    {
        [Fact]
        public void LogEventsAreEmittedToTheFileNamedAccordingToTheEventTimestamp()
        {
            TestRollingEventSequence(Some.InformationEvent());
        }

        [Fact]
        public void EventsAreWrittenWhenSharingIsEnabled()
        {
            TestRollingEventSequence(
                (pf, wt) => wt.PersistentFile(pf, shared: true, persistentFileRollingInterval: PersistentFileRollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void EventsAreWrittenWhenBufferingIsEnabled()
        {
            TestRollingEventSequence(
                (pf, wt) => wt.PersistentFile(pf, buffered: true, persistentFileRollingInterval: PersistentFileRollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void EventsAreWrittenWhenDiskFlushingIsEnabled()
        {
            // Doesn't test flushing, but ensures we haven't broken basic logging
            TestRollingEventSequence(
                (pf, wt) => wt.PersistentFile(pf, flushToDiskInterval: TimeSpan.FromMilliseconds(50), persistentFileRollingInterval: PersistentFileRollingInterval.Day),
                new[] { Some.InformationEvent() });
        }

        [Fact]
        public void WhenTheDateChangesTheCorrectFileIsWritten()
        {
            var e1 = Some.InformationEvent();
            var e2 = Some.InformationEvent(e1.Timestamp.AddDays(1));
            TestRollingEventSequence(e1, e2);
        }

        [Fact]
        public void WhenRetentionCountIsSetOldFilesAreDeleted()
        {
            LogEvent e1 = Some.InformationEvent(),
                e2 = Some.InformationEvent(e1.Timestamp.AddDays(1)),
                e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));

            TestRollingEventSequence(
                (pf, wt) => wt.PersistentFile(pf, retainedFileCountLimit: 2, persistentFileRollingInterval: PersistentFileRollingInterval.Day),
                new[] {e1, e2, e3},
                files =>
                {
                    Assert.Equal(3, files.Count);
                    Assert.True(System.IO.File.Exists(files[0]));
                    Assert.True(!System.IO.File.Exists(files[1]));
                    Assert.True(System.IO.File.Exists(files[2]));
                });
        }

        [Fact]
        public void WhenSizeLimitIsBreachedNewFilesCreated()
        {
            var fileName = Some.String() + ".txt";
            using (var temp = new TempFolder())
            using (var log = new LoggerConfiguration()
                .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1)
                .CreateLogger())
            {
                LogEvent e1 = Some.InformationEvent(),
                    e2 = Some.InformationEvent(e1.Timestamp),
                    e3 = Some.InformationEvent(e1.Timestamp);

                log.Write(e1); log.Write(e2); log.Write(e3);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(3, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith("_001.txt"), files[1]);
                Assert.True(files[2].EndsWith("_002.txt"), files[2]);
            }
        }

        [Fact]
        public void WhenStreamWrapperSpecifiedIsUsedForRolledFiles()
        {
            var gzipWrapper = new GZipHooks();
            var fileName = Some.String() + ".txt";

            using (var temp = new TempFolder())
            {
                string[] files;
                var logEvents = new[]
                {
                    Some.InformationEvent(),
                    Some.InformationEvent(),
                    Some.InformationEvent()
                };

                using (var log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, hooks: gzipWrapper)
                    .CreateLogger())
                {

                    foreach (var logEvent in logEvents)
                    {
                        log.Write(logEvent);
                    }

                    files = Directory.GetFiles(temp.Path)
                        .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    Assert.Equal(3, files.Length);
                    Assert.True(files[0].EndsWith(fileName), files[0]);
                    Assert.True(files[1].EndsWith("_001.txt"), files[1]);
                    Assert.True(files[2].EndsWith("_002.txt"), files[2]);
                }
                //with persistent file name we must reverse the first and last file of the array because, the last file we write to is always the same file
                //sorting by name will put this file at the first place instead of the last.
                var t = files[0];
                for (var i = 0; i < files.Length - 1; i++)
                    files[i] = files[i+1];
                files[files.Length - 1] = t;



                // Ensure the data was written through the wrapping GZipStream, by decompressing and comparing against
                // what we wrote
                for (var i = 0; i < files.Length; i++)
                {
                    using (var textStream = new MemoryStream())
                    {
                        using (var fs = System.IO.File.OpenRead(files[i]))
                        using (var decompressStream = new GZipStream(fs, CompressionMode.Decompress))
                        {
                            decompressStream.CopyTo(textStream);
                        }

                        textStream.Position = 0;
                        var lines = textStream.ReadAllLines();

                        Assert.Equal(1, lines.Count);
                        Assert.True(lines[0].EndsWith(logEvents[i].MessageTemplate.Text));
                    }
                }
            }
        }

        [Fact]
        public void IfTheLogFolderDoesNotExistItWillBeCreated()
        {
            var fileName = Some.String() + "-{Date}.txt";
            var temp = Some.TempFolderPath();
            var folder = Path.Combine(temp, Guid.NewGuid().ToString());
            var pathFormat = Path.Combine(folder, fileName);

            ILogger log = null;

            try
            {
                log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(pathFormat, retainedFileCountLimit: 3, persistentFileRollingInterval: PersistentFileRollingInterval.Day)
                    .CreateLogger();

                log.Write(Some.InformationEvent());

                Assert.True(Directory.Exists(folder));
            }
            finally
            {
                var disposable = (IDisposable)log;
                if (disposable != null) disposable.Dispose();
                Directory.Delete(temp, true);
            }
        }

        [Fact]
        public void AssemblyVersionIsFixedAt200()
        {
            var assembly = typeof(FileLoggerConfigurationExtensions).GetTypeInfo().Assembly;
            Assert.Equal("2.0.0.0", assembly.GetName().Version.ToString(4));
        }

        [Fact]
        public void LogFilenameShouldNotChangeAfterRollOnFileSize()
        {
            var fileName = Some.String() + ".txt";
            using (var temp = new TempFolder())
            using (var log = new LoggerConfiguration()
                .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, preserveLogFilename: true)
                .CreateLogger())
            {
                LogEvent e1 = Some.InformationEvent(),
                    e2 = Some.InformationEvent(e1.Timestamp),
                    e3 = Some.InformationEvent(e1.Timestamp);

                log.Write(e1); log.Write(e2); log.Write(e3);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                Assert.Equal(3, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith("_001.txt"), files[1]);
                Assert.True(files[2].EndsWith("_002.txt"), files[2]);
            }
        }

        //  var e1 = Some.InformationEvent();
        //var e2 = Some.InformationEvent(e1.Timestamp.AddDays(1));
        [Fact]
        static void LogFilenameShouldNotChangeAfterRollOnDate()
        {

            var fileName = "mylogfile.txt";
            using (var temp = new TempFolder())
            using (var log = new LoggerConfiguration()
                .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName),  retainedFileCountLimit: null, preserveLogFilename: true, persistentFileRollingInterval: PersistentFileRollingInterval.Day)
                .CreateLogger())
            {
                LogEvent e1 = Some.InformationEvent(),
                    e2 = Some.InformationEvent(e1.Timestamp.AddDays(1)),
                    e3 = Some.InformationEvent(e2.Timestamp.AddDays(5));
                Clock.SetTestDateTimeNow(e1.Timestamp.DateTime);
                log.Write(e1);
                Clock.SetTestDateTimeNow(e2.Timestamp.DateTime);
                log.Write(e2);
                Clock.SetTestDateTimeNow(e3.Timestamp.DateTime);
                log.Write(e3);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(3, files.Length);
                Console.Out.WriteLine(files[0]);
                Console.Out.WriteLine(files[1]);
                Console.Out.WriteLine(files[2]);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith( e2.Timestamp.ToString("yyyyMMdd")+".txt"), files[1]);
                Assert.True(files[2].EndsWith(e3.Timestamp.ToString("yyyyMMdd")+".txt"), files[2]);
            }
        }

        [Fact]
        static void LogFilenameShouldNotChangeOnMultipleRunsWhenRollOnEachProcessRunIsFalse()
        {
            var fileName = "mylogfile.txt";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp);
                MakeRunAndWriteLog(temp);
                MakeRunAndWriteLog(temp);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(1, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
            }

            void MakeRunAndWriteLog(TempFolder temp)
            {
                using (var log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName), retainedFileCountLimit: null,
                        preserveLogFilename: true, persistentFileRollingInterval: PersistentFileRollingInterval.Day,
                        rollOnEachProcessRun: false)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent();
                    Clock.SetTestDateTimeNow(e1.Timestamp.DateTime);
                    log.Write(e1);
                }
            }
        }

        [Fact]
        static void LogFilenameShouldChangeOnMultipleRunsWhenRollOnEachProcessRunIsTrue()
        {
            var fileName = "mylogfile.txt";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp, out _);
                MakeRunAndWriteLog(temp, out var t1);
                MakeRunAndWriteLog(temp, out _);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(3, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith(t1.ToString("yyyyMMdd")+".txt"), files[1]);
                Assert.True(files[2].EndsWith(t1.ToString("yyyyMMdd")+"_001.txt"), files[1]);
            }

            void MakeRunAndWriteLog(TempFolder temp, out DateTime timestamp)
            {
                using (var log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName), retainedFileCountLimit: null,
                        preserveLogFilename: true, persistentFileRollingInterval: PersistentFileRollingInterval.Day,
                        rollOnEachProcessRun: true)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent();
                    timestamp = e1.Timestamp.DateTime;
                    Clock.SetTestDateTimeNow(timestamp);
                    log.Write(e1);
                }
            }
        }

        [Fact]
        static void TestLogShouldRollWhenOverFlowed()
        {
            var temp = new TempFolder();
            const string fileName = "log.txt";
            for (var i = 0; i < 4; i++)
            {
                using (var log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName), fileSizeLimitBytes: 1000, rollOnFileSizeLimit: true, preserveLogFilename: true)
                    .CreateLogger())
                {
                    var longString = new string('0', 1000);
                    log.Information(longString);
                }
            }
            //we should have four files
            var files = Directory.GetFiles(temp.Path)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Assert.Equal(4, files.Length);
            Assert.True(files[0].EndsWith(fileName), files[0]);
            Assert.True(files[1].EndsWith("_001.txt"), files[1]);
            Assert.True(files[2].EndsWith("_002.txt"), files[2]);
            Assert.True(files[3].EndsWith("_003.txt"), files[2]);
            temp.Dispose();
        }

        static void TestRollingEventSequence(params LogEvent[] events)
        {
            TestRollingEventSequence(
                (pf, wt) => wt.PersistentFile(pf, retainedFileCountLimit: null, persistentFileRollingInterval: PersistentFileRollingInterval.Day),
                events);
        }

        static void TestRollingEventSequence(
            Action<string, LoggerSinkConfiguration> configureFile,
            IEnumerable<LogEvent> events,
            Action<IList<string>> verifyWritten = null)
        {
            var fileName = Some.String() + "-.txt";
            var folder = Some.TempFolderPath();
            var pathFormat = Path.Combine(folder, fileName);

            var config = new LoggerConfiguration();
            configureFile(pathFormat, config.WriteTo);
            var log = config.CreateLogger();

            var verified = new List<string>();

            try
            {
                var count = 0;
                foreach (var @event in events)
                {
                    Clock.SetTestDateTimeNow(@event.Timestamp.DateTime);
                    log.Write(@event);
                    //we have persistent file therefore the current file is always the path
                    var expected = pathFormat;
                    Assert.True(System.IO.File.Exists(expected));
                    if (count > 0)
                    {
                        expected = pathFormat.Replace(".txt", @event.Timestamp.ToString("yyyyMMdd") + ".txt");
                        Assert.True(System.IO.File.Exists(expected));
                    }

                    verified.Add(expected);
                    count++;
                }
            }
            finally
            {
                log.Dispose();
                verifyWritten?.Invoke(verified);
                Directory.Delete(folder, true);
            }
        }
    }
}
