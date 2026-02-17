using System;
using System.Collections.Generic;

namespace PitWall.Core.Models
{
    /// <summary>
    /// Core telemetry sample from shared memory. Contains fields mapped from
    /// LMU's rFactor 2 shared memory structures (Telemetry, Scoring, Electronics).
    /// See docs/lmu-telemetry-schema.md for the full 231-field schema reference.
    /// </summary>
    public record TelemetrySample(
        DateTime Timestamp,
        double SpeedKph,
        double[] TyreTempsC,
        double FuelLiters,
        double Brake,
        double Throttle,
        double Steering)
    {
        // --- Existing init-only properties ---
        public int LapNumber { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double LateralG { get; init; }

        // --- Engine / Drivetrain ---
        /// <summary>Engine RPM (0–18k observed range)</summary>
        public double Rpm { get; init; }
        /// <summary>Current gear (-1=reverse, 0=neutral, 1-8=forward)</summary>
        public int Gear { get; init; }
        /// <summary>Turbo boost pressure</summary>
        public double TurboBoost { get; init; }
        /// <summary>Fuel capacity in liters</summary>
        public double FuelCapacity { get; init; }

        // --- Position / World ---
        /// <summary>World position Y (vertical)</summary>
        public double PosY { get; init; }
        /// <summary>Elapsed session time in seconds</summary>
        public double ElapsedTime { get; init; }
        /// <summary>Distance traveled on current lap (meters)</summary>
        public double LapDistance { get; init; }

        // --- Damage ---
        /// <summary>DentSeverity base64 (8-zone body damage byte array)</summary>
        public string? DentSeverity { get; init; }
        /// <summary>Magnitude of last impact (0–18,855 observed)</summary>
        public double LastImpactMagnitude { get; init; }
        /// <summary>Time of last impact (session time)</summary>
        public double LastImpactTime { get; init; }

        // --- Tyre condition (per-wheel arrays, FL/FR/RL/RR) ---
        /// <summary>Tyre wear per wheel (0.0–1.0)</summary>
        public double[]? TyreWear { get; init; }
        /// <summary>Tyre pressure per wheel</summary>
        public double[]? TyrePressure { get; init; }
        /// <summary>Flat tyre flags per wheel (true = flat)</summary>
        public bool[]? TyreFlat { get; init; }
        /// <summary>Detached wheel flags per wheel</summary>
        public bool[]? WheelDetached { get; init; }

        // --- Brake temps ---
        /// <summary>Brake temperatures per wheel (°C, 0–1184°C observed)</summary>
        public double[]? BrakeTempsC { get; init; }

        // --- Suspension ---
        /// <summary>Suspension deflection per wheel</summary>
        public double[]? SuspDeflection { get; init; }

        // --- Session metadata (static per session) ---
        /// <summary>Track name (decoded from base64)</summary>
        public string? TrackName { get; init; }
        /// <summary>Driver's car class name</summary>
        public string? VehicleClass { get; init; }
        /// <summary>Driver name (decoded from base64)</summary>
        public string? DriverName { get; init; }
        /// <summary>Session type (e.g., Practice, Qualifying, Race)</summary>
        public string? SessionType { get; init; }
        /// <summary>Track length in meters</summary>
        public double TrackLength { get; init; }
        /// <summary>Number of vehicles in session</summary>
        public int NumVehicles { get; init; }

        // --- Timing ---
        /// <summary>Best lap time for this vehicle</summary>
        public double BestLapTime { get; init; }
        /// <summary>Last completed lap time</summary>
        public double LastLapTime { get; init; }
        /// <summary>Current position in race</summary>
        public int Place { get; init; }
        /// <summary>Time behind leader</summary>
        public double TimeBehindLeader { get; init; }
        /// <summary>Time behind car ahead</summary>
        public double TimeBehindNext { get; init; }

        // --- Flags ---
        /// <summary>Sector flags (3 elements)</summary>
        public int[]? SectorFlags { get; init; }
        /// <summary>Yellow flag state</summary>
        public int YellowFlagState { get; init; }
        /// <summary>Per-vehicle flag value</summary>
        public int Flag { get; init; }

        // --- Pit state ---
        /// <summary>Pit state: 0=none, 1=request, 2=entering, 4=stopped, 5=exiting</summary>
        public int PitState { get; init; }
        /// <summary>Whether vehicle is in pits</summary>
        public bool InPits { get; init; }

        // --- Multi-vehicle ---
        /// <summary>
        /// Other vehicles' telemetry (for multi-car awareness).
        /// Only active vehicles (based on NumVehicles) should be populated.
        /// </summary>
        public List<VehicleSampleData>? OtherVehicles { get; init; }
    }

    /// <summary>
    /// Reduced telemetry data for non-player vehicles.
    /// Contains only the fields needed for race awareness and strategy.
    /// </summary>
    public record VehicleSampleData
    {
        public int VehicleId { get; init; }
        public double Speed { get; init; }
        public double PosX { get; init; }
        public double PosY { get; init; }
        public double PosZ { get; init; }
        public int LapNumber { get; init; }
        public double LapDistance { get; init; }
        public int Place { get; init; }
        public double BestLapTime { get; init; }
        public double LastLapTime { get; init; }
        public double TimeBehindLeader { get; init; }
        public double TimeBehindNext { get; init; }
        public int Flag { get; init; }
        public int PitState { get; init; }
        public bool InPits { get; init; }
        public string? DriverName { get; init; }
        public string? VehicleClass { get; init; }
    }
}
