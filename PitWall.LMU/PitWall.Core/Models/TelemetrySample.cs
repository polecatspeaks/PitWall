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
        double Steering);
}
