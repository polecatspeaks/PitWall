namespace PitWall.UI.Models
{
    /// <summary>
    /// Input data for computing a vehicle's position on the track map.
    /// Typically sourced from live telemetry ScoringInfo per vehicle.
    /// </summary>
    public sealed class VehiclePositionInput
    {
        /// <summary>Vehicle identifier</summary>
        public int VehicleId { get; init; }

        /// <summary>Lap fraction (0.0–1.0) = LapDistance / TrackLength</summary>
        public double LapFraction { get; init; }

        /// <summary>Display label (e.g., position number or driver abbreviation)</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Vehicle class for color coding</summary>
        public string VehicleClass { get; init; } = string.Empty;

        /// <summary>Whether this is the player's car</summary>
        public bool IsPlayer { get; init; }

        /// <summary>Race position (1-based)</summary>
        public int Place { get; init; }
    }
}
