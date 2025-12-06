using System;

namespace PitWall.Models.Profiles
{
    /// <summary>
    /// Leaf level of hierarchical profile structure
    /// Represents aggregated telemetry data for a driver+car+track combination
    /// </summary>
    public class TrackProfile
    {
        /// <summary>
        /// Unique track identifier
        /// </summary>
        public string TrackId { get; set; } = "";

        /// <summary>
        /// Track display name
        /// </summary>
        public string TrackName { get; set; } = "";

        /// <summary>
        /// Average fuel consumption per lap (gallons)
        /// </summary>
        public float AvgFuelPerLap { get; set; }

        /// <summary>
        /// Typical lap time for this driver+car+track
        /// </summary>
        public TimeSpan AvgLapTime { get; set; }

        /// <summary>
        /// Standard deviation of lap times (seconds)
        /// Measure of consistency
        /// </summary>
        public float LapTimeStdDev { get; set; }

        /// <summary>
        /// Typical tyre wear per lap (0.0-1.0)
        /// </summary>
        public float TypicalTyreDegradation { get; set; }

        /// <summary>
        /// Number of sessions completed at this track in this car
        /// </summary>
        public int SessionsCompleted { get; set; }

        /// <summary>
        /// Total laps completed at this track
        /// </summary>
        public int LapCount { get; set; }

        /// <summary>
        /// Confidence in this profile (0.0-1.0)
        /// Based on recency, sample size, consistency, session count
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// When this profile was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Whether profile data is stale (no recent sessions)
        /// </summary>
        public bool IsStale { get; set; }

        /// <summary>
        /// Date of most recent session at this track
        /// </summary>
        public DateTime? LastSessionDate { get; set; }
    }
}
