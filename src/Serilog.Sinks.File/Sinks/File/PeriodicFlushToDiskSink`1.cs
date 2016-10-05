using System;
using System.Threading;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;

namespace Serilog.Sinks.File
{
    /// <summary>
    /// A sink wrapper that periodically flushes the wrapped sink to disk.
    /// </summary>
    /// <typeparam name="TSink">The type of the wrapped sink.</typeparam>
    public class PeriodicFlushToDiskSink<TSink> : ILogEventSink, IDisposable
        where TSink : ILogEventSink, IFlushableFileSink
    {
        readonly TSink _sink;
        readonly Timer _timer;
        int _flushRequired;

        /// <summary>
        /// Construct a <see cref="PeriodicFlushToDiskSink{TSink}"/> that wraps
        /// <paramref name="sink"/> and flushes it at the specified <paramref name="flushInterval"/>.
        /// </summary>
        /// <param name="sink">The sink to wrap.</param>
        /// <param name="flushInterval">The interval at which to flush the underlying sink.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public PeriodicFlushToDiskSink(TSink sink, TimeSpan flushInterval)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));

            _sink = sink;
            _timer = new Timer(_ => FlushToDisk(), null, flushInterval, flushInterval);
        }

        /// <inheritdoc />
        public void Emit(LogEvent logEvent)
        {
            _sink.Emit(logEvent);
            Interlocked.Exchange(ref _flushRequired, 1);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _timer.Dispose();
            (_sink as IDisposable)?.Dispose();
        }

        void FlushToDisk()
        {
            try
            {
                if (Interlocked.CompareExchange(ref _flushRequired, 0, 1) == 1)
                {
                    // May throw ObjectDisposedException, since we're not trying to synchronize
                    // anything here in the wrapper.
                    _sink.FlushToDisk();
                }
            }
            catch (Exception ex)
            {
                SelfLog.WriteLine("Could not flush the underlying sink to disk: {0}", ex);
            }
        }
    }
}
