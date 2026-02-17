using System.Collections.Generic;
using PitWall.Telemetry.Live.Models;

namespace PitWall.Telemetry.Live.Services
{
    /// <summary>
    /// Detects events by comparing consecutive telemetry snapshots.
    /// Each detector is stateful and tracks previous values internally.
    /// </summary>
    public interface IEventDetector
    {
        /// <summary>
        /// Process a new telemetry snapshot and return any detected events.
        /// The detector maintains internal state to compare with previous snapshots.
        /// </summary>
        /// <param name="snapshot">Current telemetry snapshot</param>
        /// <returns>Zero or more detected events</returns>
        IReadOnlyList<TelemetryEvent> Detect(TelemetrySnapshot snapshot);
    }
}
