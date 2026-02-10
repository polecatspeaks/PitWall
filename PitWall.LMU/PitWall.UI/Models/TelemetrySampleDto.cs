using System;
using System.Text.Json.Serialization;

namespace PitWall.UI.Models
{
    public class TelemetrySampleDto
    {
        public DateTime? Timestamp { get; set; }
        public double SpeedKph { get; set; }
        public double Throttle { get; set; }
        public double Brake { get; set; }
        public double Steering { get; set; }
        
        [JsonPropertyName("tyreTemps")]
        public double[] TyreTempsC { get; set; } = Array.Empty<double>();
        
        public double FuelLiters { get; set; }
        public int LapNumber { get; set; }
        public double ThrottlePosition { get; set; }
        public double BrakePosition { get; set; }
        public double SteeringAngle { get; set; }
    }
}
