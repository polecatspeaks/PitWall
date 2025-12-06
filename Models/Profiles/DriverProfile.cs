using System;
using System.Collections.Generic;

namespace PitWall.Models.Profiles
{
    /// <summary>
    /// Root level of hierarchical profile structure
    /// Represents a driver and all their data across different cars and tracks
    /// </summary>
    public class DriverProfile
    {
        /// <summary>
        /// Unique driver identifier
        /// </summary>
        public string DriverId { get; set; } = "";

        /// <summary>
        /// Driver display name
        /// </summary>
        public string DriverName { get; set; } = "";

        /// <summary>
        /// iRacing style number or custom rating
        /// </summary>
        public int Style { get; set; }

        /// <summary>
        /// Total number of sessions across all cars/tracks
        /// </summary>
        public int SessionsCompleted { get; set; }

        /// <summary>
        /// When this profile was last updated
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// Overall confidence in driver profile (0.0-1.0)
        /// Aggregate of all car/track confidence values
        /// </summary>
        public float Confidence { get; set; }

        /// <summary>
        /// Whether profile data is stale (no recent sessions)
        /// </summary>
        public bool IsStale { get; set; }

        /// <summary>
        /// Date of most recent session anywhere (car+track independent)
        /// </summary>
        public DateTime? LastSessionDate { get; set; }

        /// <summary>
        /// Car profiles for cars driven by this driver
        /// Hierarchy: Driver -> Car -> Track
        /// </summary>
        public List<CarProfile> CarProfiles { get; set; } = new();
    }
}
