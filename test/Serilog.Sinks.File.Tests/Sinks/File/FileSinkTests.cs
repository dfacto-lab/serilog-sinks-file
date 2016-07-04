using System.IO;
using Xunit;
using Serilog.Formatting.Json;
using Serilog.Sinks.File.Tests.Support;
using Serilog.Tests.Support;

namespace Serilog.Sinks.File.Tests
{
    public class FileSinkTests
    {
        [Fact]
        public void FileIsWrittenIfNonexistent()
        {
            using (var tmp = TempFolder.ForCaller())
            {
                var nonexistent = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent("Hello, world!");

                using (var sink = new FileSink(nonexistent, new JsonFormatter(), null))
                {
                    sink.Emit(evt);
                }

                var lines = System.IO.File.ReadAllLines(nonexistent);
                Assert.Contains("Hello, world!", lines[0]);
            }
        }

        [Fact]
        public void FileIsAppendedToWhenAlreadyCreated()
        {
            using (var tmp = TempFolder.ForCaller())
            {
                var path = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent("Hello, world!");

                using (var sink = new FileSink(path, new JsonFormatter(), null))
                {
                    sink.Emit(evt);
                }

                using (var sink = new FileSink(path, new JsonFormatter(), null))
                {
                    sink.Emit(evt);
                }

                var lines = System.IO.File.ReadAllLines(path);
                Assert.Contains("Hello, world!", lines[0]);
                Assert.Contains("Hello, world!", lines[1]);
            }
        }

        [Fact]
        public void WhenLimitIsSpecifiedFileSizeIsRestricted()
        {
            const int maxBytes = 100;

            using (var tmp = TempFolder.ForCaller())
            {
                var path = tmp.AllocateFilename("txt");
                var evt = Some.LogEvent(new string('n', maxBytes + 1));

                using (var sink = new FileSink(path, new JsonFormatter(), maxBytes))
                {
                    sink.Emit(evt);
                }

                var size = new FileInfo(path).Length;
                Assert.True(size > 0);
                Assert.True(size < maxBytes);
            }
        }
    }
}

