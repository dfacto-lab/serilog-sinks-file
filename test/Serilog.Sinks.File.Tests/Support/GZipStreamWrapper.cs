using System.IO;
using System.IO.Compression;

namespace Serilog.Sinks.File.Tests.Support
{
    /// <inheritdoc />
    /// <summary>
    /// Demonstrates the use of <seealso cref="T:Serilog.StreamWrapper" />, by compressing log output using GZip
    /// </summary>
    public class GZipStreamWrapper : StreamWrapper
    {
        readonly int _bufferSize;

        public GZipStreamWrapper(int bufferSize = 1024 * 32)
        {
            _bufferSize = bufferSize;
        }

        public override Stream Wrap(Stream sourceStream)
        {
            var compressStream = new GZipStream(sourceStream, CompressionMode.Compress);
            return new BufferedStream(compressStream, _bufferSize);
        }
    }
}
