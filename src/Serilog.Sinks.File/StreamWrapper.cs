using System.IO;

namespace Serilog
{
    /// <summary>
    /// Wraps the log file's output stream in another stream, such as a GZipStream
    /// </summary>
    public abstract class StreamWrapper
    {
        /// <summary>
        /// Wraps <paramref name="sourceStream"/> in another stream, such as a GZipStream, then returns the wrapped stream
        /// </summary>
        /// <param name="sourceStream">The source log file stream</param>
        /// <returns>The wrapped stream</returns>
        public abstract Stream Wrap(Stream sourceStream);
    }
}
