
namespace Serilog
{
    using System.IO;

    /// <summary>
    /// Enables hooking into log file lifecycle events
    /// </summary>
    public abstract class FileLifecycleHooks
    {
        /// <summary>
        /// Wraps <paramref name="underlyingStream"/> in another stream, such as a GZipStream, then returns the wrapped stream
        /// </summary>
        /// <remarks>
        /// Serilog is responsible for disposing of the wrapped stream
        /// </remarks>
        /// <param name="underlyingStream">The underlying log file stream</param>
        /// <returns>The wrapped stream</returns>
        public abstract Stream Wrap(Stream underlyingStream);
    }
}
