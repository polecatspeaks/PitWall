using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Orchestrates the live telemetry pipeline: read from data source → write to storage → broadcast to clients.
    /// Handles mode switching (live/idle), throttling for UI broadcast, and health monitoring.
    /// </summary>
    public class TelemetryPipelineService
    {
        private readonly LiveTelemetryReader _reader;
        private readonly ITelemetryWriter? _writer;
        private readonly ILogger<TelemetryPipelineService> _logger;
        private readonly int _broadcastIntervalMs;
        private string? _currentSessionId;

        /// <summary>
        /// Current operating mode of the pipeline (Idle, Live, or Replay).
        /// </summary>
        public TelemetryMode CurrentMode { get; private set; } = TelemetryMode.Idle;

        /// <summary>
        /// Most recently read telemetry snapshot.
        /// </summary>
        public TelemetrySnapshot? LatestSnapshot { get; private set; }

        /// <summary>
        /// Health metrics from the underlying reader's streaming pipeline.
        /// </summary>
        public TelemetryHealthMetrics HealthMetrics => _reader.HealthMetrics;

        /// <summary>
        /// Creates a new pipeline service.
        /// </summary>
        /// <param name="dataSource">Live telemetry data source (e.g., SharedMemoryDataSource)</param>
        /// <param name="writer">Optional writer for persisting samples to storage</param>
        /// <param name="logger">Optional logger</param>
        /// <param name="readIntervalMs">Polling interval for reading from source (default: 10ms = 100Hz)</param>
        /// <param name="broadcastIntervalMs">Throttle interval for yielding to UI (default: 100ms = 10Hz)</param>
        /// <param name="channelCapacity">Bounded channel capacity (default: 1000)</param>
        public TelemetryPipelineService(
            ITelemetryDataSource dataSource,
            ITelemetryWriter? writer = null,
            ILogger<TelemetryPipelineService>? logger = null,
            int readIntervalMs = 10,
            int broadcastIntervalMs = 100,
            int channelCapacity = 1000)
        {
            if (dataSource == null) throw new ArgumentNullException(nameof(dataSource));
            _reader = new LiveTelemetryReader(dataSource, readIntervalMs: readIntervalMs, channelCapacity: channelCapacity);
            _writer = writer;
            _logger = logger ?? NullLogger<TelemetryPipelineService>.Instance;
            _broadcastIntervalMs = broadcastIntervalMs;
        }

        /// <summary>
        /// Stream telemetry snapshots throttled to the broadcast interval.
        /// Each snapshot is also persisted to the writer (if configured).
        /// Mode switches to Live when data flows, back to Idle when cancelled.
        /// </summary>
        /// <param name="cancellationToken">Token to stop streaming</param>
        /// <returns>Throttled stream of telemetry snapshots for WebSocket broadcast</returns>
        public async IAsyncEnumerable<TelemetrySnapshot> StreamForBroadcastAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Pipeline starting. Broadcast interval: {IntervalMs}ms", _broadcastIntervalMs);
            var lastBroadcast = DateTimeOffset.MinValue;
            bool sessionWritten = false;

            try
            {
                await foreach (var snapshot in _reader.StreamAsync(cancellationToken))
                {
                    // Update state
                    LatestSnapshot = snapshot;
                    CurrentMode = TelemetryMode.Live;

                    // Write session metadata on first snapshot (or on session change)
                    if (_writer != null && (!sessionWritten || snapshot.SessionId != _currentSessionId))
                    {
                        try
                        {
                            await _writer.WriteSessionAsync(snapshot);
                            _currentSessionId = snapshot.SessionId;
                            sessionWritten = true;
                            _logger.LogInformation("Session started: {SessionId}", snapshot.SessionId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to write session metadata");
                        }
                    }

                    // Write sample to persistent storage
                    if (_writer != null)
                    {
                        try
                        {
                            await _writer.WriteSampleAsync(snapshot);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to write telemetry sample");
                        }
                    }

                    // Throttle broadcast to configured interval
                    var now = DateTimeOffset.UtcNow;
                    if ((now - lastBroadcast).TotalMilliseconds >= _broadcastIntervalMs)
                    {
                        lastBroadcast = now;
                        yield return snapshot;
                    }
                }
            }
            finally
            {
                CurrentMode = TelemetryMode.Idle;
                _logger.LogInformation("Pipeline stopped. Mode: Idle");

                // Flush any pending writes
                if (_writer != null)
                {
                    try
                    {
                        await _writer.FlushAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error flushing writer on shutdown");
                    }
                }
            }
        }
    }
}
