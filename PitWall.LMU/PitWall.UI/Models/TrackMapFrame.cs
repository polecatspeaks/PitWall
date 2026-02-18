using System.Collections.Generic;
using Avalonia;

namespace PitWall.UI.Models
{
    public sealed class TrackMapFrame
    {
        public IReadOnlyList<Point> TrackPoints { get; init; } = System.Array.Empty<Point>();
        public Point? CurrentPoint { get; init; }
        public IReadOnlyList<CarMapMarker> VehicleMarkers { get; init; } = System.Array.Empty<CarMapMarker>();
        public TrackSegmentStatus? SegmentStatus { get; init; }
        public string? MapImageUri { get; init; }
    }
}
