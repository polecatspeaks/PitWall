using System;
using System.Diagnostics;
using System.Threading;

namespace PitWall.Telemetry.Live.Models
{
    /// <summary>
    /// Thread-safe health metrics for the telemetry streaming pipeline.
    /// Tracks read rates, failures, and queue depth for monitoring.
    /// </summary>
    public class TelemetryHealthMetrics
    {
        private long _successfulReads;
        private long _failedReads;
        private int _queueDepth;
        private readonly Stopwatch _uptime = Stopwatch.StartNew();

        /// <summary>
        /// Total number of successful telemetry reads.
        /// </summary>
        public long SuccessfulReads => Interlocked.Read(ref _successfulReads);

        /// <summary>
        /// Total number of failed read attempts (exceptions or null responses).
        /// </summary>
        public long FailedReads => Interlocked.Read(ref _failedReads);

        /// <summary>
        /// Total reads (successful + failed).
        /// </summary>
        public long TotalReads => SuccessfulReads + FailedReads;

        /// <summary>
        /// Average successful reads per second since streaming started.
        /// </summary>
        public double ReadsPerSecond
        {
            get
            {
                var elapsed = _uptime.Elapsed.TotalSeconds;
                return elapsed > 0 ? SuccessfulReads / elapsed : 0;
            }
        }

        /// <summary>
        /// Current number of items buffered in the channel queue.
        /// </summary>
        public int QueueDepth => Volatile.Read(ref _queueDepth);

        /// <summary>
        /// Whether the pipeline is lagging (queue depth >= 80% of capacity).
        /// Updated externally based on channel capacity.
        /// </summary>
        public bool IsLagging { get; internal set; }

        /// <summary>
        /// Record a successful read.
        /// </summary>
        internal void RecordSuccess()
        {
            Interlocked.Increment(ref _successfulReads);
        }

        /// <summary>
        /// Record a failed read attempt.
        /// </summary>
        internal void RecordFailure()
        {
            Interlocked.Increment(ref _failedReads);
        }

        /// <summary>
        /// Update the current queue depth.
        /// </summary>
        internal void UpdateQueueDepth(int depth)
        {
            Volatile.Write(ref _queueDepth, depth);
        }

        /// <summary>
        /// Reset the uptime timer (e.g., when a new stream starts).
        /// </summary>
        internal void ResetUptime()
        {
            _uptime.Restart();
        }
    }
}
