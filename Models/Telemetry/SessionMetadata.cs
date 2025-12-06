using System;

namespace PitWall.Models.Telemetry
{
    /// <summary>
    /// Metadata for a single telemetry session (race, practice, qualifying)
    /// Represents header information extracted from IBT file
    /// </summary>
    public class SessionMetadata
    {
        /// <summary>
        /// Unique session identifier
        /// </summary>
        public string SessionId { get; set; } = "";

        /// <summary>
        /// When the session occurred (UTC)
        /// </summary>
        public DateTime SessionDate { get; set; }

        /// <summary>
        /// Session type: Race, Practice, Qualifying, Warmup
        /// </summary>
        public string SessionType { get; set; } = "";

        /// <summary>
        /// Driver iRacing ID or custom identifier
        /// </summary>
        public string DriverId { get; set; } = "";

        /// <summary>
        /// Driver display name
        /// </summary>
        public string DriverName { get; set; } = "";

        /// <summary>
        /// Car model ID from iRacing
        /// </summary>
        public string CarId { get; set; } = "";

        /// <summary>
        /// Car display name
        /// </summary>
        public string CarName { get; set; } = "";

        /// <summary>
        /// Track ID from iRacing
        /// </summary>
        public string TrackId { get; set; } = "";

        /// <summary>
        /// Track display name
        /// </summary>
        public string TrackName { get; set; } = "";

        /// <summary>
        /// Number of laps completed in session
        /// </summary>
        public int LapCount { get; set; }

        /// <summary>
        /// Total session duration
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Total fuel consumed during session
        /// </summary>
        public float TotalFuelUsed { get; set; }

        /// <summary>
        /// Average fuel per lap
        /// </summary>
        public float AvgFuelPerLap { get; set; }

        /// <summary>
        /// Session temperature (Celsius)
        /// </summary>
        public float AmbientTemp { get; set; }

        /// <summary>
        /// Track temperature (Celsius)
        /// </summary>
        public float TrackTemp { get; set; }

        /// <summary>
        /// When this metadata was extracted from IBT file
        /// </summary>
        public DateTime ProcessedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Path to the source IBT file
        /// </summary>
        public string? SourceFilePath { get; set; }
    }
}
