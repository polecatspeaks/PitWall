using System;

namespace PitWall.UI.Models
{
    public class TelemetrySampleDto
    {
        public DateTime? Timestamp { get; set; }
        public double SpeedKph { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double Steering { get; set; }
        public double[] TyreTempsC { get; set; } = Array.Empty<double>();
        public double FuelLiters { get; set; }
    }
}
