using Avalonia;

namespace PitWall.UI.Models
{
    /// <summary>
    /// Represents a single vehicle's position on the track map.
    /// Used for multi-car rendering in the TrackMapControl.
    /// </summary>
    public sealed class CarMapMarker
    {
        /// <summary>Normalized position on the track map (0-1 range)</summary>
        public Point Position { get; init; }

        /// <summary>Display label (position number or driver name abbreviation)</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>Vehicle class for color coding</summary>
        public string VehicleClass { get; init; } = string.Empty;

        /// <summary>Whether this is the player's car</summary>
        public bool IsPlayer { get; init; }

        /// <summary>Race position (1st, 2nd, etc.)</summary>
        public int Place { get; init; }

        /// <summary>Vehicle ID for correlation with telemetry data</summary>
        public int VehicleId { get; init; }
    }
}
