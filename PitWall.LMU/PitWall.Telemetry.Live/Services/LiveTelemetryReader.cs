using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Reads live telemetry data from LMU shared memory.
    /// Supports single-read (<see cref="ReadAsync"/>) and continuous streaming
    /// (<see cref="StreamAsync"/>) with bounded Channel backpressure and health monitoring.
    /// </summary>
    public class LiveTelemetryReader
    {
        private readonly ITelemetryDataSource _dataSource;
        private readonly ILogger<LiveTelemetryReader> _logger;
        private readonly int _readIntervalMs;
        private readonly int _channelCapacity;

        /// <summary>
        /// Health metrics for the streaming pipeline (reads/sec, failures, queue depth).
        /// </summary>
        public TelemetryHealthMetrics HealthMetrics { get; } = new();

        /// <summary>
        /// Create a LiveTelemetryReader with a custom data source (for testing).
        /// </summary>
        /// <param name="dataSource">Telemetry data source to read from</param>
        /// <param name="logger">Optional logger</param>
        /// <param name="readIntervalMs">Polling interval in ms for streaming (default: 10ms = 100Hz)</param>
        /// <param name="channelCapacity">Bounded channel capacity for backpressure (default: 1000)</param>
        public LiveTelemetryReader(
            ITelemetryDataSource dataSource,
            ILogger<LiveTelemetryReader>? logger = null,
            int readIntervalMs = 10,
            int channelCapacity = 1000)
        {
            _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            _logger = logger ?? NullLogger<LiveTelemetryReader>.Instance;
            _readIntervalMs = readIntervalMs;
            _channelCapacity = channelCapacity;
        }

        /// <summary>
        /// Read a single telemetry snapshot.
        /// Returns null if data source is not available or an error occurs.
        /// </summary>
        public async Task<TelemetrySnapshot?> ReadAsync()
        {
            try
            {
                // Check if data source is available
                if (!_dataSource.IsAvailable())
                {
                    _logger.LogDebug("Telemetry data source is not available");
                    return null;
                }

                // Read snapshot from source
                var snapshot = await _dataSource.ReadSnapshotAsync();
                
                if (snapshot == null)
                {
                    _logger.LogWarning("Data source returned null snapshot");
                    return null;
                }

                // Ensure snapshot has required fields
                if (snapshot.Timestamp == default)
                {
                    snapshot.Timestamp = DateTime.UtcNow;
                }

                if (string.IsNullOrEmpty(snapshot.SessionId))
                {
                    snapshot.SessionId = Guid.NewGuid().ToString();
                }

                _logger.LogDebug("Successfully read telemetry snapshot at {Timestamp}", snapshot.Timestamp);
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading telemetry snapshot");
                return null;
            }
        }

        /// <summary>
        /// Stream telemetry snapshots continuously via IAsyncEnumerable.
        /// Uses a bounded Channel for backpressure. Polls the data source at the configured interval.
        /// Gracefully stops when the cancellation token is triggered or when the consumer breaks out.
        /// </summary>
        /// <param name="cancellationToken">Token to signal stream shutdown</param>
        /// <returns>Continuous stream of telemetry snapshots</returns>
        public async IAsyncEnumerable<TelemetrySnapshot> StreamAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            HealthMetrics.ResetUptime();

            var channel = Channel.CreateBounded<TelemetrySnapshot>(
                new BoundedChannelOptions(_channelCapacity)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleWriter = true,
                    SingleReader = true
                });

            using var producerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var producerTask = ProduceAsync(channel, producerCts.Token);

            try
            {
                await foreach (var snapshot in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    yield return snapshot;
                }
            }
            finally
            {
                // Signal producer to stop when consumer is done
                await producerCts.CancelAsync();

                try
                {
                    await producerTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown
                }
            }
        }

        /// <summary>
        /// Producer loop: polls ITelemetryDataSource and writes snapshots to the channel.
        /// Runs on a background task, isolated from the consumer.
        /// </summary>
        private async Task ProduceAsync(
            Channel<TelemetrySnapshot> channel,
            CancellationToken ct)
        {
            var writer = channel.Writer;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        if (!_dataSource.IsAvailable())
                        {
                            _logger.LogDebug("Data source not available, skipping read");
                            await DelayOrCancel(ct);
                            continue;
                        }

                        var snapshot = await _dataSource.ReadSnapshotAsync();

                        if (snapshot != null)
                        {
                            // Fill defaults
                            if (snapshot.Timestamp == default)
                                snapshot.Timestamp = DateTime.UtcNow;
                            if (string.IsNullOrEmpty(snapshot.SessionId))
                                snapshot.SessionId = Guid.NewGuid().ToString();

                            await writer.WriteAsync(snapshot, ct);
                            HealthMetrics.RecordSuccess();
                        }
                        else
                        {
                            HealthMetrics.RecordFailure();
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading telemetry in streaming loop");
                        HealthMetrics.RecordFailure();
                    }

                    // Update queue depth from the reader side
                    if (channel.Reader.CanCount)
                    {
                        HealthMetrics.UpdateQueueDepth(channel.Reader.Count);
                    }

                    await DelayOrCancel(ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            finally
            {
                writer.Complete();
            }
        }

        /// <summary>
        /// Delay for the configured read interval, returning immediately if cancelled.
        /// </summary>
        private async Task DelayOrCancel(CancellationToken ct)
        {
            try
            {
                await Task.Delay(_readIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }
    }
}
