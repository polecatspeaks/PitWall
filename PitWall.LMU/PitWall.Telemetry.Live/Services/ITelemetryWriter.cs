using System;
using System.Threading.Tasks;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Interface for writing telemetry data to persistent storage.
    /// Supports batched writes for high-throughput telemetry ingestion.
    /// </summary>
    public interface ITelemetryWriter : IAsyncDisposable
    {
        /// <summary>
        /// Write session metadata (called once per session start).
        /// </summary>
        Task WriteSessionAsync(TelemetrySnapshot snapshot);

        /// <summary>
        /// Write a telemetry sample. Samples are batched internally
        /// and flushed based on batch size or time interval.
        /// </summary>
        Task WriteSampleAsync(TelemetrySnapshot snapshot);

        /// <summary>
        /// Write an event (damage, flag change, pit entry/exit, etc.).
        /// Events are written immediately (not batched).
        /// </summary>
        Task WriteEventAsync(string sessionId, int vehicleId, string eventType, string eventDataJson);

        /// <summary>
        /// Force flush any pending batched samples to storage.
        /// </summary>
        Task FlushAsync();

        /// <summary>
        /// Number of samples currently pending in the batch buffer.
        /// </summary>
        int PendingCount { get; }
    }
}
