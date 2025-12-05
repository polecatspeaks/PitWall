using System;
using System.Collections.Generic;

namespace PitWall.Models
{
    /// <summary>
    /// Driver profile with learned behavior patterns for specific track/car combinations
    /// </summary>
    public class DriverProfile
    {
        public string DriverName { get; set; } = string.Empty;
        public string TrackName { get; set; } = string.Empty;
        public string CarName { get; set; } = string.Empty;
        public double AverageFuelPerLap { get; set; }
        public double TypicalTyreDegradation { get; set; }
        public DrivingStyle Style { get; set; }
        public DateTime LastUpdated { get; set; }
        public int SessionsCompleted { get; set; }
    }

    public enum DrivingStyle
    {
        Unknown,
        Smooth,      // Consistent lap times, gentle inputs
        Aggressive,  // High variance, hard braking
        Mixed        // Changes depending on conditions
    }
}
