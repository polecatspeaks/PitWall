using System;
using System.Collections.Generic;

namespace PitWall.Models.Profiles
{
    /// <summary>
    /// Second level of hierarchical profile structure
    /// Represents a specific car driven by a driver, across multiple tracks
    /// </summary>
    public class CarProfile
    {
        /// <summary>
        /// Unique car identifier
        /// </summary>
        public string CarId { get; set; } = "";

        /// <summary>
        /// Car display name
        /// </summary>
        public string CarName { get; set; } = "";

        /// <summary>
        /// Total sessions in this car across all tracks
        /// </summary>
        public int SessionsCompleted { get; set; }

        /// <summary>
        /// Average fuel consumption per lap (gallons)
        /// Aggregated from all track data
        /// </summary>
        public float AvgFuelPerLap { get; set; }

        /// <summary>
        /// When this car profile was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Confidence in car profile data (0.0-1.0)
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Whether car profile data is stale
        /// </summary>
        public bool IsStale { get; set; }

        /// <summary>
        /// Date of most recent session in this car
        /// </summary>
        public DateTime? LastSessionDate { get; set; }

        /// <summary>
        /// Track profiles for tracks driven in this car
        /// Hierarchy: Driver -> Car -> Track
        /// </summary>
        public List<TrackProfile> TrackProfiles { get; set; } = new();
    }
}
