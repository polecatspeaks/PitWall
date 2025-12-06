using System;

namespace PitWall.Models.Telemetry
{
    /// <summary>
    /// Lap-level telemetry aggregates extracted from raw 60Hz samples
    /// Represents a single completed lap with computed statistics
    /// </summary>
    public class LapMetadata
    {
        /// <summary>
        /// Lap number in session (1-based)
        /// </summary>
        public int LapNumber { get; set; }

        /// <summary>
        /// Total time to complete lap
        /// </summary>
        public TimeSpan LapTime { get; set; }

        /// <summary>
        /// Whether lap counts toward statistics (excludes open laps, formation laps, etc)
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Whether lap was completed without collisions/incidents
        /// </summary>
        public bool IsClear { get; set; }

        /// <summary>
        /// Fuel consumed on this lap (gallons)
        /// </summary>
        public float FuelUsed { get; set; }

        /// <summary>
        /// Fuel remaining after lap
        /// </summary>
        public float FuelRemaining { get; set; }

        /// <summary>
        /// Average speed (mph)
        /// </summary>
        public float AvgSpeed { get; set; }

        /// <summary>
        /// Peak speed (mph)
        /// </summary>
        public float MaxSpeed { get; set; }

        /// <summary>
        /// Average throttle input 0.0-1.0
        /// </summary>
        public float AvgThrottle { get; set; }

        /// <summary>
        /// Average brake input 0.0-1.0
        /// </summary>
        public float AvgBrake { get; set; }

        /// <summary>
        /// Average steering angle (radians)
        /// </summary>
        public float AvgSteeringAngle { get; set; }

        /// <summary>
        /// Average tyre wear percentage 0.0-1.0
        /// </summary>
        public float AvgTyreWear { get; set; }

        /// <summary>
        /// Average engine RPM
        /// </summary>
        public int AvgEngineRpm { get; set; }

        /// <summary>
        /// Average engine temperature (Celsius)
        /// </summary>
        public float AvgEngineTemp { get; set; }

        /// <summary>
        /// When lap started (UTC)
        /// </summary>
        public DateTime LapStartTime { get; set; }

        /// <summary>
        /// Min lateral acceleration during lap (g)
        /// </summary>
        public float MinAccelLateral { get; set; }

        /// <summary>
        /// Max lateral acceleration during lap (g)
        /// </summary>
        public float MaxAccelLateral { get; set; }
    }
}
