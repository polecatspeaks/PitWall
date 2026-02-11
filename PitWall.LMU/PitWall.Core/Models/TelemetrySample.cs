using System;

namespace PitWall.Core.Models
{
    public record TelemetrySample(
        DateTime Timestamp,
        double SpeedKph,
        double[] TyreTempsC,
        double FuelLiters,
        double Brake,
        double Throttle,
        double Steering)
    {
        public int LapNumber { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public double LateralG { get; init; }
    }
}
