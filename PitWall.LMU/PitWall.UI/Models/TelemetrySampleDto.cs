using System;
using System.Text.Json.Serialization;

namespace PitWall.UI.Models
{
    public class TelemetrySampleDto
    {
        public DateTime? Timestamp { get; set; }
        public double SpeedKph { get; set; }
        
        [JsonPropertyName("tyreTemps")]
        public double[] TyreTempsC { get; set; } = Array.Empty<double>();
        
        public double FuelLiters { get; set; }
        public int LapNumber { get; set; }
        
        [JsonPropertyName("throttle")]
        public double ThrottlePosition { get; set; }
        
        [JsonPropertyName("brake")]
        public double BrakePosition { get; set; }
        
        [JsonPropertyName("steering")]
        public double SteeringAngle { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double LateralG { get; set; }
    }
}
