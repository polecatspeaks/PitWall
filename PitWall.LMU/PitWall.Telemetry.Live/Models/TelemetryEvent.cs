using System;

namespace PitWall.Telemetry.Live.Models
{
    /// <summary>
    /// Represents a telemetry event detected by analyzing snapshot state changes.
    /// Events include lap transitions, pit stops, damage, and flag changes.
    /// Written to the live_events table via <see cref="Services.ITelemetryWriter.WriteEventAsync"/>.
    /// </summary>
    public class TelemetryEvent
    {
        /// <summary>Session ID where the event occurred</summary>
        public string SessionId { get; init; } = string.Empty;

        /// <summary>Vehicle that triggered the event</summary>
        public int VehicleId { get; init; }

        /// <summary>UTC timestamp of the event</summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// Event type identifier. Standard values:
        /// lap_complete, pit_entry, pit_exit, damage, flat_tire, wheel_detached, flag_change
        /// </summary>
        public string EventType { get; init; } = string.Empty;

        /// <summary>JSON-serialized event-specific data</summary>
        public string EventDataJson { get; init; } = "{}";
    }
}
