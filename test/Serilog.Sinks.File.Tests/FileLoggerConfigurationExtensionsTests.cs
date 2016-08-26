using System;
using Serilog;
using Serilog.Sinks.File.Tests.Support;
using Serilog.Tests.Support;
using Xunit;

namespace Serilog.Tests
{
    public class FileLoggerConfigurationExtensionsTests
    {
        const string InvalidPath = "/\\";

        [Fact]
        public void WhenWritingCreationExceptionsAreSuppressed()
        {
            new LoggerConfiguration()
                .WriteTo.File(InvalidPath)
                .CreateLogger();
        }

        [Fact]
        public void WhenAuditingCreationExceptionsPropagate()
        {
            Assert.Throws<ArgumentException>(() =>
                new LoggerConfiguration()
                    .AuditTo.File(InvalidPath)
                    .CreateLogger());
        }

        [Fact]
        public void WhenWritingLoggingExceptionsAreSuppressed()
        {
            using (var tmp = TempFolder.ForCaller())
            using (var log = new LoggerConfiguration()
                .WriteTo.File(new ThrowingLogEventFormatter(), tmp.AllocateFilename())
                .CreateLogger())
            {
                log.Information("Hello");
            }
        }

        [Fact]
        public void WhenAuditingLoggingExceptionsPropagate()
        {
            using (var tmp = TempFolder.ForCaller())
            using (var log = new LoggerConfiguration()
                .AuditTo.File(new ThrowingLogEventFormatter(), tmp.AllocateFilename())
                .CreateLogger())
            {
                var ex = Assert.Throws<AggregateException>(() => log.Information("Hello"));
                Assert.IsType<NotImplementedException>(ex.GetBaseException());
            }
        }
    }
}
