using System;
using System.Collections.Generic;
using System.Linq;

namespace PitWall.UI.Models
{
    public sealed class TrackMetadata
    {
        public string Name { get; set; } = "Default";
        public List<TrackSector> Sectors { get; set; } = new();
        public List<TrackCorner> Corners { get; set; } = new();
        public List<TrackOutlinePoint> Outline { get; set; } = new();
        public string? MapImageUri { get; set; }

        public TrackSector? FindSector(double lapFraction)
        {
            return Sectors.FirstOrDefault(sector => sector.Contains(lapFraction));
        }

        public TrackCorner? FindCorner(double lapFraction)
        {
            return Corners.FirstOrDefault(corner => corner.Contains(lapFraction));
        }
    }

    public sealed class TrackSector
    {
        public string Name { get; set; } = string.Empty;
        public double Start { get; set; }
        public double End { get; set; }

        public bool Contains(double lapFraction)
        {
            return TrackRange.ContainsFraction(Start, End, lapFraction);
        }
    }

    public sealed class TrackCorner
    {
        public int Number { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public double Start { get; set; }
        public double End { get; set; }

        public bool Contains(double lapFraction)
        {
            return TrackRange.ContainsFraction(Start, End, lapFraction);
        }
    }

    public sealed class TrackOutlinePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    internal static class TrackRange
    {
        public static bool ContainsFraction(double start, double end, double value)
        {
            if (start <= end)
            {
                return value >= start && value <= end;
            }

            return value >= start || value <= end;
        }
    }
}
