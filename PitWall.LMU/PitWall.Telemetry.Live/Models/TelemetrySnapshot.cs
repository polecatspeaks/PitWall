using System;
using System.Collections.Generic;

namespace PitWall.Telemetry.Live.Models
{
    /// <summary>
    /// Represents a high-level telemetry snapshot from LMU shared memory.
    /// Designed to unify key data from Telemetry, Scoring, and (in future) Electronics structures.
    /// Based on a 231-field schema discovered from 2.5GB telemetry capture, but currently exposes only a curated subset.
    /// This model is an initial foundation and will be expanded as additional fields are implemented.
    /// </summary>
    public class TelemetrySnapshot
    {
        /// <summary>
        /// Timestamp when this snapshot was captured
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Session ID for this telemetry data
        /// </summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// Session information (static data)
        /// </summary>
        public SessionInfo? Session { get; set; }

        /// <summary>
        /// Vehicle telemetry data (physics, damage, etc.)
        /// </summary>
        public VehicleTelemetry? PlayerVehicle { get; set; }

        /// <summary>
        /// All vehicles in the session (up to 128 slots, typically 25 active)
        /// </summary>
        public List<VehicleTelemetry> AllVehicles { get; set; } = new();

        /// <summary>
        /// Scoring information (timing, positions, flags)
        /// </summary>
        public ScoringInfo? Scoring { get; set; }
    }

    /// <summary>
    /// Session metadata (static fields that don't change during session)
    /// </summary>
    public class SessionInfo
    {
        public DateTime StartTimeUtc { get; set; }
        public string SessionType { get; set; } = string.Empty;
        public string TrackName { get; set; } = string.Empty;
        public string CarName { get; set; } = string.Empty;
        public int NumVehicles { get; set; }
        public double TrackLength { get; set; }
    }

    /// <summary>
    /// Per-vehicle telemetry data (physics, damage, tires, etc.)
    /// </summary>
    public class VehicleTelemetry
    {
        public int VehicleId { get; set; }
        public bool IsPlayer { get; set; }
        public double ElapsedTime { get; set; }
        
        // Position
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        
        // Motion
        public double Speed { get; set; }
        public double LocalVelX { get; set; }
        public double LocalVelY { get; set; }
        public double LocalVelZ { get; set; }
        
        // Engine
        public double Rpm { get; set; }
        public int Gear { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double Steering { get; set; }
        public double Fuel { get; set; }
        
        // Damage data (discovered fields not in other apps)
        public byte[]? DentSeverity { get; set; }
        public double LastImpactMagnitude { get; set; }
        public double LastImpactTime { get; set; }
        
        // Tires
        public WheelData[] Wheels { get; set; } =
        {
            new WheelData(),
            new WheelData(),
            new WheelData(),
            new WheelData()
        };
    }

    /// <summary>
    /// Per-wheel data (tires, brakes, suspension)
    /// </summary>
    public class WheelData
    {
        // Tire temperatures (3 zones)
        public double TempInner { get; set; }
        public double TempMid { get; set; }
        public double TempOuter { get; set; }
        
        // Tire condition
        public double Wear { get; set; }
        public double Pressure { get; set; }
        public bool Flat { get; set; }
        public bool Detached { get; set; }
        
        // Brakes
        public double BrakeTemp { get; set; }
        
        // Suspension
        public double SuspDeflection { get; set; }
    }

    /// <summary>
    /// Scoring information (timing, positions, flags)
    /// </summary>
    public class ScoringInfo
    {
        public int SessionType { get; set; }
        public int NumVehicles { get; set; }
        
        // Flag states (critical for strategy)
        public int[] SectorFlags { get; set; } = new int[3];
        public int YellowFlagState { get; set; }
        
        // Wind conditions
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        
        // Per-vehicle scoring
        public List<VehicleScoringInfo> Vehicles { get; set; } = new();
    }

    /// <summary>
    /// Per-vehicle scoring/timing information
    /// </summary>
    public class VehicleScoringInfo
    {
        public int VehicleId { get; set; }
        public string DriverName { get; set; } = string.Empty;
        public string VehicleClass { get; set; } = string.Empty;
        
        // Timing
        public double BestLapTime { get; set; }
        public double LastLapTime { get; set; }
        public double CurrentLapTime { get; set; }
        
        // Position
        public int Place { get; set; }
        public double LapDistance { get; set; }
        public double TimeBehindLeader { get; set; }
        public double TimeBehindNext { get; set; }
        
        // Flags
        public int Flag { get; set; }
        
        // Pit state
        public int PitState { get; set; }
    }
}
