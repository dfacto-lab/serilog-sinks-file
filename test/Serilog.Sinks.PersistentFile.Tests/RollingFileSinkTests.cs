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
                e2 = Some.InformationEvent(e1.Timestamp.AddDays(-1)),
                e3 = Some.InformationEvent(e2.Timestamp.AddDays(-5));

            string pathFormat = "";

            TestRollingEventSequence(
                (pf, wt) =>
                {
                    pathFormat = pf;
                    foreach (var @event in new[] { e1, e2, e3 })
                    {
                        var dummyFile = pf.Replace(".txt", @event.Timestamp.ToString("yyyyMMdd") + ".txt");
                        File.WriteAllText(dummyFile, "");
                    }

                    wt.PersistentFile(pf, retainedFileCountLimit: 2,
                        persistentFileRollingInterval: PersistentFileRollingInterval.Day);
                },
                new[] { e1, e2, e3 },
                files =>
                {
                    Assert.Equal(1, files.Count);
                    Assert.True(System.IO.File.Exists(files[0]));

                    var folder = new FileInfo(pathFormat).Directory?.FullName ?? "";
                    Assert.Equal(2, Directory.GetFiles(folder).Length);
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

                // reverse the array of files as the last one has the oldest data, the one with perstent filename will have the newest data
                Array.Reverse(files);


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
        public void AssemblyVersionIsFixedAt210()
        {
            var assembly = typeof(FileLoggerConfigurationExtensions).GetTypeInfo().Assembly;
            Assert.Equal("2.1.0.0", assembly.GetName().Version.ToString(4));
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
                .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName), retainedFileCountLimit: null,
                    preserveLogFilename: true, persistentFileRollingInterval: PersistentFileRollingInterval.Day)
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
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith(e2.Timestamp.DateTime.ToString("yyyyMMdd") + ".txt"), files[1]);
                Assert.True(files[2].EndsWith(e3.Timestamp.DateTime.ToString("yyyyMMdd") + ".txt"), files[2]);
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
                MakeRunAndWriteLog(temp, out var t1);
                MakeRunAndWriteLog(temp, out _);
                MakeRunAndWriteLog(temp, out _);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(3, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith(t1.ToString("yyyyMMdd") + ".txt"), files[1]);
                Assert.True(files[2].EndsWith(t1.ToString("yyyyMMdd") + "_001.txt"), files[1]);
            }

            void MakeRunAndWriteLog(TempFolder temp, out DateTime timestamp)
            {
                string file = Path.Combine(temp.Path, fileName);

                using (var log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(file, retainedFileCountLimit: null,
                        preserveLogFilename: true, persistentFileRollingInterval: PersistentFileRollingInterval.Day,
                        rollOnEachProcessRun: true)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent();
                    timestamp = e1.Timestamp.DateTime;
                    Clock.SetTestDateTimeNow(timestamp);
                    log.Write(e1);
                }

                File.SetLastWriteTime(file, timestamp);
            }
        }

        [Fact]
        static void LogFilenameRollsCorrectlyWhenRollOnEachProcessRunIsTrue()
        {
            var fileName = "mylogfile.txt";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp, 0, out var t0);
                MakeRunAndWriteLog(temp, 0, out _);
                MakeRunAndWriteLog(temp, 2, out var t1);
                MakeRunAndWriteLog(temp, 2, out _);
                MakeRunAndWriteLog(temp, 3, out var t2);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(5, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith(t0.ToString("yyyyMMddHH") + ".txt"), files[1]);
                Assert.True(files[2].EndsWith(t1.ToString("yyyyMMddHH") + ".txt"), files[2]);
                Assert.True(files[3].EndsWith(t1.ToString("yyyyMMddHH") + "_001.txt"), files[3]);
                Assert.True(files[4].EndsWith(t2.ToString("yyyyMMddHH") + ".txt"), files[4]);
            }

            void MakeRunAndWriteLog(TempFolder temp, int hoursToAdd, out DateTime timestamp)
            {
                string file = Path.Combine(temp.Path, fileName);

                using (var log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(file, retainedFileCountLimit: null,
                        preserveLogFilename: true, persistentFileRollingInterval: PersistentFileRollingInterval.Hour,
                        rollOnEachProcessRun: true)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent();
                    timestamp = e1.Timestamp.DateTime.AddHours(hoursToAdd);
                    Clock.SetTestDateTimeNow(timestamp);
                    log.Write(e1);
                }

                File.SetLastWriteTime(file, timestamp);
            }
        }

        [Fact]
        static void LogFilenameRollsCorrectlyWhenRollOnEachProcessRunAndUseLastWriteAsTimestampAreTrue()
        {
            var fileName = "mylogfile.txt";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp, 0, out var t0);
                MakeRunAndWriteLog(temp, 0, out _);
                MakeRunAndWriteLog(temp, 2, out var t1);
                MakeRunAndWriteLog(temp, 2, out _);
                MakeRunAndWriteLog(temp, 3, out _);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                Assert.Equal(5, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith(t0.ToString("yyyyMMddHH") + ".txt"), string.Join(Environment.NewLine, files));
                Assert.True(files[2].EndsWith(t0.ToString("yyyyMMddHH") + "_001.txt"), string.Join(Environment.NewLine, files));
                Assert.True(files[3].EndsWith(t1.ToString("yyyyMMddHH") + ".txt"), string.Join(Environment.NewLine, files));
                Assert.True(files[4].EndsWith(t1.ToString("yyyyMMddHH") + "_001.txt"), string.Join(Environment.NewLine, files));
            }

            void MakeRunAndWriteLog(TempFolder temp, int hoursToAdd, out DateTime timestamp)
            {
                string file = Path.Combine(temp.Path, fileName);

                using (var log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(file, retainedFileCountLimit: null,
                        preserveLogFilename: true, persistentFileRollingInterval: PersistentFileRollingInterval.Hour,
                        rollOnEachProcessRun: true, useLastWriteAsTimestamp: true)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent(DateTimeOffset.Parse("2021-06-03"));
                    timestamp = e1.Timestamp.DateTime.AddHours(hoursToAdd);
                    Clock.SetTestDateTimeNow(timestamp);
                    log.Write(e1);
                }

                File.SetLastWriteTime(file, timestamp);
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

        [Fact]
        public void HighestIndexShouldBeOneLessThanRetaindFileCountLimit()
        {
            var fileName = Some.String() + ".txt";
            using (var temp = new TempFolder())
            using (var log = new LoggerConfiguration()
                .WriteTo.PersistentFile(Path.Combine(temp.Path, fileName), rollOnFileSizeLimit: true, fileSizeLimitBytes: 1, preserveLogFilename: true, retainedFileCountLimit: 3)
                .CreateLogger())
            {
                LogEvent e1 = Some.InformationEvent(),
                    e2 = Some.InformationEvent(e1.Timestamp),
                    e3 = Some.InformationEvent(e2.Timestamp),
                    e4 = Some.InformationEvent(e3.Timestamp);

                log.Write(e1); log.Write(e2); log.Write(e3); log.Write(e4);

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
        public void HighestIndexRolloverWithTimeShouldBeTwoLessThanRetaindFileCountLimit()
        {
            var fileName = Some.String() + ".txt";
            using (var temp = new TempFolder())
            {
                MakeRunAndWriteLog(temp, 0);
                MakeRunAndWriteLog(temp, 0);
                MakeRunAndWriteLog(temp, 2);
                MakeRunAndWriteLog(temp, 2);
                MakeRunAndWriteLog(temp, 4);
                MakeRunAndWriteLog(temp, 4);
                MakeRunAndWriteLog(temp, 6);

                var files = Directory.GetFiles(temp.Path)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                Assert.Equal(3, files.Length);
                Assert.True(files[0].EndsWith(fileName), files[0]);
                Assert.True(files[1].EndsWith("04.txt"), files[1]);
                Assert.True(files[2].EndsWith("04_001.txt"), files[2]);
            }

            void MakeRunAndWriteLog(TempFolder temp, int hoursToAdd)
            {
                string file = Path.Combine(temp.Path, fileName);
                DateTime timestamp;

                using (var log = new LoggerConfiguration()
                    .WriteTo.PersistentFile(file, retainedFileCountLimit: 3,
                        preserveLogFilename: true, persistentFileRollingInterval: PersistentFileRollingInterval.Hour, fileSizeLimitBytes: 1, rollOnFileSizeLimit: true,
                        rollOnEachProcessRun: true, useLastWriteAsTimestamp: true)
                    .CreateLogger())
                {
                    var e1 = Some.InformationEvent(DateTimeOffset.Parse("2021-06-03"));
                    timestamp = e1.Timestamp.DateTime.AddHours(hoursToAdd);
                    Clock.SetTestDateTimeNow(timestamp);
                    log.Write(e1);
                }

                File.SetLastWriteTime(file, timestamp);
            }
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

            var verified = new HashSet<string>();

            try
            {
                foreach (var @event in events)
                {
                    Clock.SetTestDateTimeNow(@event.Timestamp.DateTime);
                    log.Write(@event);
                    //we have persistent file therefore the current file is always the path
                    Assert.True(System.IO.File.Exists(pathFormat));
                    verified.Add(pathFormat);
                }
            }
            finally
            {
                log.Dispose();
                verifyWritten?.Invoke(verified.ToList());
                Directory.Delete(folder, true);
            }
        }
    }
}
