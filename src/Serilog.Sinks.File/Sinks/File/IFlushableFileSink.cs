namespace Serilog.Sinks.File
{
    /// <summary>
    /// Supported by (file-based) sinks that can be explicitly flushed.
    /// </summary>
    public interface IFlushableFileSink
    {
        /// <summary>
        /// Flush buffered contents to disk.
        /// </summary>
        void FlushToDisk();
    }
}
