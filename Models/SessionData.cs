using System;
using System.Collections.Generic;

namespace PitWall.Models
{
    /// <summary>
    /// Completed session data for historical analysis
    /// </summary>
    public class SessionData
    {
        public string DriverName { get; set; } = string.Empty;
        public string TrackName { get; set; } = string.Empty;
        public string CarName { get; set; } = string.Empty;
        public string SessionType { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public List<LapData> Laps { get; set; } = new();
        public double TotalFuelUsed { get; set; }
        public TimeSpan SessionDuration { get; set; }
    }

    /// <summary>
    /// Per-lap telemetry snapshot
    /// </summary>
    public class LapData
    {
        public int LapNumber { get; set; }
        public TimeSpan LapTime { get; set; }
        public double FuelUsed { get; set; }
        public double FuelRemaining { get; set; }
        public bool IsValid { get; set; }
        public bool IsClear { get; set; } // No traffic/yellows
        public double TyreWearAverage { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
