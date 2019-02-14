
namespace Serilog
{
    using System.IO;

    /// <summary>
    /// Enables hooking into log file lifecycle events
    /// </summary>
    public abstract class FileLifecycleHooks
    {
        /// <summary>
        /// Wraps <paramref name="sourceStream"/> in another stream, such as a GZipStream, then returns the wrapped stream
        /// </summary>
        /// <param name="sourceStream">The source log file stream</param>
        /// <returns>The wrapped stream</returns>
        public abstract Stream Wrap(Stream sourceStream);
    }
}
