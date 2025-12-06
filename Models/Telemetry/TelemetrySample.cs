using System;

namespace PitWall.Models.Telemetry
{
    /// <summary>
    /// Represents a single 60Hz telemetry sample from iRacing IBT file
    /// Contains all channel data for one time slice during a session
    /// </summary>
    public class TelemetrySample
    {
        /// <summary>
        /// Timestamp when sample was recorded (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Lap number when this sample was recorded
        /// </summary>
        public int LapNumber { get; set; }

        /// <summary>
        /// Vehicle speed in mph
        /// </summary>
        public float Speed { get; set; }

        /// <summary>
        /// Throttle input 0.0-1.0
        /// </summary>
        public float Throttle { get; set; }

        /// <summary>
        /// Brake input 0.0-1.0
        /// </summary>
        public float Brake { get; set; }

        /// <summary>
        /// Steering wheel angle in radians
        /// </summary>
        public float SteeringAngle { get; set; }

        /// <summary>
        /// Engine RPM
        /// </summary>
        public int EngineRpm { get; set; }

        /// <summary>
        /// Current gear (0=reverse, 1-8=forward)
        /// </summary>
        public int Gear { get; set; }

        /// <summary>
        /// Fuel remaining in tank (gallons)
        /// </summary>
        public float FuelLevel { get; set; }

        /// <summary>
        /// Engine coolant temperature (Celsius)
        /// </summary>
        public float EngineTemp { get; set; }

        /// <summary>
        /// Oil temperature (Celsius)
        /// </summary>
        public float OilTemp { get; set; }

        /// <summary>
        /// Oil pressure (psi)
        /// </summary>
        public float OilPressure { get; set; }

        /// <summary>
        /// Water temperature (Celsius)
        /// </summary>
        public float WaterTemp { get; set; }

        /// <summary>
        /// Water pressure (psi)
        /// </summary>
        public float WaterPressure { get; set; }

        /// <summary>
        /// Longitudinal acceleration (g)
        /// </summary>
        public float AccelX { get; set; }

        /// <summary>
        /// Lateral acceleration (g)
        /// </summary>
        public float AccelY { get; set; }

        /// <summary>
        /// Vertical acceleration (g)
        /// </summary>
        public float AccelZ { get; set; }

        /// <summary>
        /// Roll angle (radians)
        /// </summary>
        public float Roll { get; set; }

        /// <summary>
        /// Pitch angle (radians)
        /// </summary>
        public float Pitch { get; set; }

        /// <summary>
        /// Yaw angle (radians)
        /// </summary>
        public float Yaw { get; set; }

        /// <summary>
        /// Left front tyre wear percentage 0.0-1.0
        /// </summary>
        public float TyreWearLF { get; set; }

        /// <summary>
        /// Right front tyre wear percentage 0.0-1.0
        /// </summary>
        public float TyreWearRF { get; set; }

        /// <summary>
        /// Left rear tyre wear percentage 0.0-1.0
        /// </summary>
        public float TyreWearLR { get; set; }

        /// <summary>
        /// Right rear tyre wear percentage 0.0-1.0
        /// </summary>
        public float TyreWearRR { get; set; }

        /// <summary>
        /// Left front tyre temperature (Celsius)
        /// </summary>
        public float TyreTempLF { get; set; }

        /// <summary>
        /// Right front tyre temperature (Celsius)
        /// </summary>
        public float TyreTempRF { get; set; }

        /// <summary>
        /// Left rear tyre temperature (Celsius)
        /// </summary>
        public float TyreTempLR { get; set; }

        /// <summary>
        /// Right rear tyre temperature (Celsius)
        /// </summary>
        public float TyreTempRR { get; set; }

        /// <summary>
        /// Track surface temperature (Celsius)
        /// </summary>
        public float TrackTemp { get; set; }

        /// <summary>
        /// Wind speed (mph)
        /// </summary>
        public float WindSpeed { get; set; }
    }
}
